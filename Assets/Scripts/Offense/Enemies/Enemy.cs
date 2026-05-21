using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private EnemyData enemyData;

    public EnemyData EnemyData => enemyData;

    [Header("Path")]
    [SerializeField] public Transform[] waypoints;
    [SerializeField] private int currentWaypointIndex;

    [Header("Health Bar")]
    [SerializeField] private HealthBar healthBar;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Defender Combat")]
    [SerializeField] private float defenderDamage = 20f;
    [SerializeField] private float defenderAttackInterval = 1.25f;
    [SerializeField] private bool useAnimationEventForDefenderDamage = true;

    [Header("Attack Audio")]
    [SerializeField] private AudioClip[] defenderAttackSounds;
    [SerializeField] private float defenderAttackSoundVolume = 1f;
    [SerializeField] private float pitchRandomMin = 0.95f;
    [SerializeField] private float pitchRandomMax = 1.05f;

    public AudioClip[] IdleSounds => enemyData != null ? enemyData.idleSounds : null;

    public float PathProgress { get; private set; }
    public bool IsDead { get; private set; }
    public float CurrentHealth => currentHP;
    public Defender CurrentDefenderTarget => defenderTarget;

    private Vector3 originalScale;

    private float goldMultiplier = 1f;
    private float hpMultiplier = 1f;

    public int GoldReward =>
        enemyData != null ? Mathf.RoundToInt(enemyData.goldReward * goldMultiplier) : 0;

    public int AttackPointCost =>
        enemyData != null ? enemyData.attackPointCost : 0;

    private float speed;
    private float originalSpeed;
    private float maxHP;
    private float currentHP;

    private Coroutine slowCoroutine;
    private Coroutine deathCoroutine;

    private GameManager gameManager;
    private bool isFinished = false;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private bool slowVisualActive = false;

    private Animator anim;

    private Defender defenderTarget;
    private float defenderAttackTimer;

    public System.Action OnDeath;

    // Initializes the enemy with EnemyData and path waypoints.
    // This is important for pooled enemies because Start() is called only once,
    // but Initialize() is called every time the enemy is reused.
    public void Initialize(EnemyData data, Transform[] pathWaypoints)
    {
        enemyData = data;
        waypoints = pathWaypoints;

        ResetEnemyState();
        ApplyData();
        CacheRenderersIfNeeded();
        SaveOriginalColors();
        ResetVisualState();
        UpdateHealthBar();

        EnemyRegistry.Register(this);
    }

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        originalScale = transform.localScale;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<HealthBar>();
        }

        anim = GetComponent<Animator>();

        CacheRenderersIfNeeded();
        SaveOriginalColors();
    }

    // Start is called once when the enemy object is first created
    private void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();

        if (enemyData != null)
        {
            ApplyData();
            UpdateHealthBar();
        }
        else
        {
            Debug.LogWarning($"{name} has no EnemyData!");
        }
    }

    // Called when the enemy becomes active again from the pool
    private void OnEnable()
    {
        if (enemyData != null)
        {
            EnemyRegistry.Register(this);
        }

        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<HealthBar>();
        }

        UpdateHealthBar();
    }

    // Called when the enemy is disabled or returned to the pool
    private void OnDisable()
    {
        EnemyRegistry.Unregister(this);

        ClearDefenderTarget(null);

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }

        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }
    }

    // Reset enemy state before reuse
    private void ResetEnemyState()
    {
        currentWaypointIndex = 0;
        PathProgress = 0f;

        isFinished = false;
        IsDead = false;

        slowVisualActive = false;

        defenderTarget = null;
        defenderAttackTimer = defenderAttackInterval;

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }

        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }
    }

    // Method to cache all SpriteRenderers so we can change and reset visuals later
    private void CacheRenderersIfNeeded()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<SpriteRenderer>();
        }
    }

    // Method to save the original sprite colors before slow/fade effects change them
    private void SaveOriginalColors()
    {
        if (renderers == null)
            return;

        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                originalColors[i] = renderers[i].color;
            }
        }
    }

    // Method to reset enemy visuals when it is reused from the pool
    private void ResetVisualState()
    {
        if (renderers == null || originalColors == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && i < originalColors.Length)
            {
                renderers[i].color = originalColors[i];
                renderers[i].enabled = true;
            }
        }

        transform.localScale = originalScale;
    }

    // Method to set gold reward multiplier for this enemy
    public void SetGoldMultiplier(float multiplier)
    {
        goldMultiplier = multiplier;
    }

    // Method to apply EnemyData values to runtime variables
    private void ApplyData()
    {
        if (enemyData == null)
            return;

        maxHP = enemyData.maxHP * hpMultiplier;
        currentHP = maxHP;

        speed = enemyData.speed;
        originalSpeed = speed;

        UpdateHealthBar();
    }

    // Update is called once per frame
    private void Update()
    {
        if (isFinished)
            return;

        // If this enemy is fighting a defender, it stops moving and attacks.
        if (defenderTarget != null)
        {
            UpdateDefenderCombat();
            return;
        }

        MoveTowardsWaypoint();
    }

    // Method called by Defender when it wants this enemy to stop and fight
    public bool TryEngageDefender(Defender defender)
    {
        if (isFinished || IsDead)
            return false;

        if (enemyData != null && enemyData.immuneToDefenders)
            return false;

        if (defender == null || !defender.IsAvailableForCombat)
            return false;

        // One enemy should not be blocked by two different defenders at once
        if (defenderTarget != null && defenderTarget != defender)
            return false;

        defenderTarget = defender;
        defenderAttackTimer = defenderAttackInterval;

        TurnTowardsPosition(defender.transform.position);

        return true;
    }

    // Method to clear defender target
    public void ClearDefenderTarget(Defender defender)
    {
        // If defender is null, force clear target.
        // If defender is not null, clear only if it is the current target.
        if (defender != null && defenderTarget != defender)
            return;

        defenderTarget = null;
        defenderAttackTimer = defenderAttackInterval;

        if (anim != null)
        {
            anim.ResetTrigger("attack");
        }
    }

    // Method to update enemy combat against defender
    private void UpdateDefenderCombat()
    {
        Defender target = defenderTarget;

        if (target == null || !target.IsAvailableForCombat)
        {
            ClearDefenderTarget(null);
            return;
        }

        TurnTowardsPosition(target.transform.position);

        defenderAttackTimer += Time.deltaTime;

        if (defenderAttackTimer >= defenderAttackInterval)
        {
            StartDefenderAttackAnimation();

            // If animation event is disabled, damage is dealt immediately.
            // If it is enabled, damage is dealt by DealDefenderDamageEvent().
            if (!useAnimationEventForDefenderDamage)
            {
                DealDefenderDamageEvent();
            }

            defenderAttackTimer = 0f;
        }
    }

    // Method to start enemy attack animation against defender
    private void StartDefenderAttackAnimation()
    {
        PlayDefenderAttackSound();

        if (anim == null)
            return;

        anim.SetTrigger("attack");
    }

    // Animation Event for enemy attack animation
    public void DealDefenderDamageEvent()
    {
        if (isFinished || IsDead)
            return;

        Defender target = defenderTarget;

        if (target == null || !target.IsAvailableForCombat)
        {
            ClearDefenderTarget(null);
            return;
        }

        target.TakeDamage(defenderDamage);

        if (target == null || !target.IsAvailableForCombat)
        {
            ClearDefenderTarget(null);
        }
    }

    // Method to play enemy attack sound when attacking defender
    private void PlayDefenderAttackSound()
    {
        if (defenderAttackSounds == null || defenderAttackSounds.Length == 0)
            return;

        if (audioSource == null)
            return;

        AudioClip clip = defenderAttackSounds[Random.Range(0, defenderAttackSounds.Length)];

        if (clip == null)
            return;

        audioSource.pitch = Random.Range(pitchRandomMin, pitchRandomMax);
        audioSource.PlayOneShot(clip, defenderAttackSoundVolume);
    }

    // Method to turn enemy towards a world position
    private void TurnTowardsPosition(Vector3 targetPosition)
    {
        Vector3 newScale = transform.localScale;

        if (targetPosition.x > transform.position.x)
        {
            newScale.x = Mathf.Abs(newScale.x);
        }
        else
        {
            newScale.x = -Mathf.Abs(newScale.x);
        }

        transform.localScale = newScale;
    }

    // Method to move enemy towards the current waypoint
    private void MoveTowardsWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        if (currentWaypointIndex >= waypoints.Length)
            return;

        Transform targetWaypoint = waypoints[currentWaypointIndex];

        transform.position = Vector2.MoveTowards(
            transform.position,
            targetWaypoint.position,
            speed * Time.deltaTime
        );

        TurnTowardsPosition(targetWaypoint.position);

        UpdatePathProgress();

        if (Vector3.Distance(transform.position, targetWaypoint.position) < 0.1f)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Length)
            {
                ReachEnd();
            }
        }
    }

    // Update how far the enemy has progressed along the path
    private void UpdatePathProgress()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            PathProgress = 0f;
            return;
        }

        if (currentWaypointIndex <= 0)
        {
            float distanceToFirst = Vector3.Distance(transform.position, waypoints[0].position);
            PathProgress = 1f - distanceToFirst;
            return;
        }

        if (currentWaypointIndex >= waypoints.Length)
        {
            PathProgress = waypoints.Length;
            return;
        }

        float distanceToNext = Vector3.Distance(
            transform.position,
            waypoints[currentWaypointIndex].position
        );

        float distanceBetweenPoints = Vector3.Distance(
            waypoints[currentWaypointIndex - 1].position,
            waypoints[currentWaypointIndex].position
        );

        float progressToNext = 1f - distanceToNext / distanceBetweenPoints;

        PathProgress = currentWaypointIndex + progressToNext;
    }

    // Method to apply HP scaling for stronger waves
    public void SetHPMultiplier(float multiplier)
    {
        hpMultiplier = multiplier;

        if (enemyData != null)
        {
            maxHP = enemyData.maxHP * hpMultiplier;
            currentHP = maxHP;
        }

        UpdateHealthBar();
    }

    // Method to apply damage to the enemy
    public void TakeDamage(float damage)
    {
        if (isFinished)
            return;

        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        UpdateHealthBar();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    // Method to update enemy health bar
    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetHealth(currentHP, maxHP);
        }
    }

    // Method to play enemy hit sound when damaged by a defender
    public void PlayDefenderHitSound()
    {
        if (enemyData == null || enemyData.defenderHitSounds == null || enemyData.defenderHitSounds.Length == 0)
            return;

        if (enemyData.immuneToDefenders)
            return;

        if (audioSource == null)
            return;

        AudioClip clip = enemyData.defenderHitSounds[
            Random.Range(0, enemyData.defenderHitSounds.Length)
        ];

        audioSource.PlayOneShot(clip);
    }

    // Method to handle enemy death
    private void Die()
    {
        if (isFinished)
            return;

        isFinished = true;
        IsDead = true;

        ClearDefenderTarget(null);

        EnemyRegistry.Unregister(this);

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            slowCoroutine = null;
        }

        ResetSlowVisual();

        if (healthBar != null)
        {
            healthBar.Hide();
        }

        if (gameManager != null)
        {
            gameManager.AddGold(GoldReward);
        }

        if (enemyData != null && enemyData.deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(enemyData.deathSound);
        }

        deathCoroutine = StartCoroutine(DeathFade());
    }

    // Method called when enemy reaches the end of the path
    private void ReachEnd()
    {
        if (isFinished)
            return;

        isFinished = true;

        ClearDefenderTarget(null);

        EnemyRegistry.Unregister(this);

        if (healthBar != null)
        {
            healthBar.Hide();
        }

        if (gameManager != null)
        {
            gameManager.LoseLife();
        }

        OnDeath?.Invoke();
        ReturnToPool();
    }

    // Method to apply slow effect to the enemy
    public void ApplySlow(float slowPercent, float duration)
    {
        if (IsDead)
            return;

        if (enemyData != null && enemyData.immuneToSlow)
            return;

        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
            ResetSlowVisual();
        }

        slowCoroutine = StartCoroutine(SlowRoutine(slowPercent, duration));
    }

    // Coroutine to slow the enemy for a certain duration
    private IEnumerator SlowRoutine(float slowPercent, float duration)
    {
        speed = originalSpeed * slowPercent;

        ApplySlowVisual();

        yield return new WaitForSeconds(duration);

        speed = originalSpeed;
        ResetSlowVisual();

        slowCoroutine = null;
    }

    // Method to apply blue visual effect while enemy is slowed
    private void ApplySlowVisual()
    {
        CacheRenderersIfNeeded();

        if (renderers == null || renderers.Length == 0)
            return;

        slowVisualActive = true;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.color = Color.blue;
            }
        }
    }

    // Method to reset slow visual back to original colors
    private void ResetSlowVisual()
    {
        if (!slowVisualActive)
            return;

        if (renderers == null || originalColors == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && i < originalColors.Length)
            {
                renderers[i].color = originalColors[i];
            }
        }

        slowVisualActive = false;
    }

    // Coroutine to fade out enemy after death and then return it to the pool
    private IEnumerator DeathFade()
    {
        SpriteRenderer[] deathRenderers = GetComponentsInChildren<SpriteRenderer>();

        float duration = 0.5f;
        float elapsed = 0f;

        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(
            startScale.x * 1.2f,
            startScale.y * 0.7f,
            startScale.z
        );

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / duration;
            float alpha = Mathf.Lerp(1f, 0f, t);

            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            foreach (SpriteRenderer renderer in deathRenderers)
            {
                if (renderer == null)
                    continue;

                Color color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }

            yield return null;
        }

        OnDeath?.Invoke();
        ReturnToPool();
    }

    // Method to return enemy to the object pool instead of destroying it
    private void ReturnToPool()
    {
        OnDeath = null;

        CleanupBeforePooling();

        if (ObjectPooler.Instance != null)
        {
            ObjectPooler.Instance.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Method to clean temporary enemy state before returning to pool
    private void CleanupBeforePooling()
    {
        waypoints = null;
        currentWaypointIndex = 0;
        PathProgress = 0f;

        ClearDefenderTarget(null);

        ResetSlowBeforePooling();

        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    // Method to reset slow state before pooling
    private void ResetSlowBeforePooling()
    {
        ResetSlowVisualForced();

        speed = originalSpeed;
        slowCoroutine = null;
    }

    // Method to force enemy visuals back to their original colors
    private void ResetSlowVisualForced()
    {
        if (renderers == null || originalColors == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && i < originalColors.Length)
            {
                renderers[i].color = originalColors[i];
            }
        }

        slowVisualActive = false;
    }

    // OnDestroy is called when the object is actually destroyed, not when returned to pool
    private void OnDestroy()
    {
        EnemyRegistry.Unregister(this);
        ClearDefenderTarget(null);
        OnDeath = null;
    }
}
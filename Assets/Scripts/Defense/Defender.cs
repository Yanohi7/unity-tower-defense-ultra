using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Defender : MonoBehaviour
{
    // This is a melee defender that blocks one enemy and attacks it.
    // The defender itself does not recharge.
    // Shop / item system should handle defender placement cooldown.

    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackInterval = 1f;

    [Header("Health Bar")]
    [SerializeField] private HealthBar healthBar;

    [Header("Idle Rotation")]
    [SerializeField] private float returnToOriginalDirectionDelay = 1.5f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Attack Sounds")]
    [SerializeField] private AudioClip[] attackSounds;
    [SerializeField] private float attackSoundVolume = 1f;

    [Header("Hurt Sounds")]
    [SerializeField] private AudioClip[] hurtSounds;
    [SerializeField] private float hurtSoundVolume = 1f;

    [Header("Death Sounds")]
    [SerializeField] private AudioClip[] deathSounds;
    [SerializeField] private float deathSoundVolume = 1f;

    [Header("Pitch Random")]
    [SerializeField] private float pitchRandomMin = 0.95f;
    [SerializeField] private float pitchRandomMax = 1.05f;

    [Header("Death Fade")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private Vector3 deathScaleMultiplier = new Vector3(1.1f, 0.85f, 1f);
    [SerializeField] private float maxVerticalDistance = 0.3f;
    private float currentHealth;
    private float attackTimer;
    private float idleTimer;

    private Animator anim;
    private bool isDying;

    private Vector3 originalScale;
    private Enemy currentTarget;

    private Collider2D[] colliders;

    // List to keep track of enemies currently in range
    private readonly List<Enemy> enemiesInRange = new List<Enemy>();

    public bool IsAvailableForCombat => !isDying && currentHealth > 0f;

    // Start is called before the first frame update
    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (healthBar == null)
            healthBar = GetComponentInChildren<HealthBar>();

        anim = GetComponent<Animator>();

        originalScale = transform.localScale;

        currentHealth = maxHealth;
        attackTimer = attackInterval;

        colliders = GetComponentsInChildren<Collider2D>();

        UpdateHealthBar();
    }

    // Update is called once per frame
    private void Update()
    {
        if (!IsAvailableForCombat)
            return;

        // Remove null, dead, immune, or invalid enemies from the list
        enemiesInRange.RemoveAll(enemy =>
            enemy == null ||
            enemy.IsDead ||
            enemy.EnemyData == null ||
            enemy.EnemyData.immuneToDefenders
        );

        // If current target is no longer valid, release it
        if (!IsValidTarget(currentTarget))
        {
            ReleaseCurrentTarget();
        }

        // If we do not have a target, try to take one enemy for melee combat
        if (currentTarget == null)
        {
            currentTarget = FindNearestFreeEnemy();

            if (currentTarget != null)
            {
                bool engaged = currentTarget.TryEngageDefender(this);

                if (!engaged)
                    currentTarget = null;
            }
        }

        // If there is no target, stop attacking and return to original direction after a delay
        if (currentTarget == null)
        {
            StopAttackingAnimation();

            attackTimer = attackInterval;

            idleTimer += Time.deltaTime;

            if (idleTimer >= returnToOriginalDirectionDelay)
            {
                ReturnToOriginalDirection();
            }

            return;
        }

        idleTimer = 0f;

        TurnTowardsTarget(currentTarget);

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackInterval)
        {
            StartAttackAnimation();
            attackTimer = 0f;
        }
    }

    // Method to check if current enemy is still a valid target
    // Method to check if current enemy is still a valid target
    private bool IsValidTarget(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return false;

        if (enemy.EnemyData != null && enemy.EnemyData.immuneToDefenders)
            return false;

        if (!enemiesInRange.Contains(enemy))
            return false;

        float yDiff = Mathf.Abs(transform.position.y - enemy.transform.position.y);
        if (yDiff > maxVerticalDistance)
            return false;

        return true;
    }

    // Method to find the nearest enemy that is not already blocked by another defender
    // Method to find the nearest enemy that is not already blocked by another defender
    private Enemy FindNearestFreeEnemy()
    {
        Enemy nearestEnemy = null;
        float shortestDistance = Mathf.Infinity;

        foreach (Enemy enemy in enemiesInRange)
        {
            if (enemy == null || enemy.IsDead || enemy.EnemyData == null || enemy.EnemyData.immuneToDefenders)
                continue;

            if (enemy.CurrentDefenderTarget != null && enemy.CurrentDefenderTarget != this)
                continue;

            float yDiff = Mathf.Abs(transform.position.y - enemy.transform.position.y);
            if (yDiff > maxVerticalDistance)
                continue;

            float distance = Vector2.Distance(transform.position, enemy.transform.position);

            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    // Method to release current enemy from this defender
    private void ReleaseCurrentTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.ClearDefenderTarget(this);
        }

        currentTarget = null;
    }

    // Method to start the attack animation
    private void StartAttackAnimation()
    {
        if (anim == null)
            return;

        anim.SetTrigger("attack");
    }

    // Method to stop the attack animation
    private void StopAttackingAnimation()
    {
        if (anim == null)
            return;

        anim.ResetTrigger("attack");
    }

    // Method to turn defender towards the target enemy
    private void TurnTowardsTarget(Enemy target)
    {
        if (target == null)
            return;

        float direction = target.transform.position.x < transform.position.x ? -1f : 1f;

        transform.localScale = new Vector3(
            Mathf.Abs(originalScale.x) * direction,
            originalScale.y,
            originalScale.z
        );
    }

    // Method to return defender to the direction it had when it was placed
    private void ReturnToOriginalDirection()
    {
        transform.localScale = originalScale;
    }

    // Animation Event to deal damage to the current enemy
    public void DealDamageEvent()
    {
        if (!IsAvailableForCombat)
            return;

        Enemy target = currentTarget;

        if (!IsValidTarget(target))
        {
            ReleaseCurrentTarget();
            return;
        }

        TurnTowardsTarget(target);

        PlayRandomSound(attackSounds, attackSoundVolume);

        target.TakeDamage(damage);
        target.PlayDefenderHitSound();

        if (target == null || target.IsDead)
        {
            ReleaseCurrentTarget();
        }
    }

    // Method called by Enemy when it attacks this defender
    public void TakeDamage(float damageAmount)
    {
        if (!IsAvailableForCombat)
            return;

        currentHealth -= damageAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateHealthBar();

        if (currentHealth > 0f)
        {
            PlayRandomSound(hurtSounds, hurtSoundVolume);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    // Method to update the defender health bar
    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth, maxHealth);
        }
    }

    // Method to play random sound from an array
    private void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
            return;

        if (audioSource == null)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clip == null)
            return;

        audioSource.pitch = Random.Range(pitchRandomMin, pitchRandomMax);
        audioSource.PlayOneShot(clip, volume);
    }

    // Method to handle defender death
    private void Die()
    {
        if (isDying)
            return;

        isDying = true;

        PlayRandomSound(deathSounds, deathSoundVolume);

        StopAttackingAnimation();
        ReleaseCurrentTarget();

        // Release all enemies that may still think they are fighting this defender
        foreach (Enemy enemy in enemiesInRange)
        {
            if (enemy != null)
                enemy.ClearDefenderTarget(this);
        }

        enemiesInRange.Clear();

        SetCollidersEnabled(false);

        if (healthBar != null)
        {
            healthBar.Hide();
        }

        StartCoroutine(DeathFadeAndDestroy());
    }

    // Coroutine to fade out and destroy defender after death
    private IEnumerator DeathFadeAndDestroy()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        float elapsed = 0f;

        Vector3 startScale = transform.localScale;
        Vector3 endScale = new Vector3(
            startScale.x * deathScaleMultiplier.x,
            startScale.y * deathScaleMultiplier.y,
            startScale.z * deathScaleMultiplier.z
        );

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / fadeDuration;
            float alpha = Mathf.Lerp(1f, 0f, t);

            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                Color color = renderer.color;
                color.a = alpha;
                renderer.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    // Method to enable or disable all defender colliders
    private void SetCollidersEnabled(bool enabled)
    {
        if (colliders == null)
            return;

        foreach (Collider2D col in colliders)
        {
            if (col != null)
                col.enabled = enabled;
        }
    }

    // Methods to handle enemies entering and exiting the defender's range
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAvailableForCombat)
            return;

        if (!other.CompareTag("Enemy"))
            return;

        Enemy enemy = other.GetComponent<Enemy>();

        if (enemy != null && !enemiesInRange.Contains(enemy))
        {
            enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        Enemy enemy = other.GetComponent<Enemy>();

        if (enemy != null)
        {
            enemiesInRange.Remove(enemy);

            if (enemy == currentTarget)
            {
                ReleaseCurrentTarget();
            }
        }
    }

    private void OnDestroy()
    {
        ReleaseCurrentTarget();
    }
}
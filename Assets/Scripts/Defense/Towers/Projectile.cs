using System.Collections;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 5f;

    // Angle offset to make the projectile sprite face the correct direction
    [SerializeField] private float angleTurn = 90f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float hitVolume = 1f;
    [SerializeField] private float pitchMin = 0.95f;
    [SerializeField] private float pitchMax = 1.05f;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 4f;

    private Transform target;
    private TowerData towerData;
    private float lifetimeTimer;
    private bool hasHit;

    private SpriteRenderer[] renderers;
    private Coroutine returnRoutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    // Initializes the projectile with the target enemy and tower data
    public void Initialize(Transform newTarget, TowerData data)
    {
        target = newTarget;
        towerData = data;
        lifetimeTimer = 0f;
        hasHit = false;

        // Stop any existing return coroutine to prevent conflicts 
        // when reusing the projectile
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        // Ensure the projectile's renderers are visible when initialized
        SetRenderersVisible(true);

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.pitch = 1f;
        }

        if (target != null)
            RotateTowardsTarget();
    }

    // Update is called once per frame to handle projectile movement and lifetime
    private void Update()
    {
        if (hasHit)
            return;

        lifetimeTimer += Time.deltaTime;

        // If the projectile has exceeded its maximum lifetime, return it to the pool
        if (lifetimeTimer >= maxLifetime)
        {
            ReturnToPool();
            return;
        }

        if (target == null || towerData == null)
        {
            ReturnToPool();
            return;
        }

        RotateTowardsTarget();

        // Move the projectile towards the target
        transform.position = Vector2.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );

        if (Vector2.Distance(transform.position, target.position) < 0.2f)
        {
            Hit();
        }
    }

    // Rotates the projectile to face the target enemy
    private void RotateTowardsTarget()
    {
        if (target == null)
            return;

        Vector2 direction = target.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle + angleTurn);
    }

    // Handles the logic when the projectile hits the target enemy
    private void Hit()
    {
        if (hasHit)
            return;

        hasHit = true;

        SpawnHitEffect();

        Enemy enemy = target.GetComponent<Enemy>();

        if (enemy != null && !enemy.IsDead)
        {
            // Apply damage to the enemy and any extra effects from the tower
            enemy.TakeDamage(towerData.damage);
            ApplyExtraEffects(enemy);
        }

        PlayHitSoundAndReturn();
    }

    // Applies any extra effects from the tower to the enemy, such as slowing
    private void ApplyExtraEffects(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (towerData.appliesSlow)
        {
            enemy.ApplySlow(towerData.slowPercent, towerData.slowDuration);
        }
    }

    // Spawns a hit effect at the projectile's position when it hits an enemy
    private void SpawnHitEffect()
    {
        if (towerData.hitEffectPrefab == null)
            return;

        GameObject effect;

        // Try to spawn the hit effect from the object pool, if available,
        //  otherwise instantiate a new one
        if (ObjectPooler.Instance != null)
        {
            effect = ObjectPooler.Instance.Spawn(
                towerData.hitEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }
        else
        {
            effect = Instantiate(
                towerData.hitEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        PooledAutoReturn autoReturn = effect.GetComponent<PooledAutoReturn>();

        if (autoReturn == null)
            autoReturn = effect.AddComponent<PooledAutoReturn>();

        autoReturn.ReturnAfter(2f);
    }

    // Plays a hit sound when the projectile hits an enemy, with random pitch variation
    private void PlayHitSoundAndReturn()
    {
        SetRenderersVisible(false);

        if (towerData.hitSound == null || audioSource == null)
        {
            ReturnToPool();
            return;
        }

        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(towerData.hitSound, hitVolume);

        returnRoutine = StartCoroutine(ReturnAfterSound(towerData.hitSound.length));
    }

    // Coroutine to wait for the hit sound to finish 
    // before returning the projectile to the pool
    private IEnumerator ReturnAfterSound(float delay)
    {
        yield return new WaitForSeconds(delay);

        ReturnToPool();
    }

    // Method to set the visibility of the projectile's renderers
    private void SetRenderersVisible(bool visible)
    {
        if (renderers == null)
            renderers = GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    // Method to return the projectile to the pool and reset its state for reuse
    private void ReturnToPool()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        target = null;
        towerData = null;
        hasHit = false;

        SetRenderersVisible(true);

        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }

    // Ensure that any running return coroutine is stopped when the object is disabled to prevent unintended behavior
    private void OnDisable()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }
    }
}
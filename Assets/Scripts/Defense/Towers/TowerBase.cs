using System.Collections.Generic;
using UnityEngine;

public abstract class TowerBase : MonoBehaviour
{
    // Base class for all towers, contains common logic for targeting, attacking,
    // applying effects, playing sounds, and collecting enemies in range.

    [Header("Data")]
    [SerializeField] protected TowerData towerData;

    [Header("Scene References")]
    [SerializeField] protected Transform shootPoint;

    [Header("Audio")]
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected float soundVolume = 0.7f;
    [SerializeField] protected float pitchRandomMin = 0.95f;
    [SerializeField] protected float pitchRandomMax = 1.05f;

    protected Enemy currentTarget;
    protected Camera mainCamera;
    protected bool wasInitialized;

    // Reusable list for derived towers.
    // It is used to collect enemies without creating new arrays every attack.
    protected readonly List<Enemy> enemiesInRangeBuffer = new List<Enemy>();

    // Initialization method to set tower data from outside
    public void Initialize(TowerData data)
    {
        towerData = data;
        wasInitialized = true;
    }

    // Awake is called when the script instance is being loaded
    protected virtual void Awake()
    {
        // If no shoot point is assigned, use the tower transform itself
        if (shootPoint == null)
            shootPoint = transform;

        // Try to find AudioSource automatically if it was not assigned in Inspector
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D звук, однакова гучність
        }

        mainCamera = Camera.main;
    }

    // Start is called before the first frame update
    protected virtual void Start()
    {
        if (towerData == null)
        {
            Debug.LogWarning($"{name} has no TowerData!");
            return;
        }
    }

    // Update is called once per frame
    protected virtual void Update()
    {
        if (towerData == null)
            return;

        UpdateTarget();
        HandleAttack();
    }

    // Abstract method for handling the attack logic, implemented by derived classes
    protected abstract void HandleAttack();

    // Method to find the best target based on the tower's targeting mode
    protected Enemy FindTarget()
    {
        Enemy bestEnemy = null;
        float bestValue = 0f;

        // Loop through all active enemies from EnemyRegistry instead of using FindObjectsByType every frame.
        // We do not use ToArray() here because this method only searches for a target
        // and does not damage enemies, so it should not modify EnemyRegistry during the loop.
        foreach (Enemy enemy in EnemyRegistry.ActiveEnemies)
        {
            if (!CanTargetEnemy(enemy))
                continue;

            if (!IsVisibleInCamera(enemy.transform.position))
                continue;

            // Calculate distance to the enemy
            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance > towerData.range)
                continue;

            // Get the value of the enemy based on the targeting mode
            float value = GetTargetValue(enemy, distance);

            if (bestEnemy == null || value > bestValue)
            {
                bestEnemy = enemy;
                bestValue = value;
            }
        }

        return bestEnemy;
    }

    // Method to update the current target
    protected void UpdateTarget()
    {
        // If lockFirstTarget is enabled and the current target is still valid, keep it
        if (towerData.lockFirstTarget && IsValidTarget(currentTarget))
            return;

        currentTarget = FindTarget();
    }

    // Method to check if an enemy is still a valid target for this tower
    protected bool IsValidTarget(Enemy enemy)
    {
        if (!CanTargetEnemy(enemy))
            return false;

        if (!IsVisibleInCamera(enemy.transform.position))
            return false;

        // Check if the enemy is within range
        float distance = Vector3.Distance(transform.position, enemy.transform.position);
        return distance <= towerData.range;
    }

    // Method to collect all valid enemies within this tower's range into a reusable list
    protected void CollectEnemiesInRange(List<Enemy> result)
    {
        if (result == null)
            return;

        result.Clear();

        // First we only collect enemies.
        // We do not damage them here, because damage can kill enemies
        // and remove them from EnemyRegistry.
        foreach (Enemy enemy in EnemyRegistry.ActiveEnemies)
        {
            if (!CanTargetEnemy(enemy))
                continue;

            if (!IsVisibleInCamera(enemy.transform.position))
                continue;

            float distance = Vector2.Distance(
                transform.position,
                enemy.transform.position
            );

            if (distance <= towerData.range)
            {
                result.Add(enemy);
            }
        }
    }

    // Method to check if a world position is currently visible in the camera
    protected bool IsVisibleInCamera(Vector3 worldPosition)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return true;

        // Convert world position to viewport coordinates
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(worldPosition);

        // Check if the point is in front of the camera and within the viewport bounds
        return viewportPoint.z > 0 &&
               viewportPoint.x >= 0f &&
               viewportPoint.x <= 1f &&
               viewportPoint.y >= 0f &&
               viewportPoint.y <= 1f;
    }

    // Method to check if the tower can target a specific enemy
    protected bool CanTargetEnemy(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return false;

        // Some enemies can be immune to laser.
        // If this tower is set to ignore laser-immune enemies, it will not target them.
        if (towerData.ignoreLaserImmuneEnemies &&
            enemy.EnemyData != null &&
            enemy.EnemyData.immuneToLaser)
        {
            return false;
        }

        return true;
    }

    // Method to calculate the target value based on the tower's targeting mode
    protected float GetTargetValue(Enemy enemy, float distance)
    {
        switch (towerData.targetMode)
        {
            case TowerData.TargetMode.MostProgress:
                return enemy.PathProgress;

            case TowerData.TargetMode.Nearest:
                // Negative distance is used because FindTarget chooses the biggest value.
                // Smaller distance becomes a bigger value when negative.
                return -distance;

            case TowerData.TargetMode.Farthest:
                return distance;

            case TowerData.TargetMode.Weakest:
                // Negative health is used because lower HP should be selected as the bigger priority.
                return -enemy.CurrentHealth;

            case TowerData.TargetMode.Strongest:
                return enemy.CurrentHealth;

            default:
                return enemy.PathProgress;
        }
    }

    // Method to get the fire cooldown based on the tower's fire rate
    protected float GetFireCooldown()
    {
        return 1f / Mathf.Max(0.01f, towerData.fireRate);
    }

    // Method to apply extra effects like slow/freeze to the enemy
    protected void ApplyExtraEffects(Enemy enemy)
    {
        if (enemy == null)
            return;

        // Apply slow effect if enabled
        if (towerData.appliesSlow)
            enemy.ApplySlow(towerData.slowPercent, towerData.slowDuration);
    }

    // Method to play a sound effect with optional pitch randomization
    protected void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        // If no audio source is available, play the clip at the tower's position
        if (audioSource == null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
            return;
        }

        // Randomize pitch for variety
        audioSource.pitch = Random.Range(pitchRandomMin, pitchRandomMax);
        audioSource.PlayOneShot(clip, soundVolume);
    }

    // Draw the tower's range in the editor when selected.
    // This only appears in the Unity Scene view, not in the actual game.
    protected virtual void OnDrawGizmosSelected()
    {
        if (towerData != null)
            Gizmos.DrawWireSphere(transform.position, towerData.range);
    }

    // Called when the tower is disabled.
    // Derived tower types can override this to clean up their own effects or sounds.
    protected virtual void OnDisable()
    {
    }
}
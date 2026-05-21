using UnityEngine;

public class LaserTower : TowerBase
{
    [Header("Laser References")]
    [SerializeField] private LineRenderer laserLine;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource laserOneShotSource; // start/end
    [SerializeField] private AudioSource laserLoopSource;    // continuous loop while firing

    [Header("Laser Settings")]
    // Time the laser can remain active without a target before stopping 
    // and playing the end sound
    [SerializeField] private float laserIdleGraceTime = 0.2f;
    [SerializeField] private float textureScrollSpeed = 5f;

    [Header("Audio Settings")]
    [SerializeField] private float loopStartOverlap = 0.15f;

    private GameObject currentHitEffect;
    private bool laserActive;

    // Timer to track how long the laser has been active without a target
    // Used to determine when to stop the laser and play the end sound 
    // after a short grace period
    private float noTargetTimer;

    // Initializes references and sets up audio sources
    protected override void Awake()
    {
        base.Awake();

        if (laserLine == null)
            laserLine = GetComponentInChildren<LineRenderer>();

        if (laserLoopSource == null)
            laserLoopSource = GetComponent<AudioSource>();

        if (laserLine != null)
            laserLine.enabled = false;

        if (laserLoopSource != null)
        {
            laserLoopSource.playOnAwake = false;
            laserLoopSource.loop = true;
            laserLoopSource.clip = towerData != null ? towerData.laserLoopSound : null;
        }

        if (laserOneShotSource != null)
            laserOneShotSource.playOnAwake = false;
    }

    // Method to handle the attack logic
    protected override void HandleAttack()
    {
        // If there is no current target, increment the no-target 
        // timer and check if we should stop the laser
        if (currentTarget == null)
        {
            noTargetTimer += Time.deltaTime;

            if (noTargetTimer >= laserIdleGraceTime)
                StopLaserWithEndSound();

            return;
        }

        // If we have a target, reset the no-target timer and fire the laser
        noTargetTimer = 0f;
        FireLaser(currentTarget);
    }

    // Method to handle the logic of firing the laser at the target enemy
    private void FireLaser(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return;

        StartLaserIfNeeded();

        Vector3 startPosition = shootPoint.position;
        Vector3 endPosition = enemy.transform.position;

        // Update the laser line renderer to visually connect the tower to the target enemy
        if (laserLine != null)
        {
            laserLine.enabled = true;
            laserLine.SetPosition(0, startPosition);
            laserLine.SetPosition(1, endPosition);

            if (laserLine.material != null)
            {
                // Scroll the laser texture to create a dynamic visual effect while firing
                laserLine.material.mainTextureOffset +=
                    new Vector2(textureScrollSpeed * Time.deltaTime, 0f);
            }
        }

        // Apply continuous damage to the enemy based on the tower's laser damage per second
        enemy.TakeDamage(towerData.laserDamagePerSecond * Time.deltaTime);

        // Apply any extra effects from the tower to the enemy and update the hit effect position
        ApplyExtraEffects(enemy);
        UpdateHitEffect(endPosition);
    }

    // Method to start the laser effects and sounds if not already active
    private void StartLaserIfNeeded()
    {
        if (laserActive)
            return;

        laserActive = true;

        if (laserLine != null)
            laserLine.enabled = true;

        // Play the laser start sound and then transition to the 
        // looping sound after a short overlap
        if (laserOneShotSource != null && towerData.laserStartSound != null)
        {
            laserOneShotSource.PlayOneShot(towerData.laserStartSound);
        }

        // Start the looping laser sound with a delay to overlap with the start
        // sound for audio transition
        if (laserLoopSource != null && towerData.laserLoopSound != null)
        {
            laserLoopSource.Stop();
            laserLoopSource.clip = towerData.laserLoopSound;
            laserLoopSource.loop = true;
            laserLoopSource.time = 0f;

            float delay = 0f;

            // Calculate the delay for the looping sound to start based on the 
            // length of the start sound
            if (towerData.laserStartSound != null)
                delay = Mathf.Max(0f, towerData.laserStartSound.length - loopStartOverlap);

            laserLoopSource.PlayDelayed(delay);
        }
    }

    // Method to stop the laser effects and play the end sound when the laser is no longer active
    private void StopLaserWithEndSound()
    {
        if (!laserActive)
            return;

        laserActive = false;

        if (laserLine != null)
            laserLine.enabled = false;

        ReturnHitEffectToPool();

        if (laserLoopSource != null)
        {
            // Stop the looping sound
            laserLoopSource.Stop();
            // Reset the loop to be ready for the next time we start the laser
            laserLoopSource.loop = true;
        }

        // Play the laser end sound when the laser stops, if available
        if (laserOneShotSource != null && towerData.laserEndSound != null)
        {
            laserOneShotSource.PlayOneShot(towerData.laserEndSound);
        }
    }

    // Method to forcefully disable the laser effects and sounds,
    //  used when the tower is disabled or destroyed
    private void ForceDisableLaser()
    {
        laserActive = false;
        noTargetTimer = 0f;

        if (laserLine != null)
            laserLine.enabled = false;

        ReturnHitEffectToPool();

        if (laserLoopSource != null)
        {
            laserLoopSource.Stop();
            laserLoopSource.loop = true;
        }

        if (laserOneShotSource != null)
        {
            laserOneShotSource.Stop();
        }
    }

    // Method to update the position of the hit effect at the laser's impact point
    private void UpdateHitEffect(Vector3 position)
    {
        if (towerData.laserHitEffectPrefab == null)
            return;

        if (currentHitEffect == null)
        {
            // Try to spawn the laser hit effect from the object pool, if available,
            // otherwise instantiate a new one
            if (ObjectPooler.Instance != null)
            {
                currentHitEffect = ObjectPooler.Instance.Spawn(
                    towerData.laserHitEffectPrefab,
                    position,
                    Quaternion.identity
                );
            }
            else
            {
                currentHitEffect = Instantiate(
                    towerData.laserHitEffectPrefab,
                    position,
                    Quaternion.identity
                );
            }
        }

        // Activate and position the hit effect at the laser's impact point
        currentHitEffect.SetActive(true);
        currentHitEffect.transform.position = position;
    }

    // Method to return the laser hit effect to the pool when the laser stops
    private void ReturnHitEffectToPool()
    {
        if (currentHitEffect == null)
            return;

        if (ObjectPooler.Instance != null)
        {
            ObjectPooler.Instance.ReturnToPool(currentHitEffect);
        }
        else
        {
            Destroy(currentHitEffect);
        }

        currentHitEffect = null;
    }

    // Clean up the laser effects and sounds when the tower is disabled
    protected override void OnDisable()
    {
        ForceDisableLaser();
        base.OnDisable();
    }
}
using UnityEngine;

public class ProjectileTower : TowerBase
{
    // Tower that shoots projectiles at enemies.
    private float fireTimer;

    protected override void Start()
    {
        base.Start();
        fireTimer = GetFireCooldown();
    }

    // Updates the attack timer, then lets TowerBase update target and attack logic.
    protected override void Update()
    {
        if (towerData == null)
            return;

        fireTimer += Time.deltaTime;
        base.Update();
    }

    // Method to handle the attack logic, called every frame in Update
    protected override void HandleAttack()
    {
        if (currentTarget == null)
            return;

        if (fireTimer >= GetFireCooldown())
        {
            ShootProjectile(currentTarget);
            fireTimer = 0f;
        }
    }

    // Creates a projectile and gives it the selected enemy as a target.
    // The projectile itself handles movement, hit detection, damage and effects.
    private void ShootProjectile(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return;

        if (towerData.projectilePrefab == null)
        {
            Debug.LogWarning($"{towerData.towerName} has no [projectile] prefab!");
            return;
        }

        // Try to spawn the projectile from the object pool, if available,
        //  otherwise instantiate a new one
        GameObject projectile;

        if (ObjectPooler.Instance != null)
        {
            projectile = ObjectPooler.Instance.Spawn(
                towerData.projectilePrefab,
                shootPoint.position,
                Quaternion.identity
            );
        }
        else
        {
            projectile = Instantiate(
                towerData.projectilePrefab,
                shootPoint.position,
                Quaternion.identity
            );
        }

        // Play the shooting sound effect when firing the projectile
        PlaySound(towerData.shootSound);

        Projectile projectileScript = projectile.GetComponent<Projectile>();

        if (projectileScript != null)
        {
            projectileScript.Initialize(enemy.transform, towerData);
        }
    }
}
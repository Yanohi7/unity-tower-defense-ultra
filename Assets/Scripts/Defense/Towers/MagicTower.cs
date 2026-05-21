using UnityEngine;

public class MagicTower : TowerBase
{
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
            CastAreaAttack();
            fireTimer = 0f;
        }
    }

    // Method to perform an area attack, damaging all enemies within range
    private void CastAreaAttack()
    {
        PlaySound(towerData.shootSound);

        SpawnAreaEffect(transform.position);

        CollectEnemiesInRange(enemiesInRangeBuffer);

        int enemyCount = enemiesInRangeBuffer.Count;

        float finalDamage = towerData.damage;

        // If there are more than 3 enemies, reduce damage for this attack.
        // Each extra enemy after 3 reduces damage by 10%,
        // but damage cannot go below 50% of the base damage.
        if (enemyCount > 3)
        {
            float damageMultiplier = 1f - (enemyCount - 3) * 0.1f;
            damageMultiplier = Mathf.Max(0.3f, damageMultiplier);

            finalDamage = towerData.damage * damageMultiplier;
        }

        foreach (Enemy enemy in enemiesInRangeBuffer)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            float damageToDeal = finalDamage;

            // Ghost enemies take extra damage from magic tower
            if (towerData != null && enemy.EnemyData.enemyName.Contains("Ghost"))
            {
                damageToDeal *= towerData.ghostDamageMultiplier;
            }

            enemy.TakeDamage(damageToDeal);
            ApplyExtraEffects(enemy);
        }
    }

    // Method to spawn an area effect at the tower's position when casting an area attack
    private void SpawnAreaEffect(Vector3 position)
    {
        if (towerData.hitEffectPrefab == null)
            return;

        GameObject effect;

        // Try to spawn the area effect from the object pool, if available,
        // otherwise instantiate a new one
        if (ObjectPooler.Instance != null)
        {
            effect = ObjectPooler.Instance.Spawn(
                towerData.hitEffectPrefab,
                position,
                Quaternion.identity
            );
        }
        else
        {
            effect = Instantiate(
                towerData.hitEffectPrefab,
                position,
                Quaternion.identity
            );
        }

        MagicAreaEffect areaEffect = effect.GetComponent<MagicAreaEffect>();

        if (areaEffect != null)
        {
            areaEffect.Initialize(towerData.range);
        }
        else
        {
            PooledAutoReturn autoReturn = effect.GetComponent<PooledAutoReturn>();

            if (autoReturn == null)
                autoReturn = effect.AddComponent<PooledAutoReturn>();

            autoReturn.ReturnAfter(2f);
        }
    }

    // OnDrawGizmosSelected method to visualize the tower's range in the editor
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
    }
}
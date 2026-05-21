using UnityEngine;

[CreateAssetMenu(fileName = "NewTowerData", menuName = "Tower Defense/Tower Data")]
public class TowerData : ScriptableObject
{
    public enum TowerAttackType
    {
        Projectile,
        Laser
    }

    public enum TargetMode
    {
        MostProgress,
        Nearest,
        Farthest,
        Weakest,
        Strongest
    }

    [Header("Main")]
    public string towerName;
    public GameObject towerPrefab;

    public int cost = 100;
    public float damage = 10f;
    public float range = 5f;
    public float fireRate = 1f;

    [Header("Targeting")]
    public TargetMode targetMode = TargetMode.MostProgress;
    public bool lockFirstTarget = false;

    [Header("Attack Type")]
    public TowerAttackType attackType = TowerAttackType.Projectile;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    [Header("Damage Multipliers")]
    public float ghostDamageMultiplier;


    [Header("Laser")]
    public float laserDamagePerSecond = 10f;
    public GameObject laserHitEffectPrefab;
    [Header("Target Restrictions")]
    public bool ignoreLaserImmuneEnemies = false;

    [Header("Slow / Freeze")]
    public bool appliesSlow = false;
    public float slowPercent = 0.5f;
    public float slowDuration = 2f;
    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip hitSound;

    [Header("Laser Audio")]
    public AudioClip laserStartSound;
    public AudioClip laserLoopSound;
    public AudioClip laserEndSound;

    [Header("Visual")]
    public GameObject hitEffectPrefab;
}
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Tower Defense/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Main")]
    public string enemyName;
    public GameObject prefab;

    [Header("Stats")]
    public float maxHP = 10f;
    public float speed = 2f;

    [Header("Rewards / Cost")]
    public int goldReward = 10;
    public int attackPointCost = 10;

    [Header("Special")]
    public bool immuneToSlow = false;
    public bool immuneToLaser = false;
    public bool immuneToDefenders = false;

    [Header("Audio")]
    public AudioClip deathSound;
    public AudioClip[] defenderHitSounds;
    public AudioClip[] idleSounds;
}
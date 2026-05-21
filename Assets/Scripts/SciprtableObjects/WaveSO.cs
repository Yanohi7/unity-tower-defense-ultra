using UnityEngine;

[System.Serializable]
public class WaveEnemyEntry
{
    public GameObject enemyPrefab;
    public float spawnDelayAfter = 1f;
}

[CreateAssetMenu(fileName = "NewWave", menuName = "Tower Defense/Wave")]
public class WaveSO : ScriptableObject
{
    [Header("Enemy Queue")]
    public WaveEnemyEntry[] enemyQueue;

    [Header("Scaling")]
    public float hpMultiplier = 1f;
    public float goldMultiplier = 1f;
}
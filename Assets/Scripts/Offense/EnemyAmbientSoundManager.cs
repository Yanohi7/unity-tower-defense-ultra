using System.Collections.Generic;
using UnityEngine;

public class EnemyAmbientSoundManager : MonoBehaviour
{
    // Manager that plays random idle sounds from active enemies.

    [Header("Timing")]
    [SerializeField] private float minDelay = 2f;
    [SerializeField] private float maxDelay = 6f;

    [Header("Enemy Count Influence")]
    [SerializeField] private int enemiesForFastestSounds = 30;
    [SerializeField] private float minDelayWhenManyEnemies = 1.2f;
    [SerializeField] private float maxDelayWhenManyEnemies = 3f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float volume = 0.45f;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

    private float timer;
    private float nextSoundTime;

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        PickNextSoundTime();
    }

    // Update counts time and plays a random enemy idle sound when the timer is ready
    private void Update()
    {
        timer += Time.deltaTime;

        if (timer < nextSoundTime)
            return;

        timer = 0f;

        int validEnemyCount = CountAliveEnemiesWithIdleSounds();

        if (validEnemyCount <= 0)
        {
            PickNextSoundTime(0);
            return;
        }

        PlayRandomEnemySound(validEnemyCount);
        PickNextSoundTime(validEnemyCount);
    }

    // Method to count active enemies that are alive and have idle sounds
    private int CountAliveEnemiesWithIdleSounds()
    {
        int count = 0;

        foreach (Enemy enemy in EnemyRegistry.ActiveEnemies)
        {
            if (!IsValidEnemyForIdleSound(enemy))
                continue;

            count++;
        }

        return count;
    }

    // Method to check if an enemy can be used for idle ambient sounds
    private bool IsValidEnemyForIdleSound(Enemy enemy)
    {
        if (enemy == null || enemy.IsDead)
            return false;

        AudioClip[] sounds = enemy.IdleSounds;

        if (sounds == null || sounds.Length == 0)
            return false;

        return true;
    }

    // Method to choose the next time when an enemy sound should be played
    private void PickNextSoundTime(int enemyCount = 0)
    {
        float t = 0f;

        if (enemiesForFastestSounds > 0)
        {
            t = Mathf.Clamp01((float)enemyCount / enemiesForFastestSounds);
        }

        float currentMinDelay = Mathf.Lerp(minDelay, minDelayWhenManyEnemies, t);
        float currentMaxDelay = Mathf.Lerp(maxDelay, maxDelayWhenManyEnemies, t);

        nextSoundTime = Random.Range(currentMinDelay, currentMaxDelay);
    }

    // Method to play a random idle sound from one of the active enemies
    private void PlayRandomEnemySound(int validEnemyCount)
    {
        if (audioSource == null || validEnemyCount <= 0)
            return;

        Enemy enemy = PickRandomEnemyWithIdleSounds(validEnemyCount);

        if (enemy == null)
            return;

        AudioClip[] sounds = enemy.IdleSounds;

        if (sounds == null || sounds.Length == 0)
            return;

        AudioClip clip = sounds[Random.Range(0, sounds.Length)];

        if (clip == null)
            return;

        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(clip, volume);
    }

    // Method to pick a random valid enemy without creating a temporary list or array
    private Enemy PickRandomEnemyWithIdleSounds(int validEnemyCount)
    {
        int randomIndex = Random.Range(0, validEnemyCount);
        int currentValidIndex = 0;

        foreach (Enemy enemy in EnemyRegistry.ActiveEnemies)
        {
            if (!IsValidEnemyForIdleSound(enemy))
                continue;

            if (currentValidIndex == randomIndex)
                return enemy;

            currentValidIndex++;
        }

        return null;
    }
}
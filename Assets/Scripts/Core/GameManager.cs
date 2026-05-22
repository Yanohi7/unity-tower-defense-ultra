using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Main game manager responsible for player resources, lives, wave flow,
    // win/lose conditions, global game sounds, and state machine.

    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        Preparation,
        Battle,
        RoundEnd,
        GameOver,
        Victory
    }

    [Header("Gold")]
    [SerializeField] private int startGold = 250;
    [SerializeField] public int CurrentGold;

    [Header("Lives")]
    [SerializeField] private int maxLives = 10;
    [SerializeField] public int lives;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI waveText;

    [Header("Round State Machine")]
    [SerializeField] private float preparationDuration = 30f;
    [SerializeField] private float roundEndDuration = 2f;

    [Header("HotSeat")]
    [SerializeField] private int maxHotSeatRounds = 10;

    [Header("Building Rules")]
    [SerializeField] private bool allowBuildingDuringBattle = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip waveStartSound;
    [SerializeField] private AudioClip gameStartSound;
    [SerializeField] private AudioClip loseBaseHPSound;

    [Header("Screens")]
    [SerializeField] private GameObject gameOverScreen;
    [SerializeField] private GameObject winScreen;

    private Spawner[] spawners;

    private GameState currentState;
    private float stateTimer;
    private bool gameEnded = false;

    private int aliveEnemies = 0;

    public GameState CurrentState => currentState;
    public float StateTimer => stateTimer;
    public bool IsGameEnded => gameEnded;
    public int AliveEnemies => aliveEnemies;

    // CanBuild is used by BuildTile to decide if the player is allowed to build now.
    // Preparation always allows building.
    // Battle allows building only if allowBuildingDuringBattle is enabled.
    public bool CanBuild { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Make sure the game is not paused when the scene starts or restarts.
        Time.timeScale = 1f;

        // Find AudioSource automatically if it was not assigned in Inspector.
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Play start sound when the level begins.
        if (audioSource != null && gameStartSound != null)
            audioSource.PlayOneShot(gameStartSound);

        // Find all spawners in the scene.
        spawners = FindObjectsByType<Spawner>(FindObjectsSortMode.None);

        if (spawners == null || spawners.Length == 0)
        {
            Debug.LogError($"GameManager on {gameObject.name}: no Spawners were found!");
        }

        // Initialize player resources.
        CurrentGold = startGold;
        lives = maxLives;
        aliveEnemies = 0;

        // Update all UI at the start of the game.
        UpdateGoldUI();
        UpdateLivesUI();
        UpdateWaveUI();

        // Hide end screens at the start.
        if (gameOverScreen != null)
            gameOverScreen.SetActive(false);

        if (winScreen != null)
            winScreen.SetActive(false);

        // Start with preparation instead of instantly spawning enemies.
        StartPreparation();
    }

    private void Update()
    {
        if (gameEnded || spawners == null || spawners.Length == 0)
            return;

        switch (currentState)
        {
            case GameState.Preparation:
                UpdatePreparation();
                break;

            case GameState.Battle:
                UpdateBattle();
                break;

            case GameState.RoundEnd:
                UpdateRoundEnd();
                break;
        }
    }

    // Method called by Spawner when a valid enemy is spawned.
    public void RegisterEnemy()
    {
        aliveEnemies++;
    }

    // Method called by Spawner when an enemy dies or reaches the end.
    public void UnregisterEnemy()
    {
        aliveEnemies--;

        if (aliveEnemies < 0)
            aliveEnemies = 0;
    }

    // Method to start preparation stage before the next wave.
    private void StartPreparation()
    {
        if (gameEnded)
            return;

        if (ShouldWinNow())
        {
            WinGame();
            return;
        }

        currentState = GameState.Preparation;
        stateTimer = preparationDuration;

        // Player can always build during preparation.
        CanBuild = true;

        UpdateWaveUI();
    }

    // Method to update preparation timer.
    private void UpdatePreparation()
    {
        // In HotSeat mode preparation has no timer.
        // The attacking player must manually press Start Battle after choosing enemies.
        if (AllSpawnersHotSeat())
            return;

        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            StartBattle();
        }
    }

    // Public method for normal Skip Preparation button or HotSeat Start Battle button.
    public void SkipPreparation()
    {
        if (gameEnded)
            return;

        if (currentState != GameState.Preparation)
            return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();

        StartBattle();
    }

    public void BackToMenu()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();

        SceneManager.LoadScene("MainMenu");
    }

    // Method to start battle stage and spawn the next wave on all available spawners.
    private void StartBattle()
    {
        if (gameEnded)
            return;

        bool atLeastOneSpawnerStarted = false;

        foreach (Spawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            if (spawner.CanStartNextWave())
            {
                spawner.StartNextWave();
                atLeastOneSpawnerStarted = true;
            }
        }

        if (!atLeastOneSpawnerStarted)
        {
            if (ShouldWinNow())
                WinGame();

            return;
        }

        currentState = GameState.Battle;

        // Building during battle depends on the selected rule.
        CanBuild = allowBuildingDuringBattle;

        UpdateWaveUI();

        // Play wave start sound.
        if (audioSource != null && waveStartSound != null)
            audioSource.PlayOneShot(waveStartSound);
    }

    // Method to update battle stage.
    private void UpdateBattle()
    {
        // Battle ends only when all spawners finished spawning
        // and all active enemies are gone.
        if (!AnySpawnerStillSpawning() && aliveEnemies <= 0)
        {
            StartRoundEnd();
        }
    }

    // Method to start round end stage after a wave is cleared.
    private void StartRoundEnd()
    {
        if (gameEnded)
            return;

        currentState = GameState.RoundEnd;
        stateTimer = roundEndDuration;

        // Building is disabled during this short transition stage.
        CanBuild = false;

        UpdateWaveUI();
    }

    // Method to update round end timer and decide what happens next.
    private void UpdateRoundEnd()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer > 0f)
            return;

        if (ShouldWinNow())
        {
            WinGame();
            return;
        }

        StartPreparation();
    }

    // Method to check if any spawner is still spawning enemies.
    private bool AnySpawnerStillSpawning()
    {
        foreach (Spawner spawner in spawners)
        {
            if (spawner != null && spawner.isSpawning)
                return true;
        }

        return false;
    }

    // Method to check if the current finite game should end with victory.
    private bool ShouldWinNow()
    {
        if (aliveEnemies > 0)
            return false;

        if (AnySpawnerStillSpawning())
            return false;

        if (AllAdventureSpawnersFinished())
            return true;

        if (AllHotSeatSpawnersFinished())
            return true;

        return false;
    }

    // Method to check if all adventure spawners have finished their wave lists.
    private bool AllAdventureSpawnersFinished()
    {
        bool hasAdventureSpawner = false;

        foreach (Spawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            if (!spawner.IsAdventureMode())
                continue;

            hasAdventureSpawner = true;

            if (spawner.HasMoreAdventureWaves())
                return false;

            if (spawner.isSpawning)
                return false;
        }

        return hasAdventureSpawner;
    }

    // Method to check if all HotSeat spawners reached the maximum round count.
    private bool AllHotSeatSpawnersFinished()
    {
        bool hasHotSeatSpawner = false;

        foreach (Spawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            if (!spawner.IsHotSeatMode())
                continue;

            hasHotSeatSpawner = true;

            if (spawner.CurrentWaveNumber < maxHotSeatRounds)
                return false;

            if (spawner.isSpawning)
                return false;
        }

        return hasHotSeatSpawner;
    }

    // Method to check if all spawners use HotSeat mode.
    private bool AllSpawnersHotSeat()
    {
        if (spawners == null || spawners.Length == 0)
            return false;

        foreach (Spawner spawner in spawners)
        {
            if (spawner == null)
                continue;

            if (!spawner.IsHotSeatMode())
                return false;
        }

        return true;
    }

    // Method to add gold to the player.
    public void AddGold(int amount)
    {
        CurrentGold += amount;
        UpdateGoldUI();
    }

    // Method to spend gold if the player has enough.
    public bool SpendGold(int amount)
    {
        if (CurrentGold >= amount)
        {
            CurrentGold -= amount;
            UpdateGoldUI();
            return true;
        }

        return false;
    }

    // Method called when an enemy reaches the end of the path.
    public void LoseLife()
    {
        if (gameEnded)
            return;

        if (lives > 0)
        {
            lives--;
            UpdateLivesUI();

            if (audioSource != null && loseBaseHPSound != null)
                audioSource.PlayOneShot(loseBaseHPSound);
        }

        if (lives <= 0)
        {
            GameOver();
        }
    }

    // Method to update gold UI text.
    private void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = CurrentGold.ToString();
    }

    // Method to update lives UI text.
    private void UpdateLivesUI()
    {
        if (livesText != null)
            livesText.text = lives.ToString();
    }

    // Method to update wave UI text.
    private void UpdateWaveUI()
    {
        if (waveText == null || spawners == null || spawners.Length == 0)
            return;

        int highestWave = 0;

        foreach (Spawner spawner in spawners)
        {
            if (spawner != null && spawner.CurrentWaveNumber > highestWave)
                highestWave = spawner.CurrentWaveNumber;
        }

        // +1 during preparation makes UI show the wave being prepared.
        // During battle and round end, CurrentWaveNumber represents the active or finished wave.
        if (currentState == GameState.Preparation)
            waveText.text = "Wave " + (highestWave + 1);
        else
            waveText.text = "Wave " + highestWave;
    }

    // Method to end the game with defeat.
    private void GameOver()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        currentState = GameState.GameOver;
        CanBuild = false;

        StopAllSpawners();

        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);

        if (gameOverScreen != null)
            gameOverScreen.SetActive(true);

        // Pause the game after losing.
        Time.timeScale = 0f;
    }

    // Method to end the game with victory.
    private void WinGame()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        currentState = GameState.Victory;
        CanBuild = false;

        StopAllSpawners();

        if (audioSource != null && winSound != null)
            audioSource.PlayOneShot(winSound);

        if (winScreen != null)
            winScreen.SetActive(true);

        // Pause the game after winning.
        Time.timeScale = 0f;
    }

    // Method to stop every spawner in the scene.
    private void StopAllSpawners()
    {
        foreach (Spawner spawner in spawners)
        {
            if (spawner != null)
                spawner.StopSpawning();
        }
    }
}
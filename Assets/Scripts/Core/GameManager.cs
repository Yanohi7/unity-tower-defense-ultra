using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Main game manager responsible for player resources, lives, wave flow,
    // win/lose conditions, global game sounds, and round state machine.

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

    private Spawner spawner;

    private GameState currentState;
    private float stateTimer;
    private bool gameEnded = false;

    public GameState CurrentState => currentState;
    public float StateTimer => stateTimer;
    public bool IsGameEnded => gameEnded;

    // CanBuild is used by BuildTile to decide if the player is allowed to build now.
    // Preparation always allows building.
    // Battle allows building only if allowBuildingDuringBattle is enabled.
    public bool CanBuild { get; private set; }

    // Start is called before the first frame update
    private void Start()
    {
        // Make sure the game is not paused when the scene starts or restarts
        Time.timeScale = 1f;

        // Find AudioSource automatically if it was not assigned in Inspector
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // Play start sound when the level begins
        if (audioSource != null && gameStartSound != null)
            audioSource.PlayOneShot(gameStartSound);

        // Find the spawner that controls waves
        spawner = FindAnyObjectByType<Spawner>();

        if (spawner == null)
        {
            Debug.LogError($"GameManager on {gameObject.name}: Spawner was not found!");
        }

        // Initialize player resources
        CurrentGold = startGold;
        lives = maxLives;

        // Update all UI at the start of the game
        UpdateGoldUI();
        UpdateLivesUI();
        UpdateWaveUI();

        // Hide end screens at the start
        if (gameOverScreen != null)
            gameOverScreen.SetActive(false);

        if (winScreen != null)
            winScreen.SetActive(false);

        // Start with preparation instead of instantly spawning enemies
        StartPreparation();
    }

    // Update is called once per frame
    private void Update()
    {
        if (gameEnded || spawner == null)
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

    // Method to start preparation stage before the next wave
    private void StartPreparation()
    {
        if (gameEnded || spawner == null)
            return;

        // Adventure mode can end when there are no more prepared waves
        if (spawner.IsAdventureMode() && !spawner.HasMoreAdventureWaves())
        {
            WinGame();
            return;
        }

        // HotSeat mode can end after a fixed number of rounds if the defender survived
        if (spawner.IsHotSeatMode() && spawner.CurrentWaveNumber >= maxHotSeatRounds)
        {
            WinGame();
            return;
        }

        currentState = GameState.Preparation;
        stateTimer = preparationDuration;

        // Player can always build during preparation
        CanBuild = true;

        UpdateWaveUI();
    }

    // Method to update preparation timer
    private void UpdatePreparation()
    {
        // In HotSeat mode preparation has no timer.
        // The attacking player must manually press Start Battle after choosing enemies.
        if (spawner != null && spawner.IsHotSeatMode())
            return;

        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            StartBattle();
        }
    }

    // Public method for normal Skip Preparation button or HotSeat Start Battle button
    public void SkipPreparation()
    {
        if (gameEnded || spawner == null)
            return;

        // Skip button should only work during preparation
        if (currentState != GameState.Preparation)
            return;

        AudioManager.Instance.PlayClick();
        StartBattle();
    }

    public void BackToMenu()
    {
        AudioManager.Instance.PlayClick();
        SceneManager.LoadScene("MainMenu");
    }

    // Method to start battle stage and spawn the next wave
    private void StartBattle()
    {
        if (gameEnded || spawner == null)
            return;

        // Adventure mode cannot start battle if there are no waves left
        if (spawner.IsAdventureMode() && !spawner.HasMoreAdventureWaves())
        {
            WinGame();
            return;
        }

        // HotSeat mode cannot start battle with an empty attacker wave
        if (spawner.IsHotSeatMode() && !spawner.CanStartNextWave())
        {
            Debug.Log("HotSeat wave is empty. Add enemies before starting battle.");
            return;
        }

        // AI mode can always generate a new wave.
        // Adventure and HotSeat must pass their own checks above.
        if (!spawner.CanStartNextWave())
            return;

        currentState = GameState.Battle;

        // Building during battle depends on the selected rule.
        // For HotSeat PvP this should usually be false in Inspector.
        CanBuild = allowBuildingDuringBattle;

        // Ask the spawner to start the next wave
        spawner.StartNextWave();

        UpdateWaveUI();

        // Play wave start sound
        if (audioSource != null && waveStartSound != null)
            audioSource.PlayOneShot(waveStartSound);
    }

    // Method to update battle stage
    private void UpdateBattle()
    {
        // Battle ends only when the spawner finished spawning
        // and all active enemies are gone
        if (spawner.IsWaveFinished())
        {
            StartRoundEnd();
        }
    }

    // Method to start round end stage after a wave is cleared
    private void StartRoundEnd()
    {
        if (gameEnded || spawner == null)
            return;

        currentState = GameState.RoundEnd;
        stateTimer = roundEndDuration;

        // Building is disabled during this short transition stage
        CanBuild = false;

        UpdateWaveUI();
    }

    // Method to update round end timer and decide what happens next
    private void UpdateRoundEnd()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer > 0f)
            return;

        // Adventure mode wins after the last wave is finished
        if (spawner.IsAdventureMode() && !spawner.HasMoreAdventureWaves())
        {
            WinGame();
            return;
        }

        // HotSeat defender wins if they survived enough rounds
        if (spawner.IsHotSeatMode() && spawner.CurrentWaveNumber >= maxHotSeatRounds)
        {
            WinGame();
            return;
        }

        // Endless AI and HotSeat continue back to preparation
        StartPreparation();
    }

    // Method to add gold to the player
    public void AddGold(int amount)
    {
        CurrentGold += amount;
        UpdateGoldUI();
    }

    // Method to spend gold if the player has enough
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

    // Method called when an enemy reaches the end of the path
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

    // Method to update gold UI text
    private void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = CurrentGold.ToString();
    }

    // Method to update lives UI text
    private void UpdateLivesUI()
    {
        if (livesText != null)
            livesText.text = lives.ToString();
    }

    // Method to update wave UI text
    private void UpdateWaveUI()
    {
        if (waveText == null || spawner == null)
            return;

        // +1 during preparation makes UI show the wave being prepared.
        // During battle/round end CurrentWaveNumber already represents the active or finished wave.
        if (currentState == GameState.Preparation)
            waveText.text = "Wave " + (spawner.CurrentWaveNumber + 1);
        else
            waveText.text = "Wave " + spawner.CurrentWaveNumber;
    }

    // Method to end the game with defeat
    private void GameOver()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        currentState = GameState.GameOver;
        CanBuild = false;

        if (spawner != null)
            spawner.StopSpawning();

        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);

        if (gameOverScreen != null)
            gameOverScreen.SetActive(true);

        // Pause the game after losing
        Time.timeScale = 0f;
    }

    // Method to end the game with victory
    private void WinGame()
    {
        if (gameEnded)
            return;

        gameEnded = true;
        currentState = GameState.Victory;
        CanBuild = false;

        if (spawner != null)
            spawner.StopSpawning();

        if (audioSource != null && winSound != null)
            audioSource.PlayOneShot(winSound);

        if (winScreen != null)
            winScreen.SetActive(true);

        // Pause the game after winning
        Time.timeScale = 0f;
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    // Manager responsible for updating the in-game UI elements like gold, lives,
    // state text, timer text, and handling UI-related actions like skipping preparation,
    // restarting or quitting the game.

    private GameManager gameManager;
    private Spawner spawner;

    [Header("Main UI")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI livesText;

    [Header("State UI")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI stateTimerText;
    [SerializeField] private GameObject skipPreparationButton;

    [Header("Audio")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioSource audioSource;

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        spawner = FindAnyObjectByType<Spawner>();

        if (gameManager == null)
        {
            Debug.LogError($"UIManager on {gameObject.name}: GameManager was not found in scene {SceneManager.GetActiveScene().name}!");
        }

        if (spawner == null)
        {
            Debug.LogError($"UIManager on {gameObject.name}: Spawner was not found in scene {SceneManager.GetActiveScene().name}!");
        }

        if (goldText == null)
        {
            Debug.LogError($"UIManager on {gameObject.name}: Gold Text is not assigned!");
        }

        if (livesText == null)
        {
            Debug.LogError($"UIManager on {gameObject.name}: Lives Text is not assigned!");
        }

        UpdateGoldUI();
        UpdateLivesUI();
        UpdateStateUI();
    }

    // Update is called once per frame
    private void Update()
    {
        UpdateGoldUI();
        UpdateLivesUI();
        UpdateStateUI();
    }

    // Public method to update gold UI
    public void UpdateGoldUI()
    {
        if (gameManager == null || goldText == null)
            return;

        goldText.text = "Gold: " + gameManager.CurrentGold;
    }

    // Public method to update lives UI
    public void UpdateLivesUI()
    {
        if (gameManager == null || livesText == null)
            return;

        livesText.text = "Lives: " + gameManager.lives;
    }

    // Method to update preparation and round end UI
    private void UpdateStateUI()
    {
        if (gameManager == null)
            return;

        bool isHotSeat = spawner != null && spawner.IsHotSeatMode();
        bool showPreparation = gameManager.CurrentState == GameManager.GameState.Preparation;
        bool showRoundEnd = gameManager.CurrentState == GameManager.GameState.RoundEnd;

        if (stageText != null)
        {
            if (showPreparation)
            {
                stageText.gameObject.SetActive(true);
                stageText.text = "Preparation";
            }
            else if (showRoundEnd)
            {
                stageText.gameObject.SetActive(true);
                stageText.text = "Round End";
            }
            else
            {
                // Battle does not need text, and GameOver/Victory have their own screens
                stageText.text = "";
                stageText.gameObject.SetActive(false);
            }
        }

        if (stateTimerText != null)
        {
            // In HotSeat mode preparation has no visible timer.
            // The attacker manually starts the battle after choosing enemies.
            bool showTimer =
                showRoundEnd ||
                (showPreparation && !isHotSeat);

            if (showTimer)
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, gameManager.StateTimer));

                stateTimerText.gameObject.SetActive(true);
                stateTimerText.text = seconds.ToString();
            }
            else
            {
                stateTimerText.text = "";
                stateTimerText.gameObject.SetActive(false);
            }
        }

        if (skipPreparationButton != null)
        {
            // Normal skip button is visible only during preparation and only outside HotSeat.
            // In HotSeat mode battle should be started from HotSeatAttackBuilder after selecting enemies.
            skipPreparationButton.SetActive(showPreparation && !isHotSeat);
        }
    }

    // Method called by normal Skip Preparation button
    public void SkipPreparation()
    {
        PlayButtonSound();

        if (gameManager != null)
        {
            gameManager.SkipPreparation();
        }
    }

    // Methods to handle button actions, called from UI buttons
    public void RestartLevel()
    {
        PlayButtonSound();

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Method to quit the game, called from a UI button
    public void QuitGame()
    {
        PlayButtonSound();

        Application.Quit();
    }

    // Helper method to play a button click sound effect
    private void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }
}
using TMPro;
using UnityEngine;

public class HotSeatAttackBuilder : MonoBehaviour
{
    // UI helper for HotSeat attacker.
    // It does not generate waves by itself.
    // It only tells Spawner which enemies the attacking player selected.

    [Header("References")]
    [SerializeField] private Spawner spawner;
    [SerializeField] private GameManager gameManager;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI budgetText;
    [SerializeField] private TextMeshProUGUI selectedEnemiesText;

    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Buttons")]
    [SerializeField] private GameObject[] enemyOptionButtons;
    [SerializeField] private GameObject[] towerButtons;
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip enemyAddSound;


    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        // If panel was not assigned, use this object as the panel visual root
        if (panel == null)
            panel = gameObject;

        // CanvasGroup lets us hide the panel without disabling the script object
        if (panelCanvasGroup == null && panel != null)
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();

        if (panelCanvasGroup == null && panel != null)
            panelCanvasGroup = panel.AddComponent<CanvasGroup>();
    }

    // Start is called before the first frame update
    private void Start()
    {
        if (spawner == null)
            spawner = FindAnyObjectByType<Spawner>();

        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        UpdatePanelVisibility();
        UpdateEnemyButtonsVisibility();

        UpdateTowerButtonsVisibility();


        UpdateUI();
    }

    // Update is called once per frame
    private void Update()
    {
        UpdatePanelVisibility();
        UpdateEnemyButtonsVisibility();
        UpdateTowerButtonsVisibility();
        UpdateUI();
    }

    // Method to add an enemy to the HotSeat wave by option index
    public void AddEnemyByIndex(int enemyOptionIndex)
    {
        if (spawner == null)
            return;

        if (!CanEditWave())
            return;

        bool added = spawner.TryAddHotSeatEnemyByIndex(enemyOptionIndex);

        if (!added)
        {
            Debug.Log("Could not add enemy. Not enough budget, enemy locked, or wrong mode.");
        }

        if (added && audioSource != null && enemyAddSound != null)
        {
            audioSource.PlayOneShot(enemyAddSound);
        }
        UpdateUI();
    }

    // Method to remove the last selected enemy from the HotSeat wave
    public void RemoveLastEnemy()
    {
        if (spawner == null)
            return;

        if (!CanEditWave())
            return;

        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
        spawner.RemoveLastHotSeatEnemy();
        UpdateUI();
    }

    // Method to clear the prepared HotSeat wave
    public void ClearWave()
    {
        if (spawner == null)
            return;

        if (!CanEditWave())
            return;

        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
        spawner.ClearHotSeatWave();
        UpdateUI();
    }

    // Method called by Start Battle button
    public void StartBattle()
    {
        if (spawner == null || gameManager == null)
            return;

        if (!CanEditWave())
            return;

        if (!spawner.CanStartNextWave())
        {
            Debug.Log("HotSeat wave is empty. Add at least one enemy before starting battle.");
            return;
        }

        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
        // GameManager already knows how to skip preparation and start battle.
        // In Battle, it will call spawner.StartNextWave().
        gameManager.SkipPreparation();
    }

    // Method to check if the attacker can edit the HotSeat wave now
    private bool CanEditWave()
    {
        if (spawner == null || gameManager == null)
            return false;

        if (!spawner.IsHotSeatMode())
            return false;

        return gameManager.CurrentState == GameManager.GameState.Preparation;
    }

    // Method to update HotSeat panel visibility
    private void UpdatePanelVisibility()
    {
        if (panelCanvasGroup == null)
            return;

        bool visible = CanEditWave();

        // Do not use panel.SetActive(false), because if this script is on the panel,
        // it would disable itself and would not be able to turn the panel back on.
        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }

    // Method to hide locked enemy buttons
    private void UpdateEnemyButtonsVisibility()
    {
        if (spawner == null || enemyOptionButtons == null)
            return;

        bool canEdit = CanEditWave();

        for (int i = 0; i < enemyOptionButtons.Length; i++)
        {
            if (enemyOptionButtons[i] == null)
                continue;

            bool unlocked = spawner.IsEnemyOptionUnlockedForNextWave(i);

            // Locked enemies are hidden completely in HotSeat preparation.
            // During Battle/RoundEnd all enemy buttons are hidden too.
            enemyOptionButtons[i].SetActive(canEdit && unlocked);
        }
    }

    // if cant build, show the panel but hide all tower buttons.    
    // This way the attacker can see that they are locked and can not be added to the wave.
    private void UpdateTowerButtonsVisibility()
    {
        if (towerButtons == null)
            return;

        bool canEdit = CanEditWave();
        bool canBuild = gameManager != null && gameManager.CanBuild;

        for (int i = 0; i < towerButtons.Length; i++)
        {
            if (towerButtons[i] == null)
                continue;

            towerButtons[i].SetActive(canEdit && canBuild);
        }
    }

    // Method to update budget and selected enemies text
    private void UpdateUI()
    {
        if (spawner == null)
            return;

        if (budgetText != null)
        {
            int totalBudget = spawner.GetNextWaveAttackBudget();
            int remainingBudget = spawner.GetHotSeatRemainingBudget();

            budgetText.text = "Attack Budget: " + remainingBudget + " / " + totalBudget;
        }

        if (selectedEnemiesText != null)
        {
            selectedEnemiesText.text = "Selected Enemies: " + spawner.GetHotSeatSelectedEnemyCount();
        }
    }
}
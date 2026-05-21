using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopButton : MonoBehaviour
{
    // Script for a shop UI button.
    // It sends item data to ItemShop when the player clicks this button.
    // If this button is used for Defender, it can show cooldown instead of gold cost.

    [Header("Item Data")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private int cost;
    [SerializeField] private bool isDefender;

    [Header("Defender Cooldown")]
    [SerializeField] private bool useCooldown;
    [SerializeField] private float minCooldown = 10f;
    [SerializeField] private float maxCooldown = 15f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Button button;

    private ItemShop shop;

    private bool cooldownActive;
    private float cooldownTimer;

    public bool IsOnCooldown => cooldownActive;

    // Start is called before the first frame update
    private void Start()
    {
        // Find the shop that stores the currently selected item
        shop = FindAnyObjectByType<ItemShop>();

        if (shop == null)
        {
            Debug.LogError($"ShopButton on {gameObject.name}: ItemShop was not found!");
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (priceText == null)
        {
            priceText = GetComponentInChildren<TextMeshProUGUI>();
        }

        UpdateButtonUI();
    }

    // Update is called once per frame
    private void Update()
    {
        UpdateCooldown();
    }

    // This method should be assigned to the button OnClick event in the Inspector
    public void OnButtonClick()
    {
        if (shop == null)
            return;

        if (itemPrefab == null)
        {
            Debug.LogWarning($"ShopButton on {gameObject.name}: Item prefab is not assigned!");
            return;
        }

        // If this button uses cooldown and cooldown is active,
        // player cannot select this item now.
        if (useCooldown && cooldownActive)
            return;

        // Send this button's item data to the shop.
        // Source button is passed so ItemShop can notify this exact button after successful placement.
        shop.SelectItem(itemPrefab, cost, isDefender, this);
    }

    // Method called by ItemShop after the selected item was successfully placed
    public void NotifyItemPlaced()
    {
        // Only defender button should usually use cooldown.
        // Tower buttons can keep useCooldown disabled.
        if (!useCooldown)
            return;

        StartCooldown();
    }

    // Method to start cooldown after successful placement
    private void StartCooldown()
    {
        cooldownActive = true;
        cooldownTimer = Random.Range(minCooldown, maxCooldown);

        UpdateButtonUI();
    }

    // Method to update cooldown timer
    private void UpdateCooldown()
    {
        if (!cooldownActive)
            return;

        cooldownTimer -= Time.deltaTime;

        if (cooldownTimer <= 0f)
        {
            cooldownTimer = 0f;
            cooldownActive = false;
        }

        UpdateButtonUI();
    }

    // Method to update button text and button state
    private void UpdateButtonUI()
    {
        if (button != null)
        {
            button.interactable = !cooldownActive;
        }

        if (priceText == null)
            return;

        if (useCooldown)
        {
            if (cooldownActive)
            {
                priceText.text = Mathf.CeilToInt(cooldownTimer).ToString() + "s";
            }
            else
            {
                priceText.text = "Ready";
            }

            return;
        }

        priceText.text = cost.ToString();
    }
}
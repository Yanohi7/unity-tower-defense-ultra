using TMPro;
using UnityEngine;

public class ItemShop : MonoBehaviour
{
    // Shop script responsible for selecting towers and other placeable items.
    // It stores the currently selected prefab, cost, item type, and tells ItemPreview what to show.
    // It also remembers which ShopButton selected the current item, so cooldown can start after placement.

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI priceText;

    private ItemPreview itemPreview;

    [Header("Current Selection")]
    public GameObject SelectedItemPrefab { get; private set; }
    public int SelectedItemCost { get; private set; }
    public bool SelectedIsDefender { get; private set; }
    public TowerData SelectedTowerData { get; private set; }

    private ShopButton selectedShopButton;

    // Start is called before the first frame update
    private void Start()
    {
        // Find the preview system used to show transparent item preview before building
        itemPreview = FindAnyObjectByType<ItemPreview>();

        if (itemPreview == null)
        {
            Debug.LogError($"ItemShop on {gameObject.name}: ItemPreview was not found!");
        }
    }

    // Method to select a tower from the shop using TowerData
    public void SelectTower(TowerData towerData)
    {
        if (towerData == null)
            return;

        // Store selected tower data so BuildTile can later initialize the spawned tower
        SelectedTowerData = towerData;

        // Store the prefab and cost from TowerData
        SelectedItemPrefab = towerData.towerPrefab;
        SelectedItemCost = towerData.cost;

        SelectedIsDefender = false;

        // Tower buttons that use TowerData do not use defender cooldown
        selectedShopButton = null;

        // Show transparent preview of the selected tower
        if (itemPreview != null && towerData.towerPrefab != null)
        {
            itemPreview.ShowPreview(towerData.towerPrefab);
        }

        // Update price text in UI
        if (priceText != null)
        {
            priceText.text = towerData.cost.ToString();
        }

        Debug.Log($"Tower selected: {towerData.towerName}, Cost: {towerData.cost}");
    }

    // Method to select a non-tower item, for example defender or future road item
    public void SelectItem(GameObject prefab, int cost, bool isDefender, ShopButton sourceButton = null)
    {
        if (prefab == null)
            return;

        // Store selected item data so BuildTile knows what to place
        SelectedItemPrefab = prefab;
        SelectedItemCost = cost;
        SelectedIsDefender = isDefender;

        // This item is not selected through TowerData,
        // so SelectedTowerData should be cleared
        SelectedTowerData = null;

        // Remember which button selected this item.
        // This lets us start cooldown only after successful placement.
        selectedShopButton = sourceButton;

        // Show transparent preview of the selected item
        if (itemPreview != null)
        {
            itemPreview.ShowPreview(prefab);
        }

        // General price text can be empty for defender,
        // because defender cooldown text is shown on its own button.
        if (priceText != null)
        {
            if (isDefender)
                priceText.text = "";
            else
                priceText.text = cost.ToString();
        }

        Debug.Log($"Item selected: {prefab.name}, Cost: {cost}, IsDefender: {isDefender}");
    }

    // Method called by BuildTile after the selected item was actually placed
    public void NotifySelectedItemPlaced()
    {
        if (selectedShopButton != null)
        {
            selectedShopButton.NotifyItemPlaced();
        }
    }

    // Method to clear the current shop selection
    public void ClearSelection()
    {
        // Clear all selected item data
        SelectedItemPrefab = null;
        SelectedItemCost = 0;
        SelectedIsDefender = false;
        SelectedTowerData = null;
        selectedShopButton = null;

        // Hide preview object when nothing is selected
        if (itemPreview != null)
        {
            itemPreview.HidePreview();
        }

        // Clear price text because no item is selected anymore
        if (priceText != null)
        {
            priceText.text = "";
        }
    }
}
using UnityEngine;

public class BuildTile : MonoBehaviour
{
    // Script responsible for placing towers and road units on build tiles.
    // It checks if the selected item can be placed on this tile, spends gold,
    // spawns the selected prefab, initializes towers, starts defender cooldown,
    // and plays build sound.

    [Header("Tile Settings")]
    [SerializeField] public bool isRoadTile;
    [SerializeField] public bool isLeftTile;

    public bool hasUnit = false;

    private GameManager gameManager;
    private ItemShop itemShop;
    private ItemPreview itemPreview;
    private Camera mainCamera;

    [Header("Audio")]
    [SerializeField] private AudioClip buildSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Tower Placement")]
    [SerializeField] private Vector3 towerSpawnOffset = new Vector3(0.32f, 0.18f, 0f);

    private void Start()
    {
        // Find important scene managers used for building logic.
        gameManager = FindAnyObjectByType<GameManager>();
        itemShop = FindAnyObjectByType<ItemShop>();
        itemPreview = FindAnyObjectByType<ItemPreview>();

        // Main camera is needed to convert mouse position from screen to world position.
        mainCamera = Camera.main;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (gameManager == null)
        {
            Debug.LogError($"BuildTile on {gameObject.name}: GameManager was not found!");
        }

        if (itemShop == null)
        {
            Debug.LogError($"BuildTile on {gameObject.name}: ItemShop was not found!");
        }

        if (itemPreview == null)
        {
            Debug.LogError($"BuildTile on {gameObject.name}: ItemPreview was not found!");
        }

        if (mainCamera == null)
        {
            Debug.LogError($"BuildTile on {gameObject.name}: Main Camera was not found!");
        }
    }

    private void OnMouseEnter()
    {
        if (itemPreview == null)
            return;

        // Tell the preview system that the mouse is currently over this tile.
        itemPreview.SetCurrentBuildTile(this);
    }

    private void OnMouseExit()
    {
        if (itemPreview == null)
            return;

        // Clear this tile from preview only if it is still the current tile.
        itemPreview.ClearCurrentBuildTile(this);
    }

    private void OnMouseDown()
    {
        if (gameManager == null || itemShop == null)
            return;

        GameObject prefab = itemShop.SelectedItemPrefab;

        // If no item is selected in the shop, there is nothing to build.
        if (prefab == null)
            return;

        bool tryingToBuildDefender = itemShop.SelectedIsDefender;

        // If the current game state does not allow building, do nothing.
        if (!gameManager.CanBuild)
        {
            if (!tryingToBuildDefender)
                return;
        }

        // Check if the selected item is allowed to be placed here.
        if (!CanBuildHere(tryingToBuildDefender))
            return;

        // Spend gold before spawning the object.
        if (!gameManager.SpendGold(itemShop.SelectedItemCost))
            return;

        Vector3 finalSpawnPos;

        if (tryingToBuildDefender)
        {
            if (!TryGetMouseWorldPosition(out Vector3 mousePos))
                return;

            finalSpawnPos = mousePos;
        }
        else
        {
            finalSpawnPos = GetSpawnPosition();
        }

        GameObject spawnedObject = Instantiate(
            prefab,
            finalSpawnPos,
            Quaternion.identity
        );

        Vector3 newScale = spawnedObject.transform.localScale;

        // Flip the spawned object depending on which side of the map/tile it is on.
        if (isLeftTile)
            newScale.x = -Mathf.Abs(newScale.x);
        else
            newScale.x = Mathf.Abs(newScale.x);

        spawnedObject.transform.localScale = newScale;

        if (!tryingToBuildDefender)
        {
            TowerBase tower = spawnedObject.GetComponent<TowerBase>();

            if (tower != null)
            {
                // Initialize passes the selected TowerData into the spawned tower.
                tower.Initialize(itemShop.SelectedTowerData);
            }

            // Towers occupy this build tile.
            hasUnit = true;
        }

        PlayBuildSound(finalSpawnPos);

        // Tell ItemShop that selected item was actually placed.
        itemShop.NotifySelectedItemPlaced();

        // Clear shop selection after successful building.
        itemShop.ClearSelection();
    }

    private bool CanBuildHere(bool tryingToBuildDefender)
    {
        if (tryingToBuildDefender)
        {
            // Defenders can only be placed on road tiles.
            if (!isRoadTile)
                return false;

            if (!TryGetMouseWorldPosition(out Vector3 checkPos))
                return false;

            if (IsDefenderTooClose(checkPos))
                return false;

            return true;
        }

        if (isRoadTile)
            return false;

        if (hasUnit)
            return false;

        return true;
    }

    private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (mainCamera == null)
            return false;

        // Create a ray from the camera through the mouse position.
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Create an invisible plane at world Z = 0.
        Plane worldPlane = new Plane(Vector3.forward, Vector3.zero);

        // Try to find where the mouse ray hits the Z = 0 plane.
        if (worldPlane.Raycast(ray, out float distance))
        {
            worldPosition = ray.GetPoint(distance);
            worldPosition.z = 0f;
            return true;
        }

        return false;
    }

    private bool IsDefenderTooClose(Vector3 position)
    {
        if (itemPreview == null)
            return false;

        Collider2D[] nearby = Physics2D.OverlapCircleAll(
            position,
            itemPreview.minDefenderDistance
        );

        foreach (Collider2D col in nearby)
        {
            Defender defender = col.GetComponentInParent<Defender>();

            if (defender == null)
                continue;

            // Only living defenders block new defender placement.
            if (!defender.IsAvailableForCombat)
                continue;

            return true;
        }

        return false;
    }

    private void PlayBuildSound(Vector3 position)
    {
        if (buildSound == null)
            return;

        // PlayClipAtPoint creates a temporary audio object and does not move the build tile.
        AudioSource.PlayClipAtPoint(buildSound, position);
    }

    public Vector3 GetSpawnPosition()
    {
        return transform.position + towerSpawnOffset;
    }
}
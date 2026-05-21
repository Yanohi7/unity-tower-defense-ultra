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

    // Start is called before the first frame update
    private void Start()
    {
        // Find important scene managers used for building logic
        gameManager = FindAnyObjectByType<GameManager>();
        itemShop = FindAnyObjectByType<ItemShop>();
        itemPreview = FindAnyObjectByType<ItemPreview>();

        // Main camera is needed to convert mouse position from screen to world position
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

    // Called when the mouse enters this build tile
    private void OnMouseEnter()
    {
        if (itemPreview == null)
            return;

        // Tell the preview system that the mouse is currently over this tile
        itemPreview.SetCurrentBuildTile(this);
    }

    // Called when the mouse exits this build tile
    private void OnMouseExit()
    {
        if (itemPreview == null)
            return;

        // Clear this tile from preview only if it is still the current tile
        itemPreview.ClearCurrentBuildTile(this);
    }

    // Called when the player clicks on this tile
    private void OnMouseDown()
    {
        if (gameManager == null || itemShop == null)
            return;

        GameObject prefab = itemShop.SelectedItemPrefab;

        // If no item is selected in the shop, there is nothing to build
        if (prefab == null)
            return;

        bool tryingToBuildDefender = itemShop.SelectedIsDefender;

        // If the current game state does not allow building, do nothing.
        if (!gameManager.CanBuild)
        {
            if (!tryingToBuildDefender)
                return;
        }

        // Check if the selected item is allowed to be placed here
        if (!CanBuildHere(tryingToBuildDefender))
            return;

        // Spend gold before spawning the object.
        // Defender can have cost 0, so SpendGold(0) will still work.
        if (!gameManager.SpendGold(itemShop.SelectedItemCost))
            return;

        Vector3 finalSpawnPos;

        if (tryingToBuildDefender)
        {
            // Defenders are placed directly at the mouse position on the road,
            // Ray-to-plane calculation instead of ScreenToWorldPoint to avoid frustum warnings.
            if (!TryGetMouseWorldPosition(out Vector3 mousePos))
                return;

            finalSpawnPos = mousePos;
        }
        else
        {
            finalSpawnPos = GetSpawnPosition();
        }

        Vector3 newScale = prefab.transform.localScale;

        // Flip the spawned object depending on which side of the map/tile it is on.
        // This helps towers and defenders face the correct direction visually.
        if (isLeftTile)
        {
            newScale.x = -Mathf.Abs(newScale.x);
        }
        else
        {
            newScale.x = Mathf.Abs(newScale.x);
        }

        GameObject spawnedObject = Instantiate(
            prefab,
            finalSpawnPos,
            Quaternion.identity
        );

        spawnedObject.transform.localScale = newScale;

        if (!tryingToBuildDefender)
        {
            TowerBase tower = spawnedObject.GetComponent<TowerBase>();

            if (tower != null)
            {
                // Initialize passes the selected TowerData into the spawned tower.
                // This is important for towers created during gameplay.
                tower.Initialize(itemShop.SelectedTowerData);
            }

            // Normal towers occupy this build tile,
            // so another tower cannot be placed on the same tile.
            hasUnit = true;
        }

        PlayBuildSound(finalSpawnPos);

        // Tell ItemShop that selected item was actually placed.
        // This is needed for defender cooldown.
        // The cooldown should start only after successful placement, not after button click.
        itemShop.NotifySelectedItemPlaced();

        // Clear shop selection after successful building
        itemShop.ClearSelection();
    }

    // Method to check if the selected item can be built on this tile
    private bool CanBuildHere(bool tryingToBuildDefender)
    {
        if (tryingToBuildDefender)
        {
            // Defenders can only be placed on road tiles
            if (!isRoadTile)
                return false;

            // Convert mouse position to world position,
            // because defenders are placed exactly where the mouse is
            if (!TryGetMouseWorldPosition(out Vector3 checkPos))
                return false;

            // Prevent defenders from being placed too close to each other
            if (IsDefenderTooClose(checkPos))
                return false;

            return true;
        }

        // Towers cannot be placed on road tiles
        if (isRoadTile)
            return false;

        // Towers cannot be placed on an already occupied build tile
        if (hasUnit)
            return false;

        return true;
    }

    // Method to safely convert mouse screen position to world position on the Z = 0 plane
    private bool TryGetMouseWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (mainCamera == null)
            return false;

        // Create a ray from the camera through the mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Create an invisible plane at world Z = 0.
        // This is the gameplay plane where towers, defenders and enemies exist.
        Plane worldPlane = new Plane(Vector3.forward, Vector3.zero);

        // Try to find where the mouse ray hits the Z = 0 plane
        if (worldPlane.Raycast(ray, out float distance))
        {
            worldPosition = ray.GetPoint(distance);
            worldPosition.z = 0f;
            return true;
        }

        return false;
    }

    // Method to check if there is another defender too close to the selected position
    private bool IsDefenderTooClose(Vector3 position)
    {
        if (itemPreview == null)
            return false;

        // Check all colliders around the mouse position within the defender minimum distance
        Collider2D[] nearby = Physics2D.OverlapCircleAll(
            position,
            itemPreview.minDefenderDistance
        );

        foreach (Collider2D col in nearby)
        {
            // Only objects tagged as Defender block new defender placement.
            // Towers do not matter here.
            if (col.CompareTag("Defender"))
                return true;
        }

        return false;
    }

    // Helper method to play the build sound effect at the build position
    private void PlayBuildSound(Vector3 position)
    {
        if (audioSource != null && buildSound != null)
        {
            audioSource.transform.position = position;
            audioSource.PlayOneShot(buildSound);
        }
    }

    // Method to calculate the final tower spawn position using tile position and offset
    public Vector3 GetSpawnPosition()
    {
        return transform.position + towerSpawnOffset;
    }
}
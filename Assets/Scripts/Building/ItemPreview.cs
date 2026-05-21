using UnityEngine;

public class ItemPreview : MonoBehaviour
{
    // Script responsible for showing a transparent preview of the selected item before building.
    // It also shows tower range preview and checks if the selected item can be placed here.

    private GameObject previewItem;
    private Camera mainCamera;
    private BuildTile currentBuildTile;
    private ItemShop itemShop;

    [Header("Range Preview")]
    [SerializeField] private Material rangeMaterial;
    [SerializeField] private Color rangeColor = new Color(0f, 0.8f, 1f, 0.35f);
    [SerializeField] private int circleSegments = 120;

    [Header("Defender Spacing")]
    [SerializeField] public float minDefenderDistance = 0.5f;

    private LineRenderer rangeLine;

    // Start is called before the first frame update
    private void Start()
    {
        mainCamera = Camera.main;
        itemShop = FindAnyObjectByType<ItemShop>();

        CreateRangeLine();

        if (mainCamera == null)
        {
            Debug.LogError($"ItemPreview on {gameObject.name}: Main Camera was not found!");
        }

        if (itemShop == null)
        {
            Debug.LogError($"ItemPreview on {gameObject.name}: ItemShop was not found!");
        }
    }

    // Update is called once per frame while preview is active
    private void Update()
    {
        // If there is no preview object or no shop, there is nothing to update
        if (previewItem == null || itemShop == null)
            return;

        // Right mouse button cancels the current selected item
        if (Input.GetMouseButtonDown(1))
        {
            itemShop.ClearSelection();
            HidePreview();
            return;
        }

        // Convert mouse position from screen coordinates to world coordinates safely
        if (!TryGetMouseWorldPosition(out Vector3 mousePosition))
            return;

        bool isDefender = itemShop.SelectedIsDefender;
        bool canBuildHere = false;

        if (currentBuildTile != null)
        {
            // Defenders can only be placed on road tiles.
            // Towers can only be placed on non-road tiles.
            canBuildHere =
                (isDefender && currentBuildTile.isRoadTile) ||
                (!isDefender && !currentBuildTile.isRoadTile);

            // Towers cannot be placed on an occupied build tile
            if (!isDefender && currentBuildTile.hasUnit)
                canBuildHere = false;
        }

        // Defenders also need spacing check so they are not placed too close together
        if (canBuildHere && isDefender && IsDefenderTooClose(mousePosition))
        {
            canBuildHere = false;
        }

        if (canBuildHere)
        {
            // White transparent preview means the selected item can be placed here
            SetPreviewColor(new Color(1f, 1f, 1f, 0.5f));

            if (isDefender)
            {
                // Defenders follow the exact mouse position on the road
                previewItem.transform.position = mousePosition;

                ApplyPreviewFlip();

                // Defenders do not show tower range preview
                HideRange();
            }
            else
            {
                // Towers snap to the build tile spawn position
                Vector3 spawnPosition = currentBuildTile.GetSpawnPosition();
                previewItem.transform.position = spawnPosition;

                ApplyPreviewFlip();

                // Show selected tower range while previewing tower placement
                if (itemShop.SelectedTowerData != null)
                    ShowRange(spawnPosition, itemShop.SelectedTowerData.range);
            }
        }
        else
        {
            // Red transparent preview means the selected item cannot be placed here
            SetPreviewColor(new Color(1f, 0f, 0f, 0.5f));
            previewItem.transform.position = mousePosition;

            // If we are still over a tile, keep the preview flipped correctly
            if (currentBuildTile != null)
                ApplyPreviewFlip();

            HideRange();
        }
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

    // Method to flip the preview depending on tile side
    private void ApplyPreviewFlip()
    {
        if (previewItem == null || currentBuildTile == null)
            return;

        Vector3 newScale = previewItem.transform.localScale;

        bool isDefender = itemShop.SelectedIsDefender;

        if (currentBuildTile.isLeftTile)
        {
            if (isDefender && !currentBuildTile.isRoadTile)
                return;
            newScale.x = -Mathf.Abs(newScale.x);
        }
        else
        {
            if (isDefender && !currentBuildTile.isRoadTile)
                return;
            newScale.x = Mathf.Abs(newScale.x);
        }

        previewItem.transform.localScale = newScale;
    }

    // Method to check if there is another defender too close to the mouse position
    private bool IsDefenderTooClose(Vector3 position)
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(position, minDefenderDistance);

        foreach (Collider2D col in nearby)
        {
            // Only objects tagged as Defender block defender placement.
            // Towers should not affect this check.
            if (col.CompareTag("Defender"))
                return true;
        }

        return false;
    }

    // Method to tint all SpriteRenderers of the preview object
    private void SetPreviewColor(Color color)
    {
        if (previewItem == null)
            return;

        SpriteRenderer[] renderers = previewItem.GetComponentsInChildren<SpriteRenderer>();

        foreach (SpriteRenderer sr in renderers)
        {
            sr.color = color;
        }
    }

    // Public method called by ItemShop when the player selects an item
    public void ShowPreview(GameObject prefab)
    {
        HidePreview();

        if (prefab == null)
            return;

        previewItem = Instantiate(prefab);

        // Disable scripts on the preview object so it does not attack, move, shoot, or run gameplay logic
        MonoBehaviour[] scripts = previewItem.GetComponentsInChildren<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            script.enabled = false;
        }

        // Disable colliders on preview so it does not block mouse clicks or trigger gameplay collisions
        Collider2D[] colliders = previewItem.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D col in colliders)
        {
            col.enabled = false;
        }

        SetPreviewColor(new Color(1f, 1f, 1f, 0.5f));
    }

    // Public method to remove preview object and hide range preview
    public void HidePreview()
    {
        if (previewItem != null)
            Destroy(previewItem);

        currentBuildTile = null;
        HideRange();
    }

    // Method called by BuildTile when mouse enters a tile
    public void SetCurrentBuildTile(BuildTile buildTile)
    {
        currentBuildTile = buildTile;
    }

    // Method called by BuildTile when mouse exits a tile
    public void ClearCurrentBuildTile(BuildTile buildTile)
    {
        if (currentBuildTile == buildTile)
            currentBuildTile = null;
    }

    // Method to create the LineRenderer used for tower range preview
    private void CreateRangeLine()
    {
        GameObject rangeObject = new GameObject("PreviewRangeCircle");
        rangeObject.transform.SetParent(transform);

        rangeLine = rangeObject.AddComponent<LineRenderer>();
        rangeLine.positionCount = circleSegments + 1;
        rangeLine.loop = true;
        rangeLine.useWorldSpace = true;
        rangeLine.startWidth = 0.035f;
        rangeLine.endWidth = 0.035f;
        rangeLine.enabled = false;

        if (rangeMaterial != null)
        {
            rangeLine.material = rangeMaterial;
        }
        else
        {
            rangeLine.material = new Material(Shader.Find("Sprites/Default"));
        }

        rangeLine.startColor = rangeColor;
        rangeLine.endColor = rangeColor;
    }

    // Method to show a circular range preview around the selected tower position
    private void ShowRange(Vector3 center, float radius)
    {
        if (rangeLine == null)
            CreateRangeLine();

        rangeLine.enabled = true;

        // Create points around a circle
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / circleSegments;

            Vector3 point = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius,
                center.z
            );

            rangeLine.SetPosition(i, point);
        }
    }

    // Method to hide the tower range preview
    private void HideRange()
    {
        if (rangeLine != null)
            rangeLine.enabled = false;
    }
}
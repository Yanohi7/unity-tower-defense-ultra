using UnityEngine;

public class SortByY : MonoBehaviour
{
    // Script to sort game objects by Y coordinate
    private SpriteRenderer spriteRenderer;

    void Start() => spriteRenderer = GetComponent<SpriteRenderer>();

    void Update()
    {
        // Define order baded on position
        spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100);
    }
}
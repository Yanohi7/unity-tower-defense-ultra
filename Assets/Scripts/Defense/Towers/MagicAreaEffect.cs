using System.Collections;
using UnityEngine;

public class MagicAreaEffect : MonoBehaviour
{
    // Script for the area effect spawned by the MagicTower when it casts an area attack.
    // It only handles the visual effect: scaling to the tower range, fading out, and returning to pool.
    [SerializeField] private SpriteRenderer areaRenderer;
    [SerializeField] private float duration = 0.35f;

    private Color originalColor;
    private bool hasOriginalColor;

    // Initializes the area effect with the specified radius and starts the fade-out coroutine  
    public void Initialize(float radius)
    {
        StopAllCoroutines();

        if (areaRenderer == null)
            areaRenderer = GetComponentInChildren<SpriteRenderer>();

        if (areaRenderer == null)
        {
            ReturnToPool();
            return;
        }

        if (!hasOriginalColor)
        {
            originalColor = areaRenderer.color;
            hasOriginalColor = true;
        }

        areaRenderer.color = originalColor;

        // Set the scale of the area effect based on the tower's range (radius)
        transform.localScale = Vector3.one * radius * 2f;

        StartCoroutine(FadeOut());
    }

    // Coroutine to fade out the area effect over time and return it to the pool when finished
    private IEnumerator FadeOut()
    {
        float elapsed = 0f;

        Color startColor = originalColor;
        Color endColor = startColor;
        endColor.a = 0f;

        // Gradually fade out the area effect
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / duration;
            areaRenderer.color = Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
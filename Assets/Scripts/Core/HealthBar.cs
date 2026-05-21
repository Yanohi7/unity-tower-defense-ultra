using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    // Simple world-space health bar.
    // It uses an Image with Filled type to show current health amount.
    // It can also fix flipping when parent object turns by negative scale.

    [Header("UI")]
    [SerializeField] private Image fillImage;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private GameObject rootObject;

    [Header("Flip Fix")]
    [SerializeField] private bool keepReadableWhenParentFlips = true;

    private Vector3 originalLocalScale;

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        originalLocalScale = transform.localScale;

        if (rootObject == null)
            rootObject = gameObject;

        if (fillImage == null)
            fillImage = GetComponentInChildren<Image>();
    }

    // LateUpdate is called after normal Update, so we can fix health bar scale after defender flips
    private void LateUpdate()
    {
        if (!keepReadableWhenParentFlips)
            return;

        FixFlip();
    }

    // Method to update health bar fill amount
    public void SetHealth(float currentHealth, float maxHealth)
    {
        if (fillImage == null)
            return;

        float fillAmount = 0f;

        if (maxHealth > 0f)
        {
            fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
        }

        fillImage.fillAmount = fillAmount;

        if (rootObject != null && hideWhenFull)
        {
            rootObject.SetActive(fillAmount < 1f && fillAmount > 0f);
        }
        else if (rootObject != null)
        {
            rootObject.SetActive(fillAmount > 0f);
        }
    }

    // Method to hide the health bar manually
    public void Hide()
    {
        if (rootObject != null)
            rootObject.SetActive(false);
    }

    // Method to show the health bar manually
    public void Show()
    {
        if (rootObject != null)
            rootObject.SetActive(true);
    }

    // Method to keep health bar readable when parent object flips with negative X scale
    private void FixFlip()
    {
        if (transform.parent == null)
            return;

        Vector3 fixedScale = originalLocalScale;

        // If parent is flipped by negative X scale, child also needs negative X scale
        // so the final world scale becomes visually normal again.
        if (transform.parent.lossyScale.x < 0f)
            fixedScale.x = -Mathf.Abs(originalLocalScale.x);
        else
            fixedScale.x = Mathf.Abs(originalLocalScale.x);

        transform.localScale = fixedScale;
    }
}
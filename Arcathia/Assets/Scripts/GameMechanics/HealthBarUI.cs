using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    public static HealthBarUI Instance { get; private set; }

    [Header("UI Visual References")]
    public Image healthFillImage;  // Image set to Image Type: Filled (Horizontal)
    public TMP_Text healthText;     // Optional: Displays "80/100"

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void UpdateHealthUI(int currentHealth, int maxHealth)
    {
        float fillRatio = (float)currentHealth / maxHealth;

        if (healthFillImage != null)
        {
            healthFillImage.fillAmount = Mathf.Clamp01(fillRatio);
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }
}
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public static HealthBarUI Instance { get; private set; }

    [Header("UI References")]
    public Image healthFillImage;
    public TextMeshProUGUI healthText; // Drag your TextMeshPro object here

    private PlayerHealth localPlayerHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure Image component is configured for fillAmount manipulation
        if (healthFillImage != null && healthFillImage.type != Image.Type.Filled)
        {
            healthFillImage.type = Image.Type.Filled;
            healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        }
    }

    private void Start()
    {
        // Fallback search if player spawned before HealthBarUI initialized
        if (localPlayerHealth == null)
        {
            TryBindLocalPlayer();
        }
    }

    public void BindPlayer(PlayerHealth health)
    {
        // Unsubscribe from existing player
        if (localPlayerHealth != null)
        {
            localPlayerHealth.currentHealth.OnValueChanged -= OnHealthChanged;
        }

        localPlayerHealth = health;

        if (localPlayerHealth != null)
        {
            localPlayerHealth.currentHealth.OnValueChanged += OnHealthChanged;

            // Force immediate update on bind
            UpdateHealthBar(localPlayerHealth.currentHealth.Value);
            Debug.Log($"[HealthBarUI] Successfully bound to local player: {health.gameObject.name}");
        }
    }

    public void TryBindLocalPlayer()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (PlayerHealth player in players)
        {
            if (player.IsOwner)
            {
                BindPlayer(player);
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (localPlayerHealth != null)
        {
            localPlayerHealth.currentHealth.OnValueChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        UpdateHealthBar(newValue);
    }

    public void UpdateHealthBar(int newHealth)
    {
        if (localPlayerHealth == null) return;

        // 1. Update Fill Amount (0.0 to 1.0)
        if (healthFillImage != null)
        {
            float fillRatio = (float)newHealth / localPlayerHealth.maxHealth;
            healthFillImage.fillAmount = Mathf.Clamp01(fillRatio);
        }
        else
        {
            Debug.LogWarning("[HealthBarUI] healthFillImage reference missing in Inspector!");
        }

        // 2. Update Text Display (e.g. "100 / 100")
        if (healthText != null)
        {
            healthText.text = $"{newHealth} / {localPlayerHealth.maxHealth}";
        }
        else
        {
            Debug.LogWarning("[HealthBarUI] healthText reference missing in Inspector!");
        }
    }
}
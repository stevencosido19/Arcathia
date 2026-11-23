using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public static HealthBarUI Instance { get; private set; }

    [Header("UI References")]
    public Image healthFillImage;
    public TextMeshProUGUI healthText; 

    private PlayerHealth localPlayerHealth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        
        if (healthFillImage != null && healthFillImage.type != Image.Type.Filled)
        {
            healthFillImage.type = Image.Type.Filled;
            healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        }
    }

    private void Start()
    {
        
        if (localPlayerHealth == null)
        {
            TryBindLocalPlayer();
        }
    }

    public void BindPlayer(PlayerHealth health)
    {
        
        if (localPlayerHealth != null)
        {
            localPlayerHealth.currentHealth.OnValueChanged -= OnHealthChanged;
        }

        localPlayerHealth = health;

        if (localPlayerHealth != null)
        {
            localPlayerHealth.currentHealth.OnValueChanged += OnHealthChanged;

            
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

        
        if (healthFillImage != null)
        {
            float fillRatio = (float)newHealth / localPlayerHealth.maxHealth;
            healthFillImage.fillAmount = Mathf.Clamp01(fillRatio);
        }
        else
        {
            Debug.LogWarning("[HealthBarUI] healthFillImage reference missing in Inspector!");
        }

        
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
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;

    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("UI References")]
    public Image healthBarFill;
    public TextMeshProUGUI healthText;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        currentHealth.OnValueChanged += OnHealthChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        UpdateHealthUI(currentHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float previousValue, float newValue)
    {
        UpdateHealthUI(newValue);
    }

    public void TakeDamage(float amount)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Clamp(currentHealth.Value - amount, 0f, maxHealth);

        if (currentHealth.Value <= 0)
        {
            Respawn();
        }
    }

    private void UpdateHealthUI(float val)
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = val / maxHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(val)} / {maxHealth}";
        }
    }

    private void Respawn()
    {
        if (!IsServer) return;

        if (characterController != null) characterController.enabled = false;
        transform.position = Vector3.up * 2f;
        if (characterController != null) characterController.enabled = true;

        currentHealth.Value = maxHealth;
    }
}
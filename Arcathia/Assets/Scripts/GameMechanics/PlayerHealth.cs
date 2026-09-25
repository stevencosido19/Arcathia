using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(100);

    private HealthBarUI healthBarUI;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += OnHealthChanged;

        if (IsOwner)
        {
            healthBarUI = FindFirstObjectByType<HealthBarUI>();
            if (healthBarUI != null)
            {
                healthBarUI.UpdateHealthUI(currentHealth.Value, maxHealth);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        if (IsOwner && healthBarUI != null)
        {
            healthBarUI.UpdateHealthUI(newValue, maxHealth);
        }
    }

    // --- ADD THIS METHOD HERE ---
    [ServerRpc]
    public void TakeDamageServerRpc(int damageAmount)
    {
        TakeDamage(damageAmount);
    }

    public void TakeDamage(int damageAmount)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Clamp(currentHealth.Value - damageAmount, 0, maxHealth);

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} was defeated!");
    }
}
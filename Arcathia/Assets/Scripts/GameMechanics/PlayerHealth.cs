using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int maxHealth = 100;

    private PlayerPowerUpHandler powerUpHandler;

    private void Awake()
    {
        powerUpHandler = GetComponent<PlayerPowerUpHandler>();
    }

    public override void OnNetworkSpawn()
    {
        // Initialize health on the server when spawned
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    /// <summary>
    /// ServerRpc invoked by clients (e.g., from SpellbookUI when answering incorrectly) 
    /// to request damage application on the server.
    /// </summary>
    [ServerRpc]
    public void ApplyBackfireServerRpc(int damage)
    {
        TakeDamage(damage);
    }

    /// <summary>
    /// Deducts health on the server. Accepts an optional attacker NetworkObject for kill credit.
    /// </summary>
    public void TakeDamage(int damage, NetworkObject attacker = null)
    {
        // 1. Guard check: Only the Server is allowed to process health changes
        if (!IsServer) return;

        // 2. Check Prism Barrier shield
        if (powerUpHandler != null && powerUpHandler.isPrismBarrierActive.Value)
        {
            // Shield absorbs the damage payload and turns off
            powerUpHandler.isPrismBarrierActive.Value = false;
            Debug.Log($"<color=cyan>[PowerUp Effect]</color> Prism Barrier absorbed damage for {gameObject.name}!");
            return;
        }

        // 3. Deduct health safely
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        string attackerName = attacker != null ? attacker.name : "Environment/Self";
        Debug.Log($"[PlayerHealth] {gameObject.name} took {damage} damage from {attackerName}. Current Health: {currentHealth.Value}");

        // 4. Check for death
        if (currentHealth.Value <= 0)
        {
            Die(attacker);
        }
    }

    private void Die(NetworkObject killer = null)
    {
        Debug.Log($"[PlayerHealth] {gameObject.name} was defeated by {(killer != null ? killer.name : "the environment")}!");
        // Add death, ragdoll, or respawn logic here
    }
}
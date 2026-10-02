using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public bool allowSelfDamageScore = false; // Set true if backfires/suicide should grant score

    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        if (IsOwner)
        {
            if (HealthBarUI.Instance != null)
            {
                HealthBarUI.Instance.BindPlayer(this);
            }
            else
            {
                HealthBarUI ui = FindFirstObjectByType<HealthBarUI>();
                if (ui != null)
                {
                    ui.BindPlayer(this);
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void ApplyBackfireServerRpc(int amount)
    {
        TakeDamage(amount, NetworkObject);
    }

    public void TakeDamage(int amount, NetworkObject attacker = null)
    {
        if (!IsServer) return;

        if (currentHealth.Value <= 0) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - amount);
        Debug.Log($"[PlayerHealth] {gameObject.name} took {amount} damage. Current Health: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            DieAndRespawn(attacker);
        }
    }

    private void DieAndRespawn(NetworkObject attacker)
    {
        if (!IsServer) return;

        // --- KILL FEED NOTIFICATION ---
        if (attacker != null && KillFeedUI.Instance != null)
        {
            string attackerName = attacker.gameObject.name.Replace("(Clone)", "");
            string victimName = gameObject.name.Replace("(Clone)", "");

            KillFeedUI.Instance.SendKillNotification(attackerName, victimName);
        }

        // --- SCORE AWARDING ---
        if (attacker != null && (attacker != NetworkObject || allowSelfDamageScore))
        {
            PlayerScore attackerScore = attacker.GetComponent<PlayerScore>();
            if (attackerScore != null)
            {
                attackerScore.AddScore(1);
            }
        }

        // --- TELEPORT & RESPAWN ---
        Transform spawnPoint = PlayerSpawnManager.Instance != null
            ? PlayerSpawnManager.Instance.GetNextSpawnPoint()
            : null;

        Vector3 targetPosition = spawnPoint != null ? spawnPoint.position : new Vector3(0, 2f, 0);
        Quaternion targetRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        CharacterController controller = GetComponent<CharacterController>();
        Rigidbody rb = GetComponent<Rigidbody>();

        if (controller != null) controller.enabled = false;
        if (rb != null) rb.isKinematic = true;

        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            if (netTransform.CanCommitToTransform)
            {
                netTransform.Teleport(targetPosition, targetRotation, transform.localScale);
            }
            else
            {
                TeleportClientRpc(targetPosition, targetRotation);
            }
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        Physics.SyncTransforms();

        if (controller != null) controller.enabled = true;
        if (rb != null) rb.isKinematic = false;

        // Reset Health
        currentHealth.Value = maxHealth;
    }

    [Rpc(SendTo.Owner)]
    private void TeleportClientRpc(Vector3 position, Quaternion rotation)
    {
        CharacterController controller = GetComponent<CharacterController>();
        Rigidbody rb = GetComponent<Rigidbody>();

        if (controller != null) controller.enabled = false;
        if (rb != null) rb.isKinematic = true;

        NetworkTransform netTransform = GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.Teleport(position, rotation, transform.localScale);
        }
        else
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        Physics.SyncTransforms();

        if (controller != null) controller.enabled = true;
        if (rb != null) rb.isKinematic = false;
    }
}
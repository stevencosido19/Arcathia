using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(
        100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int maxHealth = 100;

    [Header("Respawn & Delay Settings")]
    [Tooltip("How many seconds the player remains dead before respawning.")]
    public float respawnDelay = 5.0f;

    [Header("Score Settings")]
    public int pointsPerKill = 1;

    [Header("Visual Body Root (Optional)")]
    [Tooltip("If you have a child mesh container, assign it here. If left empty, it safely hides all mesh renderers.")]
    public GameObject characterModelRoot;

    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerPowerUpHandler powerUpHandler;
    private CharacterController characterController;
    private Collider playerCollider;

    private void Awake()
    {
        powerUpHandler = GetComponent<PlayerPowerUpHandler>();
        characterController = GetComponent<CharacterController>();
        playerCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }
    }

    [ServerRpc]
    public void ApplyBackfireServerRpc(int damage)
    {
        TakeDamage(damage);
    }

    public void TakeDamage(int damage, NetworkObject attacker = null)
    {
        if (!IsServer || isDead.Value) return;

        // Check Prism Barrier shield
        if (powerUpHandler != null && powerUpHandler.isPrismBarrierActive.Value)
        {
            powerUpHandler.isPrismBarrierActive.Value = false;
            Debug.Log($"<color=cyan>[PowerUp Effect]</color> Prism Barrier absorbed damage for {gameObject.name}!");
            return;
        }

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        string attackerName = attacker != null ? attacker.name : "Environment/Self";
        Debug.Log($"[PlayerHealth] {gameObject.name} took {damage} damage from {attackerName}. Current HP: {currentHealth.Value}");

        if (currentHealth.Value <= 0)
        {
            Die(attacker);
        }
    }

    private void Die(NetworkObject killer = null)
    {
        isDead.Value = true;

        // 1. Award score to killer
        if (killer != null && killer != NetworkObject)
        {
            if (killer.TryGetComponent<PlayerScore>(out PlayerScore killerScore))
            {
                killerScore.AddScore(pointsPerKill);
            }
        }

        // 2. Hide body and disable collisions on all clients
        SetPlayerDeadStateClientRpc(true);

        // 3. Notify owner client to show Death UI & start Spectator Camera
        ulong killerId = (killer != null) ? killer.NetworkObjectId : 0;
        NotifyDeathClientRpc(respawnDelay, killerId);

        // 4. Start delayed respawn countdown on Server
        StartCoroutine(RespawnTimerRoutine());
    }

    private IEnumerator RespawnTimerRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        // Fetch spawn point from manager
        Vector3 newPos = transform.position;
        Quaternion newRot = transform.rotation;

        if (PlayerSpawnManager.Instance != null)
        {
            Transform sp = PlayerSpawnManager.Instance.GetNextSpawnPoint();
            if (sp != null)
            {
                newPos = sp.position;
                newRot = sp.rotation;
            }
        }

        // Restore health and mark alive
        currentHealth.Value = maxHealth;
        isDead.Value = false;

        // Reposition and restore player across the network
        RespawnClientRpc(newPos, newRot);
        SetPlayerDeadStateClientRpc(false);

        Debug.Log($"[PlayerHealth] {gameObject.name} respawned successfully!");
    }

    [ClientRpc]
    private void NotifyDeathClientRpc(float delay, ulong killerNetId)
    {
        if (!IsOwner) return;

        // Show Death Screen with countdown
        if (DeathScreenUI.Instance != null)
        {
            DeathScreenUI.Instance.ShowDeathScreen(delay);
        }

        // Switch camera to spectator mode
        if (ThirdPersonCamera.LocalCamera != null)
        {
            ThirdPersonCamera.LocalCamera.StartSpectating(killerNetId);
        }
    }

    [ClientRpc]
    private void SetPlayerDeadStateClientRpc(bool dead)
    {
        // SAFE VISIBILITY FIX:
        // Only disable characterModelRoot if it is a CHILD object, never the root GameObject itself!
        if (characterModelRoot != null && characterModelRoot != this.gameObject)
        {
            characterModelRoot.SetActive(!dead);
        }
        else
        {
            // Hide all 3D mesh renderers so the character disappears visually
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                r.enabled = !dead;
            }
        }

        // Toggle movement and collisions
        if (characterController != null) characterController.enabled = !dead;
        if (playerCollider != null) playerCollider.enabled = !dead;
    }

    [ClientRpc]
    private void RespawnClientRpc(Vector3 position, Quaternion rotation)
    {
        if (characterController != null) characterController.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (characterController != null) characterController.enabled = true;

        if (IsOwner)
        {
            // Close Death Screen UI
            if (DeathScreenUI.Instance != null)
            {
                DeathScreenUI.Instance.HideDeathScreen();
            }

            // Return camera to owner
            if (ThirdPersonCamera.LocalCamera != null)
            {
                ThirdPersonCamera.LocalCamera.StopSpectating();
            }
        }
    }
}
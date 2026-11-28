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

    [Header("Damage Feedback UI (Optional)")]
    [Tooltip("Auto-detected if inside Player Prefab, or uses DamageFeedbackUI.Instance in the Scene.")]
    public DamageFeedbackUI localDamageFeedback;

    public NetworkVariable<bool> isDead = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerPowerUpHandler powerUpHandler;
    private CharacterController characterController;
    private Collider playerCollider;
    private Animator playerAnimator;

    private void Awake()
    {
        powerUpHandler = GetComponent<PlayerPowerUpHandler>();
        characterController = GetComponent<CharacterController>();
        playerCollider = GetComponent<Collider>();
        playerAnimator = GetComponentInChildren<Animator>();

        if (localDamageFeedback == null)
        {
            localDamageFeedback = GetComponentInChildren<DamageFeedbackUI>(true);
        }
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

        if (powerUpHandler != null && powerUpHandler.isPrismBarrierActive.Value)
        {
            powerUpHandler.isPrismBarrierActive.Value = false;
            Debug.Log($"<color=cyan>[PowerUp Effect]</color> Prism Barrier absorbed damage for {gameObject.name}!");
            return;
        }

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);
        string attackerName = attacker != null ? attacker.name : "Environment/Self";
        Debug.Log($"[PlayerHealth] {gameObject.name} took {damage} damage from {attackerName}. Current HP: {currentHealth.Value}");

        // Trigger red screen fade feedback ONLY for the damaged player's screen
        ShowDamageFeedbackClientRpc();

        if (currentHealth.Value > 0)
        {
            PlayDamageAnimationClientRpc();
        }

        if (currentHealth.Value <= 0)
        {
            Die(attacker);
        }
    }

    [ClientRpc]
    private void ShowDamageFeedbackClientRpc()
    {
        if (!IsOwner) return;

        if (localDamageFeedback != null)
        {
            localDamageFeedback.TriggerDamageFlash();
        }
        else if (DamageFeedbackUI.Instance != null)
        {
            DamageFeedbackUI.Instance.TriggerDamageFlash();
        }
    }

    [ClientRpc]
    private void PlayDamageAnimationClientRpc()
    {
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("TakeDamage");
        }
    }

    private void Die(NetworkObject killer = null)
    {
        isDead.Value = true;

        if (killer != null && killer != NetworkObject)
        {
            if (killer.TryGetComponent<PlayerScore>(out PlayerScore killerScore))
            {
                killerScore.AddScore(pointsPerKill);
            }
        }

        SetPlayerDeadStateClientRpc(true);

        ulong killerId = (killer != null) ? killer.NetworkObjectId : 0;
        NotifyDeathClientRpc(respawnDelay, killerId);

        StartCoroutine(RespawnTimerRoutine());
    }

    private IEnumerator RespawnTimerRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

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

        currentHealth.Value = maxHealth;
        isDead.Value = false;

        RespawnClientRpc(newPos, newRot);
        SetPlayerDeadStateClientRpc(false);

        Debug.Log($"[PlayerHealth] {gameObject.name} respawned successfully!");
    }

    [ClientRpc]
    private void NotifyDeathClientRpc(float delay, ulong killerNetId)
    {
        if (!IsOwner) return;

        if (DeathScreenUI.Instance != null)
        {
            DeathScreenUI.Instance.ShowDeathScreen(delay);
        }

        if (ThirdPersonCamera.LocalCamera != null)
        {
            ThirdPersonCamera.LocalCamera.StartSpectating(killerNetId);
        }
    }

    [ClientRpc]
    private void SetPlayerDeadStateClientRpc(bool dead)
    {
        if (characterModelRoot != null && characterModelRoot != this.gameObject)
        {
            characterModelRoot.SetActive(!dead);
        }
        else
        {
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                r.enabled = !dead;
            }
        }

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
            if (localDamageFeedback != null)
            {
                localDamageFeedback.HideImmediate();
            }
            else if (DamageFeedbackUI.Instance != null)
            {
                DamageFeedbackUI.Instance.HideImmediate();
            }

            if (DeathScreenUI.Instance != null)
            {
                DeathScreenUI.Instance.HideDeathScreen();
            }

            if (ThirdPersonCamera.LocalCamera != null)
            {
                ThirdPersonCamera.LocalCamera.StopSpectating();
            }
        }
    }
}
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerFallTeleporter : NetworkBehaviour
{
    [Header("Fall Bounds")]
    [Tooltip("If the player's Y position goes below this value, they will be teleported to a spawn point.")]
    public float fallThresholdY = -10f;

    private bool isTeleporting = false;

    private void Update()
    {
        
        if (!IsOwner) return;

        if (transform.position.y < fallThresholdY && !isTeleporting)
        {
            Debug.LogWarning($"<color=orange>[FallDetector] Player fell below Y={fallThresholdY}! Requesting teleport...</color>");
            RequestSpawnPointServerRpc();
        }
    }

    [ServerRpc]
    private void RequestSpawnPointServerRpc()
    {
        PlayerSpawnManager spawnManager = Object.FindAnyObjectByType<PlayerSpawnManager>();

        if (spawnManager != null)
        {
            Vector3 targetPos = spawnManager.GetNextSpawnPosition();
            Quaternion targetRot = spawnManager.GetNextSpawnRotation();

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
            };

            TeleportOwnerClientRpc(targetPos, targetRot, clientRpcParams);
        }
    }

    [ClientRpc]
    private void TeleportOwnerClientRpc(Vector3 targetPosition, Quaternion targetRotation, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        StartCoroutine(ExecuteTeleportRoutine(targetPosition, targetRotation));
    }

    private IEnumerator ExecuteTeleportRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        isTeleporting = true;

        CharacterController cc = GetComponent<CharacterController>();
        Rigidbody rb = GetComponent<Rigidbody>();

        
        if (cc != null) cc.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

       
        transform.position = targetPosition;
        transform.rotation = targetRotation;

        Debug.Log($"<color=green>[FallDetector SUCCESS] Caught player fall! Teleported to {targetPosition}</color>");

        
        yield return new WaitForFixedUpdate();

        if (cc != null) cc.enabled = true;
        isTeleporting = false;
    }
}
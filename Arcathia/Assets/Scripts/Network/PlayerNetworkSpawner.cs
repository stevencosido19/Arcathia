using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerNetworkSpawner : NetworkBehaviour
{
    [Header("Target Stage")]
    public string gameplaySceneName = "GameStage1";

    private bool hasSpawnedAtPoint = false;

    public override void OnNetworkSpawn()
    {
        
        StartCoroutine(CheckSceneAndSpawnRoutine());
    }

    private IEnumerator CheckSceneAndSpawnRoutine()
    {
        
        while (!hasSpawnedAtPoint)
        {
            string currentScene = SceneManager.GetActiveScene().name;

            if (currentScene == gameplaySceneName)
            {
                if (IsOwner)
                {
                    Debug.Log($"<color=yellow>[Spawner] Player detected target scene '{gameplaySceneName}'. Requesting spawn location from server...</color>");
                    RequestSpawnPointServerRpc();
                    hasSpawnedAtPoint = true; 
                }
            }

            
            yield return null;
        }
    }

    [ServerRpc]
    private void RequestSpawnPointServerRpc()
    {
        Debug.Log("<color=cyan>[Spawner] Server received spawn request.</color>");

        PlayerSpawnManager spawnManager = Object.FindAnyObjectByType<PlayerSpawnManager>();

        if (spawnManager == null)
        {
            Debug.LogError("<color=red>[Spawner FAIL] PlayerSpawnManager was NOT FOUND in GameStage1! Is the script attached to an active object in GameStage1?</color>");
            return;
        }

        Vector3 targetPos = spawnManager.GetNextSpawnPosition();
        Quaternion targetRot = spawnManager.GetNextSpawnRotation();

        Debug.Log($"<color=green>[Spawner] Server fetched position: {targetPos}</color>");

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
        };

        ApplySpawnPositionClientRpc(targetPos, targetRot, clientRpcParams);
    }

    [ClientRpc]
    private void ApplySpawnPositionClientRpc(Vector3 targetPosition, Quaternion targetRotation, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner) return;

        StartCoroutine(ExecuteTeleportRoutine(targetPosition, targetRotation));
    }

    private IEnumerator ExecuteTeleportRoutine(Vector3 targetPosition, Quaternion targetRotation)
    {
        CharacterController cc = GetComponent<CharacterController>();
        PlayerController playerCtrl = GetComponent<PlayerController>();

        
        if (cc != null) cc.enabled = false;
        if (playerCtrl != null) playerCtrl.ResetVelocity();

        
        transform.position = targetPosition;
        transform.rotation = targetRotation;

        Debug.Log($"<color=cyan>[Spawner SUCCESS] Teleported local owner to {targetPosition}</color>");

        
        yield return new WaitForFixedUpdate();

        if (cc != null) cc.enabled = true;
    }

    
    public void ResetSpawnFlag()
    {
        hasSpawnedAtPoint = false;
    }
}
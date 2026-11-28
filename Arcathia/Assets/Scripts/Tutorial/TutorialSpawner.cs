using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class TutorialSpawner : MonoBehaviour
{
    [Header("Spawn Location")]
    [Tooltip("Optional: Drag a specific spawn point Transform here. If empty, uses this GameObject's position and rotation.")]
    public Transform spawnPoint;

    [Header("Fallback Prefab")]
    [Tooltip("Used only if NetworkManager doesn't automatically spawn the Player Prefab.")]
    public GameObject playerPrefab;

    private Transform TargetTransform => spawnPoint != null ? spawnPoint : transform;
    private bool hasPlacedPlayer = false;

    private void Start()
    {
        // Try immediately on frame 0 so there is no visual flicker before the screen freezes
        if (!TrySpawnOrMoveImmediately())
        {
            StartCoroutine(WaitAndSpawnRoutine());
        }
    }

    private IEnumerator WaitAndSpawnRoutine()
    {
        // Wait using unscaledDeltaTime so this still works even if TutorialManager sets Time.timeScale = 0
        float waitTimer = 0f;
        while (!hasPlacedPlayer && waitTimer < 3f)
        {
            if (TrySpawnOrMoveImmediately())
            {
                yield break;
            }

            waitTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!hasPlacedPlayer)
        {
            Debug.LogWarning("[TutorialSpawner] Could not spawn or move player. Make sure you started from MainMenu or have a NetworkManager in the scene.");
        }
    }

    private bool TrySpawnOrMoveImmediately()
    {
        if (hasPlacedPlayer) return true;

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return false;

        // Case 1: PlayerObject already exists (auto-created by NetworkManager on StartHost)
        NetworkObject existingPlayer = nm.LocalClient?.PlayerObject;
        if (existingPlayer != null)
        {
            MovePlayerToSpawn(existingPlayer.gameObject);
            hasPlacedPlayer = true;
            return true;
        }

        // Case 2: NetworkManager has no auto PlayerPrefab configured -> spawn manually here
        if (nm.IsServer && nm.NetworkConfig.PlayerPrefab == null && playerPrefab != null)
        {
            GameObject spawned = Instantiate(playerPrefab, TargetTransform.position, TargetTransform.rotation);
            NetworkObject netObj = spawned.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.SpawnAsPlayerObject(nm.LocalClientId, true);
                MovePlayerToSpawn(spawned);
                hasPlacedPlayer = true;
                return true;
            }
        }

        return false;
    }

    private void MovePlayerToSpawn(GameObject player)
    {
        Vector3 targetPos = TargetTransform.position;
        Quaternion targetRot = TargetTransform.rotation;

        // 1. Disable CharacterController before moving (prevents Unity from snapping back)
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // 2. Move / Teleport safely across NetworkTransform if present
        NetworkTransform netTransform = player.GetComponent<NetworkTransform>();
        if (netTransform != null && netTransform.IsSpawned)
        {
            netTransform.Teleport(targetPos, targetRot, player.transform.localScale);
        }
        else
        {
            player.transform.SetPositionAndRotation(targetPos, targetRot);
        }

        Physics.SyncTransforms();

        // 3. Re-enable CharacterController
        if (cc != null) cc.enabled = true;

        // 4. Reset gravity/knockback velocity on your PlayerController
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.ResetVelocity();
        }

        Debug.Log($"<color=cyan>[TutorialSpawner] Player spawned at {targetPos}</color>");
    }

    // Draws a cyan sphere & facing arrow in the Scene view so you can aim your player easily
    private void OnDrawGizmos()
    {
        Transform t = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(t.position, 0.5f);
        Gizmos.DrawRay(t.position, t.forward * 1.5f);
    }
}
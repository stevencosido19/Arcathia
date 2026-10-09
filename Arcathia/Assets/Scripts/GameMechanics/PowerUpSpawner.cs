using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PowerUpSpawner : NetworkBehaviour
{
    [System.Serializable]
    public struct PowerUpSpawnData
    {
        public string powerUpName;
        public GameObject prefab;
        [Range(1, 100)]
        public int spawnWeight;
    }

    [Header("Power-Up Drop Table")]
    public List<PowerUpSpawnData> dropTable = new List<PowerUpSpawnData>();

    [Header("Spawn Locations")]
    public List<Transform> spawnPoints;

    [Header("Spawner Settings")]
    public float initialSpawnDelay = 1.0f;
    public float spawnInterval = 5.0f;
    public int maxActivePowerUps = 3;

    private List<GameObject> activePowerUps = new List<GameObject>();

    private bool IsMatchOver()
    {
        return MatchManager.Instance != null && MatchManager.Instance.isMatchOver.Value;
    }

    private void Awake()
    {
        // Safety check to warn if NetworkObject component is missing on this GameObject
        if (GetComponent<NetworkObject>() == null)
        {
            Debug.LogError($"[PowerUpSpawner] '{gameObject.name}' is MISSING a NetworkObject component! OnNetworkSpawn() will NEVER fire.");
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            Debug.Log("[PowerUpSpawner] Running on Client - Spawning loop ignored.");
            return;
        }

        Debug.Log($"<color=green>[PowerUpSpawner] Server detected! Starting spawn loop in {initialSpawnDelay}s every {spawnInterval}s.</color>");
        InvokeRepeating(nameof(TrySpawnPowerUp), initialSpawnDelay, spawnInterval);
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            CancelInvoke(nameof(TrySpawnPowerUp));
        }
    }

    private void TrySpawnPowerUp()
    {
        if (!IsServer) return;

        if (IsMatchOver())
        {
            Debug.Log("[PowerUpSpawner] Match is over. Skipping spawn.");
            return;
        }

        activePowerUps.RemoveAll(p => p == null);

        if (activePowerUps.Count >= maxActivePowerUps)
        {
            // Already at capacity
            return;
        }

        if (dropTable.Count == 0 || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[PowerUpSpawner] Cannot spawn! DropTable or SpawnPoints list is EMPTY in Inspector.");
            return;
        }

        GameObject selectedPrefab = GetWeightedRandomPowerUp();
        Transform targetPoint = GetAvailableSpawnPoint();

        if (selectedPrefab == null)
        {
            Debug.LogWarning("[PowerUpSpawner] GetWeightedRandomPowerUp() returned null.");
            return;
        }

        if (targetPoint == null)
        {
            // All spawn spots are occupied by existing power-ups
            return;
        }

        // 1. Instantiate on Server
        GameObject spawned = Instantiate(selectedPrefab, targetPoint.position, targetPoint.rotation);
        NetworkObject netObj = spawned.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            // 2. Spawn across Netcode
            netObj.Spawn();
            activePowerUps.Add(spawned);
            Debug.Log($"<color=cyan>[PowerUpSpawner] Successfully spawned '{selectedPrefab.name}' at {targetPoint.name}. Active count: {activePowerUps.Count}</color>");
        }
        else
        {
            Debug.LogError($"[PowerUpSpawner] Prefab '{selectedPrefab.name}' is missing a NetworkObject component!");
            Destroy(spawned);
        }
    }

    private GameObject GetWeightedRandomPowerUp()
    {
        int totalWeight = 0;
        foreach (var item in dropTable)
        {
            if (item.prefab != null) totalWeight += item.spawnWeight;
        }

        if (totalWeight <= 0) return null;

        int randomValue = Random.Range(0, totalWeight);
        int currentSum = 0;

        foreach (var item in dropTable)
        {
            if (item.prefab == null) continue;
            currentSum += item.spawnWeight;
            if (randomValue < currentSum) return item.prefab;
        }

        return dropTable.Count > 0 ? dropTable[0].prefab : null;
    }

    private Transform GetAvailableSpawnPoint()
    {
        List<Transform> validPoints = new List<Transform>();

        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;

            Collider[] hitColliders = Physics.OverlapSphere(point.position, 0.5f);
            bool isOccupied = false;

            foreach (Collider col in hitColliders)
            {
                if (col.GetComponentInParent<PowerUpItem>() != null)
                {
                    isOccupied = true;
                    break;
                }
            }

            if (!isOccupied) validPoints.Add(point);
        }

        if (validPoints.Count == 0) return null;
        return validPoints[Random.Range(0, validPoints.Count)];
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (Transform point in spawnPoints)
        {
            if (point != null) Gizmos.DrawWireSphere(point.position, 0.5f);
        }
    }
}
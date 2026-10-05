using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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
    private bool isSpawningStarted = false;

    private void Start()
    {
        StartSpawningLoop();
    }

    public override void OnNetworkSpawn()
    {
        StartSpawningLoop();
    }

    private void StartSpawningLoop()
    {
        if (isSpawningStarted) return;
        isSpawningStarted = true;
        InvokeRepeating(nameof(TrySpawnPowerUp), initialSpawnDelay, spawnInterval);
    }

    private void TrySpawnPowerUp()
    {
        activePowerUps.RemoveAll(p => p == null);

        if (activePowerUps.Count >= maxActivePowerUps) return;
        if (dropTable.Count == 0 || spawnPoints.Count == 0) return;

        GameObject selectedPrefab = GetWeightedRandomPowerUp();
        Transform targetPoint = GetAvailableSpawnPoint();

        if (selectedPrefab == null || targetPoint == null) return;

        GameObject spawned = Instantiate(selectedPrefab, targetPoint.position, targetPoint.rotation);
        activePowerUps.Add(spawned);

        NetworkObject netObj = spawned.GetComponent<NetworkObject>();

        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (!netObj.IsSpawned) netObj.Spawn();
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
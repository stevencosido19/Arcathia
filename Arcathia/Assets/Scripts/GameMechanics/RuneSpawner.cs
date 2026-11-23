using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RuneSpawner : NetworkBehaviour
{
    [Header("Rune Prefabs (Must have NetworkObject)")]
    public GameObject waterRunePrefab;
    public GameObject lightningRunePrefab;

    [Header("Spawn Locations")]
    [Tooltip("Drag your empty GameObjects here to act as spawn spots.")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Spawn Timing Settings")]
    public float initialSpawnDelay = 2f;
    public float spawnInterval = 10f;
    public int maxRunesInStage = 5;

    
    private Dictionary<Transform, GameObject> activeRuneMap = new Dictionary<Transform, GameObject>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            StartCoroutine(RuneSpawnRoutine());
        }
    }

    private IEnumerator RuneSpawnRoutine()
    {
        yield return new WaitForSeconds(initialSpawnDelay);

        while (true)
        {
            
            CleanupDespawnedRunes();

            
            if (activeRuneMap.Count < maxRunesInStage)
            {
                SpawnRandomRuneAtFreeLocation();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void CleanupDespawnedRunes()
    {
        List<Transform> keysToRemove = new List<Transform>();

        foreach (var kvp in activeRuneMap)
        {
            if (kvp.Value == null)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (Transform key in keysToRemove)
        {
            activeRuneMap.Remove(key);
        }
    }

    private void SpawnRandomRuneAtFreeLocation()
    {
        
        List<Transform> freeSpawnPoints = new List<Transform>();

        foreach (Transform point in spawnPoints)
        {
            if (point != null && !activeRuneMap.ContainsKey(point))
            {
                freeSpawnPoints.Add(point);
            }
        }

        
        if (freeSpawnPoints.Count == 0) return;

        
        Transform chosenSpawnPoint = freeSpawnPoints[Random.Range(0, freeSpawnPoints.Count)];

       
        GameObject selectedPrefab = (Random.value > 0.5f) ? waterRunePrefab : lightningRunePrefab;

        if (selectedPrefab == null)
        {
            Debug.LogWarning("[RuneSpawner] Prefab is missing in Inspector!");
            return;
        }

        
        GameObject spawnedRune = Instantiate(selectedPrefab, chosenSpawnPoint.position, chosenSpawnPoint.rotation);

        
        activeRuneMap[chosenSpawnPoint] = spawnedRune;

        NetworkObject netObj = spawnedRune.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
            Debug.Log($"[RuneSpawner] Spawned {selectedPrefab.name} at free spot {chosenSpawnPoint.name}");
        }
        else
        {
            Debug.LogError($"[RuneSpawner] {selectedPrefab.name} is missing NetworkObject!");
        }
    }
}
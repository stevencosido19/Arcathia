using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new List<Transform>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Returns a random spawn point transform, or Vector3.zero if none configured.
    /// </summary>
    public Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[SpawnManager] No spawn points assigned! Respawning at origin.");
            return transform;
        }

        int index = Random.Range(0, spawnPoints.Count);
        return spawnPoints[index];
    }
}
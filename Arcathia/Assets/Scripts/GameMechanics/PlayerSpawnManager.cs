using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawnManager : MonoBehaviour
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new List<Transform>();

    private List<Transform> availableSpawnPoints = new List<Transform>();

    private void Awake()
    {
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResetSpawnPoints();
    }

    private void ResetSpawnPoints()
    {
        availableSpawnPoints = new List<Transform>(spawnPoints);
    }

    
    public Transform GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawnManager] No spawn points assigned!");
            return null;
        }

        
        if (availableSpawnPoints.Count == 0)
        {
            ResetSpawnPoints();
        }

        int index = Random.Range(0, availableSpawnPoints.Count);
        Transform selectedPoint = availableSpawnPoints[index];
        availableSpawnPoints.RemoveAt(index);

        return selectedPoint;
    }

    
    public Vector3 GetNextSpawnPosition()
    {
        Transform point = GetNextSpawnPoint();
        return point != null ? point.position : new Vector3(0, 2f, 0);
    }


    public Quaternion GetNextSpawnRotation()
    {
        Transform point = GetNextSpawnPoint();
        return point != null ? point.rotation : Quaternion.identity;
    }
}
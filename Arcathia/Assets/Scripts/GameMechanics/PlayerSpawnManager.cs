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
        // Singleton pattern setup
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

    /// <summary>
    /// Returns a single non-repeating spawn point Transform.
    /// Use this when you need both position and rotation together.
    /// </summary>
    public Transform GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawnManager] No spawn points assigned!");
            return null;
        }

        // Refill if all points have been used once
        if (availableSpawnPoints.Count == 0)
        {
            ResetSpawnPoints();
        }

        int index = Random.Range(0, availableSpawnPoints.Count);
        Transform selectedPoint = availableSpawnPoints[index];
        availableSpawnPoints.RemoveAt(index);

        return selectedPoint;
    }

    /// <summary>
    /// Returns position of the next non-repeating spawn point.
    /// </summary>
    public Vector3 GetNextSpawnPosition()
    {
        Transform point = GetNextSpawnPoint();
        return point != null ? point.position : new Vector3(0, 2f, 0);
    }

    /// <summary>
    /// Returns rotation of the next non-repeating spawn point.
    /// </summary>
    public Quaternion GetNextSpawnRotation()
    {
        Transform point = GetNextSpawnPoint();
        return point != null ? point.rotation : Quaternion.identity;
    }
}
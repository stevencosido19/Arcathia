using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class CampaignEnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefab")]
    [Tooltip("Drag your Enemy Prefab here from your Project window.")]
    public GameObject enemyPrefab;

    [Header("Spawn Locations")]
    [Tooltip("Empty GameObjects placed around the map. If left empty, it will spawn at this GameObject's position.")]
    public Transform[] spawnPoints;

    [Header("Spawn Settings")]
    public float spawnInterval = 3.5f;
    public int maxEnemiesAlive = 3;
    public int totalEnemiesToSpawn = 5;
    public float initialStartDelay = 2.0f;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private int spawnedCount = 0;
    private float spawnTimer;
    private bool isSpawningReady = false;

    private void Start()
    {
        StartCoroutine(InitializeSpawnerRoutine());
    }

    private IEnumerator InitializeSpawnerRoutine()
    {
        // 1. Wait until NetworkManager is active and we are running as Server/Host
        yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);

        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("<color=grey>[EnemySpawner] Not running as Server/Host. Spawner disabled on this client.</color>");
            yield break;
        }

        Debug.Log("<color=cyan>[EnemySpawner] Server detected! Waiting initial delay before first spawn...</color>");
        yield return new WaitForSeconds(initialStartDelay);

        isSpawningReady = true;
        Debug.Log("<color=green>[EnemySpawner] Spawner is now ACTIVE!</color>");
    }

    private void Update()
    {
        if (!isSpawningReady) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        // 1. Stop spawning if the level is already won
        if (HasPlayerWon())
        {
            isSpawningReady = false;
            Debug.Log("<color=yellow>[EnemySpawner] Player achieved victory! Stopping spawns.</color>");
            return;
        }

        // 2. Remove dead/destroyed enemies from our tracking list
        activeEnemies.RemoveAll(enemy => enemy == null || !enemy.activeInHierarchy);

        // 3. Stop if we already spawned the full quota for this stage
        if (totalEnemiesToSpawn > 0 && spawnedCount >= totalEnemiesToSpawn)
        {
            return;
        }

        // 4. Pause if maximum concurrent enemies are currently alive on the field
        if (activeEnemies.Count >= maxEnemiesAlive)
        {
            return;
        }

        // 5. Timer countdown
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("<color=red>[EnemySpawner ERROR] No Enemy Prefab assigned in the Inspector!</color>");
            return;
        }

        // Determine spawn coordinates
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Pick a non-null spawn point
            List<Transform> validPoints = new List<Transform>();
            foreach (var pt in spawnPoints)
            {
                if (pt != null) validPoints.Add(pt);
            }

            if (validPoints.Count > 0)
            {
                Transform chosen = validPoints[Random.Range(0, validPoints.Count)];
                spawnPos = chosen.position;
                spawnRot = chosen.rotation;
            }
        }

        // Snap to nearest baked NavMesh ground
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }
        else
        {
            Debug.LogWarning($"<color=orange>[EnemySpawner] No NavMesh found near {spawnPos}! Ensure NavMesh is baked in this scene.</color>");
        }

        // 1. Instantiate the enemy on the server
        GameObject enemyInstance = Instantiate(enemyPrefab, spawnPos, spawnRot);

        // 2. Replicate across Netcode
        NetworkObject netObj = enemyInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn();
            activeEnemies.Add(enemyInstance);
            spawnedCount++;
            Debug.Log($"<color=green>[EnemySpawner SUCCESS] Spawned enemy #{spawnedCount}! Alive on field: {activeEnemies.Count}</color>");
        }
        else
        {
            Debug.LogError("<color=red>[EnemySpawner ERROR] Enemy Prefab does NOT have a NetworkObject component!</color>");
            Destroy(enemyInstance);
        }
    }

    private bool HasPlayerWon()
    {
        if (CampaignVictoryManager.Instance == null) return false;

        PlayerScore player = Object.FindFirstObjectByType<PlayerScore>();
        if (player != null && CampaignVictoryManager.Instance.targetKillsToWin > 0)
        {
            return player.score.Value >= CampaignVictoryManager.Instance.targetKillsToWin;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            foreach (Transform pt in spawnPoints)
            {
                if (pt != null)
                {
                    Gizmos.DrawWireSphere(pt.position, 0.8f);
                    Gizmos.DrawLine(pt.position, pt.position + pt.forward * 1.5f);
                }
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.8f);
        }
    }
}
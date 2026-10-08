using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkObjectPool : NetworkBehaviour
{
    public static NetworkObjectPool Instance { get; private set; }

    [System.Serializable]
    public struct PoolConfig
    {
        public GameObject prefab;
        public int prewarmCount;
    }

    [Header("Pool Configurations")]
    public List<PoolConfig> pooledPrefabs = new List<PoolConfig>();

    // Map prefab -> queue of inactive NetworkObjects
    private readonly Dictionary<GameObject, Queue<NetworkObject>> poolDictionary = new Dictionary<GameObject, Queue<NetworkObject>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        InitializePools();
    }

    private void InitializePools()
    {
        foreach (var config in pooledPrefabs)
        {
            if (config.prefab == null) continue;

            if (!poolDictionary.ContainsKey(config.prefab))
            {
                poolDictionary[config.prefab] = new Queue<NetworkObject>();
            }

            // Pre-allocate instances in memory on server start
            for (int i = 0; i < config.prewarmCount; i++)
            {
                NetworkObject netObj = CreateNewInstance(config.prefab);
                poolDictionary[config.prefab].Enqueue(netObj);
            }
        }
    }

    private NetworkObject CreateNewInstance(GameObject prefab)
    {
        GameObject go = Instantiate(prefab, transform);
        go.SetActive(false);

        NetworkObject netObj = go.GetComponent<NetworkObject>();
        return netObj;
    }

    /// <summary>
    /// Fetches an available pooled NetworkObject for spawning on the server.
    /// </summary>
    public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!IsServer)
        {
            Debug.LogError("[NetworkObjectPool] GetNetworkObject must be called on the Server!");
            return null;
        }

        if (!poolDictionary.TryGetValue(prefab, out var queue))
        {
            Debug.LogError($"[NetworkObjectPool] Prefab '{prefab.name}' is not registered in NetworkObjectPool!");
            return null;
        }

        if (queue.Count > 0)
        {
            NetworkObject netObj = queue.Dequeue();
            netObj.transform.SetPositionAndRotation(position, rotation);
            netObj.gameObject.SetActive(true);
            return netObj;
        }
        else
        {
            // STRICT POOL CAP: Return null if pool capacity is exhausted
            Debug.LogWarning($"[NetworkObjectPool] Pool limit reached for {prefab.name}! Waiting for projectiles to despawn.");
            return null;
        }
    }

    /// <summary>
    /// Returns an unspawned NetworkObject back to the pool queue on the server.
    /// </summary>
    public void ReturnNetworkObject(NetworkObject netObj, GameObject prefab)
    {
        if (!IsServer) return;

        // Deactivate object without reparenting to prevent Netcode errors
        netObj.gameObject.SetActive(false);

        if (poolDictionary.TryGetValue(prefab, out var queue))
        {
            queue.Enqueue(netObj);
        }
        else
        {
            Debug.LogError($"[NetworkObjectPool] Prefab '{prefab.name}' was not found in pool dictionary!");
        }
    }
}
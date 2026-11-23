using Unity.Netcode;
using UnityEngine;

public class EnemyProjectile : NetworkBehaviour
{
    [Header("Settings")]
    public float speed = 18f;
    public float damage = 20f;
    public float lifeTime = 5f;

    private float spawnTime;

    public override void OnNetworkSpawn()
    {
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (!IsServer) return;

        
        transform.position += transform.forward * speed * Time.deltaTime;

        if (Time.time >= spawnTime + lifeTime)
        {
            DespawnProjectile();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        
        if (other.GetComponentInParent<ShootingEnemyAI>() != null)
        {
            return;
        }

        
        PlayerPowerUpHandler powerUpHandler = other.GetComponentInParent<PlayerPowerUpHandler>();
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
           
            if (powerUpHandler != null && powerUpHandler.isPrismBarrierActive.Value)
            {
                Debug.Log("[EnemyProjectile] Shot was BLOCKED by Prism Barrier!");
            }
            else
            {
                Debug.Log($"[EnemyProjectile] Player hit for {damage} damage!");
                
            }

            DespawnProjectile();
            return;
        }

        
        if (!other.isTrigger)
        {
            DespawnProjectile();
        }
    }

    private void DespawnProjectile()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
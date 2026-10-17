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

        // Move forward on server
        transform.position += transform.forward * speed * Time.deltaTime;

        if (Time.time >= spawnTime + lifeTime)
        {
            DespawnProjectile();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // 1. Ignore hitting other enemies (checks script directly, no tag needed!)
        if (other.GetComponentInParent<ShootingEnemyAI>() != null)
        {
            return;
        }

        // 2. Check if hit the player
        PlayerPowerUpHandler powerUpHandler = other.GetComponentInParent<PlayerPowerUpHandler>();
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            // Check if player's Prism Barrier shield is active
            if (powerUpHandler != null && powerUpHandler.isPrismBarrierActive.Value)
            {
                Debug.Log("[EnemyProjectile] Shot was BLOCKED by Prism Barrier!");
            }
            else
            {
                Debug.Log($"[EnemyProjectile] Player hit for {damage} damage!");
                // Hook to your player's damage method here:
                // player.TakeDamage(damage);
            }

            DespawnProjectile();
            return;
        }

        // 3. Despawn if it collides with walls / terrain (non-trigger colliders)
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
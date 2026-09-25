using Unity.Netcode;
using UnityEngine;

public class MagicProjectile : NetworkBehaviour
{
    public float speed = 25f;
    public float lifeTime = 4f;
    public int damage = 20;

    private NetworkObject shooterNetObj;

    // Called on the Server before spawning to assign who shot this spell
    public void SetShooter(NetworkObject shooter)
    {
        shooterNetObj = shooter;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Server handles lifeTime timer through Netcode Despawn
        if (IsServer)
        {
            Invoke(nameof(DespawnProjectile), lifeTime);
        }
    }

    void Update()
    {
        // Move the projectile forward
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Physics damage logic MUST strictly run on the Server
        if (!IsServer) return;

        // 1. Search for PlayerHealth on the hit object OR its parent hierarchy
        PlayerHealth targetHealth = other.GetComponentInParent<PlayerHealth>();

        if (targetHealth != null)
        {
            // 2. IGNORE COLLISION if this is the player who fired the spell!
            if (shooterNetObj != null && targetHealth.NetworkObject == shooterNetObj)
            {
                return;
            }

            // 3. APPLY DAMAGE to the opponent
            Debug.Log($"[Spell Hit] Hit {targetHealth.gameObject.name}! Dealing {damage} damage.");
            targetHealth.TakeDamage(damage);

            // 4. Despawn projectile after successful hit
            DespawnProjectile();
        }
        else if (!other.isTrigger)
        {
            // Optional: Despawn if it hits walls or non-trigger environment obstacles
            DespawnProjectile();
        }
    }

    private void DespawnProjectile()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            CancelInvoke(nameof(DespawnProjectile));
            NetworkObject.Despawn();
        }
    }
}
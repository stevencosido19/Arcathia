using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MagicProjectile : NetworkBehaviour
{
    public float speed = 25f;
    public float lifeTime = 4f;
    public int damage = 20;

    private NetworkObject shooterNetObj;
    private Rigidbody rb;
    private bool hasHit = false; // Flag to prevent multiple hits in the same frame

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetShooter(NetworkObject shooter)
    {
        shooterNetObj = shooter;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        rb.linearVelocity = transform.forward * speed; // Use rb.velocity on older Unity versions

        if (IsServer)
        {
            Invoke(nameof(DespawnProjectile), lifeTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Only server calculates damage
        if (!IsServer) return;

        // 2. Prevent double-triggering in the same frame
        if (hasHit) return;

        // 3. Ignore self-hit on shooter
        if (shooterNetObj != null && (other.gameObject == shooterNetObj.gameObject || other.transform.IsChildOf(shooterNetObj.transform)))
        {
            return;
        }

        // Check if hit target has PlayerHealth
        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            hasHit = true; // Lock immediately before dealing damage

            // Pass shooterNetObj so PlayerHealth awards score to the attacker
            playerHealth.TakeDamage(damage, shooterNetObj);
            DespawnProjectile();
        }
        else if (!other.isTrigger) // Hit environmental wall/obstacle
        {
            hasHit = true;
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
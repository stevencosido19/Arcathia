using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MagicProjectile : NetworkBehaviour
{
    public float speed = 25f;
    public float lifeTime = 4f;
    public int damage = 20;

    private NetworkObject shooterNetObj;
    private GameObject sourcePrefab; // Tracking reference required for pool recycling
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

    public void SetSourcePrefab(GameObject prefab)
    {
        sourcePrefab = prefab;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // 1. Reset state for recycled pooled objects
        hasHit = false;

        // 2. Reset and apply physics
        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = transform.forward * speed; // Use rb.velocity on older Unity versions
        }

        // 3. Schedule lifetime despawn on server
        if (IsServer)
        {
            CancelInvoke(nameof(DespawnProjectile));
            Invoke(nameof(DespawnProjectile), lifeTime);
        }
    }

    // Handles Trigger Colliders
    private void OnTriggerEnter(Collider other)
    {
        HandleImpact(other.gameObject);
    }

    // Handles Physical Non-Trigger Colliders (Walls/Environment)
    private void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision.gameObject);
    }

    private void HandleImpact(GameObject hitObject)
    {
        // 1. Only server processes hits
        if (!IsServer || hasHit) return;

        // 2. Ignore self-hit on shooter
        if (shooterNetObj != null && (hitObject == shooterNetObj.gameObject || hitObject.transform.IsChildOf(shooterNetObj.transform)))
        {
            return;
        }

        // Lock immediately to prevent double hits
        hasHit = true;

        // Check if hit target has PlayerHealth
        PlayerHealth playerHealth = hitObject.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, shooterNetObj);
        }

        // Recycle projectile on any valid hit
        DespawnProjectile();
    }

    private void DespawnProjectile()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            CancelInvoke(nameof(DespawnProjectile));

            // 1. Unspawn across Netcode without calling Object.Destroy()
            NetworkObject.Despawn(false);

            // 2. Return instance back to NetworkObjectPool
            if (NetworkObjectPool.Instance != null && sourcePrefab != null)
            {
                NetworkObjectPool.Instance.ReturnNetworkObject(NetworkObject, sourcePrefab);
            }
            else if (sourcePrefab == null)
            {
                Debug.LogError($"[MagicProjectile] {gameObject.name} despawned but sourcePrefab was NULL! Missing SetSourcePrefab call.");
            }
        }
    }
}
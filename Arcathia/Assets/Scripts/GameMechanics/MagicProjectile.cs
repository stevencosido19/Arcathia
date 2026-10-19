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
    private bool hasHit = false;

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

        hasHit = false;

        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = transform.forward * speed; // rb.velocity on older Unity versions
        }

        if (IsServer)
        {
            CancelInvoke(nameof(DespawnProjectile));
            Invoke(nameof(DespawnProjectile), lifeTime);
        }
    }

    // Handles Trigger Colliders
    private void OnTriggerEnter(Collider other)
    {
        // CRITICAL FIX: Ignore all trigger colliders (like the enemy's DetectionArea!)
        if (other.isTrigger) return;

        HandleImpact(other.gameObject);
    }

    // Handles Physical Non-Trigger Colliders (Walls/Environment/Enemies)
    private void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision.gameObject);
    }

    private void HandleImpact(GameObject hitObject)
    {
        if (!IsServer || hasHit) return;

        // 1. Ignore self-hit on shooter
        if (shooterNetObj != null)
        {
            if (hitObject == shooterNetObj.gameObject || hitObject.transform.IsChildOf(shooterNetObj.transform))
            {
                return;
            }
        }

        // Secondary self-hit safeguard for local player on Host
        PlayerController hitPlayerCtrl = hitObject.GetComponentInParent<PlayerController>();
        if (hitPlayerCtrl != null && shooterNetObj != null && hitPlayerCtrl.NetworkObject == shooterNetObj)
        {
            return;
        }

        // Lock to prevent multiple hits
        hasHit = true;

        // 2. Check if hit AI Enemy (Campaign Mode)
        ShootingEnemyAI enemyAI = hitObject.GetComponentInParent<ShootingEnemyAI>();
        if (enemyAI != null)
        {
            PlayerScore attackerScore = (shooterNetObj != null) ? shooterNetObj.GetComponent<PlayerScore>() : null;
            enemyAI.TakeDamage(damage, attackerScore);
            DespawnProjectile();
            return;
        }

        // 3. Check if hit another Player (Multiplayer Mode)
        PlayerHealth playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage, shooterNetObj);
            DespawnProjectile();
            return;
        }

        // 4. Hit world geometry / walls / obstacles
        DespawnProjectile();
    }

    private void DespawnProjectile()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            CancelInvoke(nameof(DespawnProjectile));

            // Unspawn across Netcode without destroying the GameObject (pool friendly)
            NetworkObject.Despawn(false);

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
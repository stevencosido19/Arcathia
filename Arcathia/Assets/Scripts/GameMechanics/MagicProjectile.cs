using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MagicProjectile : NetworkBehaviour
{
    public float speed = 25f;
    public float lifeTime = 4f;
    public int damage = 20;

    [Header("Knockback & Stun")]
    public float knockbackForce = 12f;
    public float knockbackUpwardForce = 4f;
    public float stunDuration = 0.5f;

    [Header("Player/Enemy Hitbox Settings")]
    [Tooltip("Optional child Transform to set the exact hitbox location. Leave empty to use Damage Hitbox Offset.")]
    public Transform damageHitboxPoint;

    [Tooltip("Local offset from the projectile center (used if Damage Hitbox Point is empty).")]
    public Vector3 damageHitboxOffset = Vector3.zero;

    [Tooltip("Big radius used ONLY to damage Players & Enemies (ignores walls/ground).")]
    public float damageRadius = 0.65f;

    public bool debugHits = true;

    private NetworkObject shooterNetObj;
    private GameObject sourcePrefab;
    private Rigidbody rb;
    private bool hasHit = false;

    private static readonly Collider[] damageHits = new Collider[16];

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
            rb.linearVelocity = transform.forward * speed;
        }

        if (IsServer)
        {
            CancelInvoke(nameof(DespawnProjectile));
            Invoke(nameof(DespawnProjectile), lifeTime);
        }
    }

    private Vector3 GetDamageHitboxPosition()
    {
        if (damageHitboxPoint != null)
        {
            return damageHitboxPoint.position;
        }

        return transform.TransformPoint(damageHitboxOffset);
    }

    private void FixedUpdate()
    {
        if (!IsServer || hasHit) return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            GetDamageHitboxPosition(),
            damageRadius,
            damageHits,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = damageHits[i];
            if (col == null) continue;
            if (IsShooter(col.gameObject)) continue;

            if (TryDamageTarget(col.gameObject))
            {
                return;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || hasHit) return;

        if (other.isTrigger) return;
        if (IsShooter(other.gameObject)) return;
        if (TryDamageTarget(other.gameObject)) return;

        if (debugHits)
        {
            Debug.Log($"[MagicProjectile] Small collider hit environment: '{other.gameObject.name}' (Layer: {LayerMask.LayerToName(other.gameObject.layer)})", other.gameObject);
        }

        hasHit = true;
        DespawnProjectile();
    }

    private bool IsShooter(GameObject hitObject)
    {
        if (shooterNetObj != null)
        {
            if (hitObject == shooterNetObj.gameObject || hitObject.transform.IsChildOf(shooterNetObj.transform))
            {
                return true;
            }
        }

        PlayerController hitPlayerCtrl = hitObject.GetComponentInParent<PlayerController>();
        if (hitPlayerCtrl != null && shooterNetObj != null && hitPlayerCtrl.NetworkObject == shooterNetObj)
        {
            return true;
        }

        return false;
    }

    private bool TryDamageTarget(GameObject hitObject)
    {
        // 1. Enemy Check
        ShootingEnemyAI enemyAI = hitObject.GetComponentInParent<ShootingEnemyAI>();
        if (enemyAI != null)
        {
            if (debugHits) Debug.Log($"[MagicProjectile] Damaged Enemy: {enemyAI.name}");
            hasHit = true;
            PlayerScore attackerScore = (shooterNetObj != null) ? shooterNetObj.GetComponent<PlayerScore>() : null;
            enemyAI.TakeDamage(damage, attackerScore);
            DespawnProjectile();
            return true;
        }

        // 2. Player Check
        PlayerHealth playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            if (debugHits) Debug.Log($"[MagicProjectile] Damaged Player: {playerHealth.name}");
            hasHit = true;
            playerHealth.TakeDamage(damage, shooterNetObj);

            // Apply Knockback and Stun to the hit player
            PlayerController targetController = hitObject.GetComponentInParent<PlayerController>();
            if (targetController != null)
            {
                Vector3 knockbackDir = (targetController.transform.position - transform.position);
                knockbackDir.y = 0f;

                // Fallback to projectile's forward direction if directly centered
                if (knockbackDir.sqrMagnitude < 0.001f)
                {
                    knockbackDir = transform.forward;
                    knockbackDir.y = 0f;
                }
                knockbackDir.Normalize();

                Vector3 totalKnockback = (knockbackDir * knockbackForce) + (Vector3.up * knockbackUpwardForce);
                targetController.ApplyKnockbackAndStun(totalKnockback, stunDuration);
            }

            DespawnProjectile();
            return true;
        }

        return false;
    }

    private void DespawnProjectile()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            CancelInvoke(nameof(DespawnProjectile));

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(GetDamageHitboxPosition(), damageRadius);
    }
}
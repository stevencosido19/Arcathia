using UnityEngine;
using Unity.Netcode;

public class SpellProjectile : NetworkBehaviour
{
    [Header("Projectile Settings")]
    public float baseDamage = 20f;
    public float lifetime = 5f;

    [HideInInspector]
    public GameObject owner;

    private void Start()
    {
        if (IsServer)
        {
            Destroy(gameObject, lifetime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only server handles damage calculations
        if (!IsServer) return;

        // Ignore collision with caster
        if (owner != null && (other.gameObject == owner || other.transform.IsChildOf(owner.transform)))
        {
            return;
        }

        PlayerHealth targetHealth = other.GetComponentInParent<PlayerHealth>();

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(baseDamage);
            DespawnAndDestroy();
        }
        else if (!other.isTrigger)
        {
            DespawnAndDestroy();
        }
    }

    private void DespawnAndDestroy()
    {
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
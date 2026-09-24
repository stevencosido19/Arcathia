using Unity.Netcode;
using UnityEngine;

public class MagicProjectile : NetworkBehaviour
{
    public float speed = 25f;
    public float lifeTime = 4f;
    public int damage = 20;

    void Start()
    {
        if (IsServer)
        {
            Destroy(gameObject, lifeTime); // Auto destroy after lifetime
        }
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // Apply damage if hitting another player
        PlayerController target = other.GetComponent<PlayerController>();
        if (target != null)
        {
            Debug.Log($"Hit player! Dealt {damage} damage.");
        }

        // Despawn projectile over network
        GetComponent<NetworkObject>().Despawn();
    }
}
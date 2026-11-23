using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class EnemyDetectionZone : MonoBehaviour
{
    private ShootingEnemyAI parentAI;
    private SphereCollider detectionCollider;

    private void Awake()
    {
        parentAI = GetComponentInParent<ShootingEnemyAI>();
        detectionCollider = GetComponent<SphereCollider>();

        
        detectionCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        
        if (parentAI == null || !parentAI.IsServer) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            parentAI.OnPlayerDetected(player.transform);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentAI == null || !parentAI.IsServer) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            parentAI.OnPlayerLost(player.transform);
        }
    }
}
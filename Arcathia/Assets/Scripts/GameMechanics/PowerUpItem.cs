using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUpItem : NetworkBehaviour
{
    public PowerUpType powerUpType;

    
    private bool consumed = false;

    public override void OnNetworkSpawn()
    {
        
        consumed = false;

        
        Debug.Log($"[PowerUpItem] '{name}' spawned on network. NetworkObjectId={NetworkObjectId}, IsServer={IsServer}");
    }

    public override void OnNetworkDespawn()
    {
        
        Debug.Log($"[PowerUpItem] '{name}' despawned. NetworkObjectId={NetworkObjectId}, IsServer={IsServer}");
    }

    private void OnTriggerEnter(Collider other)
    {
        
        if (!IsServer) return;
        if (consumed) return;

        PlayerPowerUpHandler handler = other.GetComponentInParent<PlayerPowerUpHandler>();
        if (handler != null)
        {
            consumed = true;

            
            handler.GivePowerUp(powerUpType);

            
            RemovePowerUpFromWorld();
        }
    }

    private void RemovePowerUpFromWorld()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            
            NetworkObject.Despawn(true);
        }
        else
        {
            
            Debug.LogWarning($"[PowerUpItem] '{name}' was NOT spawned on the network. " +
                             "It is only being destroyed locally, so clients will still see it.");
            Destroy(gameObject);
        }
    }
}
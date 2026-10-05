using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUpItem : NetworkBehaviour
{
    public PowerUpType powerUpType;

    private void OnTriggerEnter(Collider other)
    {
        // Only the Server handles collision detection for world pickups
        if (!IsServer) return;

        PlayerPowerUpHandler handler = other.GetComponentInParent<PlayerPowerUpHandler>();
        if (handler != null)
        {
            // Give power-up to player (works for both Host and Client)
            handler.GivePowerUp(powerUpType);

            // Properly remove the item using Netcode standard practices
            RemovePowerUpFromWorld();
        }
    }

    private void RemovePowerUpFromWorld()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            // Despawn automatically and safely destroys the GameObject on the Server AND all Clients.
            // No ClientRpc is needed.
            NetworkObject.Despawn(true);
        }
        else
        {
            // Fallback for Host if it was a standard non-networked scene object
            Destroy(gameObject);
        }
    }
}
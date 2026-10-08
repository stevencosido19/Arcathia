using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUpItem : NetworkBehaviour
{
    public PowerUpType powerUpType;

    // Prevents giving the power-up twice when a player has several colliders
    // or two players touch the item in the same physics step.
    private bool consumed = false;

    public override void OnNetworkSpawn()
    {
        // DIAGNOSTIC: remove once the ghost-item problem is solved.
        // If a client never prints this for an item it can see, that item is a local
        // "ghost" copy that the server doesn't know about.
        Debug.Log($"[PowerUpItem] '{name}' spawned on network. NetworkObjectId={NetworkObjectId}, IsServer={IsServer}");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only the Server handles collision detection for world pickups
        if (!IsServer) return;
        if (consumed) return;

        PlayerPowerUpHandler handler = other.GetComponentInParent<PlayerPowerUpHandler>();
        if (handler != null)
        {
            consumed = true;

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
            // DIAGNOSTIC: if you see this warning, this item was never spawned on the network.
            // Destroy() below only removes it on this machine, so clients keep their own copy.
            Debug.LogWarning($"[PowerUpItem] '{name}' was NOT spawned on the network. " +
                             "It is only being destroyed locally, so clients will still see it.");
            Destroy(gameObject);
        }
    }
}
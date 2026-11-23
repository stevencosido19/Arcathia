using Unity.Netcode;
using UnityEngine;

public class RunePickup : NetworkBehaviour
{
    public SpellType spellType = SpellType.Water; 

    private void OnTriggerEnter(Collider other)
    {
        
        PlayerMagicNetwork playerMagic = other.GetComponentInParent<PlayerMagicNetwork>();

        if (playerMagic != null)
        {
            
            if (playerMagic.IsOwner)
            {
                SpellbookUI spellbook = FindFirstObjectByType<SpellbookUI>();
                if (spellbook != null)
                {
                    spellbook.AddRuneToInventory(spellType);
                    Debug.Log($"Picked up {spellType} Rune!");
                }

                
                DespawnRuneRpc();
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DespawnRuneRpc()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}
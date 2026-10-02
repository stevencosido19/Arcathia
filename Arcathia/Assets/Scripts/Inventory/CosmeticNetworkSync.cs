using System.Collections.Generic;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class CosmeticNetworkSync : NetworkBehaviour
{
    [Header("Database")]
    public List<CosmeticItem> allItems;

    [Header("Cosmetic Holders")]
    public Transform hatHolder;
    public Transform faceHolder;
    public Transform accessoryHolder;
    public Transform robeHolder;
    public Transform wandHolder;

    [Header("First Person Setup")]
    public Camera playerCamera;
    public string invisibleLayerName = "LocalCosmetics";

    // NGO requires FixedString32Bytes for string syncing
    private NetworkVariable<FixedString32Bytes> netHat = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netFace = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netAccessory = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netRobe = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netWand = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        netHat.OnValueChanged += (oldVal, newVal) => ApplyCosmetic(hatHolder, newVal.ToString());
        netFace.OnValueChanged += (oldVal, newVal) => ApplyCosmetic(faceHolder, newVal.ToString());
        netAccessory.OnValueChanged += (oldVal, newVal) => ApplyCosmetic(accessoryHolder, newVal.ToString());
        netRobe.OnValueChanged += (oldVal, newVal) => ApplyCosmetic(robeHolder, newVal.ToString());
        netWand.OnValueChanged += (oldVal, newVal) => ApplyCosmetic(wandHolder, newVal.ToString());

        if (IsOwner)
        {
            // 1. Tell the camera to IGNORE the LocalCosmetics layer
            if (playerCamera != null)
            {
                int layerToIgnore = LayerMask.NameToLayer(invisibleLayerName);
                playerCamera.cullingMask &= ~(1 << layerToIgnore); 
            }

            // 2. Read local save file and tell the server what we are wearing
            PlayerSaveData data = SaveManager.LoadData();
            UpdateCosmeticsServerRpc(
                data.equippedHat, 
                data.equippedFace, 
                data.equippedAccessory, 
                data.equippedRobe, 
                data.equippedWand
            );
        }
        else
        {
            // Initial load for non-owners to match current server state
            ApplyCosmetic(hatHolder, netHat.Value.ToString());
            ApplyCosmetic(faceHolder, netFace.Value.ToString());
            ApplyCosmetic(accessoryHolder, netAccessory.Value.ToString());
            ApplyCosmetic(robeHolder, netRobe.Value.ToString());
            ApplyCosmetic(wandHolder, netWand.Value.ToString());
        }
    }

    [ServerRpc]
    private void UpdateCosmeticsServerRpc(string hat, string face, string acc, string robe, string wand)
    {
        netHat.Value = new FixedString32Bytes(hat);
        netFace.Value = new FixedString32Bytes(face);
        netAccessory.Value = new FixedString32Bytes(acc);
        netRobe.Value = new FixedString32Bytes(robe);
        netWand.Value = new FixedString32Bytes(wand);
    }

    private void ApplyCosmetic(Transform holder, string itemID)
    {
        // Turn everything off first
        for (int i = 0; i < holder.childCount; i++)
        {
            holder.GetChild(i).gameObject.SetActive(false);
        }

        if (string.IsNullOrEmpty(itemID)) return;

        CosmeticItem item = allItems.Find(x => x.itemID == itemID);
        if (item != null && item.modelIndex < holder.childCount)
        {
            GameObject cosmeticObj = holder.GetChild(item.modelIndex).gameObject;
            cosmeticObj.SetActive(true);

            // If this is our local player, change the cosmetic's layer so the camera hides it
            if (IsOwner)
            {
                SetLayerRecursively(cosmeticObj, LayerMask.NameToLayer(invisibleLayerName));
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        
        obj.layer = newLayer;
        
        foreach (Transform child in obj.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }
}
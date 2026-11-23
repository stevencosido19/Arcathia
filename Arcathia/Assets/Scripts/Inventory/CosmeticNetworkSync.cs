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

    private NetworkVariable<FixedString32Bytes> netHat = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netFace = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netAccessory = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netRobe = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);
    private NetworkVariable<FixedString32Bytes> netWand = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // 1. Subscribe to network variable changes
        netHat.OnValueChanged += OnHatChanged;
        netFace.OnValueChanged += OnFaceChanged;
        netAccessory.OnValueChanged += OnAccessoryChanged;
        netRobe.OnValueChanged += OnRobeChanged;
        netWand.OnValueChanged += OnWandChanged;

        if (IsOwner)
        {
            // Owner loads local save data and tells the server
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
            // Remote clients apply current values on spawn
            ApplyCosmetic(hatHolder, netHat.Value.ToString());
            ApplyCosmetic(faceHolder, netFace.Value.ToString());
            ApplyCosmetic(accessoryHolder, netAccessory.Value.ToString());
            ApplyCosmetic(robeHolder, netRobe.Value.ToString());
            ApplyCosmetic(wandHolder, netWand.Value.ToString());
        }
    }

    public override void OnNetworkDespawn()
    {
        // Always unsubscribe to prevent memory leaks and ghost callbacks
        netHat.OnValueChanged -= OnHatChanged;
        netFace.OnValueChanged -= OnFaceChanged;
        netAccessory.OnValueChanged -= OnAccessoryChanged;
        netRobe.OnValueChanged -= OnRobeChanged;
        netWand.OnValueChanged -= OnWandChanged;
    }

    // Callbacks
    private void OnHatChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => ApplyCosmetic(hatHolder, newVal.ToString());
    private void OnFaceChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => ApplyCosmetic(faceHolder, newVal.ToString());
    private void OnAccessoryChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => ApplyCosmetic(accessoryHolder, newVal.ToString());
    private void OnRobeChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => ApplyCosmetic(robeHolder, newVal.ToString());
    private void OnWandChanged(FixedString32Bytes oldVal, FixedString32Bytes newVal) => ApplyCosmetic(wandHolder, newVal.ToString());

    [ServerRpc]
    private void UpdateCosmeticsServerRpc(string hat, string face, string acc, string robe, string wand)
    {
        netHat.Value = new FixedString32Bytes(hat ?? string.Empty);
        netFace.Value = new FixedString32Bytes(face ?? string.Empty);
        netAccessory.Value = new FixedString32Bytes(acc ?? string.Empty);
        netRobe.Value = new FixedString32Bytes(robe ?? string.Empty);
        netWand.Value = new FixedString32Bytes(wand ?? string.Empty);
    }

    private void ApplyCosmetic(Transform holder, string itemID)
    {
        // SAFETY CHECK: Prevents UnassignedReferenceException if a socket is empty
        if (holder == null) return;

        // Turn off all child meshes under this holder first
        for (int i = 0; i < holder.childCount; i++)
        {
            holder.GetChild(i).gameObject.SetActive(false);
        }

        if (string.IsNullOrEmpty(itemID) || allItems == null) return;

        CosmeticItem item = allItems.Find(x => x != null && x.itemID == itemID);
        if (item != null && item.modelIndex >= 0 && item.modelIndex < holder.childCount)
        {
            GameObject cosmeticObj = holder.GetChild(item.modelIndex).gameObject;
            cosmeticObj.SetActive(true);
        }
    }
}
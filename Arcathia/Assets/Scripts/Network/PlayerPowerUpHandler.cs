using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public enum PowerUpType
{
    None = 0,
    RuneLens,
    PrismBarrier,
    OverchargeMatrix
}

public class PlayerPowerUpHandler : NetworkBehaviour
{
    [Header("Networked Power-Up States")]
    public NetworkVariable<bool> isPrismBarrierActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isOverchargeActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Power-Up Settings")]
    public float prismBarrierDuration = 10f;
    public float overchargeDuration = 10f;

    private readonly List<PowerUpType> storedPowerUps = new List<PowerUpType>();
    private int selectedIndex = 0;
    private SpellbookUI spellbookUI;

    public override void OnNetworkSpawn()
    {
        // Add listeners so we can print Debug.Logs when states change for ANY player
        isPrismBarrierActive.OnValueChanged += OnPrismBarrierStateChanged;
        isOverchargeActive.OnValueChanged += OnOverchargeStateChanged;

        if (!IsOwner) return;
        FindSpellbookUI();
    }

    public override void OnNetworkDespawn()
    {
        // Always clean up listeners to prevent memory leaks
        isPrismBarrierActive.OnValueChanged -= OnPrismBarrierStateChanged;
        isOverchargeActive.OnValueChanged -= OnOverchargeStateChanged;
    }

    // --- DEBUG LOGS FOR VISUAL EFFECTS ---

    private void OnPrismBarrierStateChanged(bool previous, bool current)
    {
        if (current)
            Debug.Log($"[PowerUp] PRISM BARRIER ACTIVATED for player {NetworkObjectId}! They are shielded!");
        else
            Debug.Log($"[PowerUp] PRISM BARRIER DEACTIVATED for player {NetworkObjectId}. Shield is gone.");
    }

    private void OnOverchargeStateChanged(bool previous, bool current)
    {
        if (current)
            Debug.Log($"[PowerUp] OVERCHARGE MATRIX ACTIVATED for player {NetworkObjectId}! Unlimited power!");
        else
            Debug.Log($"[PowerUp] OVERCHARGE MATRIX DEACTIVATED for player {NetworkObjectId}. Power normalized.");
    }

    // --- CORE LOGIC ---

    public void SetSpellbookUI(SpellbookUI ui) { spellbookUI = ui; }

    private void FindSpellbookUI()
    {
        spellbookUI = FindFirstObjectByType<SpellbookUI>();
        if (spellbookUI != null) spellbookUI.BindPowerUpHandler(this);
    }

    public void GivePowerUp(PowerUpType type)
    {
        if (!IsServer) return;
        ReceivePowerUpClientRpc(type);
    }

    [ClientRpc]
    private void ReceivePowerUpClientRpc(PowerUpType type)
    {
        if (!IsOwner) return;
        TryStorePowerUp(type);
    }

    public bool TryStorePowerUp(PowerUpType type)
    {
        if (!IsOwner || type == PowerUpType.None) return false;
        if (storedPowerUps.Contains(type)) return false;

        storedPowerUps.Add(type);
        selectedIndex = storedPowerUps.Count - 1;
        UpdateUI();

        Debug.Log($"[PowerUp] {type} stored in inventory!");
        return true;
    }

    public void CycleSelectedPowerUp(int direction)
    {
        if (!IsOwner || storedPowerUps.Count <= 1) return;
        selectedIndex += direction;
        if (selectedIndex >= storedPowerUps.Count) selectedIndex = 0;
        else if (selectedIndex < 0) selectedIndex = storedPowerUps.Count - 1;
        UpdateUI();
    }

    public PowerUpType GetSelectedPowerUp()
    {
        if (storedPowerUps.Count == 0 || selectedIndex < 0 || selectedIndex >= storedPowerUps.Count)
            return PowerUpType.None;
        return storedPowerUps[selectedIndex];
    }

    public int GetStoredPowerUpsCount() => storedPowerUps.Count;

    public void UseSelectedPowerUp()
    {
        if (!IsOwner || storedPowerUps.Count == 0) return;

        PowerUpType activeType = GetSelectedPowerUp();
        if (activeType == PowerUpType.None) return;

        ExecutePowerUpEffect(activeType);

        storedPowerUps.RemoveAt(selectedIndex);
        if (selectedIndex >= storedPowerUps.Count && storedPowerUps.Count > 0) selectedIndex = storedPowerUps.Count - 1;
        else if (storedPowerUps.Count == 0) selectedIndex = 0;

        UpdateUI();
    }

    private void ExecutePowerUpEffect(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.RuneLens:
                if (spellbookUI != null) spellbookUI.ApplyRuneLensEffect();
                break;
            case PowerUpType.PrismBarrier:
                ActivatePrismBarrierServerRpc(true);
                break;
            case PowerUpType.OverchargeMatrix:
                ActivateOverchargeServerRpc(true);
                break;
        }
    }

    // --- SERVER TIMERS ---

    [ServerRpc]
    private void ActivatePrismBarrierServerRpc(bool state)
    {
        isPrismBarrierActive.Value = state;
        if (state) StartCoroutine(PrismBarrierTimer());
    }

    private IEnumerator PrismBarrierTimer()
    {
        yield return new WaitForSeconds(prismBarrierDuration);
        isPrismBarrierActive.Value = false; // Turns off automatically
    }

    [ServerRpc]
    private void ActivateOverchargeServerRpc(bool state)
    {
        isOverchargeActive.Value = state;
        if (state) StartCoroutine(OverchargeTimer());
    }

    private IEnumerator OverchargeTimer()
    {
        yield return new WaitForSeconds(overchargeDuration);
        isOverchargeActive.Value = false; // Turns off automatically
    }

    private void UpdateUI()
    {
        if (spellbookUI == null) FindSpellbookUI();
        if (spellbookUI != null) spellbookUI.UpdatePowerUpDisplay(storedPowerUps, selectedIndex);
    }
}
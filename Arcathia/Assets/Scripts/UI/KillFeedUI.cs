using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class KillFeedUI : NetworkBehaviour
{
    public static KillFeedUI Instance { get; private set; }

    [Header("UI References")]
    public Transform killFeedContainer; // Top-left container (Vertical Layout Group)
    public GameObject killTextPrefab;    // Single TMP_Text prefab
    public float displayDuration = 4f;   // Time in seconds before text disappears

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Call this from Server when a player dies.
    /// </summary>
    public void SendKillNotification(string attackerName, string victimName)
    {
        if (!IsServer) return;
        NotifyKillClientRpc(attackerName, victimName);
    }

    [ClientRpc]
    private void NotifyKillClientRpc(string attackerName, string victimName)
    {
        if (killFeedContainer == null || killTextPrefab == null) return;

        // Instantiate text item inside the top-left container
        GameObject newTextObj = Instantiate(killTextPrefab, killFeedContainer, false);
        newTextObj.transform.localScale = Vector3.one;

        TMP_Text textComponent = newTextObj.GetComponent<TMP_Text>();
        if (textComponent != null)
        {
            // Format: Attacker Killed Victim
            textComponent.text = $"<color=#FF5555>{attackerName}</color> <color=#FFFFFF> Killed </color> <color=#FFFF55>{victimName}</color>";
        }

        // Auto-destroy the text line after displayDuration seconds
        Destroy(newTextObj, displayDuration);
    }
}
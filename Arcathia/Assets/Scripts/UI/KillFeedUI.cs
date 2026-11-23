using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class KillFeedUI : NetworkBehaviour
{
    public static KillFeedUI Instance { get; private set; }

    [Header("UI References")]
    public Transform killFeedContainer; 
    public GameObject killTextPrefab;    
    public float displayDuration = 4f;   

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    
    public void SendKillNotification(string attackerName, string victimName)
    {
        if (!IsServer) return;
        NotifyKillClientRpc(attackerName, victimName);
    }

    [ClientRpc]
    private void NotifyKillClientRpc(string attackerName, string victimName)
    {
        if (killFeedContainer == null || killTextPrefab == null) return;

        
        GameObject newTextObj = Instantiate(killTextPrefab, killFeedContainer, false);
        newTextObj.transform.localScale = Vector3.one;

        TMP_Text textComponent = newTextObj.GetComponent<TMP_Text>();
        if (textComponent != null)
        {
            
            textComponent.text = $"<color=#FF5555>{attackerName}</color> <color=#FFFFFF> Killed </color> <color=#FFFF55>{victimName}</color>";
        }

        
        Destroy(newTextObj, displayDuration);
    }
}
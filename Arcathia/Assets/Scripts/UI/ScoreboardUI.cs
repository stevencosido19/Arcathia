using System.Collections.Generic;
using UnityEngine;

public class ScoreboardUI : MonoBehaviour
{
    public static ScoreboardUI Instance { get; private set; }

    [Header("UI References")]
    public Transform container;
    public GameObject slotPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        RebuildScoreboard();
    }

    public void RebuildScoreboard()
    {
        if (container == null || slotPrefab == null)
        {
            Debug.LogError("[ScoreboardUI ERROR] Container or SlotPrefab is missing in the Inspector!");
            return;
        }

        // Clear previous UI slots
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }

        // Find all active PlayerScore components
        PlayerScore[] players = FindObjectsByType<PlayerScore>(FindObjectsSortMode.None);

        foreach (PlayerScore player in players)
        {
            GameObject newSlot = Instantiate(slotPrefab, container, false);
            newSlot.transform.localScale = Vector3.one;

            PlayerScoreSlotUI slotUI = newSlot.GetComponent<PlayerScoreSlotUI>();

            if (slotUI != null)
            {
                Sprite avatarToUse = player.GetAvatar();
                slotUI.SetupSlot(player, avatarToUse);
            }
            else
            {
                Debug.LogError("[ScoreboardUI ERROR] PlayerScoreSlotUI component missing on instantiated slotPrefab!");
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;

public class MatchEndUI : MonoBehaviour
{
    [Header("Timer Display HUD")]
    public TMP_Text matchTimerText;

    [Header("Gameplay HUD Container (Hidden on Match End)")]
    public GameObject gameplayHUD; 

    [Header("End Game Panel UI")]
    public GameObject endGamePanel;
    public Transform leaderboardContainer;
    public GameObject leaderboardRowPrefab; 

    [Header("Navigation Buttons & Settings")]
    public Button mainMenuButton;
    public string mainMenuSceneName = "MainMenu"; 

    [Header("Fallback Text Mode (If no prefab assigned)")]
    public TMP_Text rankingSummaryText;

    private bool hasDisplayedEndGame = false;

    private void Start()
    {
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(false);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void Update()
    {
        if (MatchManager.Instance == null) return;

        
        UpdateTimerDisplay(MatchManager.Instance.timeRemaining.Value);

        
        if (MatchManager.Instance.isMatchOver.Value && !hasDisplayedEndGame)
        {
            hasDisplayedEndGame = true;
            ShowEndGameRankings();
        }
    }

    private void UpdateTimerDisplay(float secondsRemaining)
    {
        if (matchTimerText == null) return;

        int minutes = Mathf.FloorToInt(secondsRemaining / 60f);
        int seconds = Mathf.FloorToInt(secondsRemaining % 60f);

        matchTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void ShowEndGameRankings()
    {
        
        if (gameplayHUD != null)
        {
            gameplayHUD.SetActive(false);
        }

        
        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (PlayerHealth player in allPlayers)
        {
            if (player.IsOwner)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }
        }

        
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }

        
        PlayerScore[] players = FindObjectsByType<PlayerScore>(FindObjectsSortMode.None);
        List<PlayerScore> sortedPlayers = new List<PlayerScore>(players);

        
        sortedPlayers.Sort((a, b) => b.score.Value.CompareTo(a.score.Value));

        
        if (leaderboardContainer != null && leaderboardRowPrefab != null)
        {
            
            foreach (Transform child in leaderboardContainer)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                GameObject row = Instantiate(leaderboardRowPrefab, leaderboardContainer);

                TMP_Text rowText = row.GetComponentInChildren<TMP_Text>();
                Image avatarImage = row.GetComponentInChildren<Image>();

                string playerName = $"Player {sortedPlayers[i].OwnerClientId}";
                if (sortedPlayers[i].IsOwner) playerName += " (YOU)";

                if (rowText != null)
                {
                    rowText.text = $"#{i + 1} | {playerName} - Kills: {sortedPlayers[i].score.Value}";
                }

                if (avatarImage != null)
                {
                    avatarImage.sprite = sortedPlayers[i].GetAvatar();
                }
            }
        }
        
        else if (rankingSummaryText != null)
        {
            rankingSummaryText.text = "<b>FINAL RANKINGS</b>\n\n";

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                string playerName = $"Player {sortedPlayers[i].OwnerClientId}";
                if (sortedPlayers[i].IsOwner) playerName += " (YOU)";

                rankingSummaryText.text += $"#{i + 1} : {playerName} — <b>{sortedPlayers[i].score.Value} Kills</b>\n";
            }
        }
    }

    
    public void ReturnToMainMenu()
    {
        
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
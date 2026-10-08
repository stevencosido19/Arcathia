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
    public GameObject gameplayHUD; // Drag your Gameplay HUD/Canvas parent panel here

    [Header("End Game Panel UI")]
    public GameObject endGamePanel;
    public Transform leaderboardContainer;
    public GameObject leaderboardRowPrefab; // Entry prefab with Rank, Player Name, Avatar, Kills

    [Header("Navigation Buttons & Settings")]
    public Button mainMenuButton;
    public string mainMenuSceneName = "MainMenu"; // Exact name of your Main Menu scene in Build Settings

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

        // 1. Update live match countdown timer display
        UpdateTimerDisplay(MatchManager.Instance.timeRemaining.Value);

        // 2. Check for game end trigger
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
        // 1. Hide the main gameplay HUD (Spellbook, Attack Buttons, Joystick/Mobile Controls)
        if (gameplayHUD != null)
        {
            gameplayHUD.SetActive(false);
        }

        // 2. Disable local player movement controller on match end
        PlayerHealth[] allPlayers = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (PlayerHealth player in allPlayers)
        {
            if (player.IsOwner)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }
        }

        // 3. Display the End Game Results overlay
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);
        }

        // 4. Gather all connected players with PlayerScore
        PlayerScore[] players = FindObjectsByType<PlayerScore>(FindObjectsSortMode.None);
        List<PlayerScore> sortedPlayers = new List<PlayerScore>(players);

        // 5. Sort descending by kills/score (highest score first)
        sortedPlayers.Sort((a, b) => b.score.Value.CompareTo(a.score.Value));

        // 6. Display rankings using container prefab rows if assigned
        if (leaderboardContainer != null && leaderboardRowPrefab != null)
        {
            // Clear existing elements
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
        // Fallback to single text output block
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

    /// <summary>
    /// Cleanly shuts down Netcode and loads the Main Menu scene.
    /// </summary>
    public void ReturnToMainMenu()
    {
        // 1. Shut down host/client network session if active
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 2. Load Main Menu scene
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
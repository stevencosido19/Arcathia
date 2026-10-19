using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class CampaignVictoryManager : NetworkBehaviour
{
    public static CampaignVictoryManager Instance { get; private set; }

    [Header("Win Condition")]
    [Tooltip("How many enemy kills are required to show the victory panel.")]
    public int targetKillsToWin = 5;

    [Header("Victory UI References")]
    public GameObject victoryPanel;
    public TMP_Text killProgressText;
    public Button nextStageButton;
    public Button mainMenuButton;

    [Header("Scene Navigation")]
    public string nextStageSceneName = "CampaignStage2";
    public string mainMenuSceneName = "MainMenu";

    private bool hasWon = false;
    private PlayerScore boundPlayerScore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    private void Start()
    {
        if (nextStageButton != null) nextStageButton.onClick.AddListener(OnNextStagePressed);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuPressed);

        // Try to bind player score on Start, and in Update if it wasn't found immediately
        FindAndBindPlayerScore();
    }

    private void Update()
    {
        if (boundPlayerScore == null)
        {
            FindAndBindPlayerScore();
        }
    }

    private void FindAndBindPlayerScore()
    {
        // Find the local player's score component.
        // In Campaign (Host mode), there's only one local player.
        PlayerScore player = Object.FindFirstObjectByType<PlayerScore>();
        if (player != null && player.IsOwner) // Ensure we bind to OUR player's score
        {
            boundPlayerScore = player;
            boundPlayerScore.score.OnValueChanged += CheckScoreForVictory;

            UpdateProgressUI(boundPlayerScore.score.Value);
            if (boundPlayerScore.score.Value >= targetKillsToWin)
            {
                TriggerVictory();
            }
        }
    }

    public override void OnDestroy()
    {
        if (boundPlayerScore != null)
        {
            boundPlayerScore.score.OnValueChanged -= CheckScoreForVictory;
        }
        base.OnDestroy();
    }

    private void CheckScoreForVictory(int oldScore, int newScore)
    {
        UpdateProgressUI(newScore);

        if (newScore >= targetKillsToWin && !hasWon)
        {
            TriggerVictory();
        }
    }

    private void UpdateProgressUI(int currentKills)
    {
        if (killProgressText != null)
        {
            killProgressText.text = $"Enemies: {currentKills} / {targetKillsToWin}";
        }
    }

    private void TriggerVictory()
    {
        hasWon = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("<color=green>[Campaign] VICTORY! Required kills reached.</color>");
    }

    private void OnNextStagePressed()
    {
        if (string.IsNullOrEmpty(nextStageSceneName))
        {
            Debug.LogError("[CampaignVictoryManager] Next Stage Scene Name is empty!");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(nextStageSceneName, LoadSceneMode.Single);
        }
    }

    private void OnMainMenuPressed()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
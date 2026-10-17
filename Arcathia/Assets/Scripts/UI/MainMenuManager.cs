using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject modeSelectionPanel;

    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button customizeButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Mode Selection Buttons")]
    public Button campaignButton;
    public Button multiplayerButton;
    public Button backButton;

    [Header("Scene Build Settings Names")]
    public string campaignSceneName = "CampaignStage1";
    public string multiplayerLobbySceneName = "MultiplayerLobby";
    public string shopSceneName = "ShopScene";

    void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(false);

        if (startButton != null) startButton.onClick.AddListener(OnStartButtonPressed);
        if (customizeButton != null) customizeButton.onClick.AddListener(OnCustomizeButtonPressed);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsButtonPressed);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonPressed);

        if (campaignButton != null) campaignButton.onClick.AddListener(OnCampaignButtonPressed);
        if (multiplayerButton != null) multiplayerButton.onClick.AddListener(OnMultiplayerButtonPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonPressed);
    }

    private void OnStartButtonPressed() => modeSelectionPanel.SetActive(true);

    private void OnCustomizeButtonPressed()
    {
        if (!string.IsNullOrEmpty(shopSceneName))
        {
            SceneManager.LoadScene(shopSceneName);
        }
    }

    private void OnSettingsButtonPressed() => Debug.Log("Settings pressed - Feature coming soon.");

    private void OnQuitButtonPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnCampaignButtonPressed()
    {
        if (string.IsNullOrEmpty(campaignSceneName)) return;

        if (NetworkManager.Singleton != null)
        {
            // Bind to local loopback address for single-player campaign
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
            }

            // 1. Start the game silently as Host
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log($"[Campaign] Starting Campaign Mode as Host. Loading: {campaignSceneName}");

                // 2. Load the level via Netcode SceneManager so player and network objects sync
                NetworkManager.Singleton.SceneManager.LoadScene(campaignSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[Campaign] Failed to start local Host for Campaign!");
            }
        }
        else
        {
            Debug.LogError("[MainMenu] NetworkManager was not found! Make sure the NetworkManager prefab is in the MainMenu scene.");
        }
    }

    private void OnMultiplayerButtonPressed()
    {
        if (!string.IsNullOrEmpty(multiplayerLobbySceneName))
        {
            // Make sure Netcode isn't already running when heading to the lobby
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(multiplayerLobbySceneName);
        }
    }

    private void OnBackButtonPressed()
    {
        modeSelectionPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}
using System.Collections;
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
    public GameObject settingsPanel; // Added: Settings Panel reference

    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button customizeButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Mode Selection Buttons")]
    public Button campaignButton;
    public Button multiplayerButton;
    public Button backButton;

    [Header("Settings Buttons")]
    public Button closeSettingsButton; // Added: Back/Close button for Settings panel

    [Header("Transition Settings")]
    [Tooltip("Delay in seconds between clicking a button and the panel appearing.")]
    [Range(0.05f, 0.5f)]
    public float panelTransitionDelay = 0.2f;

    [Header("Scene Build Settings Names")]
    public string campaignSceneName = "CampaignStage1";
    public string multiplayerLobbySceneName = "MultiplayerLobby";
    public string shopSceneName = "ShopScene";

    private bool isTransitioning = false;

    void Start()
    {
        // Initial panel states
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Bind Main Menu Buttons
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonPressed);
        if (customizeButton != null) customizeButton.onClick.AddListener(OnCustomizeButtonPressed);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsButtonPressed);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonPressed);

        // Bind Mode Selection Buttons
        if (campaignButton != null) campaignButton.onClick.AddListener(OnCampaignButtonPressed);
        if (multiplayerButton != null) multiplayerButton.onClick.AddListener(OnMultiplayerButtonPressed);
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonPressed);

        // Bind Settings Panel Buttons
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnCloseSettingsPressed);
    }

    #region Panel Transitions with Delay

    private void OnStartButtonPressed()
    {
        SwitchPanels(mainMenuPanel, modeSelectionPanel);
    }

    private void OnBackButtonPressed()
    {
        SwitchPanels(modeSelectionPanel, mainMenuPanel);
    }

    private void OnSettingsButtonPressed()
    {
        SwitchPanels(mainMenuPanel, settingsPanel);
    }

    private void OnCloseSettingsPressed()
    {
        SwitchPanels(settingsPanel, mainMenuPanel);
    }

    /// <summary>
    /// Smoothly transitions between two panels with a slight customizable delay.
    /// </summary>
    private void SwitchPanels(GameObject panelToHide, GameObject panelToShow)
    {
        if (!isTransitioning)
        {
            StartCoroutine(SwitchPanelsRoutine(panelToHide, panelToShow));
        }
    }

    private IEnumerator SwitchPanelsRoutine(GameObject panelToHide, GameObject panelToShow)
    {
        isTransitioning = true;

        // Use realtime so it still works if game time is paused
        yield return new WaitForSecondsRealtime(panelTransitionDelay);

        if (panelToHide != null) panelToHide.SetActive(false);
        if (panelToShow != null) panelToShow.SetActive(true);

        isTransitioning = false;
    }

    #endregion

    #region Action Buttons

    private void OnCustomizeButtonPressed()
    {
        if (!string.IsNullOrEmpty(shopSceneName))
        {
            SceneManager.LoadScene(shopSceneName);
        }
    }

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
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");
            }

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log($"[Campaign] Starting Campaign Mode as Host. Loading: {campaignSceneName}");
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
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene(multiplayerLobbySceneName);
        }
    }

    #endregion
}
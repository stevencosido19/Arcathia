using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("Your main menu background or persistent left panel with buttons.")]
    public GameObject mainMenuPanel;
    public GameObject modeSelectionPanel;
    public GameObject settingsPanel;

    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button customizeButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Mode Selection Buttons")]
    public Button campaignButton;
    public Button multiplayerButton;
    public Button tutorialButton; // <--- Added Tutorial Button
    public Button backButton;

    [Header("Settings Buttons")]
    public Button closeSettingsButton;

    [Header("Panel Closing Rules")]
    [Tooltip("If TRUE, ONLY panels that have the UIPanelPopUp script will close. " +
             "Persistent panels without UIPanelPopUp (like your leftPanel) will stay open!")]
    public bool onlyClosePanelsWithPopUp = true;

    [Tooltip("Fallback delay used only if a panel without UIPanelPopUp is forced to close.")]
    [Range(0.05f, 0.5f)]
    public float panelTransitionDelay = 0.2f;

    [Header("Scene Build Settings Names")]
    public string campaignSceneName = "CampaignStage1";
    public string multiplayerLobbySceneName = "MultiplayerLobby";
    public string tutorialSceneName = "TutorialScene"; // <--- Added Tutorial Scene Name
    public string shopSceneName = "ShopScene";

    [Header("Local Ports")]
    [Tooltip("Port used for local Tutorial host.")]
    public ushort tutorialPort = 7775;

    private bool isTransitioning = false;

    void Start()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Main Menu
        if (startButton != null) startButton.onClick.AddListener(OnStartButtonPressed);
        if (customizeButton != null) customizeButton.onClick.AddListener(OnCustomizeButtonPressed);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsButtonPressed);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonPressed);

        // Mode Selection
        if (campaignButton != null) campaignButton.onClick.AddListener(OnCampaignButtonPressed);
        if (multiplayerButton != null) multiplayerButton.onClick.AddListener(OnMultiplayerButtonPressed);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(OnTutorialButtonPressed); // <--- Bound Listener
        if (backButton != null) backButton.onClick.AddListener(OnBackButtonPressed);

        // Settings
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnCloseSettingsPressed);
    }

    #region Panel Transitions

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

        if (panelToHide != null && panelToHide.activeInHierarchy)
        {
            UIPanelPopUp popUp = panelToHide.GetComponent<UIPanelPopUp>();

            if (popUp != null)
            {
                popUp.ClosePanel();
                yield return new WaitForSecondsRealtime(popUp.closeDuration);
            }
            else if (!onlyClosePanelsWithPopUp)
            {
                panelToHide.SetActive(false);
                yield return new WaitForSecondsRealtime(panelTransitionDelay);
            }
        }

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }

        isTransitioning = false;
    }

    #endregion

    #region Action Buttons & Scene Transitions

    private void OnCustomizeButtonPressed()
    {
        if (!string.IsNullOrEmpty(shopSceneName))
        {
            LoadingScreen.Load(shopSceneName);
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

        StartLocalSession(campaignSceneName, 7776, "Campaign");
    }

    private void OnTutorialButtonPressed()
    {
        if (string.IsNullOrEmpty(tutorialSceneName)) return;

        StartLocalSession(tutorialSceneName, tutorialPort, "Tutorial");
    }

    /// <summary>
    /// Starts a local single-player host (127.0.0.1) so Netcode spawns the player prefab with full authority.
    /// </summary>
    private void StartLocalSession(string sceneName, ushort port, string sessionTag)
    {
        if (NetworkManager.Singleton != null)
        {
            // Shut down any stale session before starting anew
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                // Local loopback only
                transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            }

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log($"[{sessionTag}] Starting as Local Host. Loading: {sceneName}");
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError($"[{sessionTag}] Failed to start local Host for {sceneName}!");
            }
        }
        else
        {
            Debug.LogError("[MainMenu] NetworkManager was not found! Make sure the NetworkManager prefab is present.");
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

            LoadingScreen.Load(multiplayerLobbySceneName);
        }
    }

    #endregion
}
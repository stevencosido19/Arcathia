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

            // If panel has UIPanelPopUp, play its exit animation and wait
            if (popUp != null)
            {
                popUp.ClosePanel();
                yield return new WaitForSecondsRealtime(popUp.closeDuration);
            }
            // If panel does NOT have UIPanelPopUp, only close it if allowed
            else if (!onlyClosePanelsWithPopUp)
            {
                panelToHide.SetActive(false);
                yield return new WaitForSecondsRealtime(panelTransitionDelay);
            }
        }

        // Reveal the target panel
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

        if (NetworkManager.Singleton != null)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                // Port 7776: Dedicated port for Campaign to prevent conflicts with Multiplayer (7778)
                transport.SetConnectionData("127.0.0.1", 7776, "127.0.0.1");
            }

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log($"[Campaign] Starting Campaign Mode as Host. Loading: {campaignSceneName}");
                // Netcode handles this scene load; LoadingScreen hooks it automatically
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

            // LOADING SCREEN: MainMenu -> MultiplayerLobby
            LoadingScreen.Load(multiplayerLobbySceneName);
        }
    }

    #endregion
}
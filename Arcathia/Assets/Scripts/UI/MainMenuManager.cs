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
    public string shopSceneName = "ShopScene"; // ADDED FOR COSMETICS

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
        // Loads the Shop Scene
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
        if (!string.IsNullOrEmpty(campaignSceneName)) SceneManager.LoadScene(campaignSceneName);
    }

    private void OnMultiplayerButtonPressed()
    {
        if (!string.IsNullOrEmpty(multiplayerLobbySceneName)) SceneManager.LoadScene(multiplayerLobbySceneName);
    }

    private void OnBackButtonPressed()
    {
        modeSelectionPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}
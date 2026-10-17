using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MultiplayerLobbyManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject connectionPanel;
    public GameObject roomPanel;

    [Header("Connection UI Elements")]
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField ipInputField;
    public TMP_Text statusText;
    public Button backToMainMenuButton;

    [Header("Room UI Elements")]
    public TMP_Text hostIpDisplayText;
    public TMP_Text playerListText;
    public Button startMatchButton;
    public Button leaveRoomButton;

    [Header("Network Settings")]
    public string mainGameStageScene = "GameStage1";
    public ushort defaultPort = 7777;

    private UnityTransport transport;

    void Start()
    {
        // If returning to the lobby from a game, shut down cleanly so the player starts fresh
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        ShowConnectionPanel();

        // Bind Connection Buttons
        if (hostButton != null) hostButton.onClick.AddListener(OnHostButtonPressed);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinButtonPressed);
        if (backToMainMenuButton != null) backToMainMenuButton.onClick.AddListener(OnBackToMainMenuPressed);

        // Bind Room Buttons
        if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchPressed);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoomPressed);

        // Pre-fill local IP address
        if (ipInputField != null && string.IsNullOrEmpty(ipInputField.text))
        {
            ipInputField.text = GetLocalIPAddress();
        }
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            // Remove first to avoid duplicate bindings
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    #region WLAN Connection Actions

    private void OnHostButtonPressed()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[MultiplayerLobby] NetworkManager.Singleton is missing!");
            if (statusText != null) statusText.text = "Error: NetworkManager missing!";
            return;
        }

        string localIP = GetLocalIPAddress();
        SetConnectionAddress(localIP);

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log($"[WLAN] Host started at IP: {localIP}:{defaultPort}");
            if (hostIpDisplayText != null)
            {
                hostIpDisplayText.text = $"<b>HOST IP:</b> {localIP}\n<size=80%>(Share this IP with other mobile players)</size>";
            }
            ShowRoomPanel();
            UpdatePlayerListUI();
        }
        else
        {
            if (statusText != null) statusText.text = "Failed to start Host!";
        }
    }

    private void OnJoinButtonPressed()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[MultiplayerLobby] NetworkManager.Singleton is missing!");
            if (statusText != null) statusText.text = "Error: NetworkManager missing!";
            return;
        }

        string targetIP = ipInputField != null ? ipInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(targetIP))
        {
            if (statusText != null) statusText.text = "Please enter the Host's IP address.";
            return;
        }

        SetConnectionAddress(targetIP);

        if (statusText != null) statusText.text = $"Connecting to {targetIP}...";

        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log($"[WLAN] Client joining Host at {targetIP}:{defaultPort}");
            if (hostIpDisplayText != null)
            {
                hostIpDisplayText.text = $"<b>CONNECTED TO:</b> {targetIP}";
            }
            ShowRoomPanel();
            UpdatePlayerListUI();
        }
        else
        {
            if (statusText != null) statusText.text = "Failed to connect to Host.";
        }
    }

    private void SetConnectionAddress(string ipAddress)
    {
        if (NetworkManager.Singleton != null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ipAddress, defaultPort, "0.0.0.0");
            }
            else
            {
                Debug.LogError("[MultiplayerLobby] UnityTransport missing from NetworkManager!");
            }
        }
    }

    private string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }

    private void OnBackToMainMenuPressed()
    {
        // Cleanly shut down any active host/client before going back
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene("MainMenu");
    }

    #endregion

    #region Room Actions

    private void ShowConnectionPanel()
    {
        if (connectionPanel != null) connectionPanel.SetActive(true);
        if (roomPanel != null) roomPanel.SetActive(false);
    }

    private void ShowRoomPanel()
    {
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(true);

        if (startMatchButton != null && NetworkManager.Singleton != null)
        {
            startMatchButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        UpdatePlayerListUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        UpdatePlayerListUI();

        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowConnectionPanel();
            if (statusText != null) statusText.text = "Disconnected from room.";
        }
    }

    private void UpdatePlayerListUI()
    {
        if (playerListText == null || NetworkManager.Singleton == null) return;

        playerListText.text = "<b>CONNECTED PLAYERS:</b>\n";
        IReadOnlyList<NetworkClient> connectedClients = NetworkManager.Singleton.ConnectedClientsList;

        foreach (var client in connectedClients)
        {
            string role = (client.ClientId == NetworkManager.ServerClientId) ? " (Host)" : " (Player)";
            playerListText.text += $"• Player ID {client.ClientId}{role}\n";
        }
    }

    private void OnStartMatchPressed()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(mainGameStageScene, LoadSceneMode.Single);
        }
    }

    private void OnLeaveRoomPressed()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        ShowConnectionPanel();
    }

    #endregion
}
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    [Tooltip("Multiplayer port. Default is 7778 to avoid conflicts with 7777 and Campaign (7776).")]
    public ushort defaultPort = 7778;

    private UnityTransport transport;

    void Start()
    {
        // Cleanly shut down any existing network session when entering lobby
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

        // Pre-fill local IP address in the box for convenience
        if (ipInputField != null && string.IsNullOrEmpty(ipInputField.text))
        {
            ipInputField.text = GetLocalIPAddress();
        }
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
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

        if (NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (statusText != null) statusText.text = "Starting Host...";

        if (transport == null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }

        if (transport == null)
        {
            Debug.LogError("[MultiplayerLobby] UnityTransport component missing from NetworkManager GameObject!");
            if (statusText != null) statusText.text = "Error: UnityTransport missing!";
            return;
        }

        // Host connects internally via loopback ("127.0.0.1"), server listens on all interfaces ("0.0.0.0")
        transport.SetConnectionData("127.0.0.1", defaultPort, "0.0.0.0");

        if (NetworkManager.Singleton.StartHost())
        {
            string localIP = GetLocalIPAddress();
            Debug.Log($"<color=green>[WLAN SUCCESS] Host started! Other players can join using: {localIP}:{defaultPort}</color>");

            if (hostIpDisplayText != null)
            {
                hostIpDisplayText.text = $"<b>HOST IP:</b> {localIP}\n<size=80%>(Share this IP with other mobile/LAN players)</size>";
            }

            ShowRoomPanel();
            UpdatePlayerListUI();
            if (statusText != null) statusText.text = "";
        }
        else
        {
            Debug.LogError("[MultiplayerLobby] Failed to start Host!");
            if (statusText != null) statusText.text = "Failed to start Host. Check console.";
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

        if (transport == null)
        {
            transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        }

        if (transport != null)
        {
            transport.SetConnectionData(targetIP, defaultPort);
        }
        else
        {
            Debug.LogError("[MultiplayerLobby] UnityTransport missing from NetworkManager!");
            return;
        }

        if (statusText != null) statusText.text = $"Connecting to {targetIP}...";

        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log($"[WLAN] Client connecting to Host at {targetIP}:{defaultPort}");
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

    private string GetLocalIPAddress()
    {
        try
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus == OperationalStatus.Up &&
                    (item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     item.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ipStr = ip.Address.ToString();
                            if (!ipStr.StartsWith("169.254"))
                            {
                                return ipStr;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MultiplayerLobby] Could not parse network interfaces: {ex.Message}");
        }

        return "127.0.0.1";
    }

    private void OnBackToMainMenuPressed()
    {
        Debug.Log("<color=cyan>[Lobby] 'Back to Main Menu' button clicked!</color>");

        // 1. Cleanly shut down active host/client session
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        // 2. Play loading screen when going back to Main Menu
        LoadingScreen.Load("MainMenu");
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
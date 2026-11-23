using System.Collections;
using System.Collections.Generic;
using System.Net;
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
    [Tooltip("Multiplayer port. Default is 7778 to avoid conflicts with 7777 and Campaign (7776). " +
             "Host and joining devices must use the same value (same build).")]
    public ushort defaultPort = 7778;
    [Tooltip("How long a joining device waits for the host before giving up and showing an error.")]
    public float connectTimeoutSeconds = 10f;

    private UnityTransport transport;

    // Blocks double taps that would start two connection attempts at once.
    private bool isBusy;
    // Filled by the disconnect callback while a join attempt is running, shown when the attempt ends.
    private string pendingFailure;

    void Start()
    {
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        ShowConnectionPanel();

        if (hostButton != null) hostButton.onClick.AddListener(OnHostButtonPressed);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinButtonPressed);
        if (backToMainMenuButton != null) backToMainMenuButton.onClick.AddListener(OnBackToMainMenuPressed);

        if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchPressed);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoomPressed);

        // Don't pre-fill the device's OWN address (that is the wrong thing to join).
        // Pre-fill just the network part, e.g. "192.168.1." so the player only types the host's last number.
        if (ipInputField != null && string.IsNullOrEmpty(ipInputField.text))
        {
            string mine = GetPrimaryLocalIP();
            int lastDot = mine.LastIndexOf('.');
            if (mine != "127.0.0.1" && lastDot > 0)
                ipInputField.text = mine.Substring(0, lastDot + 1);
        }
    }

    private void OnEnable()
    {
        SubscribeCallbacks();
    }

    private void OnDisable()
    {
        UnsubscribeCallbacks();
    }

    private void SubscribeCallbacks()
    {
        if (NetworkManager.Singleton == null) return;

        // Unsubscribe first so we never end up subscribed twice.
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnsubscribeCallbacks()
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    #region WLAN Connection Actions

    private void OnHostButtonPressed()
    {
        if (isBusy) return;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[MultiplayerLobby] NetworkManager.Singleton is missing!");
            SetStatus("Error: NetworkManager missing!");
            return;
        }

        StartCoroutine(StartHostRoutine());
    }

    private IEnumerator StartHostRoutine()
    {
        isBusy = true;
        SetStatus("Starting Host...");

        NetworkManager nm = NetworkManager.Singleton;
        yield return ShutdownIfRunning(nm);

        if (nm == null || !TryGetTransport(nm))
        {
            isBusy = false;
            yield break;
        }

        SubscribeCallbacks();

        // Host connects internally via loopback ("127.0.0.1"), server listens on all interfaces ("0.0.0.0")
        transport.SetConnectionData("127.0.0.1", defaultPort, "0.0.0.0");

        if (nm.StartHost())
        {
            List<string> ips = GetLocalIPv4Addresses();
            string primary = ips.Count > 0 ? ips[0] : "127.0.0.1";
            Debug.Log($"<color=green>[WLAN SUCCESS] Host started! Other players can join using: {primary}:{defaultPort}</color>");

            if (hostIpDisplayText != null)
            {
                string text = $"<b>HOST IP:</b> {primary}  (port {defaultPort})\n<size=80%>Share this IP with other players on the same Wi-Fi</size>";
                if (ips.Count > 1)
                    text += $"\n<size=70%>Other addresses: {string.Join(", ", ips.GetRange(1, ips.Count - 1))}</size>";
                if (primary == "127.0.0.1")
                    text += "\n<size=70%>Could not detect this device's Wi-Fi IP. Check it in the phone's Wi-Fi settings.</size>";
                hostIpDisplayText.text = text;
            }

            ShowRoomPanel();
            UpdatePlayerListUI();
            SetStatus("");
        }
        else
        {
            Debug.LogError("[MultiplayerLobby] Failed to start Host! Is the port already in use?");
            SetStatus("Failed to start Host. Is another game using this port?");
        }

        isBusy = false;
    }

    private void OnJoinButtonPressed()
    {
        if (isBusy) return;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[MultiplayerLobby] NetworkManager.Singleton is missing!");
            SetStatus("Error: NetworkManager missing!");
            return;
        }

        string targetIP = ipInputField != null ? ipInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(targetIP))
        {
            SetStatus("Please enter the Host's IP address.");
            return;
        }

        // UnityTransport needs a literal IPv4 address (not a hostname, not an incomplete "192.168.1.").
        if (!IPAddress.TryParse(targetIP, out IPAddress parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            SetStatus("Enter a full IPv4 address, e.g. 192.168.1.23");
            return;
        }

        StartCoroutine(StartClientRoutine(targetIP));
    }

    private IEnumerator StartClientRoutine(string targetIP)
    {
        isBusy = true;
        pendingFailure = null;
        SetStatus($"Connecting to {targetIP}:{defaultPort}...");

        NetworkManager nm = NetworkManager.Singleton;
        yield return ShutdownIfRunning(nm);

        if (nm == null || !TryGetTransport(nm))
        {
            isBusy = false;
            yield break;
        }

        SubscribeCallbacks();

        transport.SetConnectionData(targetIP, defaultPort);
        transport.MaxConnectAttempts = 8; // fail in several seconds instead of the default ~60

        if (!nm.StartClient())
        {
            SetStatus("Failed to start client.");
            isBusy = false;
            yield break;
        }

        Debug.Log($"[WLAN] Client connecting to Host at {targetIP}:{defaultPort}");

        // Only show the room once the host has really accepted us.
        float elapsed = 0f;
        while (nm != null && nm.IsListening && !nm.IsConnectedClient && elapsed < connectTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (nm != null && nm.IsConnectedClient)
        {
            if (hostIpDisplayText != null)
                hostIpDisplayText.text = $"<b>CONNECTED TO:</b> {targetIP}";

            ShowRoomPanel();
            UpdatePlayerListUI();
            SetStatus("");
        }
        else
        {
            if (nm != null && nm.IsListening) nm.Shutdown();

            SetStatus(!string.IsNullOrEmpty(pendingFailure)
                ? pendingFailure
                : $"Could not reach {targetIP}:{defaultPort}. Check the IP, that both devices are on the same Wi-Fi, and that the host is running the same build.");
            ShowConnectionPanel();
        }

        isBusy = false;
    }

    /// <summary>
    /// Stops a running NetworkManager and waits until it has really finished,
    /// so Start Host / Start Client right afterwards doesn't fail silently.
    /// </summary>
    private static IEnumerator ShutdownIfRunning(NetworkManager nm)
    {
        if (nm == null) yield break;

        if (nm.IsListening || nm.ShutdownInProgress)
        {
            nm.Shutdown();
            yield return new WaitUntil(() => nm == null || (!nm.IsListening && !nm.ShutdownInProgress));
        }
    }

    private bool TryGetTransport(NetworkManager nm)
    {
        if (transport == null)
            transport = nm.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("[MultiplayerLobby] UnityTransport component missing from NetworkManager GameObject!");
            SetStatus("Error: UnityTransport missing!");
            return false;
        }

        return true;
    }

    // ---- Local address detection ----

    private static string GetPrimaryLocalIP()
    {
        List<string> ips = GetLocalIPv4Addresses();
        return ips.Count > 0 ? ips[0] : "127.0.0.1";
    }

    /// <summary>
    /// Returns this device's LAN IPv4 addresses, best guess first (Wi-Fi / Ethernet before anything else).
    /// Phones often also have a mobile-data address, so the first entry is not guaranteed on every device:
    /// the host screen lists the extras too.
    /// </summary>
    private static List<string> GetLocalIPv4Addresses()
    {
        var preferred = new List<string>();
        var others = new List<string>();

        try
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus != OperationalStatus.Up) continue;
                if (item.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                bool isWifiOrEthernet = item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                        item.NetworkInterfaceType == NetworkInterfaceType.Ethernet;

                foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (!IsPrivateLanAddress(ip.Address)) continue;

                    string s = ip.Address.ToString();
                    List<string> target = isWifiOrEthernet ? preferred : others;
                    if (!target.Contains(s)) target.Add(s);
                }
            }
        }
        catch (System.Exception ex)
        {
            // Some Android versions block listing network interfaces. Fall through to the other methods.
            Debug.LogWarning($"[MultiplayerLobby] Could not parse network interfaces: {ex.Message}");
        }

        var result = new List<string>(preferred);
        foreach (string s in others)
            if (!result.Contains(s)) result.Add(s);

        if (result.Count > 0) return result;

        // Fallback 1: ask the OS which address it would use for outgoing traffic (no packet is sent).
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint && !IPAddress.IsLoopback(endPoint.Address))
                    result.Add(endPoint.Address.ToString());
            }
        }
        catch { /* no route */ }

        if (result.Count > 0) return result;

        // Fallback 2: DNS lookup of this host name.
        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && IsPrivateLanAddress(ip))
                    result.Add(ip.ToString());
            }
        }
        catch { }

        return result;
    }

    private static bool IsPrivateLanAddress(IPAddress address)
    {
        byte[] b = address.GetAddressBytes();
        if (b.Length != 4) return false;

        if (b[0] == 10) return true;                          // 10.0.0.0/8
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16.0.0/12 (includes iPhone hotspot 172.20.10.x)
        if (b[0] == 192 && b[1] == 168) return true;          // 192.168.0.0/16
        return false;                                         // skips 127.x, 169.254.x (no real network) and public/carrier addresses
    }

    private void OnBackToMainMenuPressed()
    {
        Debug.Log("<color=cyan>[Lobby] 'Back to Main Menu' button clicked!</color>");

        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

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

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || clientId != nm.LocalClientId || nm.IsServer) return;

        // We (a joining device) lost or never got the connection.
        string reason = nm.DisconnectReason;
        pendingFailure = string.IsNullOrEmpty(reason)
            ? "Could not reach the host."
            : $"Disconnected: {reason}";

        // While a join attempt is running, the join routine shows this when it ends.
        if (!isBusy)
        {
            ShowConnectionPanel();
            SetStatus(string.IsNullOrEmpty(reason) ? "Disconnected from room." : $"Disconnected: {reason}");
        }
    }

    private void UpdatePlayerListUI()
    {
        if (playerListText == null || NetworkManager.Singleton == null) return;

        NetworkManager nm = NetworkManager.Singleton;

        // ConnectedClientsList only exists on the server/host. Reading it on a joining device logs errors.
        if (!nm.IsServer)
        {
            playerListText.text = nm.IsConnectedClient
                ? "<b>CONNECTED TO HOST</b>\nWaiting for the host to start the match..."
                : "";
            return;
        }

        playerListText.text = "<b>CONNECTED PLAYERS:</b>\n";
        IReadOnlyList<NetworkClient> connectedClients = nm.ConnectedClientsList;

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
        SetStatus("");
    }

    #endregion

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        if (!string.IsNullOrEmpty(message)) Debug.Log($"[MultiplayerLobby] {message}");
    }
}
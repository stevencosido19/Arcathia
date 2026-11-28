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
    public Button cancelJoinButton;
    public TMP_InputField ipInputField;
    public TMP_Text statusText;
    public Button backToMainMenuButton;

    [Header("Room UI Elements")]
    public TMP_Text hostIpDisplayText;
    public Button copyIpButton;
    public TMP_Text playerListText;
    public Button startMatchButton;
    public Button leaveRoomButton;

    [Header("Network Settings")]
    public string mainGameStageScene = "GameStage1";
    [Tooltip("Multiplayer port. Default is 7778 to avoid conflicts with default 7777 and Campaign (7776).")]
    public ushort defaultPort = 7778;
    [Tooltip("How long a joining device waits for the host before giving up.")]
    public float connectTimeoutSeconds = 8f;

    private UnityTransport transport;
    private bool isBusy;
    private bool cancelRequested;
    private string pendingFailure;
    private string detectedPrimaryIp = "127.0.0.1";

    private void Awake()
    {
        // Prevent mobile phones from dimming or sleeping while hosting/joining
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            NetworkManager.Singleton.Shutdown();
        }

        ShowConnectionPanel();
        SetConnectingUIState(false);

        if (hostButton != null) hostButton.onClick.AddListener(OnHostButtonPressed);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinButtonPressed);
        if (cancelJoinButton != null) cancelJoinButton.onClick.AddListener(OnCancelJoinPressed);
        if (backToMainMenuButton != null) backToMainMenuButton.onClick.AddListener(OnBackToMainMenuPressed);

        if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchPressed);
        if (leaveRoomButton != null) leaveRoomButton.onClick.AddListener(OnLeaveRoomPressed);
        if (copyIpButton != null) copyIpButton.onClick.AddListener(CopyHostIpToClipboard);

        // Pre-fill local subnet (e.g. "192.168.1.") so mobile users only type the last number
        if (ipInputField != null && string.IsNullOrEmpty(ipInputField.text))
        {
            string mine = GetPrimaryLocalIP();
            int lastDot = mine.LastIndexOf('.');
            if (mine != "127.0.0.1" && lastDot > 0)
            {
                ipInputField.text = mine.Substring(0, lastDot + 1);
            }
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

    private void OnDestroy()
    {
        if (hostButton != null) hostButton.onClick.RemoveListener(OnHostButtonPressed);
        if (joinButton != null) joinButton.onClick.RemoveListener(OnJoinButtonPressed);
        if (cancelJoinButton != null) cancelJoinButton.onClick.RemoveListener(OnCancelJoinPressed);
        if (backToMainMenuButton != null) backToMainMenuButton.onClick.RemoveListener(OnBackToMainMenuPressed);

        if (startMatchButton != null) startMatchButton.onClick.RemoveListener(OnStartMatchPressed);
        if (leaveRoomButton != null) leaveRoomButton.onClick.RemoveListener(OnLeaveRoomPressed);
        if (copyIpButton != null) copyIpButton.onClick.RemoveListener(CopyHostIpToClipboard);
    }

    private void SubscribeCallbacks()
    {
        if (NetworkManager.Singleton == null) return;

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
        SetConnectingUIState(true);
        SetStatus("Starting Host...");

        NetworkManager nm = NetworkManager.Singleton;
        yield return ShutdownIfRunning(nm);

        if (nm == null || !TryGetTransport(nm))
        {
            SetConnectingUIState(false);
            yield break;
        }

        SubscribeCallbacks();

        transport.SetConnectionData("127.0.0.1", defaultPort, "0.0.0.0");

        if (nm.StartHost())
        {
            List<string> ips = GetLocalIPv4Addresses();
            detectedPrimaryIp = ips.Count > 0 ? ips[0] : "127.0.0.1";
            Debug.Log($"<color=green>[WLAN SUCCESS] Host started on: {detectedPrimaryIp}:{defaultPort}</color>");

            if (hostIpDisplayText != null)
            {
                string text = $"<b>HOST IP:</b> {detectedPrimaryIp}  (port {defaultPort})\n<size=80%>Share this IP with other players on the same Wi-Fi</size>";
                if (ips.Count > 1)
                {
                    text += $"\n<size=70%>Other interfaces: {string.Join(", ", ips.GetRange(1, ips.Count - 1))}</size>";
                }
                if (detectedPrimaryIp == "127.0.0.1")
                {
                    text += "\n<size=70%>Could not detect Wi-Fi IP. Check device Wi-Fi/Hotspot settings.</size>";
                }
                hostIpDisplayText.text = text;
            }

            ShowRoomPanel();
            UpdatePlayerListUI();
            SetStatus("");
        }
        else
        {
            Debug.LogError("[MultiplayerLobby] Failed to start Host! Is the port already in use?");
            SetStatus("Failed to start Host. Port may be occupied.");
        }

        SetConnectingUIState(false);
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

        string rawInput = ipInputField != null ? ipInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(rawInput))
        {
            SetStatus("Please enter the Host's IP address.");
            return;
        }

        string targetIP = rawInput;
        ushort targetPort = defaultPort;

        if (rawInput.Contains(":"))
        {
            string[] parts = rawInput.Split(':');
            targetIP = parts[0].Trim();
            if (parts.Length > 1 && ushort.TryParse(parts[1].Trim(), out ushort parsedPort))
            {
                targetPort = parsedPort;
            }
        }

        if (!IPAddress.TryParse(targetIP, out IPAddress parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            SetStatus("Invalid IP! Enter full IPv4 address (e.g. 192.168.1.25)");
            return;
        }

        StartCoroutine(StartClientRoutine(targetIP, targetPort));
    }

    private void OnCancelJoinPressed()
    {
        if (isBusy)
        {
            cancelRequested = true;
            SetStatus("Cancelling connection...");
        }
    }

    private IEnumerator StartClientRoutine(string targetIP, ushort targetPort)
    {
        SetConnectingUIState(true);
        cancelRequested = false;
        pendingFailure = null;
        SetStatus($"Connecting to {targetIP}:{targetPort}...");

        NetworkManager nm = NetworkManager.Singleton;
        yield return ShutdownIfRunning(nm);

        if (nm == null || !TryGetTransport(nm))
        {
            SetConnectingUIState(false);
            yield break;
        }

        SubscribeCallbacks();

        transport.SetConnectionData(targetIP, targetPort);
        transport.MaxConnectAttempts = 6;
        transport.ConnectTimeoutMS = 1000;

        if (!nm.StartClient())
        {
            SetStatus("Failed to start client transport.");
            SetConnectingUIState(false);
            yield break;
        }

        float elapsed = 0f;
        while (nm != null && nm.IsListening && !nm.IsConnectedClient && elapsed < connectTimeoutSeconds)
        {
            if (cancelRequested) break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (nm != null && nm.IsConnectedClient && !cancelRequested)
        {
            if (hostIpDisplayText != null)
                hostIpDisplayText.text = $"<b>CONNECTED TO:</b> {targetIP}:{targetPort}";

            ShowRoomPanel();
            UpdatePlayerListUI();
            SetStatus("");
        }
        else
        {
            if (nm != null && nm.IsListening) nm.Shutdown();

            if (cancelRequested)
            {
                SetStatus("Connection cancelled.");
            }
            else
            {
                SetStatus(!string.IsNullOrEmpty(pendingFailure)
                    ? pendingFailure
                    : $"Could not reach {targetIP}:{targetPort}.\nCheck Wi-Fi, Hotspot, and Firewall.");
            }
            ShowConnectionPanel();
        }

        SetConnectingUIState(false);
    }

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

    private void SetConnectingUIState(bool busy)
    {
        isBusy = busy;
        if (hostButton != null) hostButton.interactable = !busy;
        if (joinButton != null) joinButton.interactable = !busy;
        if (ipInputField != null) ipInputField.interactable = !busy;
        if (backToMainMenuButton != null) backToMainMenuButton.interactable = !busy;

        if (cancelJoinButton != null) cancelJoinButton.gameObject.SetActive(busy);
    }

    #endregion

    #region IP Detection & Utilities

    public void CopyHostIpToClipboard()
    {
        if (!string.IsNullOrEmpty(detectedPrimaryIp) && detectedPrimaryIp != "127.0.0.1")
        {
            GUIUtility.systemCopyBuffer = detectedPrimaryIp;
            SetStatus("Host IP copied to clipboard!");
        }
    }

    private static string GetPrimaryLocalIP()
    {
        List<string> ips = GetLocalIPv4Addresses();
        return ips.Count > 0 ? ips[0] : "127.0.0.1";
    }

    private static List<string> GetLocalIPv4Addresses()
    {
        var preferred = new List<string>();
        var others = new List<string>();

#if UNITY_ANDROID && !UNITY_EDITOR
        // Use Java's NetworkInterface on Android because .NET's GetAllNetworkInterfaces fails/returns Unknown on Android 11+
        AddAndroidJavaIPs(preferred, others);
#else
        try
        {
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus != OperationalStatus.Up) continue;
                if (item.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                string desc = (item.Name + " " + item.Description).ToLower();
                if (desc.Contains("virtual") || desc.Contains("vmware") || desc.Contains("vbox") ||
                    desc.Contains("vethernet") || desc.Contains("wsl") || desc.Contains("hamachi") ||
                    desc.Contains("zerotier") || desc.Contains("tun") || desc.Contains("tap"))
                    continue;

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
            Debug.LogWarning($"[MultiplayerLobby] NetworkInterface query failed: {ex.Message}");
        }
#endif

        var result = new List<string>(preferred);
        foreach (string s in others)
        {
            if (!result.Contains(s)) result.Add(s);
        }

        if (result.Count > 0) return result;

        // Fallback 1: Outgoing socket route (skip if it's a cellular 10.x address when Wi-Fi failed)
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint && !IPAddress.IsLoopback(endPoint.Address))
                {
                    if (IsPrivateLanAddress(endPoint.Address))
                        result.Add(endPoint.Address.ToString());
                }
            }
        }
        catch { }

        if (result.Count > 0) return result;

        // Fallback 2: DNS lookup
        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && IsPrivateLanAddress(ip))
                    result.Add(ip.ToString());
            }
        }
        catch { }

        // Fallback 3: Standard Mobile Hotspot IP Fallback
        if (result.Count == 0 || (result.Count == 1 && result[0] == "127.0.0.1"))
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            result.Add("192.168.43.1");
#elif UNITY_IOS && !UNITY_EDITOR
            result.Add("172.20.10.1");
#endif
        }

        return result;
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static void AddAndroidJavaIPs(List<string> preferred, List<string> others)
    {
        try
        {
            using (AndroidJavaClass netInterfaceClass = new AndroidJavaClass("java.net.NetworkInterface"))
            using (AndroidJavaClass collectionsClass = new AndroidJavaClass("java.util.Collections"))
            using (AndroidJavaObject enumIfaces = netInterfaceClass.CallStatic<AndroidJavaObject>("getNetworkInterfaces"))
            {
                if (enumIfaces == null) return;

                using (AndroidJavaObject listIfaces = collectionsClass.CallStatic<AndroidJavaObject>("list", enumIfaces))
                {
                    int count = listIfaces.Call<int>("size");
                    for (int i = 0; i < count; i++)
                    {
                        using (AndroidJavaObject iface = listIfaces.Call<AndroidJavaObject>("get", i))
                        {
                            if (iface == null) continue;
                            if (iface.Call<bool>("isLoopback")) continue;

                            string name = (iface.Call<string>("getName") ?? "").ToLower();

                            // Skip cellular mobile data (rmnet/ccmni/pdp) and VPN tunnels (tun/ppp)
                            if (name.Contains("rmnet") || name.Contains("ccmni") || name.Contains("pdp") ||
                                name.Contains("tun") || name.Contains("ppp") || name.Contains("dummy"))
                                continue;

                            bool isWifiOrHotspot = name.StartsWith("wlan") || name.StartsWith("ap") ||
                                                   name.Contains("swlan") || name.Contains("softap") ||
                                                   name.StartsWith("eth");

                            using (AndroidJavaObject enumAddrs = iface.Call<AndroidJavaObject>("getInetAddresses"))
                            using (AndroidJavaObject listAddrs = collectionsClass.CallStatic<AndroidJavaObject>("list", enumAddrs))
                            {
                                int addrCount = listAddrs.Call<int>("size");
                                for (int j = 0; j < addrCount; j++)
                                {
                                    using (AndroidJavaObject addr = listAddrs.Call<AndroidJavaObject>("get", j))
                                    {
                                        if (addr == null) continue;
                                        if (addr.Call<bool>("isLoopbackAddress")) continue;

                                        string hostAddr = addr.Call<string>("getHostAddress");
                                        if (string.IsNullOrEmpty(hostAddr) || hostAddr.Contains(":")) continue;

                                        if (IPAddress.TryParse(hostAddr, out IPAddress parsed) &&
                                            parsed.AddressFamily == AddressFamily.InterNetwork &&
                                            IsPrivateLanAddress(parsed))
                                        {
                                            // Prioritize Wi-Fi/Hotspot (192.168.x.x) first
                                            if (isWifiOrHotspot)
                                            {
                                                if (hostAddr.StartsWith("192.168."))
                                                    preferred.Insert(0, hostAddr);
                                                else if (!preferred.Contains(hostAddr))
                                                    preferred.Add(hostAddr);
                                            }
                                            else
                                            {
                                                if (!others.Contains(hostAddr))
                                                    others.Add(hostAddr);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MultiplayerLobby] AndroidJava IP lookup failed: {ex.Message}");
        }
    }
#endif

    private static bool IsPrivateLanAddress(IPAddress address)
    {
        byte[] b = address.GetAddressBytes();
        if (b.Length != 4) return false;

        if (b[0] == 10) return true;                               // 10.0.0.0/8
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16.0.0/12 (iPhone hotspot 172.20.10.x)
        if (b[0] == 192 && b[1] == 168) return true;              // 192.168.0.0/16
        return false;
    }

    private void OnBackToMainMenuPressed()
    {
        Screen.sleepTimeout = SleepTimeout.SystemSetting;

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

        string reason = nm.DisconnectReason;
        pendingFailure = string.IsNullOrEmpty(reason)
            ? "Could not reach the host."
            : $"Disconnected: {reason}";

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
            if (NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(mainGameStageScene, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[MultiplayerLobby] NetworkSceneManager is null. Fallback to regular SceneManager.");
                SceneManager.LoadScene(mainGameStageScene);
            }
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
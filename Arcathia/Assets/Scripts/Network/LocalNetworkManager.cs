using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class LocalNetworkManager : MonoBehaviour
{
    public TMP_InputField ipAddressInputField;
    private UnityTransport transport;

    private void Awake()
    {
        transport = GetComponent<UnityTransport>();
    }

    public void StartHost()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartHost();
        }
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton != null && transport != null)
        {
            if (ipAddressInputField != null && !string.IsNullOrEmpty(ipAddressInputField.text))
            {
                transport.ConnectionData.Address = ipAddressInputField.text.Trim();
            }

            NetworkManager.Singleton.StartClient();
        }
    }
}
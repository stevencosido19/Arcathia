using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [Header("UI Button References")]
    public Button hostButton;
    public Button clientButton;
    public GameObject uiPanel; // Drag the parent Panel/Canvas containing these buttons

    private void Start()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(() =>
            {
                NetworkManager.Singleton.StartHost();
                HideUI();
            });
        }

        if (clientButton != null)
        {
            clientButton.onClick.AddListener(() =>
            {
                NetworkManager.Singleton.StartClient();
                HideUI();
            });
        }
    }

    private void HideUI()
    {
        // Hide buttons once connected so they don't clutter the FPS view
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }
    }
}
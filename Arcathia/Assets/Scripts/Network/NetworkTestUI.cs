using Unity.Netcode;
using UnityEngine;

public class NetworkTestUI : MonoBehaviour
{
    void OnGUI()
    {
        // Prevent NullReferenceException if NetworkManager is missing or initializing
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 200));

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Start Host (Local Host)", GUILayout.Height(40)))
            {
                NetworkManager.Singleton.StartHost();
            }
            if (GUILayout.Button("Start Client (Join)", GUILayout.Height(40)))
            {
                NetworkManager.Singleton.StartClient();
            }
        }

        GUILayout.EndArea();
    }
}
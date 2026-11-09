using UnityEngine;

public class AppBootstrap : MonoBehaviour
{
    [Header("First Scene")]
    [Tooltip("The main menu scene to load immediately after managers initialize.")]
    public string nextSceneName = "MainMenu";

    private void Start()
    {
        // Smoothly loads MainMenu in the background while covered by the loading screen
        LoadingScreen.Load(nextSceneName);
    }
}
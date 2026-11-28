using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class MenuMusicPlayer : MonoBehaviour
{
    public static MenuMusicPlayer Instance { get; private set; }

    [Header("Music Settings")]
    [Tooltip("Drag your Main Menu music clip (.mp3, .wav, .ogg) here.")]
    public AudioClip menuMusicClip;

    [Range(0f, 1f)]
    public float defaultVolume = 0.5f;

    [Header("Keep Playing In These Scenes")]
    [Tooltip("Exact names of scenes (from Build Settings) where this music should keep playing.")]
    public List<string> menuScenes = new List<string> { "MainMenu", "ShopScene", "MultiplayerLobby" };

    private AudioSource audioSource;
    private const string VolumePrefKey = "MenuMusicVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();

        // Only overwrite if menuMusicClip was assigned (so dragging into AudioSource directly works too!)
        if (menuMusicClip != null)
        {
            audioSource.clip = menuMusicClip;
        }

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // Force 2D sound so camera position never affects volume
        audioSource.mute = false;
        audioSource.volume = PlayerPrefs.GetFloat(VolumePrefKey, defaultVolume);
    }

    private void Start()
    {
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            Debug.LogWarning("[MenuMusicPlayer] No AudioListener found in this scene! Add an AudioListener to your Main Camera to hear music.");
        }

        HandleSceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneMusic(scene.name);
    }

    private void HandleSceneMusic(string sceneName)
    {
        if (audioSource == null || audioSource.clip == null)
        {
            Debug.LogWarning("[MenuMusicPlayer] No AudioClip assigned! Drag your song into 'Menu Music Clip'.");
            return;
        }

        if (menuScenes.Contains(sceneName))
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
                Debug.Log($"<color=green>[MenuMusicPlayer] Playing '{audioSource.clip.name}' in scene '{sceneName}' (Volume: {audioSource.volume})</color>");
            }
        }
        else
        {
            Debug.Log($"[MenuMusicPlayer] Stopping music because scene '{sceneName}' is not in Menu Scenes list.");
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    public void SetVolume(float volume)
    {
        float clamped = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = clamped;
        }
        PlayerPrefs.SetFloat(VolumePrefKey, clamped);
        PlayerPrefs.Save();
    }
}
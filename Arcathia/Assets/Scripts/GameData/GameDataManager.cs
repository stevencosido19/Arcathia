using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    [Header("Available Avatars")]
    [Tooltip("Drag all the avatar sprites available in the game here.")]
    public Sprite[] availableAvatars;

    [Header("Default Fallbacks")]
    public string defaultPlayerName = "Hero";

    // Stored in-memory properties
    public string PlayerName { get; private set; }
    public int SelectedAvatarIndex { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedData();
    }

    private void LoadSavedData()
    {
        PlayerName = PlayerPrefs.GetString("Arcathia_PlayerName", defaultPlayerName);
        SelectedAvatarIndex = PlayerPrefs.GetInt("Arcathia_AvatarIndex", 0);

        // Clamp index in case availableAvatars list changed
        if (availableAvatars != null && availableAvatars.Length > 0)
        {
            SelectedAvatarIndex = Mathf.Clamp(SelectedAvatarIndex, 0, availableAvatars.Length - 1);
        }
    }

    public void SetPlayerName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        PlayerName = newName.Trim();
        PlayerPrefs.SetString("Arcathia_PlayerName", PlayerName);
        PlayerPrefs.Save();
        Debug.Log($"[GameData] Player name saved: {PlayerName}");
    }

    public void SetAvatarIndex(int index)
    {
        if (availableAvatars == null || availableAvatars.Length == 0) return;

        SelectedAvatarIndex = Mathf.Clamp(index, 0, availableAvatars.Length - 1);
        PlayerPrefs.SetInt("Arcathia_AvatarIndex", SelectedAvatarIndex);
        PlayerPrefs.Save();
        Debug.Log($"[GameData] Avatar index saved: {SelectedAvatarIndex}");
    }

    public Sprite GetAvatar(int index)
    {
        if (availableAvatars == null || availableAvatars.Length == 0) return null;
        if (index < 0 || index >= availableAvatars.Length) return availableAvatars[0];
        return availableAvatars[index];
    }

    public Sprite GetCurrentPlayerAvatar()
    {
        return GetAvatar(SelectedAvatarIndex);
    }
}
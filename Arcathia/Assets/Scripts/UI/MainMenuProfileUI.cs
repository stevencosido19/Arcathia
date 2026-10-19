using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuProfileUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField nameInputField;
    public Image avatarPreviewImage;
    public Button nextAvatarButton;
    public Button prevAvatarButton;

    private int currentAvatarIndex = 0;

    private void Start()
    {
        // Load initial values from GameDataManager
        if (GameDataManager.Instance != null)
        {
            currentAvatarIndex = GameDataManager.Instance.SelectedAvatarIndex;

            if (nameInputField != null)
            {
                nameInputField.text = GameDataManager.Instance.PlayerName;
                nameInputField.onEndEdit.AddListener(OnNameInputChanged);
            }
        }

        if (nextAvatarButton != null) nextAvatarButton.onClick.AddListener(OnNextAvatarClicked);
        if (prevAvatarButton != null) prevAvatarButton.onClick.AddListener(OnPrevAvatarClicked);

        UpdateAvatarPreview();
    }

    private void OnNameInputChanged(string newName)
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetPlayerName(newName);
        }
    }

    private void OnNextAvatarClicked()
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.availableAvatars.Length == 0) return;

        currentAvatarIndex = (currentAvatarIndex + 1) % GameDataManager.Instance.availableAvatars.Length;
        SaveAndDisplayAvatar();
    }

    private void OnPrevAvatarClicked()
    {
        if (GameDataManager.Instance == null || GameDataManager.Instance.availableAvatars.Length == 0) return;

        currentAvatarIndex--;
        if (currentAvatarIndex < 0) currentAvatarIndex = GameDataManager.Instance.availableAvatars.Length - 1;
        SaveAndDisplayAvatar();
    }

    private void SaveAndDisplayAvatar()
    {
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetAvatarIndex(currentAvatarIndex);
        }
        UpdateAvatarPreview();
    }

    private void UpdateAvatarPreview()
    {
        if (avatarPreviewImage != null && GameDataManager.Instance != null)
        {
            Sprite avatar = GameDataManager.Instance.GetAvatar(currentAvatarIndex);
            if (avatar != null)
            {
                avatarPreviewImage.sprite = avatar;
                avatarPreviewImage.enabled = true;
            }
        }
    }
}
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public struct ButtonPositionData
{
    public string buttonId;
    public Vector2 anchoredPosition;
}

[System.Serializable]
public class HUDLayoutSaveData
{
    public List<ButtonPositionData> buttons = new List<ButtonPositionData>();
}

public class HUDLayoutManager : MonoBehaviour
{
    public static HUDLayoutManager Instance;

    /// <summary>
    /// True while the player is rearranging the HUD. Any gameplay input script (jump, fire,
    /// spellbook gestures, joystick, camera look...) should ignore input while this is true:
    ///     if (HUDLayoutManager.IsLayoutEditing) return;
    /// </summary>
    public static bool IsLayoutEditing => Instance != null && Instance.IsEditing;

    [Header("UI References")]
    public GameObject customizationPanel;
    public List<CustomizableButton> movableButtons;

    [Header("Edit Mode Lock")]
    [Tooltip("CanvasGroup that wraps the gameplay HUD (fire, jump, rune dial...). Its 'Interactable' is switched off " +
             "while editing so Button clicks can't fire, but dragging still works. It must NOT contain the " +
             "customization panel, unless the panel has its own CanvasGroup with 'Ignore Parent Groups' ticked.")]
    public CanvasGroup gameplayHUDGroup;

    [HideInInspector]
    public bool IsEditing = false;

    private string savePath;

    // Default positions, captured BEFORE any saved layout is applied.
    // Reset uses these instead of reloading the scene.
    private readonly Dictionary<string, Vector2> defaultPositions = new Dictionary<string, Vector2>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        savePath = Path.Combine(Application.persistentDataPath, "hud_layout.json");
    }

    private void Start()
    {
        CaptureDefaultPositions(); // must happen before LoadLayout
        LoadLayout();
        if (customizationPanel != null)
        {
            customizationPanel.SetActive(false);
        }
        ApplyEditLock();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        // Never leave the gameplay HUD locked
        if (gameplayHUDGroup != null) gameplayHUDGroup.interactable = true;
        Instance = null;
    }

    private void CaptureDefaultPositions()
    {
        defaultPositions.Clear();
        foreach (var btn in movableButtons)
        {
            if (btn == null) continue;

            RectTransform rt = btn.GetComponent<RectTransform>();
            defaultPositions[btn.buttonId] = rt.anchoredPosition;
        }
    }

    public void ToggleCustomizationPanel()
    {
        IsEditing = !IsEditing;
        if (customizationPanel != null)
        {
            customizationPanel.SetActive(IsEditing);
        }
        ApplyEditLock();
    }

    // Turns Button clicks off while editing (and back on afterwards).
    // CanvasGroup.interactable only affects Buttons/Toggles/Sliders, not drag handlers,
    // so the HUD buttons can still be dragged around.
    private void ApplyEditLock()
    {
        if (gameplayHUDGroup != null)
        {
            gameplayHUDGroup.interactable = !IsEditing;
        }
    }

    public void SaveAndClose()
    {
        SaveLayout();
        ToggleCustomizationPanel();
    }

    public void SaveLayout()
    {
        HUDLayoutSaveData saveData = new HUDLayoutSaveData();
        foreach (var btn in movableButtons)
        {
            if (btn == null) continue;

            RectTransform rt = btn.GetComponent<RectTransform>();
            saveData.buttons.Add(new ButtonPositionData
            {
                buttonId = btn.buttonId,
                anchoredPosition = rt.anchoredPosition
            });
        }

        File.WriteAllText(savePath, JsonUtility.ToJson(saveData, true));
        Debug.Log("[HUDLayout] Layout saved to game data.");
    }

    public void LoadLayout()
    {
        if (!File.Exists(savePath)) return;

        HUDLayoutSaveData saveData;
        try
        {
            saveData = JsonUtility.FromJson<HUDLayoutSaveData>(File.ReadAllText(savePath));
        }
        catch (System.Exception e)
        {
            // A corrupt/unreadable file should never break the HUD, just fall back to defaults.
            Debug.LogWarning($"[HUDLayout] Could not read layout file, using default positions. {e.Message}");
            return;
        }

        if (saveData == null || saveData.buttons == null) return;

        foreach (var data in saveData.buttons)
        {
            var btn = movableButtons.Find(x => x != null && x.buttonId == data.buttonId);
            if (btn != null)
            {
                btn.GetComponent<RectTransform>().anchoredPosition = data.anchoredPosition;
            }
        }
    }

    public void ResetToDefault()
    {
        // 1. Remove the saved layout so the defaults also apply next launch
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("[HUDLayout] Layout file deleted.");
        }

        // 2. Move every button back to where it started. No scene reload, so nothing gets duplicated.
        foreach (var btn in movableButtons)
        {
            if (btn == null) continue;

            if (defaultPositions.TryGetValue(btn.buttonId, out Vector2 defaultPos))
            {
                btn.GetComponent<RectTransform>().anchoredPosition = defaultPos;
            }
        }

        Debug.Log("[HUDLayout] Buttons restored to default positions.");
    }
}
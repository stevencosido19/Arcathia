using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[System.Serializable]
public struct NormalizedButtonData
{
    public string buttonId;
    public float normX; 
    public float normY; 
}

[System.Serializable]
public class HUDLayoutSaveData
{
    public List<NormalizedButtonData> buttons = new List<NormalizedButtonData>();
}

public class HUDLayoutManager : MonoBehaviour
{
    public enum SettingsTab { General, HUD }

    public static HUDLayoutManager Instance;

    
    [HideInInspector]
    public bool IsEditing = false;

    public static bool IsLayoutEditing => Instance != null && Instance.IsEditing;
    public static bool IsSettingsOpen { get; private set; }

    [Header("Panels")]
    [Tooltip("The main Settings container (with the left buttons and right content area).")]
    public GameObject settingsPanel;

    [Tooltip("The right-hand panel for GENERAL settings (Aim Assist).")]
    public GameObject generalPanel;

    [Tooltip("The right-hand panel for HUD customization (Contains the Mini Screen Preview).")]
    public GameObject hudPanel;

    [Header("Left Navigation Buttons")]
    public Button generalTabButton;
    public Button hudTabButton;
    public Button backButton;

    [Header("Optional: In-Game Settings Button")]
    public Button openSettingsButton;

    [Header("HUD Customization Preview References")]
    [Tooltip("Drag all the dummy draggable preview buttons inside your preview box here.")]
    public List<DraggablePreviewButton> previewButtons;

    public Button saveLayoutButton;
    public Button resetLayoutButton;

    [Header("Tab Visual Colors")]
    public bool colorizeActiveTab = true;
    public Color activeTabColor = new Color(0.48f, 0.05f, 0.85f, 1f);   
    public Color inactiveTabColor = new Color(0.25f, 0.02f, 0.45f, 1f); 

    [Header("Options")]
    public SettingsTab defaultTab = SettingsTab.General;
    public bool closeWithEscape = true;

    private string savePath;
    private SettingsTab currentTab = SettingsTab.General;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        savePath = Path.Combine(Application.persistentDataPath, "hud_layout.json");
    }

    private void Start()
    {
        if (Instance != this) return;

        
        if (previewButtons == null || previewButtons.Count == 0)
        {
            previewButtons = new List<DraggablePreviewButton>(GetComponentsInChildren<DraggablePreviewButton>(true));
        }

        WireButtons();
        LoadPreviewLayout();

        
        if (generalPanel != null) generalPanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        IsEditing = false;
        IsSettingsOpen = false;
    }

    private void Update()
    {
        if (Instance != this || !IsSettingsOpen) return;

        if (closeWithEscape && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseSettings();
        }
    }

    private void OnDestroy()
    {
        UnwireButtons();
        if (Instance == this) Instance = null;
    }

    private void WireButtons()
    {
        if (openSettingsButton != null) openSettingsButton.onClick.AddListener(OpenSettings);
        if (generalTabButton != null) generalTabButton.onClick.AddListener(ShowGeneralTab);
        if (hudTabButton != null) hudTabButton.onClick.AddListener(ShowHudTab);
        if (backButton != null) backButton.onClick.AddListener(CloseSettings);

        if (saveLayoutButton != null) saveLayoutButton.onClick.AddListener(SaveLayout);
        if (resetLayoutButton != null) resetLayoutButton.onClick.AddListener(ResetToDefault);
    }

    private void UnwireButtons()
    {
        if (openSettingsButton != null) openSettingsButton.onClick.RemoveListener(OpenSettings);
        if (generalTabButton != null) generalTabButton.onClick.RemoveListener(ShowGeneralTab);
        if (hudTabButton != null) hudTabButton.onClick.RemoveListener(ShowHudTab);
        if (backButton != null) backButton.onClick.RemoveListener(CloseSettings);

        if (saveLayoutButton != null) saveLayoutButton.onClick.RemoveListener(SaveLayout);
        if (resetLayoutButton != null) resetLayoutButton.onClick.RemoveListener(ResetToDefault);
    }

    public void OpenSettings()
    {
        if (settingsPanel == null) return;

        settingsPanel.SetActive(true); 
        IsSettingsOpen = true;
        ShowTab(defaultTab);
    }

    
    public void CloseSettings()
    {
        IsEditing = false;
        IsSettingsOpen = false;

        
        if (settingsPanel != null && settingsPanel.activeInHierarchy)
        {
            UIPanelPopUp popUp = settingsPanel.GetComponent<UIPanelPopUp>();
            if (popUp != null)
            {
                
                popUp.ClosePanel();
            }
            else
            {
                settingsPanel.SetActive(false);
                if (generalPanel != null) generalPanel.SetActive(false);
                if (hudPanel != null) hudPanel.SetActive(false);
            }
        }
        else
        {
            if (generalPanel != null) generalPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(false);
        }
    }

    public void ShowGeneralTab() => ShowTab(SettingsTab.General);
    public void ShowHudTab() => ShowTab(SettingsTab.HUD);

    private void ShowTab(SettingsTab tab)
    {
        currentTab = tab;
        IsEditing = (tab == SettingsTab.HUD);

        if (generalPanel != null) generalPanel.SetActive(tab == SettingsTab.General);
        if (hudPanel != null) hudPanel.SetActive(tab == SettingsTab.HUD);

        RefreshTabColors();
    }

    private void RefreshTabColors()
    {
        if (!colorizeActiveTab) return;

        if (generalTabButton != null && generalTabButton.targetGraphic is Image genImg)
            genImg.color = (currentTab == SettingsTab.General) ? activeTabColor : inactiveTabColor;

        if (hudTabButton != null && hudTabButton.targetGraphic is Image hudImg)
            hudImg.color = (currentTab == SettingsTab.HUD) ? activeTabColor : inactiveTabColor;
    }

    

    public void SaveOnly() => SaveLayout();

    public void SaveLayout()
    {
        
        if (previewButtons == null || previewButtons.Count == 0)
        {
            previewButtons = new List<DraggablePreviewButton>(GetComponentsInChildren<DraggablePreviewButton>(true));
        }

        HUDLayoutSaveData data = new HUDLayoutSaveData();

        foreach (var btn in previewButtons)
        {
            if (btn == null) continue;

            Vector2 norm = btn.GetNormalizedPosition();
            data.buttons.Add(new NormalizedButtonData
            {
                buttonId = btn.buttonId,
                normX = norm.x,
                normY = norm.y
            });
        }

        File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        Debug.Log($"<color=green>[HUDLayout SUCCESS] Saved {data.buttons.Count} buttons to file!</color>");

        if (InGameHUDApplier.Instance != null)
        {
            InGameHUDApplier.Instance.ApplyLayout();
        }
    }

    public void LoadPreviewLayout()
    {
        if (!File.Exists(savePath)) return;

        try
        {
            HUDLayoutSaveData data = JsonUtility.FromJson<HUDLayoutSaveData>(File.ReadAllText(savePath));
            if (data == null || data.buttons == null) return;

            if (previewButtons == null || previewButtons.Count == 0)
            {
                previewButtons = new List<DraggablePreviewButton>(GetComponentsInChildren<DraggablePreviewButton>(true));
            }

            foreach (var item in data.buttons)
            {
                var btn = previewButtons.Find(x => x != null && x.buttonId == item.buttonId);
                if (btn != null)
                {
                    btn.SetFromNormalizedPosition(new Vector2(item.normX, item.normY));
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[HUDLayout] Error reading layout file: {e.Message}");
        }
    }

    public void ResetToDefault()
    {
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("<color=yellow>[HUDLayout] Deleted layout file.</color>");
        }

        foreach (var btn in previewButtons)
        {
            if (btn != null) btn.ResetToDefault();
        }

        Debug.Log("<color=cyan>[HUDLayout] Restored default preview layout.</color>");

        if (InGameHUDApplier.Instance != null)
        {
            InGameHUDApplier.Instance.ResetToDefaultLayout();
        }
    }
}
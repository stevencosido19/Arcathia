using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class InGameHUDApplier : MonoBehaviour
{
    public static InGameHUDApplier Instance { get; private set; }

    [System.Serializable]
    public struct RealHUDButtonBinding
    {
        [Tooltip("Must match the exact Button ID in the preview (e.g. 'Jump', 'Attack', 'Spellbook', 'Runedial'). Case-sensitive!")]
        public string buttonId;
        public RectTransform realButtonTransform;
    }

    [Header("Real In-Game Buttons")]
    public List<RealHUDButtonBinding> buttonBindings = new List<RealHUDButtonBinding>();

    private string savePath;
    private Canvas rootCanvas;

    // Stores default positions captured on Awake so Reset works without reloading scenes!
    private readonly Dictionary<string, Vector3> defaultWorldPositions = new Dictionary<string, Vector3>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        savePath = Path.Combine(Application.persistentDataPath, "hud_layout.json");
        rootCanvas = GetComponentInParent<Canvas>();

        CaptureDefaultPositions();
    }

    private void Start()
    {
        ApplyLayout();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void CaptureDefaultPositions()
    {
        defaultWorldPositions.Clear();
        foreach (var b in buttonBindings)
        {
            if (b.realButtonTransform != null)
            {
                defaultWorldPositions[b.buttonId] = b.realButtonTransform.position;
            }
        }
    }

    /// <summary>
    /// Reads the saved layout and positions the real in-game buttons instantly.
    /// </summary>
    public void ApplyLayout()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("<color=yellow>[InGameHUD] No custom layout file found. Using default scene positions.</color>");
            return;
        }

        if (buttonBindings == null || buttonBindings.Count == 0)
        {
            Debug.LogError("<color=red>[InGameHUD ERROR] 'Button Bindings' list is EMPTY!</color>");
            return;
        }

        try
        {
            string jsonText = File.ReadAllText(savePath);
            HUDLayoutSaveData data = JsonUtility.FromJson<HUDLayoutSaveData>(jsonText);

            if (data == null || data.buttons == null || data.buttons.Count == 0) return;

            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();

            int appliedCount = 0;

            foreach (var item in data.buttons)
            {
                var binding = buttonBindings.Find(x => x.buttonId == item.buttonId && x.realButtonTransform != null);

                if (binding.realButtonTransform != null)
                {
                    RectTransform rt = binding.realButtonTransform;

                    // Bulletproof World-Space Screen Mapping:
                    // Calculates the exact point on the main canvas regardless of what parent panels the button is in!
                    float localX = Mathf.Lerp(canvasRect.rect.xMin, canvasRect.rect.xMax, item.normX);
                    float localY = Mathf.Lerp(canvasRect.rect.yMin, canvasRect.rect.yMax, item.normY);
                    Vector3 targetWorldPoint = canvasRect.TransformPoint(new Vector3(localX, localY, 0f));

                    rt.position = targetWorldPoint;

                    appliedCount++;
                    Debug.Log($"<color=green>[InGameHUD SUCCESS]</color> Moved '{item.buttonId}' to Screen ({item.normX:F2}, {item.normY:F2})");
                }
                else
                {
                    Debug.LogWarning($"<color=orange>[InGameHUD MISMATCH]</color> Saved ID '{item.buttonId}' was not found in InGameHUDApplier Button Bindings!");
                }
            }

            Debug.Log($"<color=green>[InGameHUD FINISHED] Applied custom layout to {appliedCount}/{data.buttons.Count} buttons.</color>");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red>[InGameHUD ERROR]</color> Failed to read layout: {e.Message}");
        }
    }

    /// <summary>
    /// Snaps all real in-game buttons back to their original positions.
    /// </summary>
    public void ResetToDefaultLayout()
    {
        int resetCount = 0;
        foreach (var b in buttonBindings)
        {
            if (b.realButtonTransform != null && defaultWorldPositions.TryGetValue(b.buttonId, out Vector3 originalPos))
            {
                b.realButtonTransform.position = originalPos;
                resetCount++;
            }
        }
        Debug.Log($"<color=cyan>[InGameHUD RESET] Successfully restored {resetCount} real buttons back to default!</color>");
    }
}
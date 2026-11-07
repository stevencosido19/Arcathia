using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class DraggablePreviewButton : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [Header("Identity")]
    [Tooltip("Unique ID matching the real in-game button (e.g. 'Jump', 'Attack', 'Joystick'). Case-sensitive!")]
    public string buttonId = "Jump";

    [HideInInspector]
    public Vector2 defaultAnchoredPos;

    private RectTransform rectTransform;
    private RectTransform previewContainerRect;
    private Canvas parentCanvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        previewContainerRect = transform.parent as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();

        defaultAnchoredPos = rectTransform.anchoredPosition;

        if (previewContainerRect == null)
        {
            Debug.LogError($"<color=red>[DraggablePreviewButton ERROR]</color> '{gameObject.name}' must be parented under a RectTransform container!");
        }
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas == null || previewContainerRect == null) return;

        rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
        ClampInsidePreview();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2 norm = GetNormalizedPosition();
        Debug.Log($"<color=yellow>[Preview Dragged]</color> '{buttonId}' moved to screen percentage: X={norm.x:F2}, Y={norm.y:F2}");
    }

    private void ClampInsidePreview()
    {
        Vector2 pos = rectTransform.anchoredPosition;
        Rect containerRect = previewContainerRect.rect;
        Rect btnRect = rectTransform.rect;

        float minX = containerRect.xMin + (btnRect.width * rectTransform.pivot.x);
        float maxX = containerRect.xMax - (btnRect.width * (1f - rectTransform.pivot.x));
        float minY = containerRect.yMin + (btnRect.height * rectTransform.pivot.y);
        float maxY = containerRect.yMax - (btnRect.height * (1f - rectTransform.pivot.y));

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        rectTransform.anchoredPosition = pos;
    }

    /// <summary>
    /// Accurately converts the button's world position into 0.0 to 1.0 percentage coordinates
    /// regardless of what anchors or pivots are used.
    /// </summary>
    public Vector2 GetNormalizedPosition()
    {
        if (previewContainerRect == null)
            previewContainerRect = transform.parent as RectTransform;

        Vector3 localPos = previewContainerRect.InverseTransformPoint(rectTransform.position);
        Rect rect = previewContainerRect.rect;

        float normX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPos.x);
        float normY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPos.y);

        return new Vector2(Mathf.Clamp01(normX), Mathf.Clamp01(normY));
    }

    public void SetFromNormalizedPosition(Vector2 normPos)
    {
        if (previewContainerRect == null)
            previewContainerRect = transform.parent as RectTransform;

        Rect rect = previewContainerRect.rect;
        float localX = Mathf.Lerp(rect.xMin, rect.xMax, normPos.x);
        float localY = Mathf.Lerp(rect.yMin, rect.yMax, normPos.y);

        Vector3 worldPos = previewContainerRect.TransformPoint(new Vector3(localX, localY, 0f));
        rectTransform.position = worldPos;

        ClampInsidePreview();
    }

    public void ResetToDefault()
    {
        rectTransform.anchoredPosition = defaultAnchoredPos;
    }
}
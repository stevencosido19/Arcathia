using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class CustomizableButton : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    public string buttonId;
    private RectTransform rectTransform;
    private Canvas canvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        bool editingState = HUDLayoutManager.Instance != null && HUDLayoutManager.Instance.IsEditing;
        Debug.Log($"[CustomizableButton] {buttonId} - BeginDrag triggered. IsEditing: {editingState}");

        if (!editingState) return;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        bool editingState = HUDLayoutManager.Instance != null && HUDLayoutManager.Instance.IsEditing;

        if (!editingState)
        {
            
            return;
        }

        if (canvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (HUDLayoutManager.Instance != null && HUDLayoutManager.Instance.IsEditing)
        {
            Debug.Log($"[CustomizableButton] {buttonId} - EndDrag. Saving layout.");
            HUDLayoutManager.Instance.SaveLayout();
        }
    }
}
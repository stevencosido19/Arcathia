using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class UIBookSwiper : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
{
    [Header("Dependencies")]
    public ElementalSpellBook spellbook;

    [Header("Swipe Settings")]
    [Tooltip("Minimum drag distance in pixels required to register a swipe.")]
    public float minSwipeDistance = 30f;

    private Vector2 pointerDownPosition;

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Keeps drag active for IEndDragHandler
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Vector2 pointerUpPosition = eventData.position;
        Vector2 swipeDelta = pointerUpPosition - pointerDownPosition;

        // Ensure horizontal drag is primary
        if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
        {
            if (Mathf.Abs(swipeDelta.x) >= minSwipeDistance)
            {
                if (swipeDelta.x < 0)
                {
                    if (spellbook != null) spellbook.SwitchPage(1);
                }
                else
                {
                    if (spellbook != null) spellbook.SwitchPage(-1);
                }
            }
        }
    }
}
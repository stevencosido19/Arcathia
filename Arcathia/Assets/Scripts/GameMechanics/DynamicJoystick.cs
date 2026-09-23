using UnityEngine;
using UnityEngine.EventSystems;

public class DynamicJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick Parts")]
    public RectTransform joystickContainer; // Outer Ring
    public RectTransform handle;            // Inner Stick Circle

    [Header("Settings")]
    public float movementRange = 100f;      // Max distance handle can move from center

    private Canvas canvas;
    private Vector2 inputVector = Vector2.zero;
    private Vector2 centerPosition;

    public Vector2 Direction => inputVector;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();

        // Hide joystick ring by default until screen is touched
        if (joystickContainer != null)
        {
            joystickContainer.gameObject.SetActive(false);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Convert touch screen position relative to the stationary Canvas
        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out centerPosition
        );

        // Snap the joystick container ring directly under finger touch position
        if (joystickContainer != null)
        {
            joystickContainer.anchoredPosition = centerPosition;
            joystickContainer.gameObject.SetActive(true);
        }

        // Reset handle to center
        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        inputVector = Vector2.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 currentTouchPosition;

        // Convert current touch position relative to parent Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out currentTouchPosition
        );

        // Vector from joystick origin (centerPosition) to current finger location
        Vector2 offset = currentTouchPosition - centerPosition;

        // Normalize input vector within movementRange
        inputVector = Vector2.ClampMagnitude(offset / movementRange, 1.0f);

        // Move handle relative to joystick container
        if (handle != null)
        {
            handle.anchoredPosition = inputVector * movementRange;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        inputVector = Vector2.zero;

        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

        // Hide joystick ring when touch is released
        if (joystickContainer != null)
        {
            joystickContainer.gameObject.SetActive(false);
        }
    }
}
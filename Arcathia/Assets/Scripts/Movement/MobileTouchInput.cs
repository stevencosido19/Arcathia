using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class MobileTouchInput : MonoBehaviour
{
    private int moveTouchId = -1;
    private int lookTouchId = -1;

    private Vector2 moveStartPosition;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    [Header("Touch Movement Sensitivity")]
    [Tooltip("Distance in pixels needed to reach full movement speed.")]
    public float maxDragDistance = 100f;

    private void Update()
    {
        LookInput = Vector2.zero;

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            ResetAllInputs();
            return;
        }

        var touches = touchscreen.touches;
        bool moveTouchFoundThisFrame = false;
        bool lookTouchFoundThisFrame = false;

        for (int i = 0; i < touches.Count; i++)
        {
            TouchControl touch = touches[i];
            if (!touch.isInProgress) continue;

            int fingerId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();
            Vector2 delta = touch.delta.ReadValue();
            var phase = touch.phase.ReadValue();

            // -------------------------------------------------------------
            // 1. Process Active Movement Touch
            // -------------------------------------------------------------
            if (fingerId == moveTouchId)
            {
                moveTouchFoundThisFrame = true;

                if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    Vector2 offset = position - moveStartPosition;
                    MoveInput = Vector2.ClampMagnitude(offset / maxDragDistance, 1f);
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    moveTouchId = -1;
                    MoveInput = Vector2.zero;
                }
                continue;
            }

            // -------------------------------------------------------------
            // 2. Process Active Camera Look Touch
            // -------------------------------------------------------------
            if (fingerId == lookTouchId)
            {
                lookTouchFoundThisFrame = true;

                if (phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    LookInput = delta;
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    lookTouchId = -1;
                }
                continue;
            }

            // -------------------------------------------------------------
            // 3. Register New Touches (Began Phase)
            // -------------------------------------------------------------
            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                // Ignore if touch started over a UI button/element
                if (IsTouchOverUI(fingerId))
                {
                    continue;
                }

                // Left 50% -> Movement
                if (position.x <= Screen.width * 0.5f && moveTouchId == -1)
                {
                    moveTouchId = fingerId;
                    moveStartPosition = position;
                    moveTouchFoundThisFrame = true;
                }
                // Right 50% -> Camera Look
                else if (position.x > Screen.width * 0.5f && lookTouchId == -1)
                {
                    lookTouchId = fingerId;
                    lookTouchFoundThisFrame = true;

                    if (delta != Vector2.zero)
                    {
                        LookInput = delta;
                    }
                }
            }
        }

        // --- SAFETY RECOVERY FIXES ---
        // Force-clear touch IDs if the physical touch list is empty or fingers lifted
        if (touches.Count == 0)
        {
            ResetAllInputs();
        }
        else
        {
            if (moveTouchId != -1 && !moveTouchFoundThisFrame)
            {
                moveTouchId = -1;
                MoveInput = Vector2.zero;
            }

            if (lookTouchId != -1 && !lookTouchFoundThisFrame)
            {
                lookTouchId = -1;
            }
        }
    }

    private bool IsTouchOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        if (Touchscreen.current == null || fingerId >= Touchscreen.current.touches.Count) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Touchscreen.current.touches[fingerId].position.ReadValue()
        };

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }

    private void ResetAllInputs()
    {
        moveTouchId = -1;
        lookTouchId = -1;
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
    }
}
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
        bool moveTouchActive = false;
        bool lookTouchActive = false;

        for (int i = 0; i < touches.Count; i++)
        {
            TouchControl touch = touches[i];
            if (!touch.isInProgress) continue;

            int fingerId = touch.touchId.ReadValue();
            Vector2 position = touch.position.ReadValue();
            Vector2 delta = touch.delta.ReadValue();
            var phase = touch.phase.ReadValue();

            // -------------------------------------------------------------
            // 1. Process Active Left-Side Movement Touch
            // -------------------------------------------------------------
            if (fingerId == moveTouchId)
            {
                if (phase == UnityEngine.InputSystem.TouchPhase.Moved || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    moveTouchActive = true;
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
            // 2. Process Active Right-Side Camera Look Touch
            // -------------------------------------------------------------
            if (fingerId == lookTouchId)
            {
                if (phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    lookTouchActive = true;
                    LookInput = delta;
                }
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    lookTouchId = -1;
                }
                continue;
            }

            // -------------------------------------------------------------
            // 3. Register New Touches
            // -------------------------------------------------------------
            if (phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                // Skip if touch starts on UI buttons (Jump, Skill, Pause, etc.)
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId))
                {
                    continue;
                }

                // Left 50% -> Movement
                if (position.x <= Screen.width * 0.5f && moveTouchId == -1)
                {
                    moveTouchId = fingerId;
                    moveStartPosition = position;
                    moveTouchActive = true;
                }
                // Right 50% -> Camera Look
                else if (position.x > Screen.width * 0.5f && lookTouchId == -1)
                {
                    lookTouchId = fingerId;
                    lookTouchActive = true;
                }
            }
        }

        // Safety Catch: If registered move touch finger disappeared, force zero
        if (moveTouchId != -1 && !moveTouchActive)
        {
            moveTouchId = -1;
            MoveInput = Vector2.zero;
        }

        // Safety Catch: If registered look touch finger disappeared, clear ID
        if (lookTouchId != -1 && !lookTouchActive)
        {
            lookTouchId = -1;
        }
    }

    private void ResetAllInputs()
    {
        moveTouchId = -1;
        lookTouchId = -1;
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
    }
}
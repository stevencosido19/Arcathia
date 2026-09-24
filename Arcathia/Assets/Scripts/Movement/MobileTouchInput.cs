using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.Netcode;

public class MobileTouchInput : MonoBehaviour
{
    [Header("Sensitivity Settings")]
    public float lookSensitivityX = 0.15f;
    public float lookSensitivityY = 0.15f;

    [Header("Joystick Settings")]
    public float joystickDeadzoneDistance = 80f;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpRequested { get; set; }

    private int moveTouchId = -1;
    private int lookTouchId = -1;
    private Vector2 moveTouchStartPos;
    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    void Update()
    {
        // CRITICAL NETWORKING FIX: If this player is NOT the local owner, clear inputs and stop!
        if (playerController != null && !playerController.IsOwner)
        {
            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            return;
        }

        LookInput = Vector2.zero;

        // Reset tracking flags for this frame
        bool moveTouchActive = false;
        bool lookTouchActive = false;

        // Mobile Touch Handling
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touches = Touchscreen.current.touches;

            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl touch = touches[i];
                if (!touch.press.isPressed) continue;

                int fingerId = touch.touchId.ReadValue();
                Vector2 touchPos = touch.position.ReadValue();
                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();

                if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    if (fingerId == moveTouchId) moveTouchId = -1;
                    if (fingerId == lookTouchId) lookTouchId = -1;
                    continue;
                }

                // Left Side: Movement
                if (touchPos.x < Screen.width / 2f)
                {
                    if (moveTouchId == -1)
                    {
                        moveTouchId = fingerId;
                        moveTouchStartPos = touchPos;
                    }

                    if (fingerId == moveTouchId)
                    {
                        moveTouchActive = true;
                        Vector2 delta = touchPos - moveTouchStartPos;
                        MoveInput = Vector2.ClampMagnitude(delta / joystickDeadzoneDistance, 1f);
                    }
                }
                // Right Side: Look / Aim
                else
                {
                    if (lookTouchId == -1)
                    {
                        lookTouchId = fingerId;
                    }

                    if (fingerId == lookTouchId)
                    {
                        lookTouchActive = true;
                        LookInput = touch.delta.ReadValue();
                    }
                }
            }
        }

        // Force reset input if touch ended
        if (!moveTouchActive)
        {
            moveTouchId = -1;
            MoveInput = Vector2.zero;
        }

        if (!lookTouchActive)
        {
            lookTouchId = -1;
        }

        // Editor / Standalone Keyboard Fallback (ONLY for Owner)
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Touchscreen.current == null || moveTouchId == -1)
        {
            Vector2 keyboardInput = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) keyboardInput.y += 1;
                if (Keyboard.current.sKey.isPressed) keyboardInput.y -= 1;
                if (Keyboard.current.dKey.isPressed) keyboardInput.x += 1;
                if (Keyboard.current.aKey.isPressed) keyboardInput.x -= 1;
                if (Keyboard.current.spaceKey.wasPressedThisFrame) JumpRequested = true;

                MoveInput = keyboardInput.normalized;
            }

            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            {
                LookInput = Mouse.current.delta.ReadValue();
            }
        }
#endif
    }
}
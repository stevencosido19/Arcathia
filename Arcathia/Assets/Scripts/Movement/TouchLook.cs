using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.EventSystems; // Required for UI raycast check
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class TouchLook : MonoBehaviour
{
    [Header("Sensitivity Settings")]
    [Tooltip("How fast the camera turns left and right. Adjust this in playmode to fine-tune.")]
    [Range(0.01f, 1.0f)]
    public float sensitivityX = 0.15f;

    [Tooltip("How fast the camera pitches up and down. Adjust this in playmode to fine-tune.")]
    [Range(0.01f, 1.0f)]
    public float sensitivityY = 0.15f;

    [Header("References")]
    public Transform playerBody;

    private float xRotation = 0f;
    private int rightSideFingerId = -1;

    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() => EnhancedTouchSupport.Disable();

    private void Update()
    {
        bool touchHandled = false;

        // 1. TOUCH CONTROL (Mobile Device / Touch Screen)
        foreach (var touch in Touch.activeTouches)
        {
            // Right-side logic (Camera Look Zone)
            if (touch.startScreenPosition.x > Screen.width * 0.5f)
            {
                // Check when touch BEGANS: If the finger tapped on UI (Fire Button, Dial, etc.), ignore this finger completely
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.finger.index))
                    {
                        continue; // Skip tracking this touch for camera movement
                    }

                    rightSideFingerId = touch.finger.index;
                }

                if (touch.finger.index == rightSideFingerId)
                {
                    touchHandled = true;

                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    {
                        Vector2 delta = touch.delta;
                        RotateCamera(delta.x * sensitivityX, delta.y * sensitivityY);
                    }

                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        rightSideFingerId = -1;
                    }
                }
            }
        }

        // 2. MOUSE CONTROL FALLBACK (New Input System for Unity Editor Testing)
        if (!touchHandled && Mouse.current != null)
        {
            if (Mouse.current.leftButton.isPressed && Mouse.current.position.ReadValue().x > Screen.width * 0.5f)
            {
                // Ignore mouse drags if the initial click landed on a UI button
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Vector2 mouseDelta = Mouse.current.delta.ReadValue();

                float mouseDamping = 0.5f;
                float mouseX = mouseDelta.x * sensitivityX * mouseDamping;
                float mouseY = mouseDelta.y * sensitivityY * mouseDamping;

                RotateCamera(mouseX, mouseY);
            }
        }
    }

    private void RotateCamera(float deltaX, float deltaY)
    {
        xRotation -= deltaY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * deltaX);
        }
    }
}
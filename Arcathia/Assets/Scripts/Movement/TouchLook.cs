using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class TouchLook : MonoBehaviour
{
    [Header("Sensitivity")]
    public float sensitivityX = 0.15f;
    public float sensitivityY = 0.15f;
    public Transform playerBody;

    private float xRotation = 0f;
    private int rightSideFingerId = -1;

    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() => EnhancedTouchSupport.Disable();

    private void Update()
    {
        foreach (var touch in Touch.activeTouches)
        {
            // Track touch on the right half of the screen (> 50% width)
            if (touch.startScreenPosition.x > Screen.width * 0.5f)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    rightSideFingerId = touch.finger.index;
                }

                if (touch.finger.index == rightSideFingerId && touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    Vector2 delta = touch.delta;

                    // Calculate look rotation
                    xRotation -= delta.y * sensitivityY;
                    xRotation = Mathf.Clamp(xRotation, -80f, 80f); // Prevents flipping camera

                    transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Vertical Look
                    playerBody.Rotate(Vector3.up * delta.x * sensitivityX);        // Horizontal Turn
                }

                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    if (touch.finger.index == rightSideFingerId) rightSideFingerId = -1;
                }
            }
        }
    }
}
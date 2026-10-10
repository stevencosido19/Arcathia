using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : NetworkBehaviour
{
    [Header("Target & Offset")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, -4f);

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.2f;
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 60f;

    private float currentX = 0f;
    private float currentY = 0f;

    public override void OnNetworkSpawn()
    {
        // If this camera belongs to a remote player, disable its camera and audio listener
        if (!IsOwner)
        {
            var cam = GetComponent<Camera>();
            if (cam != null) cam.enabled = false;

            var listener = GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;

            enabled = false;
            return;
        }

        // Automatically find the local player object if target is unassigned
        if (target == null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var playerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }

            // Fallback to parent root if NetworkManager reference isn't ready
            if (target == null)
            {
                target = transform.root;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector2 inputDelta = Vector2.zero;

        // Support mobile touch dragging using the New Input System
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            var touch = Touchscreen.current.touches[0];
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                inputDelta = touch.delta.ReadValue();
            }
        }
        // Fallback to desktop mouse delta using the New Input System
        else if (Mouse.current != null)
        {
            inputDelta = Mouse.current.delta.ReadValue();
        }

        currentX += inputDelta.x * rotationSpeed;
        currentY -= inputDelta.y * rotationSpeed;

        currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 position = target.position + rotation * offset;

        // Apply world position and rotation directly while staying a child of the prefab
        transform.position = position;
        transform.rotation = rotation;
    }
}
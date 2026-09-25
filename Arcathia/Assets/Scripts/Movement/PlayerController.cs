using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;

    [Header("Jump & Fall Customization")]
    public float jumpSpeed = 8.0f;
    public float baseGravity = -9.81f;
    public float jumpGravityMultiplier = 1.0f;
    public float fallGravityMultiplier = 2.0f;
    public float maxFallSpeed = 25.0f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public float mouseSensitivity = 0.15f;

    private CharacterController controller;
    private MobileTouchInput touchInputHandler;

    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        touchInputHandler = GetComponent<MobileTouchInput>();
    }

    public override void OnNetworkSpawn()
    {
        if (playerCamera != null)
        {
            playerCamera.enabled = IsOwner;
        }

        AudioListener listener = GetComponentInChildren<AudioListener>();
        if (listener != null)
        {
            listener.enabled = IsOwner;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        HandleCameraLook();
    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float moveX = 0f;
        float moveZ = 0f;

        // 1. WASD Keyboard Inputs
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveZ += 1f;
            if (Keyboard.current.sKey.isPressed) moveZ -= 1f;
            if (Keyboard.current.dKey.isPressed) moveX += 1f;
            if (Keyboard.current.aKey.isPressed) moveX -= 1f;

            if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
            {
                TriggerJump();
            }
        }

        // 2. Read Touch Drag Vector from Left Side of Screen
        if (touchInputHandler != null)
        {
            moveX += touchInputHandler.MoveInput.x;
            moveZ += touchInputHandler.MoveInput.y;
        }

        // Clamp total movement input
        Vector2 combinedInput = Vector2.ClampMagnitude(new Vector2(moveX, moveZ), 1f);

        Vector3 move = transform.right * combinedInput.x + transform.forward * combinedInput.y;
        controller.Move(move * moveSpeed * Time.deltaTime);

        // Dynamic Gravity Calculations
        if (velocity.y > 0)
        {
            velocity.y += baseGravity * jumpGravityMultiplier * Time.deltaTime;
        }
        else
        {
            velocity.y += baseGravity * fallGravityMultiplier * Time.deltaTime;
        }

        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleCameraLook()
    {
        if (playerCamera == null) return;

        Vector2 lookDelta = Vector2.zero;

        if (touchInputHandler != null)
        {
            lookDelta = touchInputHandler.LookInput;
        }

        float lookX = lookDelta.x * mouseSensitivity;
        float lookY = lookDelta.y * mouseSensitivity;

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (Mathf.Abs(lookX) > 0.001f)
        {
            transform.Rotate(Vector3.up * lookX);
        }
    }

    public void TriggerJump()
    {
        if (!IsOwner) return;

        if (isGrounded)
        {
            velocity.y = jumpSpeed;
        }
    }
}
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float rotationSpeed = 15.0f; // Smoothness for turning toward movement direction

    [Header("Jump & Fall Customization")]
    public float jumpSpeed = 8.0f;
    public float baseGravity = -9.81f;
    public float jumpGravityMultiplier = 1.0f;
    public float fallGravityMultiplier = 1.2f;
    public float maxFallSpeed = 25.0f;

    private CharacterController controller;
    private MobileTouchInput touchInputHandler;

    private Vector3 velocity;
    private bool isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        touchInputHandler = GetComponent<MobileTouchInput>();
    }

    public override void OnNetworkSpawn()
    {
        AudioListener listener = GetComponentInChildren<AudioListener>();
        if (listener != null)
        {
            listener.enabled = IsOwner;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (controller == null || !controller.enabled) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Ground stick force
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

        Vector2 combinedInput = Vector2.ClampMagnitude(new Vector2(moveX, moveZ), 1f);

        if (combinedInput.sqrMagnitude > 0.001f && Camera.main != null)
        {
            // Calculate movement relative to the camera's horizontal view directions
            Transform camTransform = Camera.main.transform;
            Vector3 camForward = camTransform.forward;
            Vector3 camRight = camTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = (camForward * combinedInput.y + camRight * combinedInput.x).normalized;

            // Move the character
            controller.Move(moveDir * moveSpeed * Time.deltaTime);

            // Rotate character smoothly toward movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

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

    public void TriggerJump()
    {
        if (!IsOwner) return;

        if (isGrounded)
        {
            velocity.y = jumpSpeed;
        }
    }

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
    }
}
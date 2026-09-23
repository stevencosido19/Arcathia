using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Range(1.0f, 20.0f)]
    public float moveSpeed = 5.0f;

    [Tooltip("Base downward force applied to the player.")]
    [Range(-1.0f, -30.0f)]
    public float gravity = -9.81f;

    [Header("Jump Settings")]
    [Range(1.0f, 10.0f)]
    public float jumpHeight = 2.5f;

    [Tooltip("Multiplier applied to gravity when falling to make drops faster.")]
    [Range(1.0f, 5.0f)]
    public float fallMultiplier = 2.0f; // Increases fall speed

    [Header("References")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // 1. READ INPUT
        Vector2 moveInput = Vector2.zero;
        if (Gamepad.current != null)
        {
            moveInput = Gamepad.current.leftStick.ReadValue();
        }

        // 2. CALCULATE RELATIVE DIRECTION
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x);

        // 3. APPLY HORIZONTAL MOVEMENT
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);

        // 4. APPLY GRAVITY & VERTICAL VELOCITY
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Ground snap reset
        }

        // Apply heavier gravity specifically while falling downward
        if (velocity.y < 0)
        {
            velocity.y += gravity * fallMultiplier * Time.deltaTime;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        controller.Move(velocity * Time.deltaTime);
    }

    // PUBLIC JUMP METHOD (Called by UI Jump Button OnClick)
    public void OnJumpButtonPressed()
    {
        if (controller != null && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
}
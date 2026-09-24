using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(MobileTouchInput))]
public class PlayerController : NetworkBehaviour // <-- Updated class name here!
{
    [Header("Movement Settings")]
    public float moveSpeed = 6.0f;
    public float jumpHeight = 1.5f;
    public float gravity = -19.62f;

    [Header("References")]
    public Transform cameraHolder;
    public Camera playerCamera;
    public AudioListener audioListener;

    private CharacterController characterController;
    private MobileTouchInput inputHandler;
    private Vector3 velocity;
    private float verticalCameraPitch = 0.0f;
    private bool isGrounded;

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();
        inputHandler = GetComponent<MobileTouchInput>();

        if (IsOwner)
        {
            if (playerCamera != null) playerCamera.enabled = true;
            if (audioListener != null) audioListener.enabled = true;
            if (inputHandler != null) inputHandler.enabled = true;
        }
        else
        {
            if (playerCamera != null) playerCamera.enabled = false;
            if (audioListener != null) audioListener.enabled = false;
            if (inputHandler != null) inputHandler.enabled = false;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleGroundCheck();
        HandleLook();
        HandleMovement();
        HandleJump();
    }

    private void HandleGroundCheck()
    {
        isGrounded = characterController.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }

    private void HandleLook()
    {
        Vector2 lookInput = inputHandler.LookInput;

        verticalCameraPitch -= lookInput.y * inputHandler.lookSensitivityY;
        verticalCameraPitch = Mathf.Clamp(verticalCameraPitch, -85f, 85f);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(verticalCameraPitch, 0f, 0f);
        }

        transform.Rotate(Vector3.up * (lookInput.x * inputHandler.lookSensitivityX));
    }

    private void HandleMovement()
    {
        Vector2 moveInput = inputHandler.MoveInput;
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;

        characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (inputHandler.JumpRequested)
        {
            if (isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            inputHandler.JumpRequested = false;
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    public void TriggerJump()
    {
        if (IsOwner && isGrounded)
        {
            inputHandler.JumpRequested = true;
        }
    }
}
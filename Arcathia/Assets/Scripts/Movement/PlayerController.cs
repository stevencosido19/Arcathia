using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6.0f;
    public float rotationSpeed = 15.0f;

    [Header("Jump & Fall Customization")]
    public float jumpSpeed = 8.5f;
    public float baseGravity = -20.0f;
    public float jumpGravityMultiplier = 1.0f;
    public float fallGravityMultiplier = 2.2f;
    public float maxFallSpeed = 45.0f;

    [Tooltip("Seconds after a jump during which the ground check is ignored. " +
             "Stops IsGrounded flickering true right after takeoff, which fires Land and Jump back to back.")]
    public float jumpGroundIgnoreTime = 0.1f;

    private CharacterController controller;
    private MobileTouchInput touchInputHandler;
    private Animator animator;

    private Vector3 verticalVelocity;
    private bool isGrounded;
    private bool wasGrounded;
    private float jumpGroundIgnoreTimer;

    // Animator Hashes
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        touchInputHandler = GetComponent<MobileTouchInput>();
        animator = GetComponentInChildren<Animator>();
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

        if (HUDLayoutManager.IsLayoutEditing) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        if (jumpGroundIgnoreTimer > 0f) jumpGroundIgnoreTimer -= Time.deltaTime;

        // 1. Gather Inputs
        float moveX = 0f;
        float moveZ = 0f;

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

        if (touchInputHandler != null)
        {
            moveX += touchInputHandler.MoveInput.x;
            moveZ += touchInputHandler.MoveInput.y;
        }

        Vector2 combinedInput = Vector2.ClampMagnitude(new Vector2(moveX, moveZ), 1f);
        Vector3 horizontalMove = Vector3.zero;

        // Deadzone check (prevents sliding while idling)
        float inputMagnitude = combinedInput.magnitude;

        if (inputMagnitude > 0.05f && Camera.main != null)
        {
            Transform camTransform = Camera.main.transform;
            Vector3 camForward = camTransform.forward;
            Vector3 camRight = camTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = (camForward * combinedInput.y + camRight * combinedInput.x).normalized;
            horizontalMove = moveDir * moveSpeed;

            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 2. Dynamic Gravity
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -3.0f; // Firm ground stick
        }
        else if (verticalVelocity.y > 0)
        {
            verticalVelocity.y += baseGravity * jumpGravityMultiplier * Time.deltaTime;
        }
        else
        {
            verticalVelocity.y += baseGravity * fallGravityMultiplier * Time.deltaTime;
        }

        verticalVelocity.y = Mathf.Max(verticalVelocity.y, -maxFallSpeed);

        // 3. Move Character
        Vector3 totalMovement = horizontalMove + verticalVelocity;
        controller.Move(totalMovement * Time.deltaTime);

        // 4. Update isGrounded AFTER controller.Move().
        // Ignore the ground check for a moment after a jump so it can't flicker back to true during takeoff.
        isGrounded = controller.isGrounded && jumpGroundIgnoreTimer <= 0f;

        // 5. On the frame we land, throw away any Jump trigger that was never consumed.
        // A trigger stays set until some transition uses it, so when you spam jump, leftover triggers
        // replay the jump-start animation late. That is the "animation delay".
        if (animator != null && isGrounded && !wasGrounded)
        {
            animator.ResetTrigger(JumpHash);
        }
        wasGrounded = isGrounded;

        // 6. Update Animator Parameters
        if (animator != null)
        {
            // Snap to 0 immediately when input stops to prevent delayed running
            if (inputMagnitude < 0.05f)
            {
                animator.SetFloat(SpeedHash, 0f);
            }
            else
            {
                animator.SetFloat(SpeedHash, inputMagnitude, 0.05f, Time.deltaTime);
            }

            animator.SetBool(IsGroundedHash, isGrounded);
        }
    }

    public void TriggerJump()
    {
        if (!IsOwner) return;
        if (HUDLayoutManager.IsLayoutEditing) return;

        if (isGrounded)
        {
            verticalVelocity.y = jumpSpeed;
            isGrounded = false; // Immediately flag as in-air
            wasGrounded = false;
            jumpGroundIgnoreTimer = jumpGroundIgnoreTime;

            if (animator != null)
            {
                animator.SetBool(IsGroundedHash, false);

                // Clear first so triggers never stack up, then fire exactly one.
                animator.ResetTrigger(JumpHash);
                animator.SetTrigger(JumpHash);
            }
        }
    }

    public void ResetVelocity()
    {
        verticalVelocity = Vector3.zero;
    }
}
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

    [Tooltip("Seconds after a jump during which the ground check is ignored.")]
    public float jumpGroundIgnoreTime = 0.1f;

    [Header("Knockback & Stun Settings")]
    [Tooltip("How fast the horizontal knockback decays over time.")]
    public float knockbackDamping = 8.0f;

    private CharacterController controller;
    private MobileTouchInput touchInputHandler;
    private Animator animator;

    private Vector3 verticalVelocity;
    private Vector3 knockbackVelocity;
    private float stunTimer = 0f;

    private bool isGrounded;
    private bool wasGrounded;
    private float jumpGroundIgnoreTimer;

    public bool IsStunned => stunTimer > 0f;

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

        // Process Stun Timer
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
        }

        float moveX = 0f;
        float moveZ = 0f;
        Vector3 horizontalMove = Vector3.zero;
        float inputMagnitude = 0f;

        // Only read inputs if NOT stunned
        if (!IsStunned)
        {
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
            inputMagnitude = combinedInput.magnitude;

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
        }

        // Gravity calculations
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -3.0f;
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

        // Knockback Damping
        if (knockbackVelocity.sqrMagnitude > 0.001f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDamping * Time.deltaTime);
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }

        // Combine all velocity vectors
        Vector3 totalMovement = horizontalMove + verticalVelocity + knockbackVelocity;
        controller.Move(totalMovement * Time.deltaTime);

        isGrounded = controller.isGrounded && jumpGroundIgnoreTimer <= 0f;

        if (animator != null && isGrounded && !wasGrounded)
        {
            animator.ResetTrigger(JumpHash);
        }
        wasGrounded = isGrounded;

        // Animations
        if (animator != null)
        {
            if (inputMagnitude < 0.05f || IsStunned)
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
        if (IsStunned) return; // Cannot jump while stunned
        if (HUDLayoutManager.IsLayoutEditing) return;

        if (isGrounded)
        {
            verticalVelocity.y = jumpSpeed;
            isGrounded = false;
            wasGrounded = false;
            jumpGroundIgnoreTimer = jumpGroundIgnoreTime;

            if (animator != null)
            {
                animator.SetBool(IsGroundedHash, false);
                animator.ResetTrigger(JumpHash);
                animator.SetTrigger(JumpHash);
            }
        }
    }

    /// <summary>
    /// Server entry point called by the projectile when hit.
    /// </summary>
    public void ApplyKnockbackAndStun(Vector3 force, float duration)
    {
        if (!IsServer) return;
        ApplyKnockbackAndStunClientRpc(force, duration);
    }

    [ClientRpc]
    private void ApplyKnockbackAndStunClientRpc(Vector3 force, float duration)
    {
        // Only the owner of this character executes the movement updates
        if (!IsOwner) return;

        stunTimer = Mathf.Max(stunTimer, duration);

        // Apply horizontal knockback
        knockbackVelocity = new Vector3(force.x, 0f, force.z);

        // Apply vertical lift (if any) directly into vertical velocity
        if (force.y > 0f)
        {
            verticalVelocity.y = force.y;
            isGrounded = false;
            wasGrounded = false;
            jumpGroundIgnoreTimer = jumpGroundIgnoreTime;

            if (animator != null)
            {
                animator.SetBool(IsGroundedHash, false);
            }
        }
    }

    public void ResetVelocity()
    {
        verticalVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        stunTimer = 0f;
    }
}
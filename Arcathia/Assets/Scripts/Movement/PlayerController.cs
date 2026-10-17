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
    [Tooltip("Base gravity. In video games, -18 to -25 feels much more natural than -9.81.")]
    public float baseGravity = -20.0f;
    public float jumpGravityMultiplier = 1.0f;
    [Tooltip("Multiplier applied when falling down. 2.0x to 2.5x gives snappy, accelerating falls.")]
    public float fallGravityMultiplier = 2.2f;
    [Tooltip("Maximum downward speed before terminal velocity.")]
    public float maxFallSpeed = 45.0f;

    private CharacterController controller;
    private MobileTouchInput touchInputHandler;

    private Vector3 verticalVelocity;
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

        if (HUDLayoutManager.IsLayoutEditing) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        // Unity's isGrounded flag
        isGrounded = controller.isGrounded;

        // Ground snap force only if grounded and not currently jumping upward
        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -3.0f; // Firm ground stick force
        }

        float moveX = 0f;
        float moveZ = 0f;

        // 1. Keyboard Inputs
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

        // 2. Touch Inputs
        if (touchInputHandler != null)
        {
            moveX += touchInputHandler.MoveInput.x;
            moveZ += touchInputHandler.MoveInput.y;
        }

        Vector2 combinedInput = Vector2.ClampMagnitude(new Vector2(moveX, moveZ), 1f);
        Vector3 horizontalMove = Vector3.zero;

        if (combinedInput.sqrMagnitude > 0.001f && Camera.main != null)
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

            // Rotate character smoothly toward movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 3. Dynamic Gravity Acceleration
        if (verticalVelocity.y > 0)
        {
            // Upward arc of jump
            verticalVelocity.y += baseGravity * jumpGravityMultiplier * Time.deltaTime;
        }
        else
        {
            // Downward fall - accelerate heavier toward the ground
            verticalVelocity.y += baseGravity * fallGravityMultiplier * Time.deltaTime;
        }

        // Clamp to terminal velocity
        verticalVelocity.y = Mathf.Max(verticalVelocity.y, -maxFallSpeed);

        // 4. COMBINE into ONE single Move call (Crucial fix!)
        Vector3 totalMovement = horizontalMove + verticalVelocity;
        controller.Move(totalMovement * Time.deltaTime);
    }

    public void TriggerJump()
    {
        if (!IsOwner) return;
        if (HUDLayoutManager.IsLayoutEditing) return;

        if (isGrounded)
        {
            verticalVelocity.y = jumpSpeed;
        }
    }

    public void ResetVelocity()
    {
        verticalVelocity = Vector3.zero;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        StartCoroutine(TeleportRoutine(position, rotation));
    }

    private System.Collections.IEnumerator TeleportRoutine(Vector3 position, Quaternion rotation)
    {
        if (controller != null) controller.enabled = false;
        ResetVelocity();

        transform.SetPositionAndRotation(position, rotation);

        yield return new WaitForFixedUpdate();

        if (controller != null) controller.enabled = true;
    }
}
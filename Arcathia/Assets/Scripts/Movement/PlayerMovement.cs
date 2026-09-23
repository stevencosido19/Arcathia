using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private DynamicJoystick dynamicJoystick;
    [SerializeField] private float moveSpeed = 5f;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            GameObject joystickObj = GameObject.FindWithTag("MovementJoystick");
            if (joystickObj != null)
            {
                dynamicJoystick = joystickObj.GetComponent<DynamicJoystick>();
            }
        }
    }   

    private void Update()
    {
        // Don't execute movement for players controlled by other clients
        if (!IsOwner) return;

        Vector2 moveInput = Vector2.zero;

        // Use Joystick if found
        if (dynamicJoystick != null && dynamicJoystick.Direction != Vector2.zero)
        {
            moveInput = dynamicJoystick.Direction;
        }
        else
        {
            // Keyboard fallback for testing in PC Unity Editor
            moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);
    }
}
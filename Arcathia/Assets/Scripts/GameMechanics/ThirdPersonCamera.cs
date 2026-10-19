using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : NetworkBehaviour
{
    // Static accessor for the local player's camera
    public static ThirdPersonCamera LocalCamera { get; private set; }

    [Header("Target & Offset")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, -4f);

    [Header("Input")]
    [Tooltip("Source of the look (camera) delta on touch devices. Found automatically if left empty.")]
    public MobileTouchInput touchInput;

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.2f;
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 60f;

    [Header("Aim Assist Settings (Camera Only)")]
    [Tooltip("Select which layers will be assisted (e.g. Enemy). Any object on this layer will be targeted.")]
    public LayerMask targetLayers;

    [Tooltip("Layers that block line of sight (e.g. Default, Walls, Terrain).")]
    public LayerMask obstacleLayers;

    [Range(0f, 1f)]
    [Tooltip("Loaded automatically from settings. 0 = Off, 0.5 = Balanced, 1.0 = Strong Assist.")]
    public float assistStrength = 0.5f;

    [Tooltip("Maximum detection range for aim assist.")]
    public float maxAssistDistance = 35f;

    [Tooltip("How close to the center of the screen (0 to 1) the target must be for assist to activate.")]
    public float screenAssistRadius = 0.20f;

    [Tooltip("How smoothly the camera nudges towards the enemy.")]
    public float cameraPullSpeed = 4.0f;

    private float currentX = 0f;
    private float currentY = 0f;
    private Camera cam;
    private Transform currentAssistedTarget;

    private const string PREFS_KEY = "Arcathia_AimAssistStrength";

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            var c = GetComponent<Camera>();
            if (c != null) c.enabled = false;

            var listener = GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;

            enabled = false;
            return;
        }

        // Register this instance as the local player's camera
        LocalCamera = this;
        cam = GetComponent<Camera>();

        // Load saved preference from PlayerPrefs automatically
        assistStrength = PlayerPrefs.GetFloat(PREFS_KEY, 0.5f);

        if (touchInput == null)
            touchInput = FindFirstObjectByType<MobileTouchInput>();

        // Find local player target
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

            if (target == null)
            {
                target = transform.root;
            }
        }

        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && LocalCamera == this)
        {
            LocalCamera = null;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (HUDLayoutManager.IsLayoutEditing) return;

        Vector2 inputDelta = ReadLookInput();

        currentX += inputDelta.x * rotationSpeed;
        currentY -= inputDelta.y * rotationSpeed;

        ApplyCameraAimAssist(inputDelta);

        currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 position = target.position + rotation * offset;

        transform.position = position;
        transform.rotation = rotation;
    }

    /// <summary>
    /// On touch devices the camera only listens to MobileTouchInput.LookInput, which comes from
    /// the finger that started on the right half of the screen. It must NOT read raw touches,
    /// otherwise the movement finger (left half) would rotate the camera too.
    /// Mouse is only used when there is no touchscreen (editor / desktop).
    /// </summary>
    private Vector2 ReadLookInput()
    {
        if (touchInput == null)
            touchInput = FindFirstObjectByType<MobileTouchInput>();

        if (Touchscreen.current != null && touchInput != null)
            return touchInput.LookInput;

        if (Mouse.current != null)
            return Mouse.current.delta.ReadValue();

        return Vector2.zero;
    }

    private void ApplyCameraAimAssist(Vector2 playerInput)
    {
        if (assistStrength <= 0.01f || cam == null) return;

        currentAssistedTarget = FindBestTarget();
        if (currentAssistedTarget == null) return;

        Collider col = currentAssistedTarget.GetComponentInChildren<Collider>();
        Vector3 targetPoint = (col != null) ? col.bounds.center : currentAssistedTarget.position + Vector3.up * 1.2f;

        Vector3 dirToTarget = (targetPoint - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(dirToTarget);
        Vector3 targetEuler = lookRotation.eulerAngles;

        float targetPitch = (targetEuler.x > 180f) ? targetEuler.x - 360f : targetEuler.x;
        float targetYaw = targetEuler.y;

        float deltaYaw = Mathf.DeltaAngle(currentX, targetYaw);
        float deltaPitch = Mathf.DeltaAngle(currentY, targetPitch);

        float inputBoost = (playerInput.sqrMagnitude > 0.01f) ? 1.2f : 0.8f;
        float step = cameraPullSpeed * assistStrength * inputBoost * Time.deltaTime;

        currentX = Mathf.MoveTowards(currentX, currentX + deltaYaw, step * Mathf.Abs(deltaYaw));
        currentY = Mathf.MoveTowards(currentY, currentY + deltaPitch, step * Mathf.Abs(deltaPitch));
    }

    private Transform FindBestTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, maxAssistDistance, targetLayers);
        Transform best = null;
        float closestDist = float.MaxValue;
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);

        foreach (var c in colliders)
        {
            if (c.transform.IsChildOf(transform.root)) continue;

            Vector3 center = c.bounds.center;
            Vector3 viewport = cam.WorldToViewportPoint(center);

            if (viewport.z <= 0) continue;

            float screenDist = Vector2.Distance(screenCenter, new Vector2(viewport.x, viewport.y));
            if (screenDist > screenAssistRadius) continue;

            Vector3 camPos = transform.position;
            Vector3 dir = (center - camPos).normalized;
            float dist = Vector3.Distance(camPos, center);

            if (Physics.Raycast(camPos, dir, dist, obstacleLayers))
            {
                continue;
            }

            if (screenDist < closestDist)
            {
                closestDist = screenDist;
                best = c.transform;
            }
        }

        return best;
    }
}
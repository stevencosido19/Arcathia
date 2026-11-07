using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : NetworkBehaviour
{
    public static ThirdPersonCamera LocalCamera { get; private set; }

    [Header("Target & Offset")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2f, -4f);

    [Header("Input")]
    public MobileTouchInput touchInput;

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.2f;
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 60f;

    [Header("Aim Assist Settings (Camera Only)")]
    public LayerMask targetLayers;
    public LayerMask obstacleLayers;

    [Range(0f, 1f)]
    public float assistStrength = 0.5f;
    public float maxAssistDistance = 35f;
    public float screenAssistRadius = 0.20f;
    public float cameraPullSpeed = 4.0f;

    // --- SPECTATING STATE ---
    public bool IsSpectating { get; private set; } = false;
    private Transform defaultOwnerTarget;
    private readonly List<Transform> spectateTargets = new List<Transform>();
    private int spectateIndex = 0;

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

        LocalCamera = this;
        cam = GetComponent<Camera>();
        assistStrength = PlayerPrefs.GetFloat(PREFS_KEY, 0.5f);

        if (touchInput == null)
            touchInput = Object.FindFirstObjectByType<MobileTouchInput>();

        // Cache default target (our player)
        if (target == null)
        {
            target = transform.root;
        }
        defaultOwnerTarget = target;

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

        // Only use Aim Assist while ALIVE (not while spectating)
        if (!IsSpectating)
        {
            ApplyCameraAimAssist(inputDelta);
        }

        currentY = Mathf.Clamp(currentY, minVerticalAngle, maxVerticalAngle);

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 position = target.position + rotation * offset;

        transform.position = position;
        transform.rotation = rotation;
    }

    private Vector2 ReadLookInput()
    {
        if (touchInput == null)
            touchInput = Object.FindFirstObjectByType<MobileTouchInput>();

        if (Touchscreen.current != null && touchInput != null)
            return touchInput.LookInput;

        if (Mouse.current != null)
            return Mouse.current.delta.ReadValue();

        return Vector2.zero;
    }

    // --- SPECTATOR CONTROLS ---

    public void StartSpectating(ulong killerNetObjectId = 0)
    {
        IsSpectating = true;
        BuildSpectateList();

        // If a killer exists, try to spectate them first
        if (killerNetObjectId != 0 && NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(killerNetObjectId, out NetworkObject killerObj))
        {
            target = killerObj.transform;
            UpdateSpectateUILabel();
            return;
        }

        // Otherwise spectate the first available living player
        if (spectateTargets.Count > 0)
        {
            spectateIndex = 0;
            target = spectateTargets[spectateIndex];
        }

        UpdateSpectateUILabel();
    }

    public void StopSpectating()
    {
        IsSpectating = false;
        target = defaultOwnerTarget; // Return camera to our own player
    }

    public void CycleSpectateTarget(int direction)
    {
        BuildSpectateList();

        if (spectateTargets.Count == 0) return;

        spectateIndex += direction;
        if (spectateIndex >= spectateTargets.Count) spectateIndex = 0;
        else if (spectateIndex < 0) spectateIndex = spectateTargets.Count - 1;

        target = spectateTargets[spectateIndex];
        UpdateSpectateUILabel();
    }

    private void BuildSpectateList()
    {
        spectateTargets.Clear();

        // Find all living players in the match (excluding ourselves)
        PlayerHealth[] allPlayers = Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        foreach (var p in allPlayers)
        {
            if (!p.IsOwner && !p.isDead.Value)
            {
                spectateTargets.Add(p.transform);
            }
        }

        // Fallback for Single-Player Campaign (Spectate active AI enemies if no players exist)
        if (spectateTargets.Count == 0)
        {
            ShootingEnemyAI[] enemies = Object.FindObjectsByType<ShootingEnemyAI>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e.gameObject.activeInHierarchy) spectateTargets.Add(e.transform);
            }
        }
    }

    private void UpdateSpectateUILabel()
    {
        if (DeathScreenUI.Instance != null && target != null)
        {
            PlayerScore ps = target.GetComponent<PlayerScore>();
            string nameToShow = (ps != null) ? ps.GetPlayerName() : target.gameObject.name;
            DeathScreenUI.Instance.UpdateSpectatingName(nameToShow);
        }
    }

    // --- AIM ASSIST ---

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
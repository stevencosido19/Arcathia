using UnityEngine;
using UnityEngine.UI;

public class PlayerAimAssist : MonoBehaviour
{
    public static PlayerAimAssist Instance { get; private set; }

    [Header("Layer Target Filtering")]
    [Tooltip("Select which layers will be targeted by Aim Assist (e.g. Enemy). Any object on this layer will be targeted.")]
    public LayerMask targetLayers;

    [Tooltip("Layers that block line of sight (e.g. Default, Environment, Obstacles).")]
    public LayerMask obstacleLayers;

    [Header("Assist Settings")]
    [Range(0f, 1f)]
    [Tooltip("0 = Off, 0.5 = Balanced, 1.0 = Strongest Assist.")]
    public float assistStrength = 0.5f;

    [Tooltip("Maximum range in meters to detect targets.")]
    public float maxAssistDistance = 35f;

    [Tooltip("How close to the center of the screen (0 to 1) the target must be to trigger assist.")]
    public float screenAssistRadius = 0.25f;

    [Header("UI Slider (Settings Menu)")]
    [Tooltip("Optional: Drag your UI Slider from your Settings panel here.")]
    public Slider aimAssistSlider;

    private Camera cam;
    private Transform currentTarget;

    private const string PREFS_KEY = "Arcathia_AimAssistStrength";

    private void Awake()
    {
        Instance = this;
        cam = Camera.main;

        // Load saved slider value (defaults to 0.5 if never set)
        assistStrength = PlayerPrefs.GetFloat(PREFS_KEY, 0.5f);

        if (aimAssistSlider != null)
        {
            aimAssistSlider.minValue = 0f;
            aimAssistSlider.maxValue = 1f;
            aimAssistSlider.value = assistStrength;
            aimAssistSlider.onValueChanged.AddListener(SetAssistStrength);
        }
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        // Don't search if assist is completely turned off
        if (assistStrength <= 0.01f)
        {
            currentTarget = null;
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        currentTarget = FindBestTarget();
    }

    /// <summary>
    /// Searches for the closest target to the center of the screen on the selected layers.
    /// </summary>
    private Transform FindBestTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, maxAssistDistance, targetLayers);
        Transform bestTarget = null;
        float closestDistanceToCenter = float.MaxValue;
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);

        foreach (Collider col in colliders)
        {
            // Ignore self if player is on the same layer
            if (col.transform.IsChildOf(transform.root)) continue;

            Vector3 targetCenter = col.bounds.center;
            Vector3 viewportPoint = cam.WorldToViewportPoint(targetCenter);

            // Must be in front of the camera (Z > 0)
            if (viewportPoint.z <= 0) continue;

            // Calculate distance to the crosshair / center of the screen
            float distFromCenter = Vector2.Distance(screenCenter, new Vector2(viewportPoint.x, viewportPoint.y));

            // Must be within our assist radius (scaled slightly by strength)
            float allowedRadius = screenAssistRadius * Mathf.Lerp(0.5f, 1.2f, assistStrength);
            if (distFromCenter > allowedRadius) continue;

            // Line of Sight check: ensure no walls/terrain block the view
            Vector3 rayOrigin = cam.transform.position;
            Vector3 dirToTarget = (targetCenter - rayOrigin).normalized;
            float distToTarget = Vector3.Distance(rayOrigin, targetCenter);

            if (Physics.Raycast(rayOrigin, dirToTarget, distToTarget, obstacleLayers))
            {
                continue; // Blocked by wall
            }

            // Pick the target closest to the crosshair
            if (distFromCenter < closestDistanceToCenter)
            {
                closestDistanceToCenter = distFromCenter;
                bestTarget = col.transform;
            }
        }

        return bestTarget;
    }

    /// <summary>
    /// Call this when shooting to bend the projectile's trajectory toward the enemy.
    /// </summary>
    public Vector3 GetAssistedShootDirection(Vector3 fireOrigin, Vector3 defaultForward)
    {
        if (assistStrength <= 0.01f || currentTarget == null)
        {
            return defaultForward; // Shoot straight if disabled or no target
        }

        Collider col = currentTarget.GetComponentInChildren<Collider>();
        Vector3 aimPoint = (col != null) ? col.bounds.center : currentTarget.position + Vector3.up * 1.0f;
        Vector3 dirToTarget = (aimPoint - fireOrigin).normalized;

        // Blend between where the player is pointing and the target based on assistStrength
        return Vector3.Slerp(defaultForward, dirToTarget, assistStrength).normalized;
    }

    /// <summary>
    /// UI Slider hook (can also be called by Settings menus).
    /// </summary>
    public void SetAssistStrength(float value)
    {
        assistStrength = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PREFS_KEY, assistStrength);
        PlayerPrefs.Save();
    }

    public Transform GetCurrentTarget() => currentTarget;

    // --- VISUAL GIZMOS IN SCENE VIEW ---
    private void OnDrawGizmosSelected()
    {
        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentTarget.position + Vector3.up, 0.8f);
            Gizmos.DrawLine(transform.position, currentTarget.position + Vector3.up);
        }
    }
}
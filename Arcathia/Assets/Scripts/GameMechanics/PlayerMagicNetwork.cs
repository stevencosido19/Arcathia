using Unity.Netcode;
using UnityEngine;

public class PlayerMagicNetwork : NetworkBehaviour
{
    [Header("Spell Prefabs")]
    public GameObject fireProjectilePrefab;
    public GameObject waterProjectilePrefab;
    public GameObject lightningProjectilePrefab;

    [Header("Aim Settings")]
    public LayerMask aimLayerMask = ~0; // Layers the crosshair raycast can hit
    public float maxAimDistance = 100f;  // Fallback distance if aiming at the sky
    public Vector2 crosshairViewportPoint = new Vector2(0.5f, 0.5f); // Crosshair position on screen (0.5, 0.5 = exact center)
    public bool debugAim = false;        // Draws the aim line in the Scene view and logs aim problems

    [Header("Ammo Settings")]
    public int maxAmmo = 3;
    private int fireAmmo = 0;
    private int waterAmmo = 0;
    private int lightningAmmo = 0;

    private PlayerPowerUpHandler powerUpHandler;
    private SpellbookUI spellbookUI;
    private Camera mainCamera;

    // Reused buffer so the aim raycast doesn't allocate every shot
    private static readonly RaycastHit[] aimHits = new RaycastHit[8];

    private bool IsMatchOver()
    {
        return MatchManager.Instance != null && MatchManager.Instance.isMatchOver.Value;
    }

    private void Awake()
    {
        powerUpHandler = GetComponent<PlayerPowerUpHandler>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            spellbookUI = FindFirstObjectByType<SpellbookUI>();
            mainCamera = Camera.main; // Cache main camera for local player aim raycasts
        }
    }

    public void AddSpellAmmo(SpellType type, int amount)
    {
        if (IsMatchOver()) return;

        switch (type)
        {
            case SpellType.Fire:
                fireAmmo = Mathf.Clamp(fireAmmo + amount, 0, maxAmmo);
                UpdateHUD(SpellType.Fire, fireAmmo);
                break;
            case SpellType.Water:
                waterAmmo = Mathf.Clamp(waterAmmo + amount, 0, maxAmmo);
                UpdateHUD(SpellType.Water, waterAmmo);
                break;
            case SpellType.Lightning:
                lightningAmmo = Mathf.Clamp(lightningAmmo + amount, 0, maxAmmo);
                UpdateHUD(SpellType.Lightning, lightningAmmo);
                break;
        }
    }

    public bool HasAmmo(SpellType type)
    {
        return type switch
        {
            SpellType.Fire => fireAmmo > 0,
            SpellType.Water => waterAmmo > 0,
            SpellType.Lightning => lightningAmmo > 0,
            _ => false
        };
    }

    public void CastSpell(SpellType type)
    {
        if (IsMatchOver()) return;

        if (!HasAmmo(type))
        {
            Debug.Log($"[PlayerMagic] No ammo left for {type}!");
            return;
        }

        DeductAmmo(type);

        // 1. Calculate spawn origin in front of player
        Vector3 spawnPosition = transform.position + transform.forward * 1.2f + Vector3.up * 1.2f;

        // 2. Calculate rotation facing crosshair target point in 3D world
        Quaternion aimRotation = GetAimRotation(spawnPosition);

        // 3. Request Server to spawn spell projectile with crosshair rotation
        CastSpellServerRpc(type, spawnPosition, aimRotation);
    }

    /// <summary>
    /// Raycasts from the screen crosshair to find the world point the camera is looking at,
    /// then returns the rotation from the spell spawn point toward that point.
    /// </summary>
    private Quaternion GetAimRotation(Vector3 spawnPos)
    {
        // Re-grab the camera if it was never found, was destroyed, or got disabled
        // (camera swap, Cinemachine, respawn...). A stale cached camera gives a stale aim direction.
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            if (debugAim)
                Debug.LogWarning("[PlayerMagic] No active camera tagged 'MainCamera' found. Falling back to character facing, which is flat. Tag your gameplay camera as MainCamera.");
            return transform.rotation; // Fallback to character facing direction
        }

        // Ray through the crosshair
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(crosshairViewportPoint.x, crosshairViewportPoint.y, 0f));

        // Third-person fix: start the ray at the spell spawn point's depth along the camera ray.
        // This way our own body (and anything between the camera and the player) can't block the aim ray.
        float depthToSpawn = Mathf.Max(0f, Vector3.Dot(spawnPos - ray.origin, ray.direction));
        Vector3 rayStart = ray.origin + ray.direction * depthToSpawn;
        float rayLength = Mathf.Max(0.1f, maxAimDistance - depthToSpawn);

        // Default target: far point along the ray (aiming at the sky or something out of range)
        Vector3 targetPoint = rayStart + ray.direction * rayLength;

        // Find the closest hit that isn't part of our own character
        int hitCount = Physics.RaycastNonAlloc(rayStart, ray.direction, aimHits, rayLength, aimLayerMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            if (aimHits[i].transform.IsChildOf(transform)) continue; // skip our own colliders

            if (aimHits[i].distance < closestDistance)
            {
                closestDistance = aimHits[i].distance;
                targetPoint = aimHits[i].point;
            }
        }

        // Direction from the spawn point to the crosshair world point
        Vector3 aimDirection = targetPoint - spawnPos;

        // Target practically at the muzzle: just use the camera's look direction
        if (aimDirection.sqrMagnitude < 0.25f) aimDirection = ray.direction;

        aimDirection.Normalize();

        if (debugAim) Debug.DrawLine(spawnPos, targetPoint, Color.red, 2f);

        return Quaternion.LookRotation(aimDirection);
    }

    private void DeductAmmo(SpellType type)
    {
        switch (type)
        {
            case SpellType.Fire:
                fireAmmo--;
                UpdateHUD(SpellType.Fire, fireAmmo);
                break;
            case SpellType.Water:
                waterAmmo--;
                UpdateHUD(SpellType.Water, waterAmmo);
                break;
            case SpellType.Lightning:
                lightningAmmo--;
                UpdateHUD(SpellType.Lightning, lightningAmmo);
                break;
        }
    }

    private void UpdateHUD(SpellType type, int currentCount)
    {
        if (IsOwner)
        {
            if (spellbookUI == null)
            {
                spellbookUI = FindFirstObjectByType<SpellbookUI>();
            }

            if (spellbookUI != null)
            {
                spellbookUI.UpdateAmmoUI(type, currentCount, maxAmmo);
            }
        }
    }

    [ServerRpc]
    private void CastSpellServerRpc(SpellType type, Vector3 spawnPos, Quaternion spawnRot)
    {
        if (IsMatchOver()) return;

        GameObject prefabToSpawn = type switch
        {
            SpellType.Fire => fireProjectilePrefab,
            SpellType.Water => waterProjectilePrefab,
            SpellType.Lightning => lightningProjectilePrefab,
            _ => null
        };

        if (prefabToSpawn != null)
        {
            NetworkObject netObj = null;

            if (NetworkObjectPool.Instance != null)
            {
                netObj = NetworkObjectPool.Instance.GetNetworkObject(prefabToSpawn, spawnPos, spawnRot);
            }
            else
            {
                GameObject projectile = Instantiate(prefabToSpawn, spawnPos, spawnRot);
                netObj = projectile.GetComponent<NetworkObject>();
            }

            if (netObj == null)
            {
                Debug.LogWarning($"[PlayerMagicNetwork] Pool is currently empty for {type}!");
                return;
            }

            MagicProjectile projScript = netObj.GetComponent<MagicProjectile>();
            if (projScript != null)
            {
                projScript.SetShooter(NetworkObject);
                projScript.SetSourcePrefab(prefabToSpawn);

                if (powerUpHandler != null && powerUpHandler.isOverchargeActive.Value)
                {
                    int baseDamage = projScript.damage;
                    projScript.damage *= 2;
                    powerUpHandler.isOverchargeActive.Value = false;

                    Debug.Log($"<color=orange>[PowerUp Effect]</color> Overcharge Matrix applied! Boosted damage from {baseDamage} to {projScript.damage} on {gameObject.name}.");
                }
            }

            if (!netObj.IsSpawned)
            {
                // Make sure the aim rotation is on the object BEFORE it spawns, regardless of how the
                // pool positions it. The spawn message (and the projectile's launch direction) uses this.
                netObj.transform.SetPositionAndRotation(spawnPos, spawnRot);
                netObj.Spawn();
            }
        }
    }
}
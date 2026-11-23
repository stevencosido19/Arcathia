using Unity.Netcode;
using UnityEngine;

public class PlayerMagicNetwork : NetworkBehaviour
{
    [Header("Spell Prefabs")]
    public GameObject fireProjectilePrefab;
    public GameObject waterProjectilePrefab;
    public GameObject lightningProjectilePrefab;

    [Header("Cast Point")]
    [Tooltip("Empty child at the hand / staff tip. Spells spawn here instead of from the body. " +
             "Assign it from INSIDE the player prefab's hierarchy (not from the Project window).")]
    public Transform castPoint;
    [Tooltip("Server safety check: if the cast point sent by a client is farther than this from the player, " +
             "the server uses the fallback spawn position instead. Raise it if your cast point is very far from the body.")]
    public float maxCastPointDistance = 4f;

    [Header("Aim Settings")]
    public LayerMask aimLayerMask = ~0; 
    public float maxAimDistance = 100f;  
    public Vector2 crosshairViewportPoint = new Vector2(0.5f, 0.5f); 
    public bool debugAim = false;        

    [Header("Ammo Settings")]
    public int maxAmmo = 3;

    [Header("Correct Answer Ammo Reward")]
    [Tooltip("Default ammo gained when answering a question correctly.")]
    [Min(1)]
    public int ammoPerCorrectAnswer = 1;

    [Tooltip("Enable to set different ammo rewards for Fire, Water, and Lightning.")]
    public bool customizePerSpellType = false;
    [Min(1)] public int fireAmmoGain = 1;
    [Min(1)] public int waterAmmoGain = 1;
    [Min(1)] public int lightningAmmoGain = 1;

    [Tooltip("If enabled, AddSpellAmmo() uses the Inspector reward settings above when adding ammo " +
             "(even if your quiz script passes a hardcoded 1).")]
    public bool overrideQuizPassedAmount = true;

    private int fireAmmo = 0;
    private int waterAmmo = 0;
    private int lightningAmmo = 0;

    private PlayerPowerUpHandler powerUpHandler;
    private SpellbookUI spellbookUI;
    private Camera mainCamera;
    private Animator playerAnimator; 
    private bool warnedNoCastPoint;

    
    private static readonly RaycastHit[] aimHits = new RaycastHit[8];

    private bool IsMatchOver()
    {
        return MatchManager.Instance != null && MatchManager.Instance.isMatchOver.Value;
    }

    private void Awake()
    {
        powerUpHandler = GetComponent<PlayerPowerUpHandler>();
        
        playerAnimator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            spellbookUI = FindFirstObjectByType<SpellbookUI>();
            mainCamera = Camera.main; 
        }
    }

    public int GetCustomAmmoGain(SpellType type)
    {
        if (!customizePerSpellType)
        {
            return Mathf.Max(1, ammoPerCorrectAnswer);
        }

        return type switch
        {
            SpellType.Fire => Mathf.Max(1, fireAmmoGain),
            SpellType.Water => Mathf.Max(1, waterAmmoGain),
            SpellType.Lightning => Mathf.Max(1, lightningAmmoGain),
            _ => Mathf.Max(1, ammoPerCorrectAnswer)
        };
    }

    
    public void AddCorrectAnswerAmmo(SpellType type)
    {
        AddExactSpellAmmo(type, GetCustomAmmoGain(type));
    }

    public void AddSpellAmmo(SpellType type, int amount)
    {
        int finalAmount = (overrideQuizPassedAmount && amount > 0)
            ? GetCustomAmmoGain(type)
            : amount;

        AddExactSpellAmmo(type, finalAmount);
    }

    public void AddExactSpellAmmo(SpellType type, int amount)
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

        
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("CastSpell");
        }

        
        Vector3 spawnPosition = GetSpawnPosition();

        
        Quaternion aimRotation = GetAimRotation(spawnPosition);

        
        CastSpellServerRpc(type, spawnPosition, aimRotation);
    }

    private Vector3 GetSpawnPosition()
    {
        if (castPoint != null) return castPoint.position;

        if (!warnedNoCastPoint)
        {
            warnedNoCastPoint = true;
            Debug.LogWarning($"[PlayerMagic] '{name}' has no Cast Point assigned, spells will spawn from the body. " +
                             "Add an empty child at the hand/staff tip and assign it on the prefab.", this);
        }

        return GetFallbackSpawnPosition();
    }

    private Vector3 GetFallbackSpawnPosition()
    {
        return transform.position + transform.forward * 1.2f + Vector3.up * 1.2f;
    }

    private Quaternion GetAimRotation(Vector3 spawnPos)
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            if (debugAim)
                Debug.LogWarning("[PlayerMagic] No active camera tagged 'MainCamera' found. Falling back to character facing.");
            return transform.rotation;
        }

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(crosshairViewportPoint.x, crosshairViewportPoint.y, 0f));

        float depthToSpawn = Mathf.Max(0f, Vector3.Dot(spawnPos - ray.origin, ray.direction));
        Vector3 rayStart = ray.origin + ray.direction * depthToSpawn;
        float rayLength = Mathf.Max(0.1f, maxAimDistance - depthToSpawn);

        Vector3 targetPoint = rayStart + ray.direction * rayLength;

        int hitCount = Physics.RaycastNonAlloc(rayStart, ray.direction, aimHits, rayLength, aimLayerMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            if (aimHits[i].transform.IsChildOf(transform)) continue;

            if (aimHits[i].distance < closestDistance)
            {
                closestDistance = aimHits[i].distance;
                targetPoint = aimHits[i].point;
            }
        }

        Vector3 aimDirection = targetPoint - spawnPos;

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

       
        PlayCastSpellAnimationClientRpc();

        float maxDist = Mathf.Max(0.5f, maxCastPointDistance);
        if ((spawnPos - transform.position).sqrMagnitude > maxDist * maxDist)
        {
            spawnPos = GetFallbackSpawnPosition();
        }

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
                netObj.transform.SetPositionAndRotation(spawnPos, spawnRot);
                netObj.Spawn();
            }
        }
    }

    [ClientRpc]
    private void PlayCastSpellAnimationClientRpc()
    {
        
        if (!IsOwner && playerAnimator != null)
        {
            playerAnimator.SetTrigger("CastSpell");
        }
    }
}
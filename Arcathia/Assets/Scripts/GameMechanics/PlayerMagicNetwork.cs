using Unity.Netcode;
using UnityEngine;

public class PlayerMagicNetwork : NetworkBehaviour
{
    [Header("Spell Prefabs")]
    public GameObject fireProjectilePrefab;
    public GameObject waterProjectilePrefab;
    public GameObject lightningProjectilePrefab;

    [Header("Ammo Settings")]
    public int maxAmmo = 3;
    private int fireAmmo = 0;
    private int waterAmmo = 0;
    private int lightningAmmo = 0;

    public void AddSpellAmmo(SpellType type, int amount)
    {
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
        // 1. Check if player has ammo before shooting
        if (!HasAmmo(type))
        {
            Debug.Log($"[PlayerMagic] No ammo left for {type}!");
            return;
        }

        // 2. Consume 1 ammo dot
        DeductAmmo(type);

        // 3. Request Server to spawn spell projectile
        Vector3 spawnPosition = transform.position + transform.forward * 1.5f + Vector3.up * 1.0f;
        CastSpellServerRpc(type, spawnPosition, transform.rotation);
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
            SpellbookUI ui = FindFirstObjectByType<SpellbookUI>();
            if (ui != null)
            {
                ui.UpdateAmmoUI(type, currentCount, maxAmmo);
            }
        }
    }

    [ServerRpc]
    private void CastSpellServerRpc(SpellType type, Vector3 spawnPos, Quaternion spawnRot)
    {
        // Map SpellType to Inspector Prefabs
        GameObject prefabToSpawn = type switch
        {
            SpellType.Fire => fireProjectilePrefab,
            SpellType.Water => waterProjectilePrefab,
            SpellType.Lightning => lightningProjectilePrefab,
            _ => null
        };

        if (prefabToSpawn != null)
        {
            GameObject projectile = Instantiate(prefabToSpawn, spawnPos, spawnRot);

            // Assign shooter so projectile ignores self-collision
            MagicProjectile projScript = projectile.GetComponent<MagicProjectile>();
            if (projScript != null)
            {
                projScript.SetShooter(NetworkObject);
            }

            // Spawn across all networked clients
            NetworkObject netObj = projectile.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }
        else
        {
            Debug.LogError($"[PlayerMagicNetwork] Prefab for {type} is NOT assigned in the Inspector!");
        }
    }
}
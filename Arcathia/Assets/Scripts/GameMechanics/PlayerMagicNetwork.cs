using Unity.Netcode;
using UnityEngine;

public class PlayerMagicNetwork : NetworkBehaviour
{
    [Header("Projectile Prefabs")]
    public GameObject fireProjectilePrefab;
    public GameObject waterProjectilePrefab;
    public GameObject lightningProjectilePrefab;

    [Header("Spawn Settings")]
    public Transform castPoint;

    [Header("Ammo Settings (Max 5)")]
    public int maxAmmo = 5;

    // Networked ammo variables
    private NetworkVariable<int> fireAmmo = new NetworkVariable<int>(0);
    private NetworkVariable<int> waterAmmo = new NetworkVariable<int>(0);
    private NetworkVariable<int> lightningAmmo = new NetworkVariable<int>(0);

    private SpellbookUI spellbookUI;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // ONLY the local owner connects their network variables to their local HUD Canvas
        if (IsOwner)
        {
            spellbookUI = FindFirstObjectByType<SpellbookUI>();

            if (spellbookUI != null)
            {
                // 1. Subscribe to future value changes
                fireAmmo.OnValueChanged += (oldVal, newVal) => spellbookUI.UpdateAmmoUI(SpellType.Fire, newVal, maxAmmo);
                waterAmmo.OnValueChanged += (oldVal, newVal) => spellbookUI.UpdateAmmoUI(SpellType.Water, newVal, maxAmmo);
                lightningAmmo.OnValueChanged += (oldVal, newVal) => spellbookUI.UpdateAmmoUI(SpellType.Lightning, newVal, maxAmmo);

                // 2. Force immediate UI refresh for initial spawn values
                spellbookUI.UpdateAmmoUI(SpellType.Fire, fireAmmo.Value, maxAmmo);
                spellbookUI.UpdateAmmoUI(SpellType.Water, waterAmmo.Value, maxAmmo);
                spellbookUI.UpdateAmmoUI(SpellType.Lightning, lightningAmmo.Value, maxAmmo);
            }
            else
            {
                Debug.LogError("[PlayerMagicNetwork] SpellbookUI not found in scene!");
            }
        }
    }

    public void AddSpellAmmo(SpellType type, int amount)
    {
        if (!IsOwner) return;
        AddAmmoServerRpc(type, amount);
    }

    [ServerRpc]
    private void AddAmmoServerRpc(SpellType type, int amount)
    {
        switch (type)
        {
            case SpellType.Fire:
                fireAmmo.Value = Mathf.Clamp(fireAmmo.Value + amount, 0, maxAmmo);
                break;
            case SpellType.Water:
                waterAmmo.Value = Mathf.Clamp(waterAmmo.Value + amount, 0, maxAmmo);
                break;
            case SpellType.Lightning:
                lightningAmmo.Value = Mathf.Clamp(lightningAmmo.Value + amount, 0, maxAmmo);
                break;
        }
    }

    public void CastSpell(SpellType type)
    {
        if (!IsOwner) return;

        int currentAmmo = type switch
        {
            SpellType.Fire => fireAmmo.Value,
            SpellType.Water => waterAmmo.Value,
            SpellType.Lightning => lightningAmmo.Value,
            _ => 0
        };

        if (currentAmmo > 0)
        {
            // Use camera forward direction if castPoint isn't assigned
            Vector3 spawnPos = castPoint != null ? castPoint.position : transform.position + transform.forward;
            Quaternion spawnRot = castPoint != null ? castPoint.rotation : transform.rotation;

            CastSpellServerRpc(type, spawnPos, spawnRot);
        }
        else
        {
            Debug.Log($"No ammo for {type}! Solve an equation to reload.");
        }
    }

    [ServerRpc]
    private void CastSpellServerRpc(SpellType type, Vector3 spawnPos, Quaternion spawnRot)
    {
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
            projectile.GetComponent<NetworkObject>().Spawn();

            switch (type)
            {
                case SpellType.Fire: fireAmmo.Value--; break;
                case SpellType.Water: waterAmmo.Value--; break;
                case SpellType.Lightning: lightningAmmo.Value--; break;
            }
        }
    }
}
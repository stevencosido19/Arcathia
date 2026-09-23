using UnityEngine;
using Unity.Netcode;

public class MagicShooter : NetworkBehaviour
{
    [Header("Spells & Prefabs")]
    public GameObject flamePrefab;
    public GameObject waterPrefab;
    public GameObject electricPrefab;
    public Transform spellSpawnPoint;
    public float projectileSpeed = 20f;

    [Header("Ammo Settings")]
    public int maxAmmoPerElement = 10;

    [Header("Current Ammo Count")]
    public int flameAmmo = 3;
    public int waterAmmo = 3;
    public int electricAmmo = 3;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Automatically bind the scene UI to this player instance if local owner
        if (IsOwner)
        {
            RuneDialController dial = FindFirstObjectByType<RuneDialController>();
            ElementalSpellBook book = GetComponent<ElementalSpellBook>();

            if (dial != null)
            {
                dial.BindToLocalPlayer(this, book);
            }
        }
    }

    // --- ELEMENTAL AMMO MANAGEMENT ---

    public bool IsElementFull(ElementType element)
    {
        switch (element)
        {
            case ElementType.Flame: return flameAmmo >= maxAmmoPerElement;
            case ElementType.Water: return waterAmmo >= maxAmmoPerElement;
            case ElementType.Electric: return electricAmmo >= maxAmmoPerElement;
            default: return false;
        }
    }

    public void AddElementAmmo(ElementType element, int amount)
    {
        switch (element)
        {
            case ElementType.Flame:
                flameAmmo = Mathf.Clamp(flameAmmo + amount, 0, maxAmmoPerElement);
                break;
            case ElementType.Water:
                waterAmmo = Mathf.Clamp(waterAmmo + amount, 0, maxAmmoPerElement);
                break;
            case ElementType.Electric:
                electricAmmo = Mathf.Clamp(electricAmmo + amount, 0, maxAmmoPerElement);
                break;
        }
    }

    public void TryCastSpell(ElementType element)
    {
        // Only local owner can trigger spellcasting
        if (!IsOwner) return;

        bool hasAmmo = false;

        switch (element)
        {
            case ElementType.Flame:
                if (flameAmmo > 0) { flameAmmo--; hasAmmo = true; }
                break;
            case ElementType.Water:
                if (waterAmmo > 0) { waterAmmo--; hasAmmo = true; }
                break;
            case ElementType.Electric:
                if (electricAmmo > 0) { electricAmmo--; hasAmmo = true; }
                break;
        }

        if (hasAmmo)
        {
            CastSpellServerRpc(element);
        }
    }

    // --- NETWORKED SPELL SPAWNING (Netcode 1.11.4 Syntax) ---

    [Rpc(SendTo.Server)]
    private void CastSpellServerRpc(ElementType element)
    {
        GameObject prefabToSpawn = null;
        switch (element)
        {
            case ElementType.Flame: prefabToSpawn = flamePrefab; break;
            case ElementType.Water: prefabToSpawn = waterPrefab; break;
            case ElementType.Electric: prefabToSpawn = electricPrefab; break;
        }

        if (prefabToSpawn != null && spellSpawnPoint != null)
        {
            GameObject spellObj = Instantiate(prefabToSpawn, spellSpawnPoint.position, spellSpawnPoint.rotation);

            SpellProjectile proj = spellObj.GetComponent<SpellProjectile>();
            if (proj != null)
            {
                proj.owner = this.gameObject;
            }

            Rigidbody rb = spellObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = spellSpawnPoint.forward * projectileSpeed;
            }

            // Sync the projectile across all connected clients
            NetworkObject netObj = spellObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }
        }
    }
}
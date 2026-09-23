using UnityEngine;
using TMPro;

public class MagicShooter : MonoBehaviour
{
    [Header("Dependencies")]
    public ElementalSpellBook spellBook;
    public Transform spellSpawnPoint;

    [Header("Elemental Prefabs")]
    public GameObject flameSpellPrefab;
    public GameObject waterSpellPrefab;
    public GameObject electricSpellPrefab;

    [Header("Independent Ammo Storage")]
    public int maxAmmoPerElement = 9;
    public int flameAmmo = 0;
    public int waterAmmo = 0;
    public int electricAmmo = 0;

    [Header("UI References")]
    public TextMeshProUGUI flameAmmoText;
    public TextMeshProUGUI waterAmmoText;
    public TextMeshProUGUI electricAmmoText;

    [Header("Settings")]
    public float projectileSpeed = 25f;

    private void Start()
    {
        UpdateAmmoUI();
    }

    // Helper method to check if an element is at max capacity
    public bool IsElementFull(ElementType type)
    {
        switch (type)
        {
            case ElementType.Flame:
                return flameAmmo >= maxAmmoPerElement;
            case ElementType.Water:
                return waterAmmo >= maxAmmoPerElement;
            case ElementType.Electric:
                return electricAmmo >= maxAmmoPerElement;
            default:
                return false;
        }
    }

    // Called by ElementalSpellBook when a correct rune is solved
    public void AddElementAmmo(ElementType type, int amount)
    {
        switch (type)
        {
            case ElementType.Flame:
                flameAmmo = Mathf.Min(flameAmmo + amount, maxAmmoPerElement);
                break;
            case ElementType.Water:
                waterAmmo = Mathf.Min(waterAmmo + amount, maxAmmoPerElement);
                break;
            case ElementType.Electric:
                electricAmmo = Mathf.Min(electricAmmo + amount, maxAmmoPerElement);
                break;
        }

        UpdateAmmoUI();
    }

    // Called by central CAST / Fire Button
    public void TryCastSpell()
    {
        if (spellBook == null) return;

        ElementType activeElement = spellBook.CurrentElementType;
        GameObject prefabToSpawn = null;

        // Check and deduct element-specific ammo
        switch (activeElement)
        {
            case ElementType.Flame:
                if (flameAmmo <= 0) return;
                flameAmmo--;
                prefabToSpawn = flameSpellPrefab;
                break;

            case ElementType.Water:
                if (waterAmmo <= 0) return;
                waterAmmo--;
                prefabToSpawn = waterSpellPrefab;
                break;

            case ElementType.Electric:
                if (electricAmmo <= 0) return;
                electricAmmo--;
                prefabToSpawn = electricSpellPrefab;
                break;
        }

        UpdateAmmoUI();

        // Spawn projectile
        if (prefabToSpawn != null && spellSpawnPoint != null)
        {
            GameObject spellObj = Instantiate(prefabToSpawn, spellSpawnPoint.position, spellSpawnPoint.rotation);
            Rigidbody rb = spellObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = spellSpawnPoint.forward * projectileSpeed;
            }
        }

        // Refresh SpellBook UI so the dial unlocks immediately if it was previously full
        if (spellBook != null)
        {
            spellBook.UpdateActivePageDisplay();
        }
    }

    public void UpdateAmmoUI()
    {
        if (flameAmmoText != null)
            flameAmmoText.text = $"<color=#FF4500>FLAME:</color> {flameAmmo}/{maxAmmoPerElement}";

        if (waterAmmoText != null)
            waterAmmoText.text = $"<color=#00FFFF>WATER:</color> {waterAmmo}/{maxAmmoPerElement}";

        if (electricAmmoText != null)
            electricAmmoText.text = $"<color=#FFD700>ELECTRIC:</color> {electricAmmo}/{maxAmmoPerElement}";
    }
}
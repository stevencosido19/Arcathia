using UnityEngine;
using TMPro;

public class MagicShooter : MonoBehaviour
{
    [Header("References")]
    public Transform castPoint;
    public GameObject flameProjectilePrefab;
    public TextMeshProUGUI ammoText;

    [Header("Mechanic State")]
    public int currentAmmo = 0;
    public float fireRate = 0.3f;
    private float nextFireTime = 0f;

    public void AddAmmo(int amount)
    {
        currentAmmo += amount;
        UpdateAmmoUI();
    }

    public void ShootSpell()
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("Out of magic! Solve an equation to reload.");
            return;
        }

        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireRate;

        if (flameProjectilePrefab != null && castPoint != null)
        {
            Instantiate(flameProjectilePrefab, castPoint.position, castPoint.rotation);
            currentAmmo--;
            UpdateAmmoUI();
        }
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null)
            ammoText.text = $"Flame Ammo: {currentAmmo}";
    }
}
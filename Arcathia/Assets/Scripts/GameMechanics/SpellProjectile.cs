using UnityEngine;

public class SpellProjectile : MonoBehaviour
{
    [Header("Settings")]
    public ElementType elementType;
    public float damage = 25f;
    public float lifetime = 5f;

    [Header("Impact Visual FX (Optional)")]
    public GameObject impactVFX;

    private void Start()
    {
        // Destroy projectile automatically if it misses everything
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Ignore collision with the player character
        if (collision.gameObject.CompareTag("Player"))
            return;

        // Instantiate impact particles if assigned
        if (impactVFX != null)
        {
            GameObject fx = Instantiate(impactVFX, transform.position, Quaternion.LookRotation(collision.contacts[0].normal));
            Destroy(fx, 2f);
        }

        // Destroy the spell projectile on impact
        Destroy(gameObject);
    }
}
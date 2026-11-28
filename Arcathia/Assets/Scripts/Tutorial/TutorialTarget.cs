using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TutorialTarget : MonoBehaviour
{
    [Header("Optional Hit Feedback")]
    public GameObject destroyVfxPrefab;
    public AudioClip hitSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    private bool isDestroyed = false;

    private void OnEnable()
    {
        isDestroyed = false;
    }

    public void OnHitByMagic()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (destroyVfxPrefab != null)
        {
            Instantiate(destroyVfxPrefab, transform.position, Quaternion.identity);
        }

        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position, soundVolume);
        }

        TutorialManager.Instance?.NotifyTargetDestroyed();
        gameObject.SetActive(false);
    }
}
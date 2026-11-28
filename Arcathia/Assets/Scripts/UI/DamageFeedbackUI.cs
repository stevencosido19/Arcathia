using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class DamageFeedbackUI : MonoBehaviour
{
    public static DamageFeedbackUI Instance { get; private set; }

    [Header("Fade Settings")]
    [Tooltip("Peak opacity when hit (0 = invisible, 1 = fully visible).")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.85f;

    [Tooltip("How fast the damage UI flashes in (seconds).")]
    public float fadeInDuration = 0.08f;

    [Tooltip("How long it stays at peak opacity before fading out.")]
    public float holdDuration = 0.05f;

    [Tooltip("How fast the damage UI fades back to invisible (seconds).")]
    public float fadeOutDuration = 0.6f;

    private CanvasGroup canvasGroup;
    private Coroutine flashRoutine;

    private void Awake()
    {
        // Only register as global singleton if it's in the Scene Canvas (not inside a Player Prefab)
        if (GetComponentInParent<NetworkObject>() == null)
        {
            Instance = this;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false; // Never block movement/spellbook touches
        canvasGroup.interactable = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TriggerDamageFlash()
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    public void HideImmediate()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private IEnumerator FlashRoutine()
    {
        // 1. Fade In fast (ramps smoothly from current alpha if hit repeatedly)
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeInDuration > 0f ? Mathf.Clamp01(elapsed / fadeInDuration) : 1f;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, maxAlpha, t);
            yield return null;
        }
        canvasGroup.alpha = maxAlpha;

        // 2. Brief Hold
        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        // 3. Fade Out smoothly
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeOutDuration > 0f ? Mathf.Clamp01(elapsed / fadeOutDuration) : 1f;
            canvasGroup.alpha = Mathf.Lerp(maxAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        flashRoutine = null;
    }
}
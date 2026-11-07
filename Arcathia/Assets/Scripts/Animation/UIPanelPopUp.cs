using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class UIPanelPopUp : MonoBehaviour
{
    public enum AnimationStyle
    {
        ScaleBouncy,
        SlideFromBottom,
        SlideFromTop,
        SlideFromLeft,
        SlideFromRight,
        SquashAndStretch,
        FadeOnly
    }

    [Header("Animation Style")]
    [Tooltip("Choose the entrance/exit visual style.")]
    public AnimationStyle animStyle = AnimationStyle.ScaleBouncy;

    [Header("Pre-Open & Delay Settings")]
    [Tooltip("Delay in seconds before the entrance animation starts playing.")]
    public float startDelay = 0.0f;

    [Tooltip("If TRUE, briefly squashes/pulls back before bursting open (anticipation effect).")]
    public bool enableAnticipation = false;
    public float anticipationDuration = 0.1f;

    [Header("Open Timing & Curves")]
    public float openDuration = 0.28f;

    [Tooltip("Curve used for Scale/Slide when opening. Pre-configured with punchy bounce overshoot!")]
    public AnimationCurve openCurve = new AnimationCurve(
        new Keyframe(0.0f, 0.0f, 0.0f, 3.2f),
        new Keyframe(0.72f, 1.08f, 0.3f, 0.3f),
        new Keyframe(1.0f, 1.0f, -0.2f, 0.0f)
    );

    [Header("Close Timing & Curves")]
    public float closeDuration = 0.18f;
    public AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Slide Settings")]
    [Tooltip("Distance in pixels the panel travels during slide animations.")]
    public float slideDistance = 600f;

    [Header("Fade & Transparency")]
    public bool useFade = true;

    [Header("Optional Background Dimmer")]
    [Tooltip("Optional dark overlay/image behind the panel that fades in first.")]
    public CanvasGroup backgroundDimmer;
    [Range(0f, 1f)]
    public float dimmerTargetAlpha = 0.7f;
    public float dimmerFadeDuration = 0.2f;

    [Header("Audio Effects (Optional)")]
    public AudioSource audioSource;
    public AudioClip openSfx;
    public AudioClip closeSfx;

    [Header("Events")]
    public UnityEvent onOpenStart;
    public UnityEvent onOpenComplete;
    public UnityEvent onCloseComplete;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 defaultAnchoredPos;
    private Coroutine activeAnimRoutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        defaultAnchoredPos = rectTransform.anchoredPosition;

        if (useFade)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        // Triggers automatically whenever SetActive(true) is called
        if (activeAnimRoutine != null) StopCoroutine(activeAnimRoutine);
        activeAnimRoutine = StartCoroutine(AnimateOpenRoutine());
    }

    private void OnDisable()
    {
        if (activeAnimRoutine != null)
        {
            StopCoroutine(activeAnimRoutine);
            activeAnimRoutine = null;
        }

        ResetTransformToDefault();
    }

    // --- ENTRANCE ANIMATION ---

    private IEnumerator AnimateOpenRoutine()
    {
        PrepareInitialState();

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        onOpenStart?.Invoke();
        PlaySound(openSfx);

        if (backgroundDimmer != null)
        {
            StartCoroutine(FadeDimmerRoutine(0f, dimmerTargetAlpha, dimmerFadeDuration));
        }

        // Optional: Anticipation "wind-up" before opening
        if (enableAnticipation && animStyle == AnimationStyle.ScaleBouncy)
        {
            float antElapsed = 0f;
            while (antElapsed < anticipationDuration)
            {
                antElapsed += Time.unscaledDeltaTime;
                float progress = antElapsed / anticipationDuration;
                rectTransform.localScale = new Vector3(Mathf.Lerp(0f, 1.15f, progress), Mathf.Lerp(0f, 0.45f, progress), 1f);
                yield return null;
            }
        }

        float elapsed = 0f;
        Vector2 startSlidePos = GetSlideStartPosition();

        while (elapsed < openDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / openDuration);
            float curveValue = openCurve.Evaluate(t);

            ApplyOpenTransform(curveValue, t, startSlidePos);

            yield return null;
        }

        ResetTransformToDefault();
        onOpenComplete?.Invoke();
        activeAnimRoutine = null;
    }

    private void PrepareInitialState()
    {
        if (canvasGroup != null && useFade) canvasGroup.alpha = 0f;

        switch (animStyle)
        {
            case AnimationStyle.ScaleBouncy:
            case AnimationStyle.SquashAndStretch:
            case AnimationStyle.FadeOnly:
                rectTransform.localScale = Vector3.zero;
                rectTransform.anchoredPosition = defaultAnchoredPos;
                break;

            case AnimationStyle.SlideFromBottom:
            case AnimationStyle.SlideFromTop:
            case AnimationStyle.SlideFromLeft:
            case AnimationStyle.SlideFromRight:
                rectTransform.localScale = Vector3.one;
                rectTransform.anchoredPosition = GetSlideStartPosition();
                break;
        }
    }

    private Vector2 GetSlideStartPosition()
    {
        switch (animStyle)
        {
            case AnimationStyle.SlideFromBottom: return defaultAnchoredPos - new Vector2(0f, slideDistance);
            case AnimationStyle.SlideFromTop: return defaultAnchoredPos + new Vector2(0f, slideDistance);
            case AnimationStyle.SlideFromLeft: return defaultAnchoredPos - new Vector2(slideDistance, 0f);
            case AnimationStyle.SlideFromRight: return defaultAnchoredPos + new Vector2(slideDistance, 0f);
            default: return defaultAnchoredPos;
        }
    }

    private void ApplyOpenTransform(float curveVal, float linearT, Vector2 startSlidePos)
    {
        if (canvasGroup != null && useFade)
        {
            canvasGroup.alpha = Mathf.Clamp01(linearT * 1.4f);
        }

        switch (animStyle)
        {
            case AnimationStyle.ScaleBouncy:
                rectTransform.localScale = Vector3.one * curveVal;
                break;

            case AnimationStyle.SquashAndStretch:
                float scaleX = Mathf.Lerp(0.5f, 1f, curveVal);
                float scaleY = curveVal;
                rectTransform.localScale = new Vector3(scaleX, scaleY, 1f);
                break;

            case AnimationStyle.SlideFromBottom:
            case AnimationStyle.SlideFromTop:
            case AnimationStyle.SlideFromLeft:
            case AnimationStyle.SlideFromRight:
                rectTransform.anchoredPosition = Vector2.LerpUnclamped(startSlidePos, defaultAnchoredPos, curveVal);
                break;

            case AnimationStyle.FadeOnly:
                rectTransform.localScale = Vector3.one;
                break;
        }
    }

    // --- EXIT ANIMATIONS ---

    /// <summary>
    /// Smoothly animates out and turns off the GameObject.
    /// Called automatically by MainMenuManager when switching panels!
    /// </summary>
    public void ClosePanel()
    {
        if (gameObject.activeInHierarchy)
        {
            if (activeAnimRoutine != null) StopCoroutine(activeAnimRoutine);
            activeAnimRoutine = StartCoroutine(AnimateCloseRoutine());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateCloseRoutine()
    {
        PlaySound(closeSfx);

        if (backgroundDimmer != null)
        {
            StartCoroutine(FadeDimmerRoutine(backgroundDimmer.alpha, 0f, closeDuration));
        }

        float elapsed = 0f;
        Vector3 startScale = rectTransform.localScale;
        Vector2 startSlidePos = rectTransform.anchoredPosition;
        Vector2 targetSlideExit = GetSlideStartPosition();
        float startAlpha = (canvasGroup != null) ? canvasGroup.alpha : 1f;

        while (elapsed < closeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / closeDuration);
            float curveVal = closeCurve.Evaluate(t);

            if (canvasGroup != null && useFade)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            }

            switch (animStyle)
            {
                case AnimationStyle.ScaleBouncy:
                case AnimationStyle.SquashAndStretch:
                    rectTransform.localScale = startScale * curveVal;
                    break;

                case AnimationStyle.SlideFromBottom:
                case AnimationStyle.SlideFromTop:
                case AnimationStyle.SlideFromLeft:
                case AnimationStyle.SlideFromRight:
                    rectTransform.anchoredPosition = Vector2.Lerp(startSlidePos, targetSlideExit, t);
                    break;
            }

            yield return null;
        }

        onCloseComplete?.Invoke();
        ResetTransformToDefault();
        gameObject.SetActive(false); // Turns off cleanly once animation finishes
        activeAnimRoutine = null;
    }

    private IEnumerator FadeDimmerRoutine(float start, float target, float duration)
    {
        if (backgroundDimmer == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            backgroundDimmer.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        backgroundDimmer.alpha = target;
    }

    private void ResetTransformToDefault()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.anchoredPosition = defaultAnchoredPos;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }
    }
}
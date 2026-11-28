using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HoverButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    public enum ClickTriggerMode
    {
        OnPressDown,    // Instant! Plays the moment your finger/mouse touches the button
        OnClickRelease  // Plays only after releasing the button
    }

    [Header("Zoom / Scale Settings")]
    [Tooltip("Target scale when hovered (e.g. 1.1 = 10% bigger).")]
    public float hoverScaleMultiplier = 1.12f;

    [Tooltip("How fast it zooms in and out.")]
    public float scaleSpeed = 12f;

    [Header("Shake Settings")]
    [Tooltip("Pixel distance of the shake/jitter.")]
    public float shakeStrength = 3.5f;

    [Tooltip("How fast the shake vibrates.")]
    public float shakeSpeed = 35f;

    [Header("Audio Settings")]
    [Tooltip("Sound played every time the button is clicked/tapped.")]
    public AudioClip clickSound;
    [Range(0f, 1f)]
    public float clickVolume = 1f;

    [Tooltip("(Optional) Sound played when hovering over the button. Leave empty for click-only.")]
    public AudioClip hoverSound;
    [Range(0f, 1f)]
    public float hoverVolume = 0.4f;

    [Header("Audio Timing & Adjustment")]
    [Tooltip("OnPressDown fixes the 'release lag'. OnClickRelease waits until you let go.")]
    public ClickTriggerMode triggerMode = ClickTriggerMode.OnPressDown;

    [Tooltip("Skips silent padding at the start of the audio file (in seconds). Increase slightly (e.g. 0.05 - 0.15) if your clip feels late!")]
    [Range(0f, 0.5f)]
    public float clipStartOffset = 0f;

    [Tooltip("Optional intentional delay (in seconds) before the click sound plays.")]
    [Range(0f, 1f)]
    public float soundDelay = 0f;

    [Tooltip("Adjust playback pitch/speed (1 = normal, 1.1 = slightly snappier).")]
    [Range(0.5f, 2f)]
    public float pitch = 1f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private RectTransform rectTransform;
    private Button button;
    private Vector3 originalScale;
    private Vector2 originalPosition;

    private Coroutine animationRoutine;
    private bool isHovered = false;
    private float noiseSeed;

    private static AudioSource sharedUiAudioSource;
    private static UiSoundRunner sharedRunner;

    // Hidden helper so delayed sounds still finish even if the button/panel closes immediately
    private class UiSoundRunner : MonoBehaviour { }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        originalScale = rectTransform.localScale;
        originalPosition = rectTransform.anchoredPosition;
        noiseSeed = Random.value * 100f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;

        isHovered = true;
        StartAnimation();

        if (hoverSound != null)
        {
            PlayUiSound(hoverSound, hoverVolume, 0f, 0f, 1f);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        StartAnimation();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (triggerMode != ClickTriggerMode.OnPressDown) return;
        TriggerClickSound();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (triggerMode != ClickTriggerMode.OnClickRelease) return;
        TriggerClickSound();
    }

    private void TriggerClickSound()
    {
        if (button != null && !button.interactable) return;

        if (clickSound == null)
        {
            if (showDebugLogs)
                Debug.LogWarning($"[HoverButtonEffect] '{gameObject.name}' was clicked, but 'Click Sound' is empty!", gameObject);
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[HoverButtonEffect] Playing '{clickSound.name}' on '{gameObject.name}' (Offset: {clipStartOffset}s, Delay: {soundDelay}s)");

        PlayUiSound(clickSound, clickVolume, clipStartOffset, soundDelay, pitch);
    }

    private static void EnsureAudioHost()
    {
        if (sharedUiAudioSource != null && sharedRunner != null) return;

        GameObject sfxObj = new GameObject("UI_SharedSFX");
        DontDestroyOnLoad(sfxObj);

        sharedUiAudioSource = sfxObj.AddComponent<AudioSource>();
        sharedUiAudioSource.playOnAwake = false;
        sharedUiAudioSource.spatialBlend = 0f;
        sharedUiAudioSource.ignoreListenerPause = true;

        sharedRunner = sfxObj.AddComponent<UiSoundRunner>();
    }

    private static void PlayUiSound(AudioClip clip, float volume, float startOffset, float delay, float soundPitch)
    {
        if (clip == null) return;
        EnsureAudioHost();

        if (delay > 0.001f)
        {
            sharedRunner.StartCoroutine(PlayDelayedRoutine(clip, volume, startOffset, delay, soundPitch));
        }
        else
        {
            PlayClipNow(clip, volume, startOffset, soundPitch);
        }
    }

    private static IEnumerator PlayDelayedRoutine(AudioClip clip, float volume, float startOffset, float delay, float soundPitch)
    {
        yield return new WaitForSecondsRealtime(delay);
        PlayClipNow(clip, volume, startOffset, soundPitch);
    }

    private static void PlayClipNow(AudioClip clip, float volume, float startOffset, float soundPitch)
    {
        if (sharedUiAudioSource == null || clip == null) return;

        sharedUiAudioSource.pitch = soundPitch;

        if (startOffset > 0.001f && startOffset < clip.length - 0.01f)
        {
            // PlayOneShot always starts at 0.0s, so use .clip + .time + .Play() to trim leading silence
            sharedUiAudioSource.Stop();
            sharedUiAudioSource.clip = clip;
            sharedUiAudioSource.time = startOffset;
            sharedUiAudioSource.volume = volume;
            sharedUiAudioSource.Play();
        }
        else
        {
            sharedUiAudioSource.volume = 1f;
            sharedUiAudioSource.PlayOneShot(clip, volume);
        }
    }

    private void OnDisable()
    {
        isHovered = false;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        ResetToDefaults();
    }

    private void StartAnimation()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        if (gameObject.activeInHierarchy)
        {
            animationRoutine = StartCoroutine(AnimateButtonRoutine());
        }
    }

    private IEnumerator AnimateButtonRoutine()
    {
        Vector3 targetScale = originalScale * hoverScaleMultiplier;

        while (true)
        {
            if (isHovered)
            {
                rectTransform.localScale = Vector3.Lerp(
                    rectTransform.localScale,
                    targetScale,
                    Time.unscaledDeltaTime * scaleSpeed
                );

                float noiseX = (Mathf.PerlinNoise(Time.unscaledTime * shakeSpeed, noiseSeed) - 0.5f) * 2f;
                float noiseY = (Mathf.PerlinNoise(noiseSeed, Time.unscaledTime * shakeSpeed) - 0.5f) * 2f;
                rectTransform.anchoredPosition = originalPosition + new Vector2(noiseX, noiseY) * shakeStrength;
            }
            else
            {
                rectTransform.localScale = Vector3.Lerp(
                    rectTransform.localScale,
                    originalScale,
                    Time.unscaledDeltaTime * scaleSpeed
                );

                rectTransform.anchoredPosition = Vector2.Lerp(
                    rectTransform.anchoredPosition,
                    originalPosition,
                    Time.unscaledDeltaTime * scaleSpeed
                );

                if (Vector3.SqrMagnitude(rectTransform.localScale - originalScale) < 0.0001f &&
                    Vector2.SqrMagnitude(rectTransform.anchoredPosition - originalPosition) < 0.01f)
                {
                    ResetToDefaults();
                    animationRoutine = null;
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void ResetToDefaults()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale;
            rectTransform.anchoredPosition = originalPosition;
        }
    }
}
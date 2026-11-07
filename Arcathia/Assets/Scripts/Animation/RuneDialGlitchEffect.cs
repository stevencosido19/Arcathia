using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class RuneDialGlitchEffect : MonoBehaviour
{
    [Header("Image Target & Sprites")]
    [Tooltip("The Image component of the dial container. If left empty, it will auto-detect on this object or children.")]
    public Image dialImage;

    [Tooltip("Your normal, default rune dial container sprite.")]
    public Sprite defaultSprite;

    [Tooltip("The glitch effect sprite.")]
    public Sprite glitchSprite;

    [Header("Interval & Duration Settings")]
    [Tooltip("Seconds between glitch events.")]
    public float glitchInterval = 2.0f;

    [Tooltip("How long the glitch/shake lasts in seconds.")]
    public float glitchDuration = 0.25f;

    [Tooltip("Add a slight random variation to the interval so it feels more natural/unstable.")]
    public bool randomizeInterval = true;
    public float intervalRandomSpread = 0.5f;

    [Header("Shake Settings")]
    [Tooltip("Maximum pixel offset during the shake.")]
    public float shakeStrength = 7.0f;

    [Header("Flicker Settings")]
    [Tooltip("How rapidly (in seconds) it flickers between default and glitch sprite during the shake.")]
    public float flickerSpeed = 0.04f;

    private RectTransform rectTransform;
    private Vector2 currentBasePosition; // Captured dynamically so it respects custom HUD layouts!
    private Coroutine glitchLoopCoroutine;
    private bool isGlitching = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (dialImage == null)
        {
            dialImage = GetComponent<Image>();
            if (dialImage == null)
            {
                dialImage = GetComponentInChildren<Image>();
            }
        }
    }

    private void OnEnable()
    {
        ResetSprite();
        glitchLoopCoroutine = StartCoroutine(GlitchLoopRoutine());
    }

    private void OnDisable()
    {
        if (glitchLoopCoroutine != null)
        {
            StopCoroutine(glitchLoopCoroutine);
            glitchLoopCoroutine = null;
        }

        // If disabled mid-shake, restore the custom position immediately
        if (isGlitching && rectTransform != null)
        {
            rectTransform.anchoredPosition = currentBasePosition;
            isGlitching = false;
        }

        ResetSprite();
    }

    private IEnumerator GlitchLoopRoutine()
    {
        while (true)
        {
            float waitTime = glitchInterval;
            if (randomizeInterval)
            {
                waitTime += Random.Range(-intervalRandomSpread, intervalRandomSpread);
                waitTime = Mathf.Max(0.2f, waitTime);
            }

            yield return new WaitForSeconds(waitTime);

            if (!isGlitching)
            {
                yield return StartCoroutine(PerformGlitchRoutine());
            }
        }
    }

    public void TriggerGlitch()
    {
        if (gameObject.activeInHierarchy && !isGlitching)
        {
            StartCoroutine(PerformGlitchRoutine());
        }
    }

    private IEnumerator PerformGlitchRoutine()
    {
        isGlitching = true;

        // CRITICAL FIX: Capture wherever the dial is currently positioned right now!
        // This preserves your custom HUD layout completely.
        currentBasePosition = rectTransform.anchoredPosition;

        float elapsed = 0f;
        float flickerTimer = 0f;
        bool showGlitch = true;

        // Set initial glitch sprite immediately
        if (dialImage != null && glitchSprite != null)
        {
            dialImage.sprite = glitchSprite;
            dialImage.overrideSprite = glitchSprite;
        }

        while (elapsed < glitchDuration)
        {
            elapsed += Time.deltaTime;
            flickerTimer += Time.deltaTime;

            // 1. Shake around the CURRENT custom position
            Vector2 randomOffset = Random.insideUnitCircle * shakeStrength;
            rectTransform.anchoredPosition = currentBasePosition + randomOffset;

            // 2. Flicker sprites
            if (flickerTimer >= flickerSpeed)
            {
                flickerTimer = 0f;
                showGlitch = !showGlitch;

                if (dialImage != null)
                {
                    Sprite target = showGlitch ? glitchSprite : defaultSprite;
                    dialImage.sprite = target;
                    dialImage.overrideSprite = target;
                }
            }

            yield return null;
        }

        // Return to the custom position and default sprite
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = currentBasePosition;
        }

        ResetSprite();
        isGlitching = false;
    }

    private void ResetSprite()
    {
        if (dialImage != null && defaultSprite != null)
        {
            dialImage.sprite = defaultSprite;
            dialImage.overrideSprite = defaultSprite;
        }
    }
}
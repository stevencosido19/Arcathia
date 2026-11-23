using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class HoverButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
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

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Vector2 originalPosition;

    private Coroutine animationRoutine;
    private bool isHovered = false;
    private float noiseSeed;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        originalPosition = rectTransform.anchoredPosition;
        noiseSeed = Random.value * 100f; 
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        StartAnimation();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        StartAnimation();
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
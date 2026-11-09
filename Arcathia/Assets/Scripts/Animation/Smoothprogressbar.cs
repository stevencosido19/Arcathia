using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Smoothly animates a loading bar toward a target value.
/// Even if the target jumps from 0 to 1 instantly, the fill slides (speed is capped by Min Full Duration).
/// Works with an Image (Image Type = Filled) and/or a Slider. Uses unscaled time, so it still
/// animates when Time.timeScale = 0 (typical on loading screens).
///
/// Usage:  bar.SetTarget(progress01);   // call as often / as jumpy as you like
/// </summary>
[DisallowMultipleComponent]
public class SmoothProgressBar : MonoBehaviour
{
    [Header("Fill Targets (use either or both)")]
    [Tooltip("Image with Image Type = Filled")]
    public Image fillImage;
    public Slider slider;

    [Header("Smoothing")]
    [Tooltip("Higher = softer ease in/out")]
    [Range(0.01f, 2f)] public float smoothTime = 0.25f;
    [Tooltip("Slowest time the bar may take to go from 0% to 100%. This is what makes an instant 0->100 jump still animate.")]
    public float minFullDuration = 1.2f;
    public bool useUnscaledTime = true;
    [Tooltip("Ignore targets lower than the current target (loading should never go backwards)")]
    public bool neverGoBackwards = true;

    [Header("Events")]
    public UnityEvent<float> onValueChanged;
    public UnityEvent onFilled;

    float current;
    float target;
    float velocity;
    bool filledFired;

    /// <summary>Value currently shown (0-1)</summary>
    public float Value => current;
    /// <summary>Value the bar is sliding toward (0-1)</summary>
    public float Target => target;
    /// <summary>True once the bar has visually reached 100%</summary>
    public bool IsFull => current >= 1f;

    void Awake()
    {
        if (fillImage && fillImage.type != Image.Type.Filled)
            Debug.LogWarning("[SmoothProgressBar] fillImage must have Image Type = Filled (Fill Method = Horizontal) for fillAmount to work.", fillImage);
        Apply();
    }

    /// <summary>Set where the bar should slide to (0-1). Safe to call every frame or in big jumps.</summary>
    public void SetTarget(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        if (neverGoBackwards && value01 < target) return;
        target = value01;
        if (target < 1f) filledFired = false;
    }

    /// <summary>Jump to a value immediately, no animation.</summary>
    public void SetValueInstant(float value01)
    {
        current = target = Mathf.Clamp01(value01);
        velocity = 0f;
        filledFired = current >= 1f;
        Apply();
    }

    /// <summary>Back to 0%, no animation. Call before reusing the bar.</summary>
    public void ResetBar()
    {
        current = target = 0f;
        velocity = 0f;
        filledFired = false;
        Apply();
    }

    /// <summary>Use in a coroutine: yield return bar.WaitUntilFull();</summary>
    public System.Collections.IEnumerator WaitUntilFull()
    {
        while (!IsFull) yield return null;
    }

    void Update()
    {
        if (current == target) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float maxSpeed = 1f / Mathf.Max(0.01f, minFullDuration);

        current = Mathf.SmoothDamp(current, target, ref velocity, smoothTime, maxSpeed, dt);

        // SmoothDamp approaches the target forever, so snap when close enough
        if (Mathf.Abs(target - current) < 0.002f)
        {
            current = target;
            velocity = 0f;
        }

        Apply();

        if (current >= 1f && !filledFired)
        {
            filledFired = true;
            onFilled?.Invoke();
        }
    }

    void Apply()
    {
        if (fillImage) fillImage.fillAmount = current;
        if (slider) slider.SetValueWithoutNotify(Mathf.Lerp(slider.minValue, slider.maxValue, current));
        onValueChanged?.Invoke(current);
    }
}
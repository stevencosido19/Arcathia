using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


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

    
    public float Value => current;
    
    public float Target => target;
    
    public bool IsFull => current >= 1f;

    void Awake()
    {
        if (fillImage && fillImage.type != Image.Type.Filled)
            Debug.LogWarning("[SmoothProgressBar] fillImage must have Image Type = Filled (Fill Method = Horizontal) for fillAmount to work.", fillImage);
        Apply();
    }

    
    public void SetTarget(float value01)
    {
        value01 = Mathf.Clamp01(value01);
        if (neverGoBackwards && value01 < target) return;
        target = value01;
        if (target < 1f) filledFired = false;
    }

    
    public void SetValueInstant(float value01)
    {
        current = target = Mathf.Clamp01(value01);
        velocity = 0f;
        filledFired = current >= 1f;
        Apply();
    }

    
    public void ResetBar()
    {
        current = target = 0f;
        velocity = 0f;
        filledFired = false;
        Apply();
    }

    
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
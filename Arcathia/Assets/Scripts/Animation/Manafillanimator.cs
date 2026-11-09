using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum ManaElement { Fire, Water, Lightning }

/// <summary>
/// Put this on each mana fill Image (Image Type = Filled). Call SetTarget(current / max) whenever ammo changes.
/// Each element behaves like itself:
///   WATER     - the fill SLOSHES: springs past the target and settles with wobble, gentle bobbing, cool shimmer.
///   FIRE      - the fill FLARES UP fast, then flickers (Perlin) between orange and hot yellow, flame-licks in height.
///   LIGHTNING - the fill ZAPS up in snappy random steps, crackles with white-blue flashes and random arc flickers.
/// All three get a flash + scale pop on gain, and a smaller flash when mana is spent.
/// Optional: UI/ElementalMana shader adds surface effects (ripples, flame distortion, crackle lines).
/// Uses unscaled time, so it animates even when the game is paused.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ManaFillAnimator : MonoBehaviour
{
    [Header("Element")]
    public ManaElement element = ManaElement.Fire;

    [Header("Optional shader (UI/ElementalMana)")]
    public bool useShader = true;
    [Tooltip("Auto-filled when you add the component in the editor. Assign manually if the shader was imported later.")]
    public Shader elementShader;
    [Range(0f, 3f)] public float shaderDistortion = 1f;
    [Range(0f, 3f)] public float shaderSpeed = 1f;
    [Range(0f, 3f)] public float shaderIntensity = 1f;

    [Header("Water: slosh")]
    [Tooltip("Spring speed. Lower = slower, heavier slosh")]
    public float waterFrequency = 2.2f;
    [Tooltip("Lower = more wobble / overshoot")]
    [Range(0.05f, 1f)] public float waterDamping = 0.30f;
    public float waterBobAmount = 0.008f;
    public float waterBobSpeed = 2.4f;

    [Header("Fire: flare + flicker")]
    [Tooltip("Seconds to rise to the new value (small = flares up)")]
    public float fireRiseTime = 0.14f;
    [Range(0f, 0.5f)] public float fireFlickerAmount = 0.18f;
    public float fireFlickerSpeed = 11f;

    [Header("Lightning: stepped + crackle")]
    public float lightningStepsPerSecond = 18f;
    [Range(0f, 1f)] public float lightningFlickerAmount = 0.25f;
    [Tooltip("Random seconds between idle arc flashes")]
    public Vector2 lightningArcInterval = new Vector2(0.4f, 1.4f);

    [Header("Debug")]
    [Tooltip("Logs SetTarget calls and setup problems to the Console")]
    public bool debugLogs = false;

    [Header("Performance")]
    [Tooltip("Only matters when the shader is NOT used. Code-driven idle motion (flicker / bobbing) dirties the Canvas every frame. Turn off for the lowest cost.")]
    public bool animateWhileIdle = true;

    [Header("Gain / Spend Feedback")]
    public float flashDecay = 4f;
    [Tooltip("Scale pop on gain. Skipped automatically while HUD layout editing is active.")]
    public bool useScalePop = true;
    public float popAmount = 0.12f;
    public float popReturnTime = 0.18f;
    public UnityEvent onGain;
    public UnityEvent onSpend;

    // ---- state ----
    Image fill;
    Material mat;
    Color baseColor;
    float target, value, vel;
    float flash, arcFlash, pop, popVel;
    float time;
    bool idleApplied;
    bool targetAssigned;   // SetTarget may be called BEFORE Awake if the object started inactive

    // lightning
    float stepTimer, arcTimer, stepFlicker = 1f;

    // scale pop bookkeeping (robust to the HUD layout editor changing the scale)
    Vector3 appliedScaleFactor = Vector3.one;

    static readonly int FlashId = Shader.PropertyToID("_Flash");

    public float Value => value;
    public float Target => target;

    void Reset()
    {
#if UNITY_EDITOR
        elementShader = Shader.Find("UI/ElementalMana");
#endif
    }

    void Awake()
    {
        fill = GetComponent<Image>();
        baseColor = fill.color;
        arcTimer = Random.Range(lightningArcInterval.x, lightningArcInterval.y);

        // an invisible (alpha 0) base colour would keep the fill invisible forever
        if (baseColor.a < 0.01f)
        {
            Debug.LogWarning("[ManaFillAnimator] Image colour alpha was 0, using 1 so the fill is visible.", this);
            baseColor.a = 1f;
        }
        if (fill.sprite == null)
            Debug.LogWarning("[ManaFillAnimator] The Image has no Source Image. A Filled image needs a sprite, otherwise it always draws full.", this);
        if (fill.type != Image.Type.Filled) fill.type = Image.Type.Filled;

        if (useShader && elementShader == null)
            Debug.LogWarning("[ManaFillAnimator] 'Use Shader' is on but Element Shader is empty -> using code-only effects. Drag UI/ElementalMana into the field.", this);
        else if (useShader && !elementShader.isSupported)
            Debug.LogError("[ManaFillAnimator] UI/ElementalMana is not supported / failed to compile. Check the Console for shader errors. Using code-only effects.", this);

        if (useShader && elementShader != null && elementShader.isSupported)
        {
            mat = new Material(elementShader) { name = "ManaFill_" + element };
            mat.SetFloat("_Element", (float)ElementIndex());
            mat.SetFloat("_Distortion", shaderDistortion);
            mat.SetFloat("_Speed", shaderSpeed);
            mat.SetFloat("_Intensity", shaderIntensity);
            mat.SetColor("_FlashColor", FlashColor());
            fill.material = mat;
        }

        // players start with no mana: start empty instead of the Image's default Fill Amount of 1.
        // If SetTarget() already ran before Awake (object started inactive), keep that target and animate to it.
        value = 0f;
        vel = 0f;
        if (!targetAssigned) target = 0f;
        fill.fillAmount = 0f;
        idleApplied = false;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    void OnDisable()
    {
        ApplyScale(Vector3.one);
    }

    // ============================================================ API

    /// <summary>Set the new fill fraction (current ammo / max ammo). Call whenever ammo changes.</summary>
    public void SetTarget(float fill01, bool instant = false)
    {
        fill01 = Mathf.Clamp01(fill01);
        float prev = target;
        target = fill01;
        targetAssigned = true;

        if (debugLogs)
            Debug.Log($"[ManaFillAnimator:{element}] SetTarget({fill01:F2})  (was {prev:F2}, activeAndEnabled={isActiveAndEnabled})", this);
        if (!isActiveAndEnabled && fill != null)
            Debug.LogWarning($"[ManaFillAnimator:{element}] SetTarget called while this object is inactive/disabled - it will animate when enabled.", this);

        if (instant)
        {
            value = target;
            vel = 0f;
            return;
        }

        if (target > prev + 0.0001f) OnGain();
        else if (target < prev - 0.0001f) OnSpend();
    }

    [ContextMenu("Debug: Print State")]
    void PrintState()
    {
        if (fill == null) fill = GetComponent<Image>();
        Debug.Log(
            $"[ManaFillAnimator:{element}] target={target:F2} value={value:F2}  fillAmount={fill.fillAmount:F2}\n" +
            $"activeAndEnabled={isActiveAndEnabled}  imageEnabled={fill.enabled}  type={fill.type}  fillMethod={fill.fillMethod}\n" +
            $"sprite={(fill.sprite ? fill.sprite.name : "NONE")}  color={fill.color}  scale={transform.localScale}\n" +
            $"material={(fill.material ? fill.material.name : "none")}  shader={(fill.material && fill.material.shader ? fill.material.shader.name : "none")}  usingShaderMaterial={(mat != null)}\n" +
            $"canvas={(fill.canvas ? fill.canvas.name : "NONE (not under a Canvas!)")}  rect={fill.rectTransform.rect.size}  layoutEditing={HUDLayoutManager.IsLayoutEditing}",
            this);
    }

    [ContextMenu("Test: +25%")]
    void TestGain() => SetTarget(target + 0.25f);

    [ContextMenu("Test: -25%")]
    void TestSpend() => SetTarget(target - 0.25f);

    // ======================================================== feedback

    void OnGain()
    {
        flash = 1f;
        pop = 1f;
        popVel = 0f;
        if (element == ManaElement.Lightning) arcFlash = 1f;
        onGain?.Invoke();
    }

    void OnSpend()
    {
        flash = Mathf.Max(flash, 0.5f);
        onSpend?.Invoke();
    }

    // ========================================================= update

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        time += dt;

        bool shaderActive = mat != null;

        // random idle arc flashes for lightning (only while there is visible mana)
        if (element == ManaElement.Lightning && value > 0.001f)
        {
            arcTimer -= dt;
            if (arcTimer <= 0f)
            {
                arcFlash = 1f;
                arcTimer = Random.Range(lightningArcInterval.x, lightningArcInterval.y);
            }
        }

        // ---- IDLE EARLY-OUT: nothing changing -> touch nothing -> the Canvas is not rebuilt.
        // With the shader, idle motion (ripples / flames / crackle) is done on the GPU for free.
        bool settled = Mathf.Abs(target - value) < 0.0005f && Mathf.Abs(vel) < 0.001f
                       && flash <= 0f && arcFlash <= 0f && Mathf.Abs(pop) < 0.001f;
        bool needsAmbient = !shaderActive && animateWhileIdle && value > 0.001f;
        if (settled && !needsAmbient)
        {
            if (!idleApplied)
            {
                value = target;
                vel = 0f;
                pop = 0f;
                ApplyVisual(1f, 1f);
                idleApplied = true;
            }
            return;
        }
        idleApplied = false;

        float brightness = 1f;
        float scaleY = 1f;

        switch (element)
        {
            case ManaElement.Water:
                StepWater(dt);
                break;

            case ManaElement.Fire:
                value = Mathf.SmoothDamp(value, target, ref vel, Mathf.Max(0.01f, fireRiseTime), Mathf.Infinity, dt);
                if (!shaderActive)   // with the shader the flicker is done on the GPU
                {
                    float f = (Mathf.PerlinNoise(time * fireFlickerSpeed, 0.37f) - 0.5f) * 2f;   // -1..1
                    brightness = 1f + f * fireFlickerAmount;
                    scaleY = 1f + f * 0.02f;                                                      // flame lick
                }
                break;

            case ManaElement.Lightning:
                StepLightning(dt, out brightness);
                if (shaderActive) brightness = 1f;
                break;
        }

        // flashes decay
        flash = Mathf.MoveTowards(flash, 0f, flashDecay * dt);
        arcFlash = Mathf.MoveTowards(arcFlash, 0f, 14f * dt);

        // scale pop spring-back
        pop = Mathf.SmoothDamp(pop, 0f, ref popVel, Mathf.Max(0.01f, popReturnTime), Mathf.Infinity, dt);

        ApplyVisual(brightness, scaleY);
    }

    void StepWater(float dt)
    {
        // under-damped spring: overshoots the target and wobbles into place = sloshing water
        float w = 2f * Mathf.PI * waterFrequency;
        float k = w * w;
        float c = 2f * waterDamping * w;

        int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
        float h = dt / steps;
        for (int i = 0; i < steps; i++)
        {
            float acc = k * (target - value) - c * vel;
            vel += acc * h;
            value += vel * h;
        }
    }

    void StepLightning(float dt, out float brightness)
    {
        // snappy random steps toward the target
        stepTimer += dt;
        float stepInterval = 1f / Mathf.Max(1f, lightningStepsPerSecond);
        while (stepTimer >= stepInterval)
        {
            stepTimer -= stepInterval;
            float diff = target - value;
            value += Mathf.Abs(diff) < 0.015f ? diff : diff * Random.Range(0.35f, 1f);
            stepFlicker = 1f + (Random.value - 0.5f) * 2f * lightningFlickerAmount;
        }

        brightness = stepFlicker;
    }

    // ========================================================= visuals

    void ApplyVisual(float brightness, float scaleY)
    {
        float display = element == ManaElement.Water ? WaterDisplay() : value;
        fill.fillAmount = Mathf.Clamp01(display);

        float f = Mathf.Max(flash, arcFlash * 0.6f);

        Color c = new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
        if (mat != null)
        {
            mat.SetFloat(FlashId, f);          // shader adds the flash on top of the sprite
        }
        else
        {
            Color fc = FlashColor();
            c = Color.Lerp(c, new Color(fc.r, fc.g, fc.b, baseColor.a), f * 0.7f);
        }
        fill.color = c;

        // scale pop (skipped while the HUD layout editor is active)
        if (useScalePop && !HUDLayoutManager.IsLayoutEditing)
        {
            float p = pop * popAmount;
            ApplyScale(new Vector3(1f + p * 0.4f, (1f + p) * scaleY, 1f));
        }
        else
        {
            ApplyScale(Vector3.one);
        }
    }

    float WaterDisplay()
    {
        if (mat != null) return value;   // shader already ripples the surface for free

        // gentle bobbing, faded out near empty / full so it never fights the real value
        float edge = Mathf.Clamp01(target * 8f) * Mathf.Clamp01((1f - target) * 8f);
        return value + Mathf.Sin(time * waterBobSpeed) * waterBobAmount * edge;
    }

    // multiply the current scale by 'factor' after undoing last frame's factor
    void ApplyScale(Vector3 factor)
    {
        if (factor == appliedScaleFactor) return;
        Vector3 s = transform.localScale;
        s = new Vector3(s.x / appliedScaleFactor.x, s.y / appliedScaleFactor.y, s.z / appliedScaleFactor.z);
        transform.localScale = Vector3.Scale(s, factor);
        appliedScaleFactor = factor;
    }

    int ElementIndex()
    {
        switch (element)
        {
            case ManaElement.Water: return 0;
            case ManaElement.Fire: return 1;
            default: return 2;
        }
    }

    Color FlashColor()
    {
        switch (element)
        {
            case ManaElement.Water: return new Color(0.70f, 0.95f, 1.00f, 1f);
            case ManaElement.Fire: return new Color(1.00f, 0.75f, 0.30f, 1f);
            default: return new Color(0.85f, 0.92f, 1.00f, 1f);
        }
    }
}
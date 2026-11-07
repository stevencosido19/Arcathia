using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime behaviour for the water-magic projectile. Independent of FireballController, so
/// tweaking one never affects the other.
/// - Grow-in on spawn with a slight overshoot (water "pops" a bit past full size, then settles)
/// - Jelly wobble: x/y scale oscillate out of phase, so the volume feels conserved
/// - Slow turbulence on top, plus optional forward motion
/// No material instances and no MaterialPropertyBlock, so every water ball keeps SRP batching.
/// Local +Z is the travel direction; the tail trails toward -Z.
/// </summary>
[DisallowMultipleComponent]
public class WaterBallController : MonoBehaviour
{
    [Tooltip("The sphere that uses the Stylized/Water Core material. Defaults to the first child.")]
    public Transform core;

    [Header("Motion")]
    [Tooltip("Units per second along transform.forward. 0 = stationary.")]
    [Min(0f)] public float speed = 0f;
    [Tooltip("Extra stretch along the travel axis while moving.")]
    [Range(0f, 0.5f)] public float stretchWithSpeed = 0.2f;

    [Header("Grow-in (on spawn / enable)")]
    [Tooltip("Seconds to grow from the start scale to full size. 0 = no grow-in.")]
    [Min(0f)] public float growDuration = 0.4f;
    [Tooltip("Fraction of full size the water ball starts at.")]
    [Range(0f, 1f)] public float startScale = 0.15f;
    [Tooltip("Values above 1 overshoot full size before settling. Default has a small pop.")]
    public AnimationCurve growCurve = DefaultGrowCurve();
    [Tooltip("Also ramp particle size and emission so the trail builds up with the core.")]
    public bool growParticles = true;

    [Header("Jelly wobble")]
    [Tooltip("How much the body squashes and stretches. Volume-preserving (x and y move opposite).")]
    [Range(0f, 0.3f)] public float wobbleAmount = 0.08f;
    [Min(0f)] public float wobbleFrequency = 7f;
    [Tooltip("Wobble starts strong on launch and settles down over this many seconds. 0 = constant.")]
    [Min(0f)] public float wobbleSettleTime = 0.6f;

    [Header("Turbulence")]
    [Range(0f, 0.2f)] public float turbulenceAmount = 0.03f;
    [Min(0f)] public float turbulenceSpeed = 4f;

    [Header("Launch burst")]
    [Tooltip("Parent of the one-shot splash systems. Auto-found as a child named 'LaunchBurst'. " +
             "It plays at full size on spawn and is excluded from the grow-in.")]
    public Transform launchBurst;

    Vector3 baseScale = Vector3.one;
    float seed;
    float age;
    bool grown;
    bool enabledBefore;

    ParticleSystem[] systems;        // trail systems that take part in the grow-in
    ParticleSystem[] burstSystems;   // one-shot launch effects
    float[] baseSize;
    float[] baseRate;

    static AnimationCurve DefaultGrowCurve()
    {
        var c = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.6f, 1.12f),
            new Keyframe(1f, 1f));
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    void Awake()
    {
        if (core == null && transform.childCount > 0) core = transform.GetChild(0);
        if (core != null) baseScale = core.localScale;
        seed = Random.value * 100f;

        if (launchBurst == null) launchBurst = transform.Find("LaunchBurst");

        var all = GetComponentsInChildren<ParticleSystem>(true);
        var trail = new List<ParticleSystem>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            if (launchBurst != null && all[i].transform.IsChildOf(launchBurst)) continue;
            trail.Add(all[i]);
        }
        systems = trail.ToArray();
        burstSystems = launchBurst != null
            ? launchBurst.GetComponentsInChildren<ParticleSystem>(true)
            : new ParticleSystem[0];

        baseSize = new float[systems.Length];
        baseRate = new float[systems.Length];
        for (int i = 0; i < systems.Length; i++)
        {
            baseSize[i] = systems[i].main.startSizeMultiplier;
            baseRate[i] = systems[i].emission.rateOverTimeMultiplier;
        }
    }

    // Runs every time the object is enabled, before the first rendered frame, so the small
    // starting size is applied immediately (no full-size flash) and pooled reuse works.
    void OnEnable()
    {
        age = 0f;
        grown = growDuration <= 0f;

        if (enabledBefore)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(false);
                systems[i].Play(false);
            }
            for (int i = 0; i < burstSystems.Length; i++)
            {
                burstSystems[i].Clear(false);
                burstSystems[i].Play(false);
            }
        }
        enabledBefore = true;

        if (!grown)
        {
            if (core != null) core.localScale = baseScale * startScale;
            if (growParticles) ApplyParticleScale(startScale);
        }
    }

    void Update()
    {
        if (speed > 0f)
            transform.position += transform.forward * (speed * Time.deltaTime);

        // Clamp so a frame hitch while spawning can't skip the grow-in.
        age += Mathf.Min(Time.deltaTime, 0.05f);

        float grow = 1f;
        if (!grown)
        {
            float t = Mathf.Clamp01(age / growDuration);
            grow = Mathf.LerpUnclamped(startScale, 1f, growCurve.Evaluate(t));
            if (growParticles) ApplyParticleScale(grow);
            if (t >= 1f)
            {
                grown = true;
                if (growParticles) ApplyParticleScale(1f);   // restore exact authored values
            }
        }

        if (core == null) return;

        // Jelly wobble: strong at launch, settling over wobbleSettleTime.
        float settle = wobbleSettleTime > 0f ? Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(age / wobbleSettleTime)) : 1f;
        float w = Mathf.Sin(Time.time * wobbleFrequency + seed) * wobbleAmount * settle;

        // Slow turbulence on top.
        float n = Time.time * turbulenceSpeed;
        float a = Mathf.PerlinNoise(n, seed) * 2f - 1f;
        float b = Mathf.PerlinNoise(seed, n * 1.3f) * 2f - 1f;

        float stretch = speed > 0f ? stretchWithSpeed : 0f;

        core.localScale = new Vector3(
            baseScale.x * (1f + w + a * turbulenceAmount),
            baseScale.y * (1f - w + b * turbulenceAmount),
            baseScale.z * (1f + (a + b) * 0.5f * turbulenceAmount + stretch)) * grow;
    }

    void ApplyParticleScale(float s)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.startSizeMultiplier = baseSize[i] * s;
            var em = systems[i].emission;
            em.rateOverTimeMultiplier = baseRate[i] * s;
        }
    }
}
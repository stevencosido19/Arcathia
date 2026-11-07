using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cheap runtime life for the fireball: grow-in on spawn, noise-driven squash/stretch flicker,
/// and optional forward motion. No material instancing and no MaterialPropertyBlock, so every
/// fireball keeps batching (SRP Batcher). Local +Z is the travel direction; the tail trails toward -Z.
/// </summary>
[DisallowMultipleComponent]
public class FireballController : MonoBehaviour
{
    [Tooltip("The sphere that uses the Stylized/Fireball Core material.")]
    public Transform core;

    [Header("Motion")]
    [Tooltip("Units per second along transform.forward. 0 = stationary.")]
    [Min(0f)] public float speed = 0f;

    [Header("Grow-in (on spawn / enable)")]
    [Tooltip("Seconds to grow from the start scale to full size. 0 = no grow-in.")]
    [Min(0f)] public float growDuration = 0.35f;
    [Tooltip("Fraction of full size the fireball starts at.")]
    [Range(0f, 1f)] public float startScale = 0.15f;
    public AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Also ramp particle size and emission so the trail builds up smoothly.")]
    public bool growParticles = true;

    [Header("Launch burst")]
    [Tooltip("Parent of the one-shot flash/spark systems. Auto-found as a child named 'LaunchBurst'. " +
             "It plays at full size on spawn and is excluded from the grow-in.")]
    public Transform launchBurst;

    [Header("Flicker")]
    [Range(0f, 0.3f)] public float flickerAmount = 0.07f;
    [Min(0f)] public float flickerSpeed = 9f;
    [Tooltip("Extra stretch along the travel axis when moving.")]
    [Range(0f, 0.5f)] public float stretchWithSpeed = 0.15f;

    Vector3 baseScale = Vector3.one;
    float seed;
    float age;
    bool grown;
    bool enabledBefore;

    ParticleSystem[] systems;        // trail systems that take part in the grow-in
    ParticleSystem[] burstSystems;   // one-shot launch effects
    float[] baseSize;
    float[] baseRate;

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

    // Runs every time the object is enabled, so pooled fireballs grow in again on reuse.
    // It also runs before the first rendered frame, so we apply the small starting size
    // right here instead of waiting for Update (which caused a one-frame full-size flash).
    void OnEnable()
    {
        age = 0f;
        grown = growDuration <= 0f;

        if (enabledBefore)
        {
            // Pooled reuse: drop leftover world-space particles from the previous life.
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(false);
                systems[i].Play(false);
            }
            // Replay the flash and sparks for every pooled launch.
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
            grow = Mathf.Lerp(startScale, 1f, growCurve.Evaluate(t));
            if (growParticles) ApplyParticleScale(grow);
            if (t >= 1f)
            {
                grown = true;
                if (growParticles) ApplyParticleScale(1f);   // restore exact authored values
            }
        }

        if (core == null) return;

        float n = Time.time * flickerSpeed;
        float a = Mathf.PerlinNoise(n, seed) * 2f - 1f;
        float b = Mathf.PerlinNoise(seed, n * 1.3f) * 2f - 1f;
        float stretch = speed > 0f ? stretchWithSpeed : 0f;

        core.localScale = new Vector3(
            baseScale.x * (1f + a * flickerAmount),
            baseScale.y * (1f + b * flickerAmount),
            baseScale.z * (1f + (a + b) * 0.5f * flickerAmount + stretch)) * grow;
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
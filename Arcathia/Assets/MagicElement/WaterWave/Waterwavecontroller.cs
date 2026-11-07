using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runtime controller for the water wave projectile made by the Water Wave builders.
/// - Moves the wave, drives shader dissolve (_Progress)
/// - Animates the wave body: spawn pop, squash/stretch curves, bob and rock
/// - Swept box hit-check (no Rigidbody needed, no tunnelling) + splash on impact
/// Pool friendly: call Launch() again after SetActive(true).
/// </summary>
[DisallowMultipleComponent]
public class WaterWaveController : MonoBehaviour
{
    enum State { Idle, Flying, Finishing }

    [Header("References (auto-filled by the Builder)")]
    public Renderer waveRenderer;
    [Tooltip("Object that gets scaled / bobbed. Defaults to the wave renderer's transform.")]
    public Transform visualRoot;
    public ParticleSystem trailDroplets;
    public ParticleSystem impactSplash;

    [Header("Movement")]
    [Tooltip("Launch automatically when the object is enabled (handy for testing). Turn off for pooling.")]
    public bool launchOnEnable = true;
    public float speed = 16f;
    public float lifetime = 1.4f;
    public AnimationCurve speedOverLife = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.55f));

    [Header("Body Animation Over Life (squash & stretch)")]
    public float sizeMultiplier = 1f;
    [Tooltip("Uniform scale multiplier")]
    public AnimationCurve scaleOverLife = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));
    [Tooltip("X: pops in wide, settles, narrows slightly")]
    public AnimationCurve widthOverLife = new AnimationCurve(
        new Keyframe(0f, 0.30f), new Keyframe(0.08f, 1.12f), new Keyframe(0.18f, 1f), new Keyframe(1f, 0.9f));
    [Tooltip("Y: pops up tall, settles, sinks as it fades")]
    public AnimationCurve heightOverLife = new AnimationCurve(
        new Keyframe(0f, 0.10f), new Keyframe(0.08f, 1.25f), new Keyframe(0.20f, 1f), new Keyframe(1f, 0.8f));
    [Tooltip("Z (travel direction): stretches out the longer it flies")]
    public AnimationCurve lengthOverLife = new AnimationCurve(
        new Keyframe(0f, 0.40f), new Keyframe(0.10f, 1f), new Keyframe(0.5f, 1.15f), new Keyframe(1f, 1.35f));

    [Header("Dissolve (shader _Progress)")]
    [Tooltip("0 = full wave, 1 = gone")]
    public AnimationCurve dissolveOverLife = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.7f, 0.05f), new Keyframe(1f, 0.7f));
    [Tooltip("How long the wave takes to dissolve after a hit / end of life")]
    public float finishTime = 0.25f;
    [Tooltip("Keep the object alive this long after finishing so the splash can play out")]
    public float impactLinger = 0.6f;

    [Header("Surf Motion (bob & rock)")]
    public float bobHeight = 0.04f;
    public float bobSpeed = 9f;
    public float rockDegrees = 3f;
    public float rockSpeed = 7f;
    [Tooltip("Extra culling-bounds padding in metres, so vertex-animated waves don't pop at screen edges")]
    public float boundsPadding = 0.6f;

    [Header("Hit Detection")]
    public LayerMask hitMask = ~0;
    public Vector3 hitBoxHalfExtents = new Vector3(1.5f, 0.6f, 0.3f);
    [Tooltip("Off = wave pierces through everything (each collider is hit once)")]
    public bool stopOnHit = true;
    public UnityEvent<Collider> onHit;
    public UnityEvent onFinished;

    [Header("Lifecycle")]
    public bool destroyWhenDone = true;

    State state = State.Idle;
    float age, finishT, visualTime;
    float currentProgress, finishFromProgress;
    Vector3 currentAxisScale = Vector3.one, finishFromAxisScale = Vector3.one;
    Transform owner;
    Vector3 visualBaseScale = Vector3.one, visualBasePos;
    Quaternion visualBaseRot = Quaternion.identity;
    MaterialPropertyBlock mpb;
    readonly HashSet<Collider> hitSet = new HashSet<Collider>();

    static readonly int ProgressId = Shader.PropertyToID("_Progress");

    void Awake()
    {
        if (!waveRenderer) waveRenderer = GetComponentInChildren<MeshRenderer>();
        if (!visualRoot && waveRenderer) visualRoot = waveRenderer.transform;
        if (visualRoot)
        {
            visualBaseScale = visualRoot.localScale;
            visualBasePos = visualRoot.localPosition;
            visualBaseRot = visualRoot.localRotation;
        }
        mpb = new MaterialPropertyBlock();

#if UNITY_2021_2_OR_NEWER
        // vertex-animated meshes move outside their imported bounds -> pad the culling bounds
        if (waveRenderer)
        {
            var mf = waveRenderer.GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh)
            {
                Bounds b = mf.sharedMesh.bounds;
                float s = Mathf.Max(1e-4f, Mathf.Abs(waveRenderer.transform.lossyScale.x));
                b.Expand(boundsPadding / s);
                waveRenderer.localBounds = b;
            }
        }
#endif
    }

    void OnEnable()
    {
        if (launchOnEnable && Application.isPlaying)
            Launch(transform.position, transform.forward);
    }

    void OnDisable()
    {
        state = State.Idle;
    }

    // ---------------------------------------------------------------- API

    /// <summary>Launch from the current position along the given direction.</summary>
    public void Launch(Vector3 direction) => Launch(transform.position, direction, null);

    /// <summary>Launch from a position/direction. Pass the caster as owner so it is never hit.</summary>
    public void Launch(Vector3 position, Vector3 direction, Transform ownerToIgnore = null)
    {
        if (direction.sqrMagnitude < 1e-4f) direction = transform.forward;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction.normalized));

        owner = ownerToIgnore;
        age = 0f;
        finishT = 0f;
        visualTime = 0f;
        hitSet.Clear();
        state = State.Flying;

        if (impactSplash) impactSplash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (trailDroplets)
        {
            trailDroplets.Clear(true);
            trailDroplets.Play(true);
        }

        currentAxisScale = AxisScaleAt(0f);
        currentProgress = dissolveOverLife.Evaluate(0f);
        ApplyVisual(currentProgress, currentAxisScale);
    }

    /// <summary>End the wave early (dissolves out).</summary>
    public void Stop()
    {
        if (state == State.Flying) BeginFinish();
    }

    // ------------------------------------------------------------- Update

    Vector3 AxisScaleAt(float t)
    {
        return new Vector3(widthOverLife.Evaluate(t), heightOverLife.Evaluate(t), lengthOverLife.Evaluate(t))
               * scaleOverLife.Evaluate(t);
    }

    void Update()
    {
        if (state == State.Idle) return;
        float dt = Time.deltaTime;
        visualTime += dt;

        if (state == State.Flying)
        {
            age += dt;
            float t = Mathf.Clamp01(age / lifetime);
            currentAxisScale = AxisScaleAt(t);
            currentProgress = dissolveOverLife.Evaluate(t);

            Vector3 dir = transform.forward;
            float step = speed * speedOverLife.Evaluate(t) * dt;
            bool stopped = false;

            if (step > 0f)
            {
                Vector3 ext = Vector3.Scale(hitBoxHalfExtents, transform.lossyScale);
                ext = Vector3.Scale(ext, currentAxisScale) * sizeMultiplier;
                if (Physics.BoxCast(transform.position, ext, dir, out RaycastHit hit, transform.rotation,
                        step, hitMask, QueryTriggerInteraction.Ignore))
                {
                    bool isOwner = owner && hit.transform.IsChildOf(owner);
                    if (!isOwner && hitSet.Add(hit.collider))
                    {
                        HandleHit(hit);
                        if (stopOnHit)
                        {
                            transform.position += dir * hit.distance;
                            BeginFinish();
                            stopped = true;
                        }
                    }
                }
            }

            if (!stopped)
            {
                transform.position += dir * step;
                ApplyVisual(currentProgress, currentAxisScale);
                if (age >= lifetime) BeginFinish();
            }
        }
        else // Finishing
        {
            finishT += dt;
            float k = Mathf.Clamp01(finishT / Mathf.Max(0.0001f, finishTime));
            ApplyVisual(Mathf.Lerp(finishFromProgress, 1f, k), finishFromAxisScale * Mathf.Lerp(1f, 1.08f, k));

            if (finishT >= Mathf.Max(finishTime, impactLinger))
                End();
        }
    }

    // ------------------------------------------------------------ Helpers

    void HandleHit(RaycastHit hit)
    {
        if (impactSplash)
        {
            Vector3 n = hit.normal.sqrMagnitude > 0.001f ? hit.normal : -transform.forward;
            impactSplash.transform.SetPositionAndRotation(hit.point, Quaternion.LookRotation(n));
            impactSplash.Play(true);
        }
        onHit?.Invoke(hit.collider);
    }

    void BeginFinish()
    {
        state = State.Finishing;
        finishT = 0f;
        finishFromProgress = currentProgress;
        finishFromAxisScale = currentAxisScale;
        if (trailDroplets) trailDroplets.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void End()
    {
        state = State.Idle;
        onFinished?.Invoke();
        if (destroyWhenDone) Destroy(gameObject);
        else gameObject.SetActive(false);
    }

    void ApplyVisual(float progress, Vector3 axisScale)
    {
        if (!waveRenderer) return;
        Transform vt = visualRoot ? visualRoot : waveRenderer.transform;

        vt.localScale = Vector3.Scale(visualBaseScale, axisScale) * sizeMultiplier;
        float bob = Mathf.Sin(visualTime * bobSpeed) * bobHeight;
        float rock = Mathf.Sin(visualTime * rockSpeed) * rockDegrees;
        vt.localPosition = visualBasePos + new Vector3(0f, bob, 0f);
        vt.localRotation = visualBaseRot * Quaternion.Euler(0f, 0f, rock);

        waveRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ProgressId, progress);
        waveRenderer.SetPropertyBlock(mpb);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, hitBoxHalfExtents * 2f);
    }

    [ContextMenu("Test Launch")]
    void TestLaunch() => Launch(transform.position, transform.forward);
}
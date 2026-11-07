using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only. Put this file in any folder named "Editor".
/// Menu: GameObject > Effects > Stylized Water Ball
/// Builds a water-magic projectile prefab under Assets/WaterBall. It uses WaterBallController,
/// which is separate from FireballController, so the two effects can be tweaked independently.
/// </summary>
public static class WaterBallBuilder
{
    const string Root = "Assets/WaterBall";
    const string Gen = Root + "/Generated";
    const string SharedNoise = "Assets/Fireball/Generated/FireballNoise.png"; // reused if it already exists

    [MenuItem("GameObject/Effects/Stylized Water Ball", false, 12)]
    static void Create()
    {
        EnsureFolder(Root);
        EnsureFolder(Gen);

        Texture2D noise = GetOrCreateNoise();
        Material coreMat = GetOrCreateMaterial("WaterCore.mat", "Stylized/Water Core", noise);
        Material partMat = GetOrCreateMaterial("WaterParticle.mat", "Stylized/Water Particle", noise);

        if (coreMat == null || partMat == null)
        {
            Debug.LogError("Water shaders not found. Make sure WaterBall.shader and WaterParticle.shader are in the project and the project uses URP.");
            return;
        }

        var root = new GameObject("WaterBall");
        Undo.RegisterCreatedObjectUndo(root, "Create Water Ball");

        Transform core = BuildCore(root.transform, coreMat);
        BuildSplashTrail(root.transform, partMat);
        BuildDroplets(root.transform, partMat);
        Transform burst = BuildLaunchBurst(root.transform, partMat);

        var ctrl = root.AddComponent<WaterBallController>();
        ctrl.core = core;
        ctrl.launchBurst = burst;

        string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/WaterBall.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
        Selection.activeGameObject = root;
        Debug.Log("Stylized water ball created. Prefab saved to " + path);
    }

    // ---------------------------------------------------------------- core mesh

    static Transform BuildCore(Transform parent, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Core";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.7f;

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }

    // ---------------------------------------------------------------- particles

    static ParticleSystem NewSystem(Transform parent, string name, Material mat, out ParticleSystemRenderer rend)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return ps;
    }

    // alpha drives the dissolve in the particle shader: 1 = fresh, 0 = gone.
    static Gradient WaterGradient(Color c0, Color c1, Color c2, float fadeIn = 0.08f)
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(c0, 0f),
                new GradientColorKey(c1, 0.4f),
                new GradientColorKey(c2, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(1f, fadeIn),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }

    static readonly Color Light = new Color(0.45f, 1f, 0.92f);
    static readonly Color Mid = new Color(0.02f, 0.72f, 0.98f);
    static readonly Color Deep = new Color(0.04f, 0.33f, 0.8f);
    static readonly Color Foam = new Color(0.9f, 1f, 1f);

    // Blobby splash chunks that trail behind and sag with gravity.
    static void BuildSplashTrail(Transform parent, Material mat)
    {
        var ps = NewSystem(parent, "Splash", mat, out var rend);

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = 0.4f;
        main.maxParticles = 40;

        var em = ps.emission;
        em.rateOverTime = 40f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.18f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.3f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(WaterGradient(Light, Mid, Deep));

        rend.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    // Small flicked-off drops that arc downward.
    static void BuildDroplets(Transform parent, Material mat)
    {
        var ps = NewSystem(parent, "Droplets", mat, out var rend);

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = Color.white;
        main.gravityModifier = 1f;
        main.maxParticles = 30;

        var em = ps.emission;
        em.rateOverTime = 22f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(WaterGradient(Foam, Light, Mid, 0.03f));

        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 2f;
        rend.velocityScale = 0.1f;
        ps.Play();
    }

    // Crown splash on launch: a foamy puff, radiating water tongues, and a scatter of drops.
    static Transform BuildLaunchBurst(Transform parent, Material mat)
    {
        var holder = new GameObject("LaunchBurst");
        holder.transform.SetParent(parent, false);

        // --- Puff: one big foam-rimmed splash disc
        var puff = NewSystem(holder.transform, "Puff", mat, out var puffRend);
        var pm = puff.main;
        pm.loop = false;
        pm.duration = 0.4f;
        pm.simulationSpace = ParticleSystemSimulationSpace.World;
        pm.startLifetime = 0.3f;
        pm.startSpeed = 0f;
        pm.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.0f);
        pm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        pm.startColor = Color.white;
        pm.maxParticles = 2;

        var pe = puff.emission;
        pe.rateOverTime = 0f;
        pe.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });

        var pshape = puff.shape;
        pshape.enabled = false;

        var psize = puff.sizeOverLifetime;
        psize.enabled = true;
        psize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(1f, 1.1f)));

        var pcol = puff.colorOverLifetime;
        pcol.enabled = true;
        pcol.color = new ParticleSystem.MinMaxGradient(WaterGradient(Foam, Light, Mid, 0.02f));

        puffRend.renderMode = ParticleSystemRenderMode.Billboard;
        puff.Play();

        // --- Crown: stretched tongues radiating out, then falling
        var crown = NewSystem(holder.transform, "Crown", mat, out var crownRend);
        var cm = crown.main;
        cm.loop = false;
        cm.duration = 0.5f;
        cm.simulationSpace = ParticleSystemSimulationSpace.World;
        cm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        cm.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        cm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
        cm.startColor = Color.white;
        cm.gravityModifier = 1.2f;
        cm.maxParticles = 20;

        var ce = crown.emission;
        ce.rateOverTime = 0f;
        ce.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)10, (short)14) });

        var cshape = crown.shape;
        cshape.shapeType = ParticleSystemShapeType.Sphere;
        cshape.radius = 0.12f;

        var csize = crown.sizeOverLifetime;
        csize.enabled = true;
        csize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

        var ccol = crown.colorOverLifetime;
        ccol.enabled = true;
        ccol.color = new ParticleSystem.MinMaxGradient(WaterGradient(Foam, Light, Mid, 0.02f));

        crownRend.renderMode = ParticleSystemRenderMode.Stretch;
        crownRend.lengthScale = 3f;
        crownRend.velocityScale = 0.12f;
        crown.Play();

        // --- Drops: fine scatter of tiny white-cyan beads
        var drops = NewSystem(holder.transform, "Drops", mat, out var dropRend);
        var dm = drops.main;
        dm.loop = false;
        dm.duration = 0.5f;
        dm.simulationSpace = ParticleSystemSimulationSpace.World;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        dm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        dm.startColor = Color.white;
        dm.gravityModifier = 1.5f;
        dm.maxParticles = 24;

        var de = drops.emission;
        de.rateOverTime = 0f;
        de.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)12, (short)18) });

        var dshape = drops.shape;
        dshape.shapeType = ParticleSystemShapeType.Sphere;
        dshape.radius = 0.15f;

        var dcol = drops.colorOverLifetime;
        dcol.enabled = true;
        dcol.color = new ParticleSystem.MinMaxGradient(WaterGradient(Foam, Light, Mid, 0.02f));

        dropRend.renderMode = ParticleSystemRenderMode.Stretch;
        dropRend.lengthScale = 2f;
        dropRend.velocityScale = 0.1f;
        drops.Play();

        return holder.transform;
    }

    // ---------------------------------------------------------------- assets

    static Material GetOrCreateMaterial(string file, string shaderName, Texture2D noise)
    {
        string path = Gen + "/" + file;
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) return null;
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetTexture("_NoiseTex", noise);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Texture2D GetOrCreateNoise()
    {
        // Share the fireball's noise texture if it's already in the project.
        var shared = AssetDatabase.LoadAssetAtPath<Texture2D>(SharedNoise);
        if (shared != null) return shared;

        string path = Gen + "/WaterNoise.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                float f = 0f, amp = 0.5f, sum = 0f;
                int period = 4;
                for (int o = 0; o < 3; o++)
                {
                    f += amp * PeriodicNoise(u * period, v * period, period, 17 + o * 31);
                    sum += amp;
                    amp *= 0.5f;
                    period *= 2;
                }
                f /= sum;
                f = Mathf.Clamp01((f - 0.5f) * 1.6f + 0.5f);
                byte b = (byte)(f * 255f);
                px[y * N + x] = new Color32(b, b, b, 255);
            }
        }
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.sRGBTexture = false;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.filterMode = FilterMode.Bilinear;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 128;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Value noise on a lattice that wraps every `period` cells, so the texture tiles perfectly.
    static float PeriodicNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        int xa = x0 % period, xb = (x0 + 1) % period;
        int ya = y0 % period, yb = (y0 + 1) % period;

        float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed);
        float c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
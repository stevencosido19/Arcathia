using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only. Put this file in any folder named "Editor".
/// Menu: GameObject > Effects > Stylized Fireball
/// Generates a tileable noise texture, two materials and a ready-to-use prefab under Assets/Fireball.
/// </summary>
public static class FireballBuilder
{
    const string Root = "Assets/Fireball";
    const string Gen = Root + "/Generated";

    [MenuItem("GameObject/Effects/Stylized Fireball", false, 10)]
    static void Create()
    {
        EnsureFolder(Root);
        EnsureFolder(Gen);

        Texture2D noise = GetOrCreateNoise();
        Material coreMat = GetOrCreateMaterial("FireballCore.mat", "Stylized/Fireball Core", noise);
        Material partMat = GetOrCreateMaterial("FireballParticle.mat", "Stylized/Fireball Particle", noise);

        if (coreMat == null || partMat == null)
        {
            Debug.LogError("Fireball shaders not found. Make sure Fireball.shader and FireballParticle.shader are in the project and the project uses URP.");
            return;
        }

        var root = new GameObject("Fireball");
        Undo.RegisterCreatedObjectUndo(root, "Create Fireball");

        Transform core = BuildCore(root.transform, coreMat);
        BuildFlames(root.transform, partMat);
        BuildEmbers(root.transform, partMat);
        BuildGlow(root.transform, partMat);
        Transform burst = BuildLaunchBurst(root.transform, partMat);

        var ctrl = root.AddComponent<FireballController>();
        ctrl.core = core;
        ctrl.launchBurst = burst;

        string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Fireball.prefab");
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction);
        Selection.activeGameObject = root;
        Debug.Log("Stylized fireball created. Prefab saved to " + path);
    }

    // ---------------------------------------------------------------- core mesh

    static Transform BuildCore(Transform parent, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Core";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.8f;

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

    // alpha is the dissolve driver in the particle shader: 1 = fresh, 0 = burnt out.
    static Gradient FireGradient(Color c0, Color c1, Color c2, float fadeIn = 0.08f)
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(c0, 0f),
                new GradientColorKey(c1, 0.35f),
                new GradientColorKey(c2, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.6f, 0f),
                new GradientAlphaKey(1f, fadeIn),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }

    static void BuildFlames(Transform parent, Material mat)
    {
        var ps = NewSystem(parent, "Flames", mat, out var rend);

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // flames lag behind a moving fireball
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.maxParticles = 40;

        var em = ps.emission;
        em.rateOverTime = 45f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.2f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.25f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FireGradient(
            new Color(1f, 0.95f, 0.65f), new Color(1f, 0.5f, 0.1f), new Color(0.6f, 0.08f, 0.02f)));

        rend.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    static void BuildEmbers(Transform parent, Material mat)
    {
        var ps = NewSystem(parent, "Embers", mat, out var rend);

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor = Color.white;
        main.gravityModifier = -0.1f;
        main.maxParticles = 30;

        var em = ps.emission;
        em.rateOverTime = 20f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.25f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FireGradient(
            new Color(1f, 0.9f, 0.5f), new Color(1f, 0.45f, 0.08f), new Color(0.7f, 0.1f, 0.02f), 0.02f));

        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 2f;
        rend.velocityScale = 0.08f;
        ps.Play();
    }

    // A few big, dim, slowly pulsing quads act as a fake light halo. Far cheaper than a real-time light on mobile.
    static void BuildGlow(Transform parent, Material mat)
    {
        var ps = NewSystem(parent, "Glow", mat, out var rend);

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 1f;
        main.startSpeed = 0f;
        main.startSize = 2.5f;
        main.startColor = new Color(0.55f, 0.16f, 0.03f);
        main.maxParticles = 4;

        var em = ps.emission;
        em.rateOverTime = 3f;

        var sh = ps.shape;
        sh.enabled = false;

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(g);

        rend.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
    }

    // One-shot flash + spark spray that plays when the fireball appears.
    static Transform BuildLaunchBurst(Transform parent, Material mat)
    {
        var holder = new GameObject("LaunchBurst");
        holder.transform.SetParent(parent, false);

        // --- Flash: one big bright puff that pops and dissolves in ~0.18s
        var flash = NewSystem(holder.transform, "Flash", mat, out var flashRend);
        var fm = flash.main;
        fm.loop = false;
        fm.duration = 0.3f;
        fm.simulationSpace = ParticleSystemSimulationSpace.World;   // stays at the muzzle as the fireball flies off
        fm.startLifetime = 0.18f;
        fm.startSpeed = 0f;
        fm.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
        fm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        fm.startColor = Color.white;
        fm.maxParticles = 2;

        var fe = flash.emission;
        fe.rateOverTime = 0f;
        fe.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });

        var fshape = flash.shape;
        fshape.enabled = false;

        var fsize = flash.sizeOverLifetime;
        fsize.enabled = true;
        fsize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.4f), new Keyframe(1f, 1.1f)));

        var fcol = flash.colorOverLifetime;
        fcol.enabled = true;
        fcol.color = new ParticleSystem.MinMaxGradient(FireGradient(
            new Color(1f, 0.97f, 0.8f), new Color(1f, 0.6f, 0.15f), new Color(0.9f, 0.25f, 0.05f), 0.01f));

        flashRend.renderMode = ParticleSystemRenderMode.Billboard;
        flash.Play();

        // --- Sparks: a quick spray of streaks flying outward
        var sparks = NewSystem(holder.transform, "Sparks", mat, out var sparkRend);
        var sm = sparks.main;
        sm.loop = false;
        sm.duration = 0.5f;
        sm.simulationSpace = ParticleSystemSimulationSpace.World;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        sm.startColor = Color.white;
        sm.gravityModifier = 0.3f;
        sm.maxParticles = 30;

        var se = sparks.emission;
        se.rateOverTime = 0f;
        se.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14, (short)20) });

        var sshape = sparks.shape;
        sshape.shapeType = ParticleSystemShapeType.Sphere;
        sshape.radius = 0.1f;

        var ssize = sparks.sizeOverLifetime;
        ssize.enabled = true;
        ssize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var scol = sparks.colorOverLifetime;
        scol.enabled = true;
        scol.color = new ParticleSystem.MinMaxGradient(FireGradient(
            new Color(1f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.1f), new Color(0.8f, 0.15f, 0.03f), 0.01f));

        sparkRend.renderMode = ParticleSystemRenderMode.Stretch;
        sparkRend.lengthScale = 2.5f;
        sparkRend.velocityScale = 0.1f;
        sparks.Play();

        return holder.transform;
    }

    // For fireballs you already built: select the fireball (scene instance, or inside Prefab Mode) and run this.
    [MenuItem("GameObject/Effects/Add Launch Burst to Fireball", false, 11)]
    static void AddLaunchBurstToSelected()
    {
        var go = Selection.activeGameObject;
        var ctrl = go != null ? go.GetComponentInParent<FireballController>() : null;
        if (ctrl == null)
        {
            Debug.LogWarning("Select a fireball (an object with FireballController) first.");
            return;
        }
        if (ctrl.transform.Find("LaunchBurst") != null)
        {
            Debug.LogWarning("This fireball already has a LaunchBurst child.");
            return;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(Gen + "/FireballParticle.mat");
        if (mat == null)
        {
            Debug.LogError("Couldn't find " + Gen + "/FireballParticle.mat. Create a fireball with the menu first.");
            return;
        }

        Transform burst = BuildLaunchBurst(ctrl.transform, mat);
        Undo.RegisterCreatedObjectUndo(burst.gameObject, "Add Launch Burst");
        Undo.RecordObject(ctrl, "Add Launch Burst");
        ctrl.launchBurst = burst;
        EditorUtility.SetDirty(ctrl);
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
        string path = Gen + "/FireballNoise.png";
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
                f = Mathf.Clamp01((f - 0.5f) * 1.6f + 0.5f);   // punch contrast for crisper bands
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
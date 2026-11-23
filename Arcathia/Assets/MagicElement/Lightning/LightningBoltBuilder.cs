#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > VFX > Create Lightning Bolt Prefab
/// Builds: materials (bolt ribbon, impact glow, sparks), a prefab with LightningBolt already wired up,
/// an impact glow quad and a small spark particle system. Everything is procedural, no textures needed.
/// </summary>
public static class LightningBoltBuilder
{
    const string Folder = "Assets/VFX/LightningBolt/Generated";

    [MenuItem("Tools/VFX/Create Lightning Bolt Prefab")]
    static void Build()
    {
        Shader boltShader = Shader.Find("VFX/LightningBolt");
        Shader glowShader = Shader.Find("VFX/LightningGlow");
        if (boltShader == null || glowShader == null)
        {
            EditorUtility.DisplayDialog("Lightning Bolt Builder",
                "Shaders not found. Import LightningBolt.shader and LightningGlow.shader first.", "OK");
            return;
        }

        EnsureFolder(Folder);

        // ---- materials
        Material boltMat = GetOrCreateMaterial(Folder + "/LightningBolt_Mat.mat", boltShader);

        Material glowMat = GetOrCreateMaterial(Folder + "/LightningImpactGlow_Mat.mat", glowShader);
        glowMat.SetFloat("_Billboard", 1f);
        glowMat.SetColor("_Color", new Color(0.20f, 0.50f, 1.00f, 1f));
        glowMat.SetFloat("_Intensity", 1.4f);
        glowMat.SetFloat("_Power", 1.8f);
        EditorUtility.SetDirty(glowMat);

        Material sparkMat = GetOrCreateMaterial(Folder + "/LightningSpark_Mat.mat", glowShader);
        sparkMat.SetFloat("_Billboard", 0f);
        sparkMat.SetColor("_Color", new Color(0.30f, 0.65f, 1.00f, 1f));
        sparkMat.SetFloat("_Intensity", 1.6f);
        EditorUtility.SetDirty(sparkMat);

        // ---- hierarchy
        var root = new GameObject("LightningBolt");
        var bolt = root.AddComponent<LightningBolt>();

        var boltGO = new GameObject("Bolt");
        boltGO.transform.SetParent(root.transform, false);
        var mf = boltGO.AddComponent<MeshFilter>();
        var mr = boltGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = boltMat;
        ConfigureRenderer(mr);

        var glowGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glowGO.name = "ImpactGlow";
        Object.DestroyImmediate(glowGO.GetComponent<Collider>());
        glowGO.transform.SetParent(root.transform, false);
        var glowR = glowGO.GetComponent<MeshRenderer>();
        glowR.sharedMaterial = glowMat;
        ConfigureRenderer(glowR);

        ParticleSystem sparks = CreateSparks(root.transform, sparkMat);

        bolt.boltMeshFilter = mf;
        bolt.boltRenderer = mr;
        bolt.impactGlow = glowR;
        bolt.sparks = sparks;
        bolt.previewLoop = true;   // so you see it immediately in Play mode; turn off and call Strike() in gameplay

        string prefabPath = Folder + "/LightningBolt.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (SceneView.lastActiveSceneView != null)
            inst.transform.position = SceneView.lastActiveSceneView.pivot;
        Selection.activeGameObject = inst;
        EditorGUIUtility.PingObject(prefab);

        Debug.Log("[LightningBoltBuilder] Built " + prefabPath + "  ->  press Play to see it. In gameplay call bolt.Strike(start, end) and turn off Preview Loop.");
    }

    static ParticleSystem CreateSparks(Transform parent, Material sparkMat)
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.85f, 1f), Color.white);
        main.gravityModifier = 1.5f;
        main.maxParticles = 24;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 50f;
        sh.radius = 0.12f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = sparkMat;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.08f;
        r.lengthScale = 2.2f;
        ConfigureRenderer(r);
        return ps;
    }

    static void ConfigureRenderer(Renderer r)
    {
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    static Material GetOrCreateMaterial(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        path = path.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif

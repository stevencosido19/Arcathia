#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > VFX > Water Wave Flow Builder
/// Drag your FBX (the model asset itself) in and it will:
///  - read the model's real up/forward axes (Blender's -90 X rotation / big scale are handled)
///  - auto-size it to a target length in metres and centre the pivot
///  - generate a stylized cel-water texture (or use your own painted texture)
///  - build a material with VFX/WaterWaveFlow (flow = vertex animation in the model, texture stays still)
///  - add droplet trail + impact splash, and save a prefab with WaterWaveController attached
/// </summary>
public class WaterWaveFlowBuilder : EditorWindow
{
    [MenuItem("Tools/VFX/Water Wave Flow Builder")]
    static void Open() => GetWindow<WaterWaveFlowBuilder>("Water Wave Flow Builder");

    enum TexMode { Layered, GenerateStylized, UseMyTexture }

    // ---- output
    string outputFolder = "Assets/VFX/WaterWave/Generated";
    string waveName = "WaterWaveFlow";
    bool addToScene = true;

    // ---- model
    [SerializeField] GameObject model;
    float targetLength = 4f;
    int rotateY = 0;
    bool centerPivot = true;
    bool usePlanar = true;

    // ---- texture
    TexMode texMode = TexMode.Layered;
    [SerializeField] Texture2D myTexture;
    [SerializeField] Gradient gradient;
    Color foamColor = Color.white;
    int texSize = 512;
    float swirlScale = 3.2f;
    float warp = 1.6f;
    float foamThreshold = 0.84f;
    float lineWidth = 0.05f;
    float lineStrength = 1f;
    int seed = 7;

    // ---- layered look (matches the reference video)
    Color colorLow = new Color(0.10f, 0.45f, 0.85f);
    Color colorHigh = new Color(0.36f, 0.76f, 1.00f);
    Color streakColor = new Color(0.78f, 0.94f, 1.00f);
    float scrollSpeed = 0.35f;
    float streakStrength = 0.6f;
    float lipStart = 0.78f;
    float lipNoise = 0.35f;
    float bandStrength = 1f;
    float bubbleCut = 0.82f;
    float bottomFadeStart = 0.05f;
    float bottomFadeWidth = 0.25f;

    // ---- flow (vertex animation) -- values are in METRES, converted to mesh space automatically
    float amp1 = 0.12f, freq1 = 2.5f, speed1 = 3f, steep = 0.6f, cross1 = 0.8f;
    float amp2 = 0.06f, freq2 = 4.5f, speed2 = -2f;
    float swayAmp = 0.08f, swayFreq = 1.5f, swaySpeed = 2f;

    // ---- look
    Color tint = Color.white;
    float intensity = 1f;
    float shadeAmount = 0.25f;
    float fresnel = 0.3f;

    // ---- surf motion
    float bobHeight = 0.04f;
    float rockDegrees = 3f;

    // ---- particles
    bool useTrail = true;
    int trailMax = 24;
    float trailRate = 6f;
    bool useImpact = true;
    int impactCount = 14;

    // ---- projectile
    float speed = 16f;
    float lifetime = 1.4f;

    Vector2 scroll;
    Texture2D lastPreview;

    void OnEnable()
    {
        if (gradient == null) gradient = DefaultGradient();
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient { mode = GradientMode.Fixed };
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.09f, 0.31f, 0.70f), 0f),
                new GradientColorKey(new Color(0.12f, 0.50f, 0.92f), 0.35f),
                new GradientColorKey(new Color(0.30f, 0.76f, 1.00f), 0.62f),
                new GradientColorKey(new Color(0.68f, 0.92f, 1.00f), 0.80f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // ================================================================= GUI
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("Folder", outputFolder);
        waveName = EditorGUILayout.TextField("Prefab Name", waveName);
        addToScene = EditorGUILayout.Toggle("Add To Scene After Build", addToScene);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
        model = (GameObject)EditorGUILayout.ObjectField("FBX Model", model, typeof(GameObject), false);
        targetLength = EditorGUILayout.Slider("Target Length (m, along travel)", targetLength, 0.5f, 15f);
        rotateY = EditorGUILayout.IntPopup("Rotate Y (if it faces wrong way)", rotateY,
            new[] { "0", "90", "180", "270" }, new[] { 0, 90, 180, 270 });
        centerPivot = EditorGUILayout.Toggle("Center Pivot", centerPivot);
        usePlanar = EditorGUILayout.Toggle("Planar Top-Down Texture Mapping", usePlanar);
        if (!usePlanar)
            EditorGUILayout.HelpBox("Using the mesh's own UVs. If the UV is a cube-cross layout the texture will look patchy; keep planar on in that case.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stylized Texture", EditorStyles.boldLabel);
        texMode = (TexMode)EditorGUILayout.EnumPopup("Source", texMode);
        if (texMode == TexMode.Layered)
        {
            EditorGUILayout.HelpBox("Layered water like the reference video: blue height gradient, light streaks scrolling down the slope, " +
                                    "bubbles, foam lip on top, foam bands, ragged bottom fade. No painted texture needed.", MessageType.None);
            colorLow = EditorGUILayout.ColorField("Water Low (bottom)", colorLow);
            colorHigh = EditorGUILayout.ColorField("Water High (top)", colorHigh);
            streakColor = EditorGUILayout.ColorField("Streak Color", streakColor);
            foamColor = EditorGUILayout.ColorField("Foam Color", foamColor);
            scrollSpeed = EditorGUILayout.Slider("Streak Scroll Speed (0 = off)", scrollSpeed, -3f, 3f);
            streakStrength = EditorGUILayout.Slider("Streak Strength", streakStrength, 0f, 1f);
            lipStart = EditorGUILayout.Slider("Foam Lip Height", lipStart, 0.3f, 0.98f);
            lipNoise = EditorGUILayout.Slider("Foam Lip Raggedness", lipNoise, 0f, 1f);
            bandStrength = EditorGUILayout.Slider("Foam Bands Strength", bandStrength, 0f, 1f);
            bubbleCut = EditorGUILayout.Slider("Bubbles (higher = fewer)", bubbleCut, 0.5f, 1f);
            bottomFadeStart = EditorGUILayout.Slider("Bottom Fade Start", bottomFadeStart, 0f, 0.6f);
            bottomFadeWidth = EditorGUILayout.Slider("Bottom Fade Width", bottomFadeWidth, 0.02f, 0.8f);
        }
        else if (texMode == TexMode.UseMyTexture)
        {
            myTexture = (Texture2D)EditorGUILayout.ObjectField("Texture (RGBA)", myTexture, typeof(Texture2D), false);
            EditorGUILayout.HelpBox("Alpha = holes/breakup (white = solid). Enable 'Alpha Is Transparency' in the texture import settings.", MessageType.None);
        }
        else
        {
            gradient = EditorGUILayout.GradientField("Water Bands (Fixed = cel)", gradient);
            foamColor = EditorGUILayout.ColorField("Foam Color", foamColor);
            texSize = EditorGUILayout.IntPopup("Texture Size", texSize, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
            swirlScale = EditorGUILayout.Slider("Swirl Scale", swirlScale, 1f, 8f);
            warp = EditorGUILayout.Slider("Swirl Warp (curl amount)", warp, 0f, 4f);
            foamThreshold = EditorGUILayout.Slider("Foam Patches Threshold", foamThreshold, 0.5f, 1f);
            lineWidth = EditorGUILayout.Slider("Foam Line Width", lineWidth, 0.01f, 0.15f);
            lineStrength = EditorGUILayout.Slider("Foam Line Strength", lineStrength, 0f, 1f);
            seed = EditorGUILayout.IntSlider("Seed", seed, 0, 99);

            if (GUILayout.Button("Generate Texture Only (preview)"))
            {
                EnsureFolder(outputFolder);
                lastPreview = GenerateTexture(outputFolder.TrimEnd('/') + "/" + waveName + "_Stylized.png", 0.75f);
                Selection.activeObject = lastPreview;
                EditorGUIUtility.PingObject(lastPreview);
            }
            if (lastPreview != null)
            {
                Rect r = GUILayoutUtility.GetRect(10, 120, GUILayout.ExpandWidth(true));
                EditorGUI.DrawPreviewTexture(r, lastPreview, null, ScaleMode.ScaleToFit);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Vertex Flow (movement is in the model, values in metres)", EditorStyles.boldLabel);
        amp1 = EditorGUILayout.Slider("Swell Height", amp1, 0f, 0.8f);
        freq1 = EditorGUILayout.Slider("Swell Frequency (rad/m)", freq1, 0f, 10f);
        speed1 = EditorGUILayout.Slider("Swell Speed", speed1, -10f, 10f);
        steep = EditorGUILayout.Slider("Crest Roll Steepness", steep, 0f, 1.5f);
        cross1 = EditorGUILayout.Slider("Wave Front Slant", cross1, -4f, 4f);
        amp2 = EditorGUILayout.Slider("2nd Swell Height", amp2, 0f, 0.5f);
        freq2 = EditorGUILayout.Slider("2nd Swell Frequency", freq2, 0f, 10f);
        speed2 = EditorGUILayout.Slider("2nd Swell Speed", speed2, -10f, 10f);
        swayAmp = EditorGUILayout.Slider("Sway Amount", swayAmp, 0f, 0.5f);
        swayFreq = EditorGUILayout.Slider("Sway Frequency", swayFreq, 0f, 8f);
        swaySpeed = EditorGUILayout.Slider("Sway Speed", swaySpeed, -8f, 8f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Surf Motion (whole body)", EditorStyles.boldLabel);
        bobHeight = EditorGUILayout.Slider("Bob Height (m)", bobHeight, 0f, 0.3f);
        rockDegrees = EditorGUILayout.Slider("Rock (deg)", rockDegrees, 0f, 15f);
        EditorGUILayout.HelpBox("Spawn pop and squash/stretch are curves on the prefab's WaterWaveController (Width / Height / Length Over Life).", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
        tint = EditorGUILayout.ColorField("Tint", tint);
        intensity = EditorGUILayout.Slider("Intensity", intensity, 0.5f, 3f);
        shadeAmount = EditorGUILayout.Slider("Form Shading", shadeAmount, 0f, 0.8f);
        fresnel = EditorGUILayout.Slider("Rim / Fresnel Foam", fresnel, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Particles", EditorStyles.boldLabel);
        useTrail = EditorGUILayout.Toggle("Trail Droplets", useTrail);
        using (new EditorGUI.DisabledScope(!useTrail))
        {
            trailMax = EditorGUILayout.IntSlider("Max Trail Particles", trailMax, 4, 48);
            trailRate = EditorGUILayout.Slider("Rate / Meter", trailRate, 1f, 20f);
        }
        useImpact = EditorGUILayout.Toggle("Impact Splash", useImpact);
        using (new EditorGUI.DisabledScope(!useImpact))
            impactCount = EditorGUILayout.IntSlider("Impact Droplets", impactCount, 4, 30);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projectile Defaults", EditorStyles.boldLabel);
        speed = EditorGUILayout.FloatField("Speed", speed);
        lifetime = EditorGUILayout.FloatField("Lifetime", lifetime);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(model == null))
        {
            if (GUILayout.Button("Build Water Wave Prefab", GUILayout.Height(34)))
                Build();
        }
        if (model == null)
            EditorGUILayout.HelpBox("Drag your FBX file (VFX_MODELS) from the Project window into 'FBX Model'.", MessageType.Warning);

        EditorGUILayout.EndScrollView();
    }

    // ============================================================== BUILD
    void Build()
    {
        var shader = Shader.Find("VFX/WaterWaveFlow");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Water Wave Flow Builder",
                "Shader 'VFX/WaterWaveFlow' not found. Import StylizedWaterWaveFlow.shader first.", "OK");
            return;
        }
        if (texMode == TexMode.UseMyTexture && myTexture == null)
        {
            EditorUtility.DisplayDialog("Water Wave Flow Builder", "Assign your texture, or switch Source to Generate Stylized.", "OK");
            return;
        }

        EnsureFolder(outputFolder);
        string f = outputFolder.TrimEnd('/');

        // ---- material asset (filled in below once the model's size/axes are known)
        string matPath = f + "/" + waveName + "_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = shader;

        // ---- hierarchy: Root(controller) / Wave(visualRoot) / Model(FBX copy)
        var root = new GameObject(waveName);
        var ctrl = root.AddComponent<WaterWaveController>();
        var visual = new GameObject("Wave");
        visual.transform.SetParent(root.transform, false);

        var inst = Instantiate(model);
        inst.name = "Model";
        inst.transform.SetParent(visual.transform, false);
        foreach (var a in inst.GetComponentsInChildren<Animator>(true)) DestroyImmediate(a);
        foreach (var a in inst.GetComponentsInChildren<Animation>(true)) DestroyImmediate(a);

        var renderers = inst.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
        {
            DestroyImmediate(root);
            EditorUtility.DisplayDialog("Water Wave Flow Builder", "No MeshRenderer found in that model.", "OK");
            return;
        }
        foreach (var r in renderers)
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
        var mr = renderers[0];

        // ---- orient, auto-size along Z (travel), centre pivot (root is at world origin, identity)
        inst.transform.localRotation = Quaternion.Euler(0f, rotateY, 0f);
        Bounds wb = WorldBounds(renderers);
        float k = targetLength / Mathf.Max(wb.size.z, 1e-4f);
        inst.transform.localScale = inst.transform.localScale * k;
        wb = WorldBounds(renderers);
        if (centerPivot) inst.transform.position -= wb.center;
        wb = WorldBounds(renderers);

        // ---- mesh-space axes (handles Blender's -90 X rotation and big node scales)
        Transform mt = mr.transform;
        Vector3 upObj = mt.InverseTransformDirection(Vector3.up).normalized;
        Vector3 flowObj = mt.InverseTransformDirection(Vector3.forward).normalized;
        float sc = Mathf.Max(1e-5f, Mathf.Abs(mt.lossyScale.x)); // metres per mesh unit
        Mesh mesh = mr.GetComponent<MeshFilter>().sharedMesh;

        Vector3 side = Vector3.Cross(upObj, flowObj).normalized;
        ProjectBounds(mesh.bounds, side, flowObj, out float minS, out float maxS, out float minF, out float maxF);
        float sizeS = Mathf.Max(1e-5f, maxS - minS), sizeF = Mathf.Max(1e-5f, maxF - minF);
        ProjectBounds(mesh.bounds, upObj, upObj, out float minU, out float maxU, out _, out _);
        float sizeU = Mathf.Max(1e-5f, maxU - minU);

        // ---- textures (generated at the same aspect as the mapped area so swirls aren't stretched)
        Texture2D mainTex = texMode == TexMode.Layered ? null
            : texMode == TexMode.UseMyTexture ? myTexture
            : GenerateTexture(f + "/" + waveName + "_Stylized.png", usePlanar ? sizeS / sizeF : 1f);
        var noise = LoadOrCreateTileableNoise(f + "/WaterWaveFlow_Noise.png");
        var drop = LoadOrCreateDroplet(f + "/WaterWave_Droplet.png");

        // ---- material values
        mat.SetTexture("_MainTex", mainTex);
        mat.SetTexture("_NoiseTex", noise);
        mat.SetColor("_Tint", tint);
        mat.SetColor("_FoamColor", foamColor);
        mat.SetFloat("_Intensity", intensity);
        mat.SetFloat("_ShadeAmount", shadeAmount);
        mat.SetFloat("_FresnelStrength", fresnel);
        mat.SetFloat("_Cull", 2f);   // Back (closed mesh)
        mat.SetFloat("_ZWrite", 1f);

        // layered look
        mat.SetFloat("_UseMainTex", texMode == TexMode.Layered ? 0f : 1f);
        mat.SetColor("_ColorLow", colorLow);
        mat.SetColor("_ColorHigh", colorHigh);
        mat.SetColor("_StreakColor", streakColor);
        mat.SetVector("_HeightRange", new Vector4(minU, 1f / sizeU, 0f, 0f));
        mat.SetFloat("_ScrollSpeed", scrollSpeed);
        mat.SetFloat("_StreakStrength", streakStrength);
        mat.SetFloat("_LipStart", lipStart);
        mat.SetFloat("_LipNoise", lipNoise);
        mat.SetFloat("_BandStrength", bandStrength);
        mat.SetFloat("_BubbleCut", bubbleCut);
        mat.SetVector("_BottomFade", new Vector4(bottomFadeStart, bottomFadeWidth, 0f, 0f));

        mat.SetFloat("_UsePlanar", usePlanar ? 1f : 0f);
        mat.SetVector("_PlanarRect", new Vector4(minS, minF, 1f / sizeS, 1f / sizeF));
        FitUvToIsland(mat, mesh);

        mat.SetVector("_FlowDir", new Vector4(flowObj.x, flowObj.y, flowObj.z, 0f));
        mat.SetVector("_UpAxis", new Vector4(upObj.x, upObj.y, upObj.z, 0f));
        // metres -> mesh units
        mat.SetFloat("_Amp1", amp1 / sc);
        mat.SetFloat("_Freq1", freq1 * sc);
        mat.SetFloat("_Speed1", speed1);
        mat.SetFloat("_Steep", steep);
        mat.SetFloat("_Cross1", cross1 * sc);
        mat.SetFloat("_Amp2", amp2 / sc);
        mat.SetFloat("_Freq2", freq2 * sc);
        mat.SetFloat("_Speed2", speed2);
        mat.SetFloat("_SwayAmp", swayAmp / sc);
        mat.SetFloat("_SwayFreq", swayFreq * sc);
        mat.SetFloat("_SwaySpeed", swaySpeed);
        mat.enableInstancing = false;
        EditorUtility.SetDirty(mat);

        // ---- droplet material (Sprites/Default exists in Built-in and URP)
        string dropMatPath = f + "/" + waveName + "_DropletMat.mat";
        var dropMat = AssetDatabase.LoadAssetAtPath<Material>(dropMatPath);
        if (dropMat == null)
        {
            dropMat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(dropMat, dropMatPath);
        }
        dropMat.mainTexture = drop;
        EditorUtility.SetDirty(dropMat);

        ParticleSystem trail = null, impact = null;
        if (useTrail) trail = CreateParticles(root.transform, dropMat, true, wb);
        if (useImpact) impact = CreateParticles(root.transform, dropMat, false, wb);

        // ---- controller
        ctrl.waveRenderer = mr;
        ctrl.visualRoot = visual.transform;
        ctrl.trailDroplets = trail;
        ctrl.impactSplash = impact;
        ctrl.speed = speed;
        ctrl.lifetime = lifetime;
        ctrl.bobHeight = bobHeight;
        ctrl.rockDegrees = rockDegrees;
        ctrl.hitBoxHalfExtents = new Vector3(Mathf.Max(wb.extents.x, 0.2f), Mathf.Max(wb.extents.y, 0.3f), Mathf.Max(wb.extents.z, 0.2f));
        ctrl.boundsPadding = Mathf.Max(amp1 + amp2, swayAmp) * 2f + 0.3f;

        string prefabPath = f + "/" + waveName + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (addToScene && prefab != null)
        {
            var spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            spawned.transform.position = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            Selection.activeGameObject = spawned;
        }
        else
        {
            Selection.activeObject = prefab;
        }
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[WaterWaveFlowBuilder] Built '{waveName}' -> {prefabPath}  " +
                  $"(verts: {mesh.vertexCount}, tris: {mesh.GetIndexCount(0) / 3}, size: {wb.size.x:F2} x {wb.size.y:F2} x {wb.size.z:F2} m, " +
                  $"mesh->metre scale: {sc:F5}, up(obj): {upObj}, flow(obj): {flowObj})");
    }

    static Bounds WorldBounds(Renderer[] rs)
    {
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // project the 8 mesh-bounds corners on two axes -> planar mapping rectangle
    static void ProjectBounds(Bounds b, Vector3 axisS, Vector3 axisF,
        out float minS, out float maxS, out float minF, out float maxF)
    {
        minS = minF = float.MaxValue;
        maxS = maxF = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3(
                (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            float s = Vector3.Dot(c, axisS), fo = Vector3.Dot(c, axisF);
            if (s < minS) minS = s;
            if (s > maxS) maxS = s;
            if (fo < minF) minF = fo;
            if (fo > maxF) maxF = fo;
        }
    }

    // if the mesh UVs only use part of 0-1 (yours use ~0.25-0.75), stretch them to the full texture
    static void FitUvToIsland(Material mat, Mesh mesh)
    {
        Vector2 min = Vector2.zero, max = Vector2.one;
        try
        {
            var uvs = mesh.uv;
            if (uvs != null && uvs.Length > 0)
            {
                min = max = uvs[0];
                foreach (var uv in uvs)
                {
                    min = Vector2.Min(min, uv);
                    max = Vector2.Max(max, uv);
                }
            }
        }
        catch { /* mesh not readable: keep full 0-1 */ }

        Vector2 size = new Vector2(Mathf.Max(1e-4f, max.x - min.x), Mathf.Max(1e-4f, max.y - min.y));
        mat.SetTextureScale("_MainTex", new Vector2(1f / size.x, 1f / size.y));
        mat.SetTextureOffset("_MainTex", new Vector2(-min.x / size.x, -min.y / size.y));
    }

    // ========================================================== PARTICLES
    ParticleSystem CreateParticles(Transform parent, Material dropMat, bool isTrail, Bounds b)
    {
        var go = new GameObject(isTrail ? "TrailDroplets" : "ImpactSplash");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        var sh = ps.shape;
        sh.enabled = true;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        if (isTrail)
        {
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.93f, 1f), Color.white);
            main.gravityModifier = 0.6f;
            main.maxParticles = trailMax;
            em.rateOverDistance = trailRate;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(Mathf.Max(b.size.x, 0.3f) * 0.8f, Mathf.Max(b.size.y, 0.2f), Mathf.Max(b.size.z, 0.3f) * 0.8f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }
        else
        {
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.88f, 1f), Color.white);
            main.gravityModifier = 1.2f;
            main.maxParticles = Mathf.Max(impactCount + 4, 8);
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)impactCount) });
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 38f;
            sh.radius = 0.25f;
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dropMat;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.06f;
        r.lengthScale = 1.6f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return ps;
    }

    // ===================================================== STYLIZED TEXTURE
    // Domain-warped noise -> swirling height field -> fixed colour bands (cel look),
    // white foam patches on the high areas, thin white swirl lines on the contour ridges.
    // 'ratio' = width / height of the area the texture is mapped on (so swirls stay round).
    Texture2D GenerateTexture(string path, float ratio)
    {
        ratio = Mathf.Clamp(ratio, 0.25f, 4f);
        int w, h;
        if (ratio >= 1f) { w = texSize; h = Mathf.Max(16, Mathf.RoundToInt(texSize / ratio)); }
        else { h = texSize; w = Mathf.Max(16, Mathf.RoundToInt(texSize * ratio)); }
        float aspect = w / (float)h;

        var height = new float[w * h];
        var ridge = new float[w * h];

        float s = seed * 17.31f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = (x / (float)w) * aspect * swirlScale + 50f;
                float py = (y / (float)h) * swirlScale + 50f;

                // two levels of domain warping = curling swirls
                float qx = px + (Fbm(px + s, py + 1.7f) - 0.5f) * warp * 2f;
                float qy = py + (Fbm(px + 5.2f, py + s + 1.3f) - 0.5f) * warp * 2f;
                float rx = qx + (Fbm(qx * 1.3f + s + 9.1f, qy * 1.3f + 2.8f) - 0.5f) * warp * 2f;
                float ry = qy + (Fbm(qx * 1.3f + 3.3f, qy * 1.3f + s + 7.4f) - 0.5f) * warp * 2f;

                int i = y * w + x;
                height[i] = Fbm(rx + 20f, ry + 20f);
                ridge[i] = Fbm(rx * 1.8f + 40f, ry * 1.8f + 40f);
            }
        Normalize(height);
        Normalize(ridge);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px32 = new Color[w * h];
        for (int i = 0; i < px32.Length; i++)
        {
            float hv = height[i];
            Color c = gradient.Evaluate(hv);

            float foam = Smooth(foamThreshold, foamThreshold + 0.03f, hv);
            float line = Smooth(1f - lineWidth, 1f, 1f - Mathf.Abs(2f * ridge[i] - 1f))
                         * Smooth(0.30f, 0.50f, hv) * lineStrength;

            c = Color.Lerp(c, foamColor, Mathf.Max(foam, line));
            c.a = 1f;
            px32[i] = c;
        }
        tex.SetPixels(px32);
        tex.Apply();

        return SavePng(tex, path, imp =>
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 1024;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Compressed;
        });
    }

    // Seamlessly tileable noise (R) - needed because the streaks scroll across the texture edge.
    Texture2D LoadOrCreateTileableNoise(string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 128;
        var n = new float[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float sum = 0f, amp = 0.5f, total = 0f;
                int freq = 4;
                for (int o = 0; o < 3; o++)
                {
                    sum += amp * TilePerlin(u, v, freq, 100f + o * 37f);
                    total += amp;
                    amp *= 0.5f;
                    freq *= 2;
                }
                n[y * size + x] = sum / total;
            }
        Normalize(n);

        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(n[i], n[i], n[i], 1f);
        tex.SetPixels(px);
        tex.Apply();
        return SavePng(tex, path, imp =>
        {
            imp.sRGBTexture = false;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 128;
            imp.textureCompression = TextureImporterCompression.Compressed;
        });
    }

    // Tileable Perlin by blending 4 offset samples
    static float TilePerlin(float u, float v, float freq, float seed)
    {
        float x = u * freq, y = v * freq;
        float a = Mathf.PerlinNoise(x + seed, y + seed);
        float b = Mathf.PerlinNoise(x - freq + seed, y + seed);
        float c = Mathf.PerlinNoise(x + seed, y - freq + seed);
        float d = Mathf.PerlinNoise(x - freq + seed, y - freq + seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    Texture2D LoadOrCreateDroplet(string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((0.42f - d) / 0.06f)));
            }
        tex.Apply();
        return SavePng(tex, path, imp =>
        {
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
        });
    }

    static Texture2D SavePng(Texture2D tex, string path, System.Action<TextureImporter> setup)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        setup(imp);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float Fbm(float x, float y)
    {
        float sum = 0f, amp = 0.5f, total = 0f, f = 1f;
        for (int o = 0; o < 4; o++)
        {
            sum += amp * Mathf.PerlinNoise(x * f, y * f);
            total += amp;
            amp *= 0.5f;
            f *= 2f;
        }
        return sum / total;
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-5f, b - a));
        return t * t * (3f - 2f * t);
    }

    static void Normalize(float[] a)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var v in a) { if (v < min) min = v; if (v > max) max = v; }
        float inv = 1f / Mathf.Max(0.0001f, max - min);
        for (int i = 0; i < a.Length; i++) a[i] = (a[i] - min) * inv;
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
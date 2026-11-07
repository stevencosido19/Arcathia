#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > VFX > Water Wave Builder
/// Generates: crescent wave mesh (1 draw call, ~200 verts), cel colour ramp,
/// tileable noise, droplet sprite, material, trail + impact particles,
/// and a prefab with WaterWaveController already attached.
/// </summary>
public class WaterWaveBuilder : EditorWindow
{
    [MenuItem("Tools/VFX/Water Wave Builder")]
    static void Open() => GetWindow<WaterWaveBuilder>("Water Wave Builder");

    // ---- output
    string outputFolder = "Assets/VFX/WaterWave/Generated";
    string waveName = "WaterWaveProjectile";
    bool addToScene = true;
    bool regenerateNoise = false;

    // ---- shape
    float width = 3.2f;
    float thickness = 1.0f;
    float arcDegrees = 115f;
    float bendBack = 0.45f;
    float skew = 0.85f;
    float taper = 0.7f;
    float tilt = 0f;
    float layerSpacing = 0.35f;
    float depth = 1.2f;
    float curl = 0.6f;
    float bodyThickness = 0.45f;
    int layers = 2;
    int segments = 20;

    // ---- look
    Gradient gradient;
    Color foamColor = Color.white;
    float intensity = 1.15f;

    // ---- particles (keep low for mobile)
    bool useTrail = true;
    int trailMaxParticles = 24;
    float trailRatePerMeter = 6f;
    bool useImpact = true;
    int impactCount = 14;

    // ---- projectile defaults
    float speed = 16f;
    float lifetime = 1.4f;

    Vector2 scroll;

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
                new GradientColorKey(new Color(0.07f, 0.24f, 0.55f), 0f),
                new GradientColorKey(new Color(0.13f, 0.45f, 0.85f), 0.40f),
                new GradientColorKey(new Color(0.30f, 0.78f, 1.00f), 0.70f),
                new GradientColorKey(Color.white, 0.88f)
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
        regenerateNoise = EditorGUILayout.Toggle("Regenerate Noise Texture", regenerateNoise);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Wave Shape", EditorStyles.boldLabel);
        width = EditorGUILayout.Slider("Width", width, 0.5f, 10f);
        thickness = EditorGUILayout.Slider("Thickness", thickness, 0.2f, 3f);
        arcDegrees = EditorGUILayout.Slider("Arc Degrees", arcDegrees, 40f, 180f);
        bendBack = EditorGUILayout.Slider("Tips Bend Back (Z)", bendBack, 0f, 1.5f);
        skew = EditorGUILayout.Slider("Asymmetry", skew, 0.5f, 1.5f);
        taper = EditorGUILayout.Slider("Tip Sharpness", taper, 0.3f, 1.5f);
        tilt = EditorGUILayout.Slider("Tilt (Z deg, diagonal slash)", tilt, -90f, 90f);
        layers = EditorGUILayout.IntSlider("Layers (back to front)", layers, 1, 3);
        depth = EditorGUILayout.Slider("Depth (side-view thickness)", depth, 0f, 3f);
        curl = EditorGUILayout.Slider("Crest Curl (overhang)", curl, 0f, 1.5f);
        bodyThickness = EditorGUILayout.Slider("Body Thickness (solid lens)", bodyThickness, 0f, 1.5f);
        layerSpacing = EditorGUILayout.Slider("Layer Z Spacing", layerSpacing, 0f, 1f);
        segments = EditorGUILayout.IntSlider("Segments (mobile: 12-20)", segments, 8, 40);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
        gradient = EditorGUILayout.GradientField("Color Ramp (Fixed = cel)", gradient);
        foamColor = EditorGUILayout.ColorField("Foam Color", foamColor);
        intensity = EditorGUILayout.Slider("Intensity", intensity, 0.5f, 3f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Particles", EditorStyles.boldLabel);
        useTrail = EditorGUILayout.Toggle("Trail Droplets", useTrail);
        using (new EditorGUI.DisabledScope(!useTrail))
        {
            trailMaxParticles = EditorGUILayout.IntSlider("Max Trail Particles", trailMaxParticles, 4, 48);
            trailRatePerMeter = EditorGUILayout.Slider("Rate / Meter", trailRatePerMeter, 1f, 20f);
        }
        useImpact = EditorGUILayout.Toggle("Impact Splash", useImpact);
        using (new EditorGUI.DisabledScope(!useImpact))
            impactCount = EditorGUILayout.IntSlider("Impact Droplets", impactCount, 4, 30);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projectile Defaults", EditorStyles.boldLabel);
        speed = EditorGUILayout.FloatField("Speed", speed);
        lifetime = EditorGUILayout.FloatField("Lifetime", lifetime);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Water Wave Prefab", GUILayout.Height(34)))
            Build();

        EditorGUILayout.HelpBox(
            "Tip: swap the generated Noise texture with your own hand-painted foam/splash texture " +
            "(R = fingers, G = streaks) to get closer to your reference art.", MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    // ============================================================== BUILD
    void Build()
    {
        var shader = Shader.Find("VFX/StylizedWaterWave");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Water Wave Builder",
                "Shader 'VFX/StylizedWaterWave' not found. Import StylizedWaterWave.shader first.", "OK");
            return;
        }

        EnsureFolder(outputFolder);
        string f = outputFolder.TrimEnd('/');

        // ---- textures
        var ramp = SaveRamp(f + "/" + waveName + "_Ramp.png");
        var noise = LoadOrCreateNoise(f + "/WaterWave_Noise.png");
        var drop = LoadOrCreateDroplet(f + "/WaterWave_Droplet.png");

        // ---- material
        string matPath = f + "/" + waveName + "_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = shader;
        mat.SetTexture("_RampTex", ramp);
        mat.SetTexture("_NoiseTex", noise);
        mat.SetColor("_FoamColor", foamColor);
        mat.SetFloat("_Intensity", intensity);
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

        // ---- mesh
        Mesh newMesh = BuildMesh();
        string meshPath = f + "/" + waveName + "_Mesh.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            AssetDatabase.CreateAsset(newMesh, meshPath);
            mesh = newMesh;
        }
        else
        {
            EditorUtility.CopySerialized(newMesh, mesh);
            DestroyImmediate(newMesh);
        }

        // ---- hierarchy
        var root = new GameObject(waveName);
        var ctrl = root.AddComponent<WaterWaveController>();

        var waveGO = new GameObject("Wave");
        waveGO.transform.SetParent(root.transform, false);
        waveGO.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        waveGO.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = waveGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        ParticleSystem trail = null, impact = null;
        if (useTrail) trail = CreateTrail(root.transform, dropMat);
        if (useImpact) impact = CreateImpact(root.transform, dropMat);

        // ---- controller defaults
        ctrl.waveRenderer = mr;
        ctrl.trailDroplets = trail;
        ctrl.impactSplash = impact;
        ctrl.speed = speed;
        ctrl.lifetime = lifetime;
        Vector3 e = mesh.bounds.extents;
        float m = Mathf.Max(e.x, e.y);
        ctrl.hitBoxHalfExtents = Mathf.Abs(tilt) > 1f
            ? new Vector3(m * 0.75f, m * 0.75f, Mathf.Max(e.z, 0.3f))
            : new Vector3(e.x, e.y, Mathf.Max(e.z, 0.3f));

        // ---- save prefab
        string prefabPath = f + "/" + waveName + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (addToScene && prefab != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            Selection.activeGameObject = inst;
        }
        else
        {
            Selection.activeObject = prefab;
        }
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[WaterWaveBuilder] Built '{waveName}' -> {prefabPath}  (verts: {mesh.vertexCount}, tris: {mesh.triangles.Length / 3})");
    }

    // ============================================================== MESH
    // Solid crescent "lens" body, layered back->front in ONE mesh (one draw call).
    // Each layer = 2 sheets (underside + top face) that meet at the tail and the crest tip and
    // bulge apart in the middle, so the wave has real volume from every angle.
    // UV: x along arc, y = 0 inner edge -> 1 outer foam edge.
    // Color: R = layer/sheet brightness shift, G = foam strength.
    Vector2 Profile(float v, float th, float env, float thick, float k)
    {
        // returns (radial offset from the outer arc, z) of the wave face at v
        float c3 = v * v * v;
        float rho = -th * (1f - v) - curl * thick * 0.5f * c3 * v * env;
        float z = (depth * (v - 0.5f) + curl * depth * c3) * env * Mathf.Lerp(1f, 0.7f, k);
        return new Vector2(rho, z);
    }

    Mesh BuildMesh()
    {
        const int rows = 7;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var cols = new List<Color32>();
        var tris = new List<int>();

        for (int L = 0; L < layers; L++)
        {
            float k = layers == 1 ? 0.5f : L / (float)(layers - 1);
            float arc = arcDegrees * Mathf.Deg2Rad * Mathf.Lerp(1f, 0.78f, k);
            float chord = width * Mathf.Lerp(1f, 0.82f, k);
            float thick = thickness * Mathf.Lerp(1f, 0.62f, k);
            float R = chord * 0.5f / Mathf.Sin(arc * 0.5f);
            float sag = R * (1f - Mathf.Cos(arc * 0.5f));
            float zOff = k * layerSpacing;
            float belly = bodyThickness * Mathf.Lerp(1f, 0.7f, k);

            for (int sheet = 0; sheet < 2; sheet++)      // 0 = underside, 1 = top face
            {
                float sgn = sheet == 0 ? -1f : 1f;
                int baseIndex = verts.Count;

                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float a = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, t);
                    float tp = Mathf.Pow(t, skew);
                    float env = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * tp)), taper); // 0 at tips, 1 mid
                    float th = thick * env;

                    for (int r = 0; r < rows; r++)
                    {
                        float v = r / (rows - 1f);
                        Vector2 p = Profile(v, th, env, thick, k);

                        // push the two sheets apart along the face normal -> lens-shaped body
                        Vector2 p0 = Profile(Mathf.Clamp01(v - 0.02f), th, env, thick, k);
                        Vector2 p1 = Profile(Mathf.Clamp01(v + 0.02f), th, env, thick, k);
                        Vector2 tan = p1 - p0;
                        float len = tan.magnitude;
                        if (len > 1e-5f)
                        {
                            Vector2 n = new Vector2(-tan.y, tan.x) / len;
                            float bulge = belly * env * Mathf.Sin(Mathf.PI * Mathf.Clamp01(v / 0.85f));
                            p += n * (sgn * bulge);
                        }

                        float rad = R + p.x;
                        float x = Mathf.Sin(a) * rad;
                        float y = Mathf.Cos(a) * rad - R + sag * 0.5f + k * thickness * 0.15f;
                        float z = -(1f - Mathf.Cos(a)) * R * bendBack + zOff + p.y;

                        verts.Add(new Vector3(x, y, z));
                        uvs.Add(new Vector2(t, v));
                        float colR = Mathf.Clamp01(k * 0.6f + (sheet == 1 ? 0.4f : 0f));
                        float colG = Mathf.Lerp(0.35f, 1f, k) * (sheet == 1 ? 1f : 0.5f);
                        cols.Add(new Color32((byte)(colR * 255f), (byte)(colG * 255f), 0, 255));
                    }
                }

                for (int i = 0; i < segments; i++)
                    for (int r = 0; r < rows - 1; r++)
                    {
                        int a = baseIndex + i * rows + r;
                        int b = a + 1;
                        int c = a + rows;
                        int d = c + 1;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                    }
            }
        }

        var mesh = new Mesh { name = waveName + "_Mesh" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ========================================================== PARTICLES
    ParticleSystem CreateTrail(Transform parent, Material dropMat)
    {
        var go = new GameObject("TrailDroplets");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.93f, 1f), Color.white);
        main.gravityModifier = 0.6f;
        main.maxParticles = trailMaxParticles;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.rateOverDistance = trailRatePerMeter;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(width * 0.8f, thickness * 0.5f, 0.2f);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        SetupParticleRenderer(go, dropMat);
        return ps;
    }

    ParticleSystem CreateImpact(Transform parent, Material dropMat)
    {
        var go = new GameObject("ImpactSplash");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.88f, 1f), Color.white);
        main.gravityModifier = 1.2f;
        main.maxParticles = Mathf.Max(impactCount + 4, 8);

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)impactCount) });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 38f;
        sh.radius = 0.25f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        SetupParticleRenderer(go, dropMat);
        return ps;
    }

    static void SetupParticleRenderer(GameObject go, Material dropMat)
    {
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = dropMat;
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.06f;
        r.lengthScale = 1.6f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // =========================================================== TEXTURES
    Texture2D SaveRamp(string path)
    {
        const int w = 256, h = 2;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int x = 0; x < w; x++)
        {
            Color c = gradient.Evaluate(x / (w - 1f));
            for (int y = 0; y < h; y++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return SavePng(tex, path, imp =>
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed; // tiny, keeps hard bands clean
            imp.npotScale = TextureImporterNPOTScale.None;
        });
    }

    Texture2D LoadOrCreateNoise(string path)
    {
        if (!regenerateNoise)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
        }

        const int size = 128;
        var r = new float[size * size];
        var g = new float[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                r[y * size + x] = Fbm(u, v, 4, 100f, 3);
                g[y * size + x] = Fbm(u, v, 6, 300f, 2);
            }
        Normalize(r);
        Normalize(g);

        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(r[i], g[i], 0f, 1f);
        tex.SetPixels(px);
        tex.Apply();

        return SavePng(tex, path, imp =>
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false; // data texture
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 128;
            imp.textureCompression = TextureImporterCompression.Compressed; // ASTC/ETC2 on mobile
        });
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
                float a = Mathf.Clamp01((0.42f - d) / 0.06f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return SavePng(tex, path, imp =>
        {
            imp.textureType = TextureImporterType.Default;
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

    // Tileable Perlin by blending 4 offset samples
    static float Tile(float u, float v, float freq, float seed)
    {
        float x = u * freq, y = v * freq;
        float a = Mathf.PerlinNoise(x + seed, y + seed);
        float b = Mathf.PerlinNoise(x - freq + seed, y + seed);
        float c = Mathf.PerlinNoise(x + seed, y - freq + seed);
        float d = Mathf.PerlinNoise(x - freq + seed, y - freq + seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Fbm(float u, float v, int baseFreq, float seed, int octaves)
    {
        float sum = 0f, amp = 0.5f, total = 0f;
        int f = baseFreq;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Tile(u, v, f, seed + o * 37f);
            total += amp;
            amp *= 0.5f;
            f *= 2;
        }
        return sum / total;
    }

    static void Normalize(float[] a)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var f in a) { if (f < min) min = f; if (f > max) max = f; }
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
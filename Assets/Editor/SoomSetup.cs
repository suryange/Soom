// SoomSetup.cs  (Editor-only)
// One-click setup for the SOOM sandstorm-clarity PoC on Meta Quest (URP + OpenXR).
//
//   SOOM ▸ Setup ▸ 1. Install XR Packages (OpenXR)   -> adds xr.management + xr.openxr
//   SOOM ▸ Setup ▸ 2. Build PoC Scene                -> creates+wires every object/asset
//   SOOM ▸ Setup ▸ Open Read-Me / XR steps            -> prints the remaining manual clicks
//
// The build step is idempotent: re-running reuses objects/assets found by name instead of
// duplicating them. Everything the README asked you to do by hand (material, signal asset,
// camera tracking, dust quad, particles, controller wiring) happens here in code.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.XR;   // TrackedPoseDriver (Input System) for HMD head tracking
using UnityEngine.InputSystem;
using UnityEngine.UI;               // Image / HorizontalLayoutGroup / LayoutElement for rep icons
using TMPro;                        // world-space text

static class SoomSetup
{
    const string SignalPath   = "Assets/Settings/BreathSignal.asset";
    const string EventsPath   = "Assets/Settings/BreathEvents.asset";
    const string MaterialPath = "Assets/Settings/DustVignette.mat";
    const string NoisePath    = "Assets/dust_noise.png";

    // --- natural desert palette (pale, low-saturation sand — not muddy orange) -----------
    static readonly Color SandGround = new Color(0.82f, 0.74f, 0.58f); // dry sand floor
    static readonly Color DustHaze   = new Color(0.87f, 0.81f, 0.69f); // pale airborne/surface dust
    static readonly Color FogSand    = new Color(0.85f, 0.80f, 0.70f); // milky sand haze on the horizon

    // ---------------------------------------------------------------- packages
    static UnityEditor.PackageManager.Requests.AddAndRemoveRequest _xrRequest;

    [MenuItem("SOOM/Setup/1. Install XR Packages (OpenXR)")]
    static void InstallXR()
    {
        if (_xrRequest != null && !_xrRequest.IsCompleted)
        {
            Debug.Log("[SOOM] XR package install already in progress…");
            return;
        }
        // Name-only (no version) lets the Package Manager resolve the version compatible with
        // THIS Unity instead of pinning one in manifest.json (which can brick project load).
        // AddAndRemove adds BOTH in a single request — Client only handles one request at a
        // time, so two back-to-back Client.Add() calls would silently drop the second.
        _xrRequest = UnityEditor.PackageManager.Client.AddAndRemove(
            packagesToAdd: new[] { "com.unity.xr.management", "com.unity.xr.openxr" });
        EditorApplication.update += PollXR;
        Debug.Log("[SOOM] Installing com.unity.xr.management + com.unity.xr.openxr… " +
                  "(this can take a minute; watch for the result here).");
    }

    static void PollXR()
    {
        if (_xrRequest == null || !_xrRequest.IsCompleted) return;
        EditorApplication.update -= PollXR;
        if (_xrRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
            Debug.Log("[SOOM] XR packages installed ✓  Next: SOOM ▸ Setup ▸ 2. Build PoC Scene, " +
                      "then the XR Plug-in Management clicks (SOOM ▸ Setup ▸ Open Read-Me · XR steps).");
        else
            Debug.LogError("[SOOM] XR install failed: " + _xrRequest.Error?.message);
        _xrRequest = null;
    }

    // ---------------------------------------------------------------- scene build
    [MenuItem("SOOM/Setup/2. Build PoC Scene")]
    static void BuildScene()
    {
        var signal = GetOrCreateSignal();
        var mat    = GetOrCreateDustMaterial();
        ConfigureNoiseTexture();

        // --- camera (head-tracked) ------------------------------------------------
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            cam = camGo.GetComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.transform.position = new Vector3(0f, 1.6f, 0f); // standing eye height (HMD overrides in VR)
        cam.transform.rotation = Quaternion.identity;
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 200f);
        // HMD pose -> camera, so looking around in the headset feels right (comfort).
        if (cam.GetComponent<TrackedPoseDriver>() == null)
        {
            var tpd = cam.gameObject.AddComponent<TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(new InputAction(
                "HMD Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction(
                "HMD Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        }

        // --- dust overlay quad (child of camera, head-locked) ---------------------
        var quad = FindChildByName(cam.transform, "DustOverlay");
        if (quad == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "DustOverlay";
            Object.DestroyImmediate(q.GetComponent<MeshCollider>()); // no physics on an overlay
            quad = q.transform;
            quad.SetParent(cam.transform, false);
        }
        quad.localPosition = new Vector3(0f, 0f, 0.5f);
        quad.localRotation = Quaternion.identity;
        quad.localScale    = new Vector3(1.2f, 1.2f, 1f);
        var quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.sharedMaterial = mat;
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        // --- world sand particles -------------------------------------------------
        var sand = GameObject.Find("SandParticles");
        if (sand == null) sand = new GameObject("SandParticles", typeof(ParticleSystem));
        sand.transform.position = new Vector3(0f, 1.2f, 2f);
        var ps = sand.GetComponent<ParticleSystem>();
        ConfigureParticles(ps);

        // --- desert ground + drifting surface sand + warm light -------------------
        var ground = GameObject.Find("DesertGround");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "DesertGround";
            Object.DestroyImmediate(ground.GetComponent<MeshCollider>());
        }
        ground.transform.position   = Vector3.zero;
        ground.transform.localScale = new Vector3(30f, 1f, 30f); // 300 m of desert
        ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateGroundMaterial();

        var gdust = GameObject.Find("GroundDust");
        if (gdust == null)
        {
            gdust = GameObject.CreatePrimitive(PrimitiveType.Plane);
            gdust.name = "GroundDust";
            Object.DestroyImmediate(gdust.GetComponent<MeshCollider>());
        }
        gdust.transform.position   = new Vector3(0f, 0.03f, 0f); // just above the floor
        gdust.transform.localScale = new Vector3(30f, 1f, 30f);
        var groundDustRenderer = gdust.GetComponent<Renderer>();
        groundDustRenderer.sharedMaterial = GetOrCreateGroundDustMaterial();
        groundDustRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ConfigureEnvironment();

        // --- controller + driver --------------------------------------------------
        var fx = GameObject.Find("SandstormFX");
        if (fx == null) fx = new GameObject("SandstormFX", typeof(SandstormController));
        var ctrl = fx.GetComponent<SandstormController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("signal").objectReferenceValue        = signal;
        so.FindProperty("dustOverlay").objectReferenceValue   = quadRenderer;
        so.FindProperty("sandParticles").objectReferenceValue = ps;
        so.FindProperty("groundDust").objectReferenceValue    = groundDustRenderer;
        // Natural-feeling dust amounts (lighter than the defaults so it doesn't look like a wall).
        so.FindProperty("maxEmission").floatValue    = 320f;   // fewer airborne grains
        so.FindProperty("maxFogDensity").floatValue  = 0.022f; // thin horizon haze, not pea-soup
        so.FindProperty("minGroundDust").floatValue  = 0.08f;  // faint drift when clear
        so.FindProperty("maxGroundDust").floatValue  = 0.6f;   // visible but not opaque in the storm
        so.ApplyModifiedPropertiesWithoutUndo();

        var events = GetOrCreateEvents();
        var driverGo = GameObject.Find("ClarityDriver");
        if (driverGo == null) driverGo = new GameObject("ClarityDriver", typeof(ClarityPoCDriver));
        var driver = driverGo.GetComponent<ClarityPoCDriver>();
        var dso = new SerializedObject(driver);
        dso.FindProperty("signal").objectReferenceValue = signal;
        dso.FindProperty("events").objectReferenceValue = events;
        dso.ApplyModifiedPropertiesWithoutUndo();

        // --- save -----------------------------------------------------------------
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = fx;
        Debug.Log("[SOOM] Desert scene built & wired (ground + drifting surface sand + airborne haze " +
                  "+ particles + warm light/fog). Press Play, hold Up/Down arrows (or right thumbstick " +
                  "in headset) to breathe the sandstorm clear. Tune on SandstormFX ▸ SandstormController.");
    }

    // ---------------------------------------------------------------- UI build
    [MenuItem("SOOM/Setup/3. Build Breath UI")]
    static void BuildUI()
    {
        var signal = GetOrCreateSignal();
        var events = GetOrCreateEvents();

        // Korean-capable dynamic font; falls back to the TMP default (Latin-only) if unavailable.
        var font = GetOrCreateKoreanFont() ?? TMP_Settings.defaultFontAsset;
        if (font == null)
            Debug.LogWarning("[SOOM] No usable TMP font. Import TMP Essential Resources " +
                "(Window ▸ TextMeshPro) and/or drop a Korean .ttf in Assets/Fonts, then re-run.");

        var cam = Camera.main; // worldCamera for the world-space canvases

        // Single root that holds the whole breath UI. Destroy + recreate so re-running doesn't
        // stack duplicate components (PopupSystem etc.) on the existing root.
        var existing = GameObject.Find("SoomUI");
        if (existing != null) Object.DestroyImmediate(existing);
        var root = new GameObject("SoomUI");

        // --- transient popup system (instruction lines + SUCCESS!) ------------------
        var popupTemplate = MakeWorldCanvas("WorldPopupTemplate", root.transform, cam, new Vector2(900f, 320f));
        var popupGroup    = popupTemplate.AddComponent<CanvasGroup>();
        var popupLabel    = MakeLabel(popupTemplate.transform, "", 110f, Color.white, font);
        var worldPopup    = popupTemplate.AddComponent<WorldPopup>();
        Wire(worldPopup, w =>
        {
            w.FindProperty("label").objectReferenceValue = popupLabel;
            w.FindProperty("group").objectReferenceValue = popupGroup;
        });
        popupTemplate.SetActive(false); // template; PopupSystem instantiates copies from it

        var popupSys = root.AddComponent<PopupSystem>();
        Wire(popupSys, p =>
        {
            p.FindProperty("events").objectReferenceValue      = events;
            p.FindProperty("popupPrefab").objectReferenceValue = worldPopup;
        });

        // --- rep icons row (left → right) -------------------------------------------
        var repCanvas = MakeWorldCanvas("RepIcons", root.transform, cam, new Vector2(900f, 220f));
        AddComfort(repCanvas, vertical: 0.3f);  // a little above eye line
        var layout = repCanvas.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;   // honour LayoutElement preferred sizes
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // round dot
        const int repCount = 3; // matches ClarityPoCDriver.repsTotal default
        var icons = new Image[repCount];
        for (int i = 0; i < repCount; i++)
        {
            var dot = new GameObject($"Rep{i}", typeof(Image), typeof(LayoutElement));
            dot.transform.SetParent(repCanvas.transform, false);
            var img = dot.GetComponent<Image>();
            img.sprite = knob;
            img.color  = new Color(1f, 1f, 1f, 0.25f);
            var le = dot.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 130f;
            icons[i] = img;
        }
        var repIcons = repCanvas.AddComponent<BreathRepIcons>();
        Wire(repIcons, r =>
        {
            r.FindProperty("events").objectReferenceValue = events;
            var arr = r.FindProperty("icons");
            arr.arraySize = repCount;
            for (int i = 0; i < repCount; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
        });

        // --- persistent guide / feedback line ---------------------------------------
        var guideCanvas = MakeWorldCanvas("GuidePanel", root.transform, cam, new Vector2(1100f, 160f));
        AddComfort(guideCanvas, vertical: -0.4f); // below eye line
        var guideLabel = MakeLabel(guideCanvas.transform, "", 64f, new Color(1f, 0.95f, 0.85f), font);
        var guidePanel = guideCanvas.AddComponent<BreathGuidePanel>();
        Wire(guidePanel, g =>
        {
            g.FindProperty("signal").objectReferenceValue = signal;
            g.FindProperty("events").objectReferenceValue = events;
            g.FindProperty("label").objectReferenceValue  = guideLabel;
        });

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = root;
        Debug.Log("[SOOM] Breath UI built under 'SoomUI' (popups + rep icons + guide line) and wired to " +
                  "BreathSignal/BreathEvents. Press Play; hold Up to inhale clear, release to exhale — " +
                  "each completed breath fills a dot, 3 reps → SUCCESS!. If text is blank, import TMP " +
                  "Essential Resources and re-run this.");
    }

    [MenuItem("SOOM/Setup/Open Read-Me · XR steps")]
    static void XrSteps()
    {
        Debug.Log(
        "[SOOM] Remaining manual XR clicks (UI-only, version-fragile to script):\n" +
        "1. Project Settings ▸ XR Plug-in Management ▸ install if prompted.\n" +
        "2. Switch the platform tab to Android, tick 'OpenXR'.\n" +
        "3. XR Plug-in Management ▸ OpenXR ▸ add interaction profile 'Oculus Touch Controller'\n" +
        "   and enable the 'Meta Quest Support' feature group.\n" +
        "4. File ▸ Build Settings ▸ switch platform to Android, set the Quest as run target.\n" +
        "5. Build & Run.  (Editor PC play works without any of this for quick visual tuning.)");
    }

    // ---------------------------------------------------------------- helpers
    static BreathSignalSO GetOrCreateSignal()
    {
        var s = AssetDatabase.LoadAssetAtPath<BreathSignalSO>(SignalPath);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<BreathSignalSO>();
            AssetDatabase.CreateAsset(s, SignalPath);
            AssetDatabase.SaveAssets();
        }
        return s;
    }

    static BreathEventsSO GetOrCreateEvents()
    {
        var e = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(EventsPath);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<BreathEventsSO>();
            AssetDatabase.CreateAsset(e, EventsPath);
            AssetDatabase.SaveAssets();
        }
        return e;
    }

    // The default TMP font (LiberationSans) has NO Hangul glyphs -> Korean shows as □ tofu boxes.
    // Build a DYNAMIC TMP font asset from a Korean .ttf so glyphs are pulled on demand. For a PoC
    // we borrow a system Korean font; for a shipped build swap in a properly licensed font
    // (e.g. Noto Sans KR, SIL OFL) dropped into Assets/Fonts.
    const string FontDir   = "Assets/Fonts";
    const string KoreanTtf = "Assets/Fonts/AppleGothic.ttf";
    const string KoreanTmp = "Assets/Fonts/AppleGothic SDF.asset";
    static readonly string[] SystemKoreanFonts =
    {
        "/System/Library/Fonts/Supplemental/AppleGothic.ttf",
    };

    static TMP_FontAsset GetOrCreateKoreanFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanTmp);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(FontDir)) AssetDatabase.CreateFolder("Assets", "Fonts");

        // Copy a Korean .ttf into the project if one isn't there yet.
        if (AssetDatabase.LoadAssetAtPath<Font>(KoreanTtf) == null)
        {
            string src = null;
            foreach (var p in SystemKoreanFonts) if (System.IO.File.Exists(p)) { src = p; break; }
            if (src == null)
            {
                Debug.LogWarning("[SOOM] No Korean system font found. Drop a Korean .ttf at " +
                                 KoreanTtf + " and re-run '3. Build Breath UI'.");
                return null;
            }
            System.IO.File.Copy(src, KoreanTtf, true);
            AssetDatabase.ImportAsset(KoreanTtf, ImportAssetOptions.ForceSynchronousImport);
        }

        var srcFont = AssetDatabase.LoadAssetAtPath<Font>(KoreanTtf);
        if (srcFont == null) { Debug.LogError("[SOOM] Failed to import " + KoreanTtf); return null; }

        var fontAsset = TMP_FontAsset.CreateFontAsset(srcFont); // dynamic atlas (default)
        if (fontAsset == null) { Debug.LogError("[SOOM] TMP_FontAsset.CreateFontAsset failed."); return null; }

        AssetDatabase.CreateAsset(fontAsset, KoreanTmp);
        // The generated atlas texture + material must be stored as sub-assets or they vanish on reload.
        if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
        {
            fontAsset.atlasTextures[0].name = "Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "AppleGothic SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(KoreanTmp);
        return fontAsset;
    }

    static Material GetOrCreateDustMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (m == null)
        {
            var sh = Shader.Find("SOOM/DustVignette");
            if (sh == null) { Debug.LogError("[SOOM] Shader 'SOOM/DustVignette' not found."); return null; }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, MaterialPath);
        }
        m.SetColor("_Color", DustHaze);
        m.SetFloat("_MaxAlpha", 0.8f);     // lighter haze, not a solid wall
        m.SetFloat("_InnerRadius", 0.28f); // keep a bit more of the centre clear
        var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        if (noise != null)
        {
            m.SetTexture("_DustTex", noise);
            m.SetTextureScale("_DustTex", new Vector2(3f, 3f)); // finer grains across the FOV
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static void ConfigureNoiseTexture()
    {
        var imp = AssetImporter.GetAtPath(NoisePath) as TextureImporter;
        if (imp == null) return;
        if (imp.wrapMode != TextureWrapMode.Repeat)
        {
            imp.wrapMode = TextureWrapMode.Repeat; // seamless scrolling needs tiling
            imp.SaveAndReimport();
        }
    }

    static void ConfigureParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // world sand -> parallax/depth
        main.startLifetime   = new ParticleSystem.MinMaxCurve(2.5f, 4.5f); // varied so they don't pulse in sync
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);  // big, overlapping puffs read as haze
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 6.2831f); // random spin -> no repeating sprite
        main.maxParticles    = 1000;
        main.startColor      = new Color(DustHaze.r, DustHaze.g, DustHaze.b, 0.4f);
        main.playOnAwake     = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f; // SandstormController drives this from Clarity

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(8f, 4f, 1.5f); // wide, tall, some depth for parallax

        var vel = ps.velocityOverLifetime;     // sideways wind drift
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        // All three axes MUST share the same curve mode or Unity spams
        // "Particle Velocity curves must all be in the same mode". Use plain Constant on every
        // axis (unambiguous); per-particle variety comes from the randomized startSpeed instead.
        vel.x = new ParticleSystem.MinMaxCurve(0.9f); // steady wind across
        vel.y = new ParticleSystem.MinMaxCurve(0.0f);
        vel.z = new ParticleSystem.MinMaxCurve(0.0f);

        // Fade each puff IN then OUT over its life — kills the hard pop/disappear.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Slow tumble so overlapping soft sprites churn like real airborne dust.
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode  = ParticleSystemRenderMode.Billboard;
        r.sortingFudge = 0f;
        r.sharedMaterial = GetOrCreateParticleMaterial(); // soft round sprite, not the tiling noise
    }

    static Material GetOrCreateParticleMaterial()
    {
        const string p = "Assets/Settings/SandParticle.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) { Debug.LogError("[SOOM] URP Particles/Unlit shader not found."); return null; }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetFloat("_Surface", 1f);   // transparent
        m.SetFloat("_Blend", 0f);     // alpha blend
        m.SetTexture("_BaseMap", GetOrCreateDustSprite()); // soft round alpha — this is the fix
        m.SetColor("_BaseColor", DustHaze);
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    // Procedurally bake a soft, wispy round dust sprite so billboards read as puffs, not squares.
    static Texture2D GetOrCreateDustSprite()
    {
        const string p = "Assets/Settings/DustPuff.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        if (existing != null) return existing;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "DustPuff" };
        float c = size * 0.5f, maxR = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - c), dy = (y + 0.5f - c);
            float d  = Mathf.Sqrt(dx * dx + dy * dy) / maxR;       // 0 center .. 1 edge
            float a  = 1f - Mathf.SmoothStep(0.05f, 1f, d);        // soft radial falloff
            float n  = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);    // break the perfect disc into wisps
            a *= Mathf.Lerp(0.45f, 1f, n);
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }
        tex.SetPixels(px);
        tex.Apply();
        AssetDatabase.CreateAsset(tex, p);
        AssetDatabase.SaveAssets();
        return tex;
    }

    static Material GetOrCreateGroundMaterial()
    {
        const string p = "Assets/Settings/DesertGround.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) { Debug.LogError("[SOOM] URP/Lit shader not found."); return null; }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetColor("_BaseColor", SandGround);
        m.SetFloat("_Smoothness", 0.05f); // dry sand, not shiny
        m.SetFloat("_Metallic", 0f);
        var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        if (noise != null)
        {
            m.SetTexture("_BaseMap", noise);
            m.SetTextureScale("_BaseMap", new Vector2(40f, 40f)); // fine sand grain across 300 m
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static Material GetOrCreateGroundDustMaterial()
    {
        const string p = "Assets/Settings/GroundDust.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            var sh = Shader.Find("SOOM/GroundDust");
            if (sh == null) { Debug.LogError("[SOOM] Shader 'SOOM/GroundDust' not found."); return null; }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetColor("_Color", DustHaze);
        var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
        if (noise != null) m.SetTexture("_DustTex", noise);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    // Warm desert lighting + sand-tinted fog so cleared vision still reads as "desert".
    static void ConfigureEnvironment()
    {
        var light = Object.FindAnyObjectByType<Light>();
        if (light == null)
        {
            var lgo = new GameObject("Directional Light", typeof(Light));
            light = lgo.GetComponent<Light>();
            light.type = LightType.Directional;
        }
        if (light.type == LightType.Directional)
        {
            light.color = new Color(1f, 0.96f, 0.88f); // soft sun, low orange tint
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
        }
        RenderSettings.ambientLight = new Color(0.72f, 0.69f, 0.62f); // lighter, neutral-warm fill
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.ExponentialSquared;
        RenderSettings.fogColor     = FogSand; // pale sand haze, not muddy orange
    }

    static Transform FindChildByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    // ---------------------------------------------------------------- UI helpers
    // A world-space canvas scaled so 1 UI unit = 1 mm (localScale 0.001): a 900-wide rect is
    // 0.9 m across, comfortably legible ~1.8 m from the eyes.
    static GameObject MakeWorldCanvas(string name, Transform parent, Camera cam, Vector2 size)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta   = size;
        rt.localScale  = Vector3.one * 0.001f;
        return go;
    }

    static TextMeshProUGUI MakeLabel(Transform canvas, string text, float fontSize, Color color, TMP_FontAsset font)
    {
        var go = new GameObject("Label", typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text      = text;
        t.fontSize  = fontSize;
        t.color     = color;
        t.alignment = TextAlignmentOptions.Center;
        var rt = t.rectTransform;          // stretch to fill the canvas rect
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return t;
    }

    static void AddComfort(GameObject canvas, float vertical)
    {
        var cb = canvas.AddComponent<ComfortBillboard>();
        var so = new SerializedObject(cb);
        so.FindProperty("vertical").floatValue = vertical;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Wire(Object component, System.Action<SerializedObject> set)
    {
        var so = new SerializedObject(component);
        set(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

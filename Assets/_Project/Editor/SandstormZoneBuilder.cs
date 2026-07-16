using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 모래바람 구역(기능 명세 4장) 씬 배선용 원클릭 에디터 빌더.
/// 현재 열려 있는 씬에 트리거 존(BoxCollider) + 모래폭풍 파티클 + SandstormController +
/// 진입/지시문 World Space UI를 생성하고 서로 연결한다.
///
/// .unity 씬 파일 자체는 이 저장소의 편집/커밋 대상이 아니므로, 실제 배선은 항상
/// Unity 에디터를 열고 있는 사람이 이 메뉴 아이템을 직접 실행해서 만들어야 한다.
/// 이미 "SandstormZone" 루트가 씬에 있으면 중복 생성 없이 그 오브젝트를 선택만 한다.
/// </summary>
public static class SandstormZoneBuilder
{
    private const string RootName = "SandstormZone";
    private const string BreathEventsChannelPath = "Assets/_Project/Scripts/System/BreathEventsChannel.asset";
    private const string KoreanFontAssetPath = "Assets/_Project/Resources/Fonts/NotoSansKR SDF.asset";
    private const string DustTexturePath = "Assets/_Project/Art/Particles/DustPuff.asset";
    private const string ParticleMaterialPath = "Assets/_Project/Materials/SandParticle.mat";
    private const string VignetteMaterialPath = "Assets/_Project/Materials/DustVignette.mat";
    private const string Scene03Path = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";

    [MenuItem("SOOM/Scene 03/Setup Sandstorm Breath Zone")]
    public static void Build()
    {
        Material particleMaterial = BuildDustAssets();
        Material vignetteMaterial = AssetDatabase.LoadAssetAtPath<Material>(VignetteMaterialPath);
        GameObject existingRoot = GameObject.Find(RootName);

        BreathEventsSO breathEvents = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(BreathEventsChannelPath);
        if (breathEvents == null)
        {
            Debug.LogWarning($"[SandstormZoneBuilder] BreathEventsSO 에셋을 찾을 수 없습니다: {BreathEventsChannelPath}. " +
                              "SandstormController.breathEventsChannel을 수동으로 연결해야 합니다.");
        }

        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
        if (koreanFont == null)
        {
            Debug.LogWarning($"[SandstormZoneBuilder] 한글 TMP 폰트를 찾을 수 없습니다: {KoreanFontAssetPath}. " +
                              "기본 폰트로 생성되니 필요 시 수동으로 교체하세요.");
        }

        Vector3 anchorPosition = FindAnchorPosition();

        GameObject root = existingRoot;
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Sandstorm Zone");
            root.transform.position = anchorPosition;
        }

        GameObject triggerGO = BuildTriggerZone(root.transform);
        GameObject particlesGO = BuildParticles(root.transform, particleMaterial);
        Renderer vignetteRenderer = BuildVignetteOverlay(vignetteMaterial);
        GameObject chapterUI = BuildWorldText(
            root.transform, "ChapterUI",
            "<size=140%>끝없는 모래</size>\n불안한 첫 발",
            new Vector3(0f, 2.6f, 0f), 3f, koreanFont);
        GameObject zoneTextUI = BuildWorldText(
            root.transform, "ZoneTextUI",
            "모래 폭풍",
            new Vector3(0f, 2.1f, 0f), 2.4f, koreanFont);
        GameObject instructionUI = BuildWorldText(
            root.transform, "InstructionUI",
            "깊게 호흡하여 모래폭풍을 잠재우세요",
            new Vector3(0f, 2.1f, 0f), 1.6f, koreanFont);

        Transform controllerTransform = root.transform.Find("SandstormController");
        GameObject controllerGO = controllerTransform != null
            ? controllerTransform.gameObject
            : CreateChild(root.transform, "SandstormController");
        SandstormController controller = GetOrAddComponent<SandstormController>(controllerGO);

        GuidingLightController guidingLight = Object.FindFirstObjectByType<GuidingLightController>();

        BreathCircleUI breathCircleUI = FindSceneComponent<BreathCircleUI>("BreathCircle_Outside");
        if (breathCircleUI == null)
        {
            Debug.LogWarning("[SandstormZoneBuilder] 씬에서 BreathCircleUI를 찾지 못했습니다. " +
                              "호흡 UI를 표시하려면 SandstormController.breathCircleUI를 수동으로 연결하세요.");
        }

        HologramMessage guidingLightProvider = FindSceneComponent<HologramMessage>(null);
        if (guidingLightProvider == null)
            Debug.LogWarning("[SandstormZoneBuilder] 런타임 Guiding Light 공급자인 HologramMessage를 찾지 못했습니다.");

        WireController(controller, breathEvents, chapterUI, zoneTextUI, instructionUI,
            particlesGO.GetComponent<ParticleSystem>(), vignetteRenderer,
            guidingLight, guidingLightProvider, breathCircleUI);

        WireTrigger(triggerGO.GetComponent<SandstormZoneTrigger>(), controller);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[SandstormZoneBuilder] 모래폭풍 구역 생성/갱신 완료. " +
                  "먼지 텍스처/머티리얼/파티클/XR 비네트 참조를 갱신했습니다.");
    }

    /// <summary>CI 또는 명령줄에서 Scene 03을 열고 동일한 빌더를 실행한다.</summary>
    public static void BuildScene03Batch()
    {
        EditorSceneManager.OpenScene(Scene03Path, OpenSceneMode.Single);
        Build();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static Vector3 FindAnchorPosition()
    {
        GameObject finalGuidingWaypoint = GameObject.Find("Waypoint_12");
        if (finalGuidingWaypoint != null)
        {
            Vector3 waypointPosition = finalGuidingWaypoint.transform.position;
            waypointPosition.y = 0f;
            return waypointPosition;
        }

        GameObject clue = GameObject.Find("ClueObject");
        if (clue != null)
        {
            return clue.transform.position + clue.transform.forward * 5f;
        }
        return Vector3.zero;
    }

    private static GameObject BuildTriggerZone(Transform parent)
    {
        Transform existing = parent.Find("SandstormZoneTrigger");
        GameObject go = existing != null ? existing.gameObject : CreateChild(parent, "SandstormZoneTrigger");
        if (existing == null)
            go.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        BoxCollider box = GetOrAddComponent<BoxCollider>(go);
        box.isTrigger = true;
        if (existing == null) box.size = new Vector3(6f, 3f, 6f);

        GetOrAddComponent<SandstormZoneTrigger>(go);
        return go;
    }

    private static GameObject BuildParticles(Transform parent, Material particleMaterial)
    {
        Transform existing = parent.Find("SandstormParticles");
        GameObject go = existing != null ? existing.gameObject : CreateChild(parent, "SandstormParticles");
        if (existing == null)
            go.transform.localPosition = new Vector3(0f, 1f, 0f);

        ParticleSystem ps = GetOrAddComponent<ParticleSystem>(go);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 5.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.62f, 0.50f, 0.33f, 0.18f),
            new Color(0.82f, 0.70f, 0.48f, 0.38f));
        main.maxParticles = 1600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f; // SandstormController가 런타임에 Lerp로 제어

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(16f, 5f, 16f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.22f, 0.65f);
        noise.frequency = 0.18f;
        noise.scrollSpeed = 0.22f;
        noise.octaveCount = 1;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.76f, 0.68f, 0.5f), 0f),
                new GradientColorKey(new Color(0.76f, 0.68f, 0.5f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.4f, 0.3f),
                new GradientAlphaKey(0.4f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.15f), new Keyframe(0.22f, 1f),
            new Keyframe(0.72f, 0.9f), new Keyframe(1f, 0.05f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.alignment = ParticleSystemRenderSpace.View;
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;
            if (particleMaterial != null) psRenderer.sharedMaterial = particleMaterial;
        }

        if (!EditorApplication.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return go;
    }

    private static Material BuildDustAssets()
    {
        EnsureFolder("Assets/_Project/Art");
        EnsureFolder("Assets/_Project/Art/Particles");
        EnsureFolder("Assets/_Project/Materials");

        Texture2D dustTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(DustTexturePath);
        if (dustTexture == null)
        {
            const int size = 256;
            dustTexture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "DustPuff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float warpX = (Mathf.PerlinNoise(x * 0.031f + 18.2f, y * 0.031f + 7.4f) - 0.5f) * 0.24f;
                float warpY = (Mathf.PerlinNoise(x * 0.027f + 51.7f, y * 0.027f + 33.1f) - 0.5f) * 0.24f;
                float radius = Mathf.Sqrt((u + warpX) * (u + warpX) + (v + warpY) * (v + warpY));
                float falloff = 1f - Mathf.SmoothStep(0.18f, 1f, radius);
                float detail = Mathf.Lerp(0.55f, 1f,
                    Mathf.PerlinNoise(x * 0.064f + 4.3f, y * 0.064f + 12.9f));
                float alpha = Mathf.Clamp01(falloff * detail);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            dustTexture.SetPixels(pixels);
            dustTexture.Apply(true, false);
            AssetDatabase.CreateAsset(dustTexture, DustTexturePath);
        }

        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Particles/Standard Unlit");
        Material particleMaterial = CreateOrUpdateMaterial(ParticleMaterialPath, particleShader);
        if (particleMaterial != null)
        {
            if (particleMaterial.HasProperty("_BaseMap")) particleMaterial.SetTexture("_BaseMap", dustTexture);
            if (particleMaterial.HasProperty("_MainTex")) particleMaterial.SetTexture("_MainTex", dustTexture);
            if (particleMaterial.HasProperty("_BaseColor"))
                particleMaterial.SetColor("_BaseColor", new Color(0.78f, 0.64f, 0.42f, 0.5f));
            if (particleMaterial.HasProperty("_Surface")) particleMaterial.SetFloat("_Surface", 1f);
            if (particleMaterial.HasProperty("_Blend")) particleMaterial.SetFloat("_Blend", 0f);
            if (particleMaterial.HasProperty("_SrcBlend"))
                particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (particleMaterial.HasProperty("_DstBlend"))
                particleMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (particleMaterial.HasProperty("_ZWrite")) particleMaterial.SetFloat("_ZWrite", 0f);
            particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particleMaterial.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(particleMaterial);
        }

        Shader vignetteShader = Shader.Find("SOOM/DustVignette");
        Material vignetteMaterial = CreateOrUpdateMaterial(VignetteMaterialPath, vignetteShader);
        if (vignetteMaterial != null)
        {
            vignetteMaterial.SetColor("_Color", new Color(0.58f, 0.45f, 0.28f, 0.78f));
            vignetteMaterial.SetFloat("_Obscurity", 0f);
            vignetteMaterial.SetFloat("_Clarity", 1f);
            EditorUtility.SetDirty(vignetteMaterial);
        }

        AssetDatabase.SaveAssets();
        return particleMaterial;
    }

    private static Material CreateOrUpdateMaterial(string path, Shader shader)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (shader == null)
        {
            Debug.LogError($"[SandstormZoneBuilder] Shader를 찾지 못해 머티리얼을 만들 수 없습니다: {path}");
            return material;
        }
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static Renderer BuildVignetteOverlay(Material material)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            foreach (Camera candidate in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.name.Contains("Main Camera") || candidate.transform.parent?.name.Contains("XR") == true)
                {
                    camera = candidate;
                    break;
                }
            }
        }
        if (camera == null)
        {
            Debug.LogWarning("[SandstormZoneBuilder] XR Main Camera를 찾지 못해 DustVignetteOverlay를 생성하지 않았습니다.");
            return null;
        }

        Transform existing = camera.transform.Find("DustVignetteOverlay");
        GameObject overlay;
        if (existing != null)
        {
            overlay = existing.gameObject;
        }
        else
        {
            overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "DustVignetteOverlay";
            Undo.RegisterCreatedObjectUndo(overlay, "Create Dust Vignette Overlay");
            Object.DestroyImmediate(overlay.GetComponent<Collider>());
            overlay.transform.SetParent(camera.transform, false);
        }

        float distance = Mathf.Max(camera.nearClipPlane + 0.03f, 0.08f);
        float coverage = distance * 10f;
        overlay.transform.localPosition = new Vector3(0f, 0f, distance);
        overlay.transform.localRotation = Quaternion.identity;
        overlay.transform.localScale = new Vector3(coverage, coverage, 1f);
        overlay.layer = camera.gameObject.layer;

        MeshRenderer renderer = overlay.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.enabled = false;
        return renderer;
    }

    private static GameObject BuildWorldText(Transform parent, string name, string text, Vector3 localPosition,
        float fontSize, TMP_FontAsset font)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : CreateChild(parent, name);
        if (existing == null)
            go.transform.localPosition = localPosition;

        TextMeshPro tmp = GetOrAddComponent<TextMeshPro>(go);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;

        GetOrAddComponent<FaceCamera>(go);

        if (existing == null) go.SetActive(false); // SandstormController가 시점에 맞춰 활성화
        return go;
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Sandstorm Zone");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    private static T FindSceneComponent<T>(string preferredName) where T : Component
    {
        T fallback = null;
        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component == null || !component.gameObject.scene.IsValid()) continue;
            if (!string.IsNullOrEmpty(preferredName) && component.name == preferredName)
                return component;
            if (fallback == null) fallback = component;
        }
        return fallback;
    }

    private static void WireController(SandstormController controller, BreathEventsSO breathEvents,
        GameObject chapterUI, GameObject zoneTextUI, GameObject instructionUI,
        ParticleSystem particles, Renderer vignetteRenderer, GuidingLightController guidingLight,
        HologramMessage guidingLightProvider, BreathCircleUI breathCircleUI)
    {
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("breathEventsChannel").objectReferenceValue = breathEvents;
        so.FindProperty("chapterUIRoot").objectReferenceValue = chapterUI;
        so.FindProperty("zoneTextUIRoot").objectReferenceValue = zoneTextUI;
        so.FindProperty("sandstormParticles").objectReferenceValue = particles;
        so.FindProperty("dustVignetteRenderer").objectReferenceValue = vignetteRenderer;
        so.FindProperty("guidingLight").objectReferenceValue = guidingLight;
        so.FindProperty("guidingLightProvider").objectReferenceValue = guidingLightProvider;
        SerializedProperty postClearWaypoints = so.FindProperty("postClearWaypoints");
        Transform waypoint13 = GameObject.Find("Waypoint_13")?.transform;
        Transform waypoint14 = GameObject.Find("Waypoint_14")?.transform;
        int waypointCount = (waypoint13 != null ? 1 : 0) + (waypoint14 != null ? 1 : 0);
        postClearWaypoints.arraySize = waypointCount;
        int waypointIndex = 0;
        if (waypoint13 != null) postClearWaypoints.GetArrayElementAtIndex(waypointIndex++).objectReferenceValue = waypoint13;
        if (waypoint14 != null) postClearWaypoints.GetArrayElementAtIndex(waypointIndex).objectReferenceValue = waypoint14;
        so.FindProperty("breathCircleUI").objectReferenceValue = breathCircleUI;
        so.FindProperty("instructionUIRoot").objectReferenceValue = instructionUI;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireTrigger(SandstormZoneTrigger trigger, SandstormController controller)
    {
        SerializedObject so = new SerializedObject(trigger);
        so.FindProperty("sandstormController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

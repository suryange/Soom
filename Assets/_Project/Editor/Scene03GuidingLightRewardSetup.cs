using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.XR.CoreUtils;

/// <summary>
/// Scene 03의 길잡이 등불 프로젝트 프리팹을 만들고 호흡 성공 보상/안내에 연결한다.
/// </summary>
internal static class Scene03GuidingLightRewardSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string PrefabPath = "Assets/_Project/Prefabs/GuidingLight.prefab";
    private const string MaterialPath = "Assets/_Project/Prefabs/GuidingLightGlow.mat";
    private const string VolumeProfilePath = "Assets/_Project/Arts/Scene03_GuidingLightVolume.asset";

    [MenuItem("SOOM/Scene 03/Build and Setup Guiding Light Reward")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[Scene03GuidingLightRewardSetup] {ScenePath} 씬을 연 상태에서 실행해주세요.");
            return;
        }

        Setup(scene);
        EditorSceneManager.SaveScene(scene);
    }

    public static void ApplyToScene03()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Setup(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Scene03GuidingLightRewardSetup] Scene 03 저장 완료.");
    }

    private static void Setup(Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new MissingReferenceException($"길잡이 등불 프리팹을 찾을 수 없습니다: {PrefabPath}");

        HologramMessage hologram = Object.FindFirstObjectByType<HologramMessage>(FindObjectsInactive.Include);
        if (hologram == null)
            throw new MissingReferenceException("Scene 03에서 HologramMessage를 찾지 못했습니다.");

        BreathMissionGuideController guide =
            Object.FindFirstObjectByType<BreathMissionGuideController>(FindObjectsInactive.Include);
        if (guide == null)
            throw new MissingReferenceException("Scene 03에서 BreathMissionGuideController를 찾지 못했습니다.");

        hologram.guidingLightPrefab = prefab;
        hologram.postBreathPlayerSpawnPoint = EnsurePostBreathPlayerSpawnPoint(hologram);
        hologram.xrOrigin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        CreateOrUpdateGlowMaterial();
        TuneGuidingLightPrefabVisuals();
        EnsureSceneBloom();

        SerializedObject guideObject = new SerializedObject(guide);
        guideObject.FindProperty("guidingLightMessage").stringValue = "길라잡이";
        guideObject.ApplyModifiedPropertiesWithoutUndo();

        // 프로젝트 프리팹 참조로 교체했으므로 기존 씬 내부 비활성 템플릿은 제거한다.
        Transform oldTemplate = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.gameObject.scene == scene && item.name == "GuidingLight_Template");
        if (oldTemplate != null)
            Undo.DestroyObjectImmediate(oldTemplate.gameObject);

        if (hologram.spawnPoint == null)
            Debug.LogWarning("[Scene03GuidingLightRewardSetup] GuidingLightSpawnPoint 참조가 비어 있습니다.");
        if (hologram.missionWaypoints == null || hologram.missionWaypoints.Length == 0)
            Debug.LogWarning("[Scene03GuidingLightRewardSetup] missionWaypoints가 비어 있어 등불이 이동하지 않습니다.");

        EditorUtility.SetDirty(hologram);
        EditorUtility.SetDirty(guide);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = prefab;

        Debug.Log(
            "[Scene03GuidingLightRewardSetup] 완료: GuidingLight.prefab 생성, " +
            "호흡 성공 스폰 참조와 '길라잡이' 안내 연결, 씬 템플릿 제거.");
    }

    private static GameObject BuildGuidingLightPrefab()
    {
        Material glowMaterial = CreateOrUpdateGlowMaterial();

        GameObject root = new GameObject("GuidingLight");
        GuidingLightController controller = root.AddComponent<GuidingLightController>();
        controller.speed = 2.2f;
        controller.waypointTolerance = 0.35f;
        controller.maxLeadDistance = 7f;
        controller.resumeLeadDistance = 4.5f;
        controller.turnSpeed = 360f;
        controller.useSineMovement = true;
        controller.sineAmplitude = 0.75f;
        controller.sineWavelength = 5f;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glow.name = "GlowOrb";
        glow.transform.SetParent(root.transform, false);
        glow.transform.localScale = Vector3.one * 0.55f;
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        MeshRenderer renderer = glow.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = glowMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        GameObject lightObject = new GameObject("PointLight", typeof(Light));
        lightObject.transform.SetParent(root.transform, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.82f, 0.38f);
        light.intensity = 3.2f;
        light.range = 6f;
        light.shadows = LightShadows.None;
        controller.guidingLight = light;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static Material CreateOrUpdateGlowMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                throw new MissingReferenceException("Guiding Light에 사용할 기본 Shader를 찾지 못했습니다.");

            material = new Material(shader) { name = "GuidingLightGlow" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader != null && material.shader != litShader)
            material.shader = litShader;

        Color glowColor = new Color(1f, 0.72f, 0.2f, 0.38f);
        material.color = glowColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glowColor);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.65f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(4f, 2.4f, 0.45f, 1f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform EnsurePostBreathPlayerSpawnPoint(HologramMessage hologram)
    {
        GameObject existing = GameObject.Find("PostBreathPlayerSpawnPoint");
        Transform target = existing != null ? existing.transform : null;
        if (target == null)
        {
            var go = new GameObject("PostBreathPlayerSpawnPoint");
            Undo.RegisterCreatedObjectUndo(go, "Create Post Breath Player Spawn Point");
            target = go.transform;
        }

        if (hologram.spawnPoint == null)
            return target;

        Vector3 forward = Vector3.forward;
        if (hologram.missionWaypoints != null && hologram.missionWaypoints.Length > 0 &&
            hologram.missionWaypoints[0] != null)
        {
            forward = Vector3.ProjectOnPlane(
                hologram.missionWaypoints[0].position - hologram.spawnPoint.position, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
        }

        Vector3 floorSpawn = hologram.spawnPoint.position;
        floorSpawn.y = 0f;
        target.SetPositionAndRotation(floorSpawn + forward * 2.2f,
            Quaternion.LookRotation(forward, Vector3.up));
        return target;
    }

    private static void TuneGuidingLightPrefabVisuals()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Material glowMaterial = CreateOrUpdateGlowMaterial();
            Transform glow = root.transform.Find("GlowOrb");
            if (glow != null)
            {
                glow.localScale = Vector3.one * 0.55f;
                MeshRenderer renderer = glow.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = glowMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            Light light = root.GetComponentInChildren<Light>(true);
            if (light != null)
            {
                light.color = new Color(1f, 0.82f, 0.38f);
                light.intensity = 3.2f;
                light.range = 6f;
                light.shadows = LightShadows.None;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureSceneBloom()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[Scene03GuidingLightRewardSetup] Bloom Volume Profile을 찾지 못했습니다: {VolumeProfilePath}");
            return;
        }

        Volume volume = Object.FindObjectsByType<Volume>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == "Scene03 Guiding Light Volume");
        if (volume == null)
        {
            GameObject volumeObject = new GameObject("Scene03 Guiding Light Volume");
            Undo.RegisterCreatedObjectUndo(volumeObject, "Create Scene03 Guiding Light Volume");
            volume = volumeObject.AddComponent<Volume>();
        }

        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            EditorUtility.SetDirty(cameraData);
        }
    }
}

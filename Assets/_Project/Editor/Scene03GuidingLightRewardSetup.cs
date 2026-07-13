using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene 03의 길잡이 등불 프로젝트 프리팹을 만들고 호흡 성공 보상/안내에 연결한다.
/// </summary>
internal static class Scene03GuidingLightRewardSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string PrefabPath = "Assets/PowerUp/Prefabs/PowerUpContainerYellowDollar.prefab";
    private const string MaterialPath = "Assets/_Project/Prefabs/GuidingLightGlow.mat";

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

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glow.name = "GlowOrb";
        glow.transform.SetParent(root.transform, false);
        glow.transform.localScale = Vector3.one * 0.28f;
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        glow.GetComponent<MeshRenderer>().sharedMaterial = glowMaterial;

        GameObject lightObject = new GameObject("PointLight", typeof(Light));
        lightObject.transform.SetParent(root.transform, false);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.82f, 0.38f);
        light.intensity = 2.5f;
        light.range = 5f;
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
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                throw new MissingReferenceException("Guiding Light에 사용할 기본 Shader를 찾지 못했습니다.");

            material = new Material(shader) { name = "GuidingLightGlow" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        Color glowColor = new Color(1f, 0.72f, 0.2f, 1f);
        material.color = glowColor;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glowColor);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", glowColor * 3f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }
}

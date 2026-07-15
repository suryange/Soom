using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 모래바람 구역(기능 명세 4장) 씬 배선용 원클릭 에디터 빌더.
/// 현재 열려 있는 씬에 트리거 존(BoxCollider) + 모래폭풍 파티클 + SandstormController +
/// 진입/지시문 World Space UI를 생성하고 서로 연결한다.
///
/// .unity 씬 파일 자체는 이 저장소의 편집/커밋 대상이 아니므로, 실제 배선은 항상
/// Unity 에디터를 열고 있는 사람이 이 메뉴 아이템을 직접 실행해서 만들어야 한다.
/// 이미 "SandstormZone" 루트가 씬에 있으면 중복 생성 없이 그 오브젝트를 선택만 한다.
/// </summary>
internal static class SandstormZoneBuilder
{
    private const string RootName = "SandstormZone";
    private const string BreathEventsChannelPath = "Assets/_Project/Scripts/System/BreathEventsChannel.asset";
    private const string KoreanFontAssetPath = "Assets/_Project/Resources/Fonts/NotoSansKR SDF.asset";

    [MenuItem("SOOM/Scene 03/Setup Sandstorm Breath Zone")]
    public static void Build()
    {
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
        GameObject particlesGO = BuildParticles(root.transform);
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
            particlesGO.GetComponent<ParticleSystem>(), guidingLight, guidingLightProvider, breathCircleUI);

        WireTrigger(triggerGO.GetComponent<SandstormZoneTrigger>(), controller);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[SandstormZoneBuilder] 모래폭풍 구역 생성/갱신 완료. " +
                  "기존 Transform은 보존했으며, Wind Clip 미지정 시 절차적 바람 루프를 사용합니다.");
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

    private static GameObject BuildParticles(Transform parent)
    {
        Transform existing = parent.Find("SandstormParticles");
        GameObject go = existing != null ? existing.gameObject : CreateChild(parent, "SandstormParticles");
        if (existing == null)
            go.transform.localPosition = new Vector3(0f, 1f, 0f);

        ParticleSystem ps = GetOrAddComponent<ParticleSystem>(go);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.startLifetime = 4f;
        main.startSpeed = 0.6f;
        main.startSize = 0.6f;
        main.startColor = new Color(0.76f, 0.68f, 0.5f, 0.35f);
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f; // SandstormController가 런타임에 Lerp로 제어

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 4f, 12f);

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

        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            Material defaultParticleMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            if (defaultParticleMat != null) psRenderer.sharedMaterial = defaultParticleMat;
        }

        if (!EditorApplication.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return go;
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
        ParticleSystem particles, GuidingLightController guidingLight,
        HologramMessage guidingLightProvider, BreathCircleUI breathCircleUI)
    {
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("breathEventsChannel").objectReferenceValue = breathEvents;
        so.FindProperty("chapterUIRoot").objectReferenceValue = chapterUI;
        so.FindProperty("zoneTextUIRoot").objectReferenceValue = zoneTextUI;
        so.FindProperty("sandstormParticles").objectReferenceValue = particles;
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

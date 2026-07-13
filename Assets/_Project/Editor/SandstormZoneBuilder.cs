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

    [MenuItem("SOOM/Build Sandstorm Zone")]
    private static void Build()
    {
        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null)
        {
            Debug.LogWarning($"[SandstormZoneBuilder] 씬에 이미 '{RootName}'이(가) 존재합니다. " +
                              "다시 만들려면 기존 오브젝트를 지운 뒤 실행하세요.", existingRoot);
            Selection.activeGameObject = existingRoot;
            return;
        }

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

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Sandstorm Zone");
        root.transform.position = anchorPosition;

        GameObject triggerGO = BuildTriggerZone(root.transform);
        GameObject particlesGO = BuildParticles(root.transform);
        GameObject chapterUI = BuildWorldText(
            root.transform, "ChapterUI",
            "<size=140%>되살아난 모래</size>\n불안한 첫 발",
            new Vector3(0f, 2.6f, 0f), 3f, koreanFont);
        GameObject zoneTextUI = BuildWorldText(
            root.transform, "ZoneTextUI",
            "모래 폭풍",
            new Vector3(0f, 2.1f, 0f), 2.4f, koreanFont);
        GameObject instructionUI = BuildWorldText(
            root.transform, "InstructionUI",
            "깊은 호흡으로 모래폭풍을 잠재우세요",
            new Vector3(0f, 2.1f, 0f), 1.6f, koreanFont);

        GameObject controllerGO = new GameObject("SandstormController");
        Undo.RegisterCreatedObjectUndo(controllerGO, "Build Sandstorm Zone");
        controllerGO.transform.SetParent(root.transform, false);
        SandstormController controller = controllerGO.AddComponent<SandstormController>();

        GuidingLightController guidingLight = Object.FindFirstObjectByType<GuidingLightController>();
        if (guidingLight == null)
        {
            Debug.LogWarning("[SandstormZoneBuilder] 씬에서 GuidingLightController를 찾지 못했습니다. " +
                              "등불 빛 페이드 연출을 쓰려면 SandstormController.guidingLight를 수동으로 연결하세요.");
        }

        BreathCircleUI breathCircleUI = Object.FindFirstObjectByType<BreathCircleUI>();
        if (breathCircleUI == null)
        {
            Debug.LogWarning("[SandstormZoneBuilder] 씬에서 BreathCircleUI를 찾지 못했습니다. " +
                              "호흡 UI를 표시하려면 SandstormController.breathCircleUI를 수동으로 연결하세요.");
        }

        WireController(controller, breathEvents, chapterUI, zoneTextUI, instructionUI,
            particlesGO.GetComponent<ParticleSystem>(), guidingLight, breathCircleUI);

        WireTrigger(triggerGO.GetComponent<SandstormZoneTrigger>(), controller);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        Debug.Log("[SandstormZoneBuilder] 모래바람 구역 배선 완료. " +
                  "트리거 존 위치/크기, UI 텍스트 위치, 옵션 SFX/파티클 머티리얼은 씬에서 직접 다듬어주세요.");
    }

    private static Vector3 FindAnchorPosition()
    {
        GameObject clue = GameObject.Find("ClueObject");
        if (clue != null)
        {
            return clue.transform.position + clue.transform.forward * 5f;
        }
        return Vector3.zero;
    }

    private static GameObject BuildTriggerZone(Transform parent)
    {
        GameObject go = new GameObject("SandstormZoneTrigger");
        Undo.RegisterCreatedObjectUndo(go, "Build Sandstorm Zone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(6f, 3f, 6f);

        go.AddComponent<SandstormZoneTrigger>();
        return go;
    }

    private static GameObject BuildParticles(Transform parent)
    {
        GameObject go = new GameObject("SandstormParticles");
        Undo.RegisterCreatedObjectUndo(go, "Build Sandstorm Zone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

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

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return go;
    }

    private static GameObject BuildWorldText(Transform parent, string name, string text, Vector3 localPosition,
        float fontSize, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Sandstorm Zone");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;

        go.AddComponent<FaceCamera>();

        go.SetActive(false); // SandstormController가 시점에 맞춰 활성화
        return go;
    }

    private static void WireController(SandstormController controller, BreathEventsSO breathEvents,
        GameObject chapterUI, GameObject zoneTextUI, GameObject instructionUI,
        ParticleSystem particles, GuidingLightController guidingLight, BreathCircleUI breathCircleUI)
    {
        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("breathEventsChannel").objectReferenceValue = breathEvents;
        so.FindProperty("chapterUIRoot").objectReferenceValue = chapterUI;
        so.FindProperty("zoneTextUIRoot").objectReferenceValue = zoneTextUI;
        so.FindProperty("sandstormParticles").objectReferenceValue = particles;
        so.FindProperty("guidingLight").objectReferenceValue = guidingLight;
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

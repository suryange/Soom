// Scene02InsideTutorialBuilder.cs  (Editor-only)
// Scene_02_InGame_Inside(우주선 내부) 튜토리얼에 필요한 씬 오브젝트를 한 번에 생성·배선한다.
//
//   SOOM ▸ Build Scene02 Inside Tutorial
//
// 생성/배선하는 것:
//   - InsideTutorialSequence 루트 + 시퀀스 컨트롤러 컴포넌트
//   - WakeUpVignetteEffect 자식 오브젝트 (2.1.1 기상 연출)
//   - 공용 안내 문구 World Space Canvas + TutorialMessagePanel (2.1.2 / 2.2 성공 안내 / 2.3 완료 안내)
//   - HatchController 자식 오브젝트 + 해치 플레이스홀더 큐브 + 스팟 라이트 플레이스홀더 (2.3)
//
// 씬에 이미 'SOOM ▸ Build Breath Circle UI'로 만든 BreathCircleUI("SoomUI/BreathCircle")가
// 있으면 자동으로 찾아 연결한다. 없으면 breathCircleUI 필드는 비워두고 경고만 남긴다
// (InsideTutorialSequence는 null 가드가 되어 있어 예외 없이 동작한다).
//
// 멱등적(idempotent)으로 동작한다: 기존에 만들어둔 InsideTutorialSequence 루트가 있으면
// 지우고 새로 만든다.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

static class Scene02InsideTutorialBuilder
{
    const string RootName = "InsideTutorialSequence";

    // Assets/_Project/Scripts/System/BreathEventsChannel.asset
    const string EventsChannelGuid = "91e1c179be2cf2444978f568e7476427";

    [MenuItem("SOOM/Build Scene02 Inside Tutorial")]
    static void Build()
    {
        var events = FindEventsChannel();
        if (events == null)
        {
            Debug.LogWarning("[SOOM] BreathEventsChannel.asset을 찾지 못했습니다(guid " + EventsChannelGuid +
                              "). breathEventsChannel 없이 진행합니다 — 이 경우 호흡 미션은 자동으로 성공 처리됩니다.");
        }

        var cam = Camera.main;

        // --- 멱등성: 기존 루트가 있으면 제거하고 새로 만든다 ---------------------
        var existingRoot = GameObject.Find(RootName);
        if (existingRoot != null) Object.DestroyImmediate(existingRoot);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Scene02 Inside Tutorial");

        // --- 2.1.1 기상 연출 ------------------------------------------------------
        var wakeUpGo = new GameObject("WakeUpVignetteEffect");
        wakeUpGo.transform.SetParent(root.transform, false);
        var wakeUpEffect = wakeUpGo.AddComponent<WakeUpVignetteEffect>();

        // --- 공용 안내 문구 World Space Canvas -------------------------------------
        var messagePanel = BuildMessagePanel(root.transform, cam);

        // --- 2.2 호흡 캘리브레이션: 기존 BreathCircle UI가 있으면 재사용 ----------
        var breathCircleUI = FindExistingBreathCircleUI();
        if (breathCircleUI == null)
        {
            Debug.LogWarning("[SOOM] 씬에서 BreathCircleUI를 찾지 못했습니다. 먼저 " +
                "'SOOM ▸ Build Breath Circle UI'로 호흡 UI를 만든 뒤 이 메뉴를 다시 실행하면 자동으로 연결됩니다.");
        }

        // --- 2.3 해치 개방: HatchController + 플레이스홀더 큐브/라이트 ------------
        var hatchController = BuildHatchController(root.transform);

        // --- InsideTutorialSequence 컨트롤러 배선 ----------------------------------
        var sequence = root.AddComponent<InsideTutorialSequence>();
        Wire(sequence, so =>
        {
            so.FindProperty("breathEventsChannel").objectReferenceValue = events;
            so.FindProperty("wakeUpEffect").objectReferenceValue = wakeUpEffect;
            so.FindProperty("messagePanel").objectReferenceValue = messagePanel;
            so.FindProperty("breathCircleUI").objectReferenceValue = breathCircleUI;
            so.FindProperty("hatchController").objectReferenceValue = hatchController;
            // nextScene은 스크립트 기본값이 이미 Scene_03_InGame_Outside이므로 별도 설정하지 않는다.
        });

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;

        Debug.Log("[SOOM] 'InsideTutorialSequence' 루트를 생성하고 기상 연출 / 안내 UI / 해치 컨트롤러를 배선했습니다. " +
                  "우주선 내부 모델, 안내 음성 클립 등 옵션 에셋은 인스펙터에서 추가로 연결해주세요.");
    }

    static BreathEventsSO FindEventsChannel()
    {
        string path = AssetDatabase.GUIDToAssetPath(EventsChannelGuid);
        var byGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<BreathEventsSO>(path);
        if (byGuid != null) return byGuid;

        // 폴백: guid가 어긋난 경우(재임포트 등) 타입/이름으로 재검색
        var guids = AssetDatabase.FindAssets("t:BreathEventsSO BreathEventsChannel");
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(assetPath);
            if (asset != null) return asset;
        }
        return null;
    }

    static BreathCircleUI FindExistingBreathCircleUI()
    {
        // BreathCircleSetup.cs가 만드는 'SoomUI/BreathCircle' 관례를 그대로 따라간다.
        var soomUI = GameObject.Find("SoomUI");
        if (soomUI == null) return null;

        var breathCircle = FindChildByName(soomUI.transform, "BreathCircle");
        return breathCircle != null ? breathCircle.GetComponent<BreathCircleUI>() : null;
    }

    static TutorialMessagePanel BuildMessagePanel(Transform parent, Camera cam)
    {
        var canvasGo = MakeWorldCanvas("TutorialMessageCanvas", parent, cam, new Vector2(1400f, 400f));
        canvasGo.transform.localPosition = new Vector3(0f, 1.6f, 2f); // 임시 초기 위치. 런타임에 카메라 정면으로 재배치된다.
        canvasGo.AddComponent<FaceCamera>(); // 플레이어를 향해 항상 billboard

        var group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        canvasGo.SetActive(false); // 첫 Show() 전까지는 꺼둔다

        var textGo = new GameObject("MessageText", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.fontSize = 64f;
        tmp.color = Color.white;
        tmp.text = string.Empty; // 실제 문구는 런타임에 TutorialMessagePanel.Show()가 채운다

        var panel = canvasGo.AddComponent<TutorialMessagePanel>();
        Wire(panel, so =>
        {
            so.FindProperty("messageText").objectReferenceValue = tmp;
            so.FindProperty("group").objectReferenceValue = group;
        });

        return panel;
    }

    static HatchController BuildHatchController(Transform parent)
    {
        var hatchGo = new GameObject("HatchController");
        hatchGo.transform.SetParent(parent, false);
        hatchGo.transform.localPosition = new Vector3(0f, 1.2f, 4f); // 임시: 우주선 내부 모델 준비 전 배치 좌표

        // 해치 슬래브 플레이스홀더 (실제 우주선 모델이 준비되면 인스펙터에서 교체)
        var hatchSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hatchSlab.name = "Hatch_Placeholder";
        hatchSlab.transform.SetParent(hatchGo.transform, false);
        hatchSlab.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);

        // 해치 너머에서 쏟아지는 빛 플레이스홀더
        var lightGo = new GameObject("HatchLight_Placeholder");
        lightGo.transform.SetParent(hatchGo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1f, 1.5f);
        lightGo.transform.localRotation = Quaternion.Euler(40f, 180f, 0f); // 대략 플레이어 쪽을 비추도록
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = 90f;
        light.range = 15f;
        light.color = Color.white;
        light.intensity = 0f; // 개방 전에는 빛 없음

        var controller = hatchGo.AddComponent<HatchController>();
        Wire(controller, so =>
        {
            so.FindProperty("hatch").objectReferenceValue = hatchSlab.transform;
            so.FindProperty("hatchLight").objectReferenceValue = light;
        });

        return controller;
    }

    // BreathCircleSetup.cs와 동일한 컨벤션: 1 UI unit = 1mm (localScale 0.001)
    static GameObject MakeWorldCanvas(string name, Transform parent, Camera cam, Vector2 size)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one * 0.001f;
        return go;
    }

    static Transform FindChildByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    static void Wire(Object component, System.Action<SerializedObject> set)
    {
        var so = new SerializedObject(component);
        set(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

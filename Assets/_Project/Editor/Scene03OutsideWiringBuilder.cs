// Scene03OutsideWiringBuilder.cs (Editor-only)
// 우주선 외부(Scene_03_InGame_Outside) 미완성 배선을 한 번에 마무리하는 빌더.
//
//   SOOM ▸ Build Scene03 Outside Wiring
//
// 열린 씬에 아래 항목들을 생성/배선한다 (멱등성: 이름이 같은 기존 오브젝트는 제거 후 재생성).
//   - InteractableDataSO 에셋 (Assets/_Project/Resources/Interactables/UnknownDevice.asset)
//   - ClueObject(HologramMessage) 컴포넌트 부착 + World UI/데이터/웨이포인트/등불 배선
//   - 감지 UI(InteractableWorldUI), 호흡 UI(BreathCircleUI), 지시문 UI(MissionGuideTextUI)
//   - 웨이포인트 경로(빈 Transform 5개), 길잡이 등불 템플릿, 트리거 입력 브리지, 씬 부트스트랩
//
// 배선과 검증이 모두 끝나면 현재 씬을 저장한다.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

internal static class Scene03OutsideWiringBuilder
{
    // Assets/_Project/Scripts/System/BreathEventsChannel.asset
    const string EventsChannelGuid = "91e1c179be2cf2444978f568e7476427";

    const string InteractablesFolder = "Assets/_Project/Resources/Interactables";
    const string UnknownDeviceAssetPath = InteractablesFolder + "/UnknownDevice.asset";
    const string GuidingLightPrefabPath = "Assets/_Project/Prefabs/GuidingLight.prefab";

    [MenuItem("SOOM/Build Scene03 Outside Wiring")]
    public static void Build()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.name.Contains("Scene_03"))
        {
            Debug.LogError("[SOOM] Scene_03_InGame_Outside 씬을 연 상태에서 실행해주세요. 현재 열린 씬: " +
                (activeScene.IsValid() ? activeScene.name : "(none)"));
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
            Debug.LogWarning("[SOOM] Camera.main을 찾지 못했습니다. UI는 FaceCamera 빌보드로 폴백합니다.");

        BreathEventsSO events = FindEventsChannel();
        if (events == null)
            Debug.LogWarning("[SOOM] BreathEventsChannel.asset을 찾지 못했습니다. 관련 컴포넌트의 이벤트 채널을 수동으로 연결해주세요.");

        InteractionDetector detector = Object.FindFirstObjectByType<InteractionDetector>(FindObjectsInactive.Include);
        if (detector == null)
            Debug.LogWarning("[SOOM] 씬에서 InteractionDetector를 찾지 못했습니다. TriggerInteractionInputBridge.detector를 수동으로 연결해주세요.");

        InteractableDataSO data = CreateOrLoadInteractableData();

        GameObject clue = FindOrCreateClueObject();
        HologramMessage hologram = EnsureHologramMessage(clue);
        InteractableWorldUI worldUI = BuildDetectionUI(clue.transform, cam);
        Transform[] waypoints = BuildWaypoints(clue.transform);
        GameObject guidingLightTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(GuidingLightPrefabPath);
        if (guidingLightTemplate == null)
            guidingLightTemplate = BuildGuidingLightTemplate();
        Transform spawnPoint = EnsureSpawnPoint(clue.transform);
        Transform postBreathPlayerSpawnPoint = EnsurePostBreathPlayerSpawnPoint(spawnPoint, waypoints);
        WireHologram(hologram, clue, data, worldUI, waypoints, guidingLightTemplate, spawnPoint,
            postBreathPlayerSpawnPoint, events);
        Scene03GrabInteractionSetup.Setup(activeScene);

        BreathCircleUI breathCircleUI = BuildBreathCircleUI(cam, events);
        BuildMissionGuideTextUI(cam);
        BuildBreathMissionGuideController(breathCircleUI, events);
        BuildInputBridge(detector);
        BuildBootstrap();

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Selection.activeGameObject = clue;

        Debug.Log("[SOOM] Scene03 Outside 배선 및 Scene 저장을 완료했습니다.\n" +
            "남은 수동 단계: TriggerInteractionInputBridge.triggerAction에 XRI 트리거 액션 연결, " +
            "웨이포인트 위치를 실제 오아시스 경로로 재배치, 등불/디텍션 UI 아트 교체.");
    }

    // ------------------------------------------------------------ Interactable Data (3.2)
    static InteractableDataSO CreateOrLoadInteractableData()
    {
        if (!AssetDatabase.IsValidFolder(InteractablesFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources"))
                AssetDatabase.CreateFolder("Assets/_Project", "Resources");
            AssetDatabase.CreateFolder("Assets/_Project/Resources", "Interactables");
        }

        var data = AssetDatabase.LoadAssetAtPath<InteractableDataSO>(UnknownDeviceAssetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<InteractableDataSO>();
            AssetDatabase.CreateAsset(data, UnknownDeviceAssetPath);
        }

        data.objectName = "UNKNOWN DEVICE DETECTED";
        data.descriptionText = "Origin: Unknown";
        data.missionGuideText = "오아시스로 가야 함을 알려주는 단서입니다. 호흡 능력을 통해 길을 찾으세요.";
        data.requiresBreathing = true;
        data.targetBreathCount = 3;

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return data;
    }

    // ------------------------------------------------------------ ClueObject (기존 오브젝트 우선 재사용)
    static GameObject FindOrCreateClueObject()
    {
        var go = GameObject.Find("ClueObject");
        if (go != null) return go;

        Debug.LogWarning("[SOOM] 씬에서 'ClueObject'를 찾지 못해 플레이스홀더를 생성합니다. 실제 아트 에셋으로 교체해주세요.");

        go = new GameObject("ClueObject");
        Undo.RegisterCreatedObjectUndo(go, "Create ClueObject Placeholder");

        var closeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        closeGo.name = "messageClose";
        closeGo.transform.SetParent(go.transform, false);
        closeGo.transform.localScale = Vector3.one * 0.2f;

        var openGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        openGo.name = "messageOpen";
        openGo.transform.SetParent(go.transform, false);
        openGo.transform.localScale = Vector3.one * 0.2f;
        openGo.SetActive(false);

        return go;
    }

    static HologramMessage EnsureHologramMessage(GameObject clue)
    {
        var hologram = clue.GetComponent<HologramMessage>();
        if (hologram == null) hologram = Undo.AddComponent<HologramMessage>(clue);
        return hologram;
    }

    static void WireHologram(HologramMessage hologram, GameObject clue, InteractableDataSO data,
        InteractableWorldUI worldUI, Transform[] waypoints, GameObject guidingLightTemplate,
        Transform spawnPoint, Transform postBreathPlayerSpawnPoint, BreathEventsSO events)
    {
        var messageCloseTr = clue.transform.Find("messageClose");
        var messageOpenTr = clue.transform.Find("messageOpen");

        if (messageCloseTr != null) hologram.messageClose = messageCloseTr.gameObject;
        else Debug.LogWarning("[SOOM] ClueObject 하위에서 'messageClose'를 찾지 못했습니다. 수동으로 연결해주세요.");

        if (messageOpenTr != null) hologram.messageOpen = messageOpenTr.gameObject;
        else Debug.LogWarning("[SOOM] ClueObject 하위에서 'messageOpen'을 찾지 못했습니다. 수동으로 연결해주세요.");

        hologram.breathEvents = events;
        hologram.interactableData = data;
        hologram.worldUI = worldUI;
        hologram.missionWaypoints = waypoints;
        hologram.guidingLightPrefab = guidingLightTemplate;
        hologram.spawnPoint = spawnPoint;
        hologram.postBreathPlayerSpawnPoint = postBreathPlayerSpawnPoint;

        EditorUtility.SetDirty(hologram);
    }

    static Transform EnsureSpawnPoint(Transform clueTransform)
    {
        var existing = clueTransform.Find("GuidingLightSpawnPoint");
        if (existing != null) return existing;

        var go = new GameObject("GuidingLightSpawnPoint");
        Undo.RegisterCreatedObjectUndo(go, "Create Guiding Light Spawn Point");
        go.transform.SetParent(clueTransform, false);
        return go.transform;
    }

    static Transform EnsurePostBreathPlayerSpawnPoint(Transform guidingSpawnPoint, Transform[] waypoints)
    {
        GameObject existing = GameObject.Find("PostBreathPlayerSpawnPoint");
        Transform target = existing != null ? existing.transform : null;
        if (target == null)
        {
            var go = new GameObject("PostBreathPlayerSpawnPoint");
            Undo.RegisterCreatedObjectUndo(go, "Create Post Breath Player Spawn Point");
            target = go.transform;
        }

        Vector3 forward = Vector3.forward;
        if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
        {
            forward = Vector3.ProjectOnPlane(
                waypoints[0].position - guidingSpawnPoint.position, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
        }

        Vector3 floorSpawn = guidingSpawnPoint.position;
        floorSpawn.y = 0f;
        target.SetPositionAndRotation(floorSpawn + forward * 2.2f,
            Quaternion.LookRotation(forward, Vector3.up));
        return target;
    }

    // ------------------------------------------------------------ 감지 UI (3.2)
    static InteractableWorldUI BuildDetectionUI(Transform parent, Camera cam)
    {
        var existing = parent.Find("DetectionUI");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var canvasGo = MakeWorldCanvas("DetectionUI", parent, cam, new Vector2(900f, 300f));
        canvasGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        CompensateParentScale(canvasGo.transform, parent); // ClueObject의 비균일 스케일이 UI 크기에 곱해지지 않도록 상쇄
        canvasGo.AddComponent<FaceCamera>();
        var group = canvasGo.AddComponent<CanvasGroup>();

        var nameText = MakeText(canvasGo.transform, "ObjectNameText", "UNKNOWN DEVICE DETECTED",
            48, Color.white, new Vector2(0f, 0.55f), new Vector2(1f, 1f));
        var descText = MakeText(canvasGo.transform, "DescriptionText", "Origin: Unknown",
            32, new Color(1f, 1f, 1f, 0.8f), new Vector2(0f, 0f), new Vector2(1f, 0.45f));

        var ui = canvasGo.AddComponent<InteractableWorldUI>();
        Wire(ui, so =>
        {
            so.FindProperty("objectNameText").objectReferenceValue = nameText;
            so.FindProperty("descriptionText").objectReferenceValue = descText;
            so.FindProperty("group").objectReferenceValue = group;
        });

        canvasGo.SetActive(false); // 기본은 숨김. IInteractable.ShowUI()/HideUI()가 제어한다.
        return ui;
    }

    // ------------------------------------------------------------ 호흡 UI (3.4)
    static BreathCircleUI BuildBreathCircleUI(Camera cam, BreathEventsSO events)
    {
        BreathCircleUI shared = Object.FindObjectsByType<BreathCircleUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(circle =>
                circle != null &&
                circle.gameObject.name == "BreathCircle" &&
                circle.transform.parent != null &&
                circle.transform.parent.name == "SoomUI");

        if (shared != null)
        {
            foreach (BreathCircleUI circle in Object.FindObjectsByType<BreathCircleUI>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (circle != null && circle != shared)
                    Object.DestroyImmediate(circle.gameObject);
            }

            Canvas sharedCanvas = shared.GetComponent<Canvas>();
            if (sharedCanvas != null) sharedCanvas.worldCamera = cam;
            Wire(shared, so => so.FindProperty("events").objectReferenceValue = events);
            shared.gameObject.SetActive(false);
            return shared;
        }

        Transform parent = GetOrCreateUiRoot().transform;

        var existing = FindChildByName(parent, "BreathCircle");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var canvasGo = MakeWorldCanvas("BreathCircle", parent, cam, new Vector2(900f, 900f));

        var group = canvasGo.AddComponent<CanvasGroup>();

        var largeOutline = MakeImage(canvasGo.transform, "LargeOutline", new Vector2(700f, 700f),
            CircleSpriteFactory.CreateRing(Color.white, 0.06f), Color.white);
        var smallOutline = MakeImage(canvasGo.transform, "SmallOutline", new Vector2(280f, 280f),
            CircleSpriteFactory.CreateRing(Color.white, 0.1f), new Color(1f, 1f, 1f, 0.95f));
        var bead = MakeImage(canvasGo.transform, "Bead", new Vector2(700f, 700f),
            CircleSpriteFactory.CreateFilledCircle(Color.white), new Color(1f, 0.85f, 0.4f, 1f));
        bead.rectTransform.localScale = Vector3.one * 0.4f;

        const int slotCount = 3; // BreathManager.targetLoopCount 기본값과 일치
        var slotsParent = new GameObject("Slots", typeof(RectTransform));
        slotsParent.transform.SetParent(canvasGo.transform, false);
        var slotsRt = (RectTransform)slotsParent.transform;
        slotsRt.anchorMin = slotsRt.anchorMax = new Vector2(0.5f, 1f);
        slotsRt.anchoredPosition = new Vector2(0f, -60f);
        slotsRt.sizeDelta = new Vector2(600f, 140f);
        var layout = slotsParent.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var slotRings = new Image[slotCount];
        var slotFills = new Image[slotCount];
        var ringSprite = CircleSpriteFactory.CreateRing(Color.white, 0.12f);
        var fillSprite = CircleSpriteFactory.CreateFilledCircle(Color.white);
        for (int i = 0; i < slotCount; i++)
        {
            var slotGo = new GameObject($"Slot{i}", typeof(RectTransform), typeof(LayoutElement));
            slotGo.transform.SetParent(slotsParent.transform, false);
            var le = slotGo.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 130f;

            var ring = MakeImage(slotGo.transform, "Ring", new Vector2(130f, 130f), ringSprite, new Color(1f, 1f, 1f, 0.8f));
            var fill = MakeImage(slotGo.transform, "Fill", new Vector2(110f, 110f), fillSprite, new Color(1f, 0.85f, 0.4f, 1f));
            fill.enabled = false;
            slotRings[i] = ring;
            slotFills[i] = fill;
        }

        var ui = canvasGo.AddComponent<BreathCircleUI>();
        Wire(ui, so =>
        {
            so.FindProperty("events").objectReferenceValue = events;
            so.FindProperty("largeCircleOutline").objectReferenceValue = largeOutline;
            so.FindProperty("smallCircleOutline").objectReferenceValue = smallOutline;
            so.FindProperty("bead").objectReferenceValue = bead;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("cameraLocalPosition").vector3Value = new Vector3(0f, -0.08f, 0.65f);
            so.FindProperty("minimumOutlineAlpha").floatValue = 0.95f;
            so.FindProperty("minimumSlotAlpha").floatValue = 0.8f;
            so.FindProperty("minimumBeadAlpha").floatValue = 1f;

            var rings = so.FindProperty("slotRings");
            rings.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++) rings.GetArrayElementAtIndex(i).objectReferenceValue = slotRings[i];

            var fills = so.FindProperty("slotFills");
            fills.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++) fills.GetArrayElementAtIndex(i).objectReferenceValue = slotFills[i];
        });

        canvasGo.SetActive(false); // BreathMissionGuideController가 Show()/Hide()로 제어한다.
        return ui;
    }

    // ------------------------------------------------------------ 지시문/안내 UI (3.3, 3.4)
    static void BuildMissionGuideTextUI(Camera cam)
    {
        var existingComponent = Object.FindFirstObjectByType<MissionGuideTextUI>(FindObjectsInactive.Include);
        if (existingComponent != null) Object.DestroyImmediate(existingComponent.gameObject);

        Transform parent = cam != null ? cam.transform : GetOrCreateUiRoot().transform;
        var canvasGo = MakeWorldCanvas("MissionGuideTextUI", parent, cam, new Vector2(1100f, 220f));
        if (cam != null)
        {
            canvasGo.transform.localPosition = new Vector3(0f, -0.25f, 0.6f);
            canvasGo.transform.localRotation = Quaternion.identity;
        }
        else
        {
            canvasGo.AddComponent<FaceCamera>();
        }

        var group = canvasGo.AddComponent<CanvasGroup>();
        var text = MakeText(canvasGo.transform, "MessageText", string.Empty, 42, Color.white,
            Vector2.zero, Vector2.one);

        var ui = canvasGo.AddComponent<MissionGuideTextUI>();
        Wire(ui, so =>
        {
            so.FindProperty("messageText").objectReferenceValue = text;
            so.FindProperty("group").objectReferenceValue = group;
        });

        canvasGo.SetActive(false); // ShowMessage() 호출 시 활성화된다.
    }

    static void BuildBreathMissionGuideController(BreathCircleUI breathCircleUI, BreathEventsSO events)
    {
        var existing = GameObject.Find("BreathMissionGuideController");
        var go = existing != null ? existing : new GameObject("BreathMissionGuideController");
        if (existing == null) Undo.RegisterCreatedObjectUndo(go, "Create Breath Mission Guide Controller");
        var controller = go.GetComponent<BreathMissionGuideController>();
        if (controller == null) controller = go.AddComponent<BreathMissionGuideController>();

        Wire(controller, so =>
        {
            so.FindProperty("breathCircleUI").objectReferenceValue = breathCircleUI;
        });
    }

    // ------------------------------------------------------------ 웨이포인트 경로 (3.4)
    static Transform[] BuildWaypoints(Transform clueTransform)
    {
        var existing = GameObject.Find("Scene03_GuidingWaypoints");
        if (existing != null && existing.transform.childCount > 0)
        {
            // 실제 동선에 맞춰 수동/전용 도구로 배치한 경로를 전체 배선 재실행이 덮어쓰지 않는다.
            var preserved = new Transform[existing.transform.childCount];
            for (int i = 0; i < preserved.Length; i++)
                preserved[i] = existing.transform.GetChild(i);
            Debug.Log("[Scene03OutsideWiringBuilder] 기존 Scene03_GuidingWaypoints 경로를 보존합니다.");
            return preserved;
        }

        var root = existing != null ? existing : new GameObject("Scene03_GuidingWaypoints");
        Undo.RegisterCreatedObjectUndo(root, "Create Guiding Waypoints");
        root.transform.position = clueTransform.position;

        const int count = 14;
        var waypoints = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            var wp = new GameObject($"Waypoint_{i + 1}");
            wp.transform.SetParent(root.transform, false);
            // 임시 배치: 클루 오브젝트 정면으로 퍼지는 간이 경로. 실제 오아시스 경로에 맞게 수동 재배치 필요.
            wp.transform.position = clueTransform.position + clueTransform.forward * (3f * (i + 1)) + Vector3.up * 0.5f;
            waypoints[i] = wp.transform;
        }
        return waypoints;
    }

    // ------------------------------------------------------------ 길잡이 등불 템플릿 (3.4)
    static GameObject BuildGuidingLightTemplate()
    {
        var existing = GameObject.Find("GuidingLight_Template");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("GuidingLight_Template");
        Undo.RegisterCreatedObjectUndo(go, "Create Guiding Light Template");

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.5f);
        light.range = 4f;
        light.intensity = 2f;

        go.AddComponent<GuidingLightController>();

        // 참고: 현재는 포인트라이트 플레이스홀더입니다. 실제 아트(파티클/메시)로 교체 권장.
        go.SetActive(false); // 템플릿 자체는 비활성 — HologramMessage가 Instantiate 후 SetActive(true) 처리
        return go;
    }

    // ------------------------------------------------------------ 트리거 입력 브리지 (3.3)
    static void BuildInputBridge(InteractionDetector detector)
    {
        var existing = GameObject.Find("TriggerInteractionInputBridge");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("TriggerInteractionInputBridge");
        Undo.RegisterCreatedObjectUndo(go, "Create Trigger Interaction Input Bridge");
        var bridge = go.AddComponent<TriggerInteractionInputBridge>();
        bridge.detector = detector;
        EditorUtility.SetDirty(bridge);
    }

    // ------------------------------------------------------------ 씬 부트스트랩 (3.1)
    static void BuildBootstrap()
    {
        var existing = GameObject.Find("OutsideSceneBootstrap");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("OutsideSceneBootstrap");
        Undo.RegisterCreatedObjectUndo(go, "Create Outside Scene Bootstrap");
        go.AddComponent<OutsideSceneBootstrap>();
    }

    // ------------------------------------------------------------ 공용 헬퍼
    static BreathEventsSO FindEventsChannel()
    {
        string path = AssetDatabase.GUIDToAssetPath(EventsChannelGuid);
        var byGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<BreathEventsSO>(path);
        if (byGuid != null) return byGuid;

        var guids = AssetDatabase.FindAssets("t:BreathEventsSO BreathEventsChannel");
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(assetPath);
            if (asset != null) return asset;
        }
        return null;
    }

    static GameObject GetOrCreateUiRoot()
    {
        var root = GameObject.Find("SoomUI");
        if (root == null) root = new GameObject("SoomUI");
        return root;
    }

    // parent의 lossyScale(비균일 스케일 포함)이 child의 월드 크기에 그대로 곱해지지 않도록
    // child의 localScale을 역보정한다. World Space Canvas의 mm 컨벤션을 유지하기 위함.
    static void CompensateParentScale(Transform child, Transform parent)
    {
        Vector3 parentScale = parent.lossyScale;
        Vector3 compensation = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / parentScale.z);
        child.localScale = Vector3.Scale(child.localScale, compensation);
    }

    static Transform FindChildByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    // 1 UI unit = 1mm 컨벤션 (기존 BreathCircleSetup과 동일)
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

    static Image MakeImage(Transform parent, string name, Vector2 size, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.rectTransform.sizeDelta = size;
        return img;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, float fontSize, Color color,
        Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return tmp;
    }

    static void Wire(Object component, System.Action<SerializedObject> set)
    {
        var so = new SerializedObject(component);
        set(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

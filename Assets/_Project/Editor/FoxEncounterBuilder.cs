using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// 여우와의 조우 (기능 명세서 5장) — 씬 배선용 에디터 빌더.
///
/// 여우 모델/애니메이션 에셋이 아직 없으므로 캡슐+큐브 조합의 플레이스홀더를 만들고,
/// 상태 UI 캔버스 / 상호작용 UI(프롬프트+액션 버튼) / 불안의 막 VFX 구체(+머티리얼) /
/// FoxEncounterController / InteractableDataSO 에셋을 모두 생성하고 서로 배선한다.
///
/// 사용법: Scene_03_InGame_Outside 씬을 열어둔 상태에서 메뉴 SOOM/Build Fox Encounter 실행.
/// 이 스크립트는 씬을 자동 저장하지 않는다 — 실행 후 결과가 마음에 들면 씬을 직접 저장해야 한다.
/// </summary>
public static class FoxEncounterBuilder
{
    private const string FoxRootName = "Fox_Encounter";
    private const string DataAssetPath = "Assets/_Project/Data/Fox_InteractableData.asset";
    private const string MembraneMaterialPath = "Assets/_Project/Arts/Mat_AnxietyMembrane.mat";
    private const string BreathEventsAssetPath = "Assets/_Project/Scripts/System/BreathEventsChannel.asset";

    [MenuItem("SOOM/Build Fox Encounter")]
    private static void BuildFoxEncounter()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.name.Contains("Scene_03"))
        {
            Debug.LogWarning(
                $"[FoxEncounterBuilder] 현재 활성 씬이 'Scene_03_InGame_Outside'가 아닙니다(현재: '{activeScene.name}'). " +
                "여우와의 조우는 해당 씬에서 동작하도록 설계되었습니다. 계속 진행합니다.");
        }

        if (GameObject.Find(FoxRootName) != null)
        {
            Debug.LogWarning(
                $"[FoxEncounterBuilder] 씬에 이미 '{FoxRootName}' 오브젝트가 있습니다. " +
                "다시 생성하려면 기존 오브젝트를 먼저 삭제한 뒤 실행하세요.");
            return;
        }

        Undo.SetCurrentGroupName("Build Fox Encounter");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureFolder("Assets/_Project", "Data");

        InteractableDataSO data = CreateOrLoadInteractableData();
        BreathEventsSO breathEvents = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(BreathEventsAssetPath);
        if (breathEvents == null)
        {
            Debug.LogWarning(
                $"[FoxEncounterBuilder] '{BreathEventsAssetPath}'에서 BreathEventsSO를 찾지 못했습니다. " +
                "호흡 이벤트 연동 없이 진행합니다 — FoxEncounterController.breathEvents를 수동으로 연결하세요.");
        }
        Material membraneMaterial = CreateOrLoadMembraneMaterial();

        EnsureEventSystem();

        GameObject fox = BuildFoxRoot();

        Animator animator = fox.AddComponent<Animator>(); // 모델/컨트롤러는 아직 없음 (옵션 참조)

        var (membraneGo, membraneRenderer) = BuildMembrane(fox.transform, membraneMaterial);
        BreathCircleUI breathUI = BuildBreathCircleUI(fox.transform, breathEvents);
        var (statusRoot, statusText) = BuildStatusUI(fox.transform);
        FoxInteractionUIRefs interactionUI = BuildInteractionUI(fox.transform);
        FoxCompanionFollower companionFollower = BuildCompanionFollower(fox);

        FoxInteractable foxInteractable = fox.AddComponent<FoxInteractable>();
        FoxEncounterController controller = fox.AddComponent<FoxEncounterController>();

        WireFoxInteractable(foxInteractable, data, interactionUI, controller);
        WireFoxEncounterController(
            controller, data, breathEvents, statusRoot, statusText, interactionUI,
            breathUI, membraneGo, membraneRenderer, animator, companionFollower);

        ConfigureSharedInteractionDetector();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(fox.scene);
        Selection.activeGameObject = fox;

        Debug.Log(
            "[FoxEncounterBuilder] 여우와의 조우 배선 완료. 'Fox_Encounter'를 지형 위 적절한 위치로 " +
            "옮기고, 씬을 저장하세요. 여우 모델이 준비되면 Visual_Body/Visual_Head를 교체하고 " +
            "Animator에 컨트롤러(Sit_Growl/Stand_Joy 상태 포함)를 연결하면 됩니다.");
    }

    // ============================================================
    // 데이터 / 머티리얼 에셋
    // ============================================================

    private static InteractableDataSO CreateOrLoadInteractableData()
    {
        var existing = AssetDatabase.LoadAssetAtPath<InteractableDataSO>(DataAssetPath);
        if (existing != null) return existing;

        var so = ScriptableObject.CreateInstance<InteractableDataSO>();
        so.objectName = "ANIMAL / FOX";
        so.descriptionText = "낯선 기척에 몸을 웅크린 여우";
        so.missionGuideText = "숨의 호흡 능력은 동료의 집중된 마음을 알아볼 수 있습니다. 깊게 호흡하세요";
        so.requiresBreathing = true;
        so.targetBreathCount = 3;

        AssetDatabase.CreateAsset(so, DataAssetPath);
        AssetDatabase.SaveAssets();
        return so;
    }

    private static Material CreateOrLoadMembraneMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MembraneMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("SOOM/AnxietyMembrane");
        if (shader == null)
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] 'SOOM/AnxietyMembrane' 셰이더를 찾지 못했습니다(아직 임포트되지 않았을 수 있음). " +
                "URP 기본 Lit 셰이더로 대체합니다 — 한 번 더 임포트된 뒤 다시 실행하면 정상 셰이더로 만들어집니다.");
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null) return null;

        var mat = new Material(shader) { name = "Mat_AnxietyMembrane" };
        if (mat.HasProperty("_Alpha")) mat.SetFloat("_Alpha", 1f);

        AssetDatabase.CreateAsset(mat, MembraneMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void EnsureFolder(string parentFolder, string newFolderName)
    {
        string fullPath = parentFolder + "/" + newFolderName;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }
    }

    // ============================================================
    // EventSystem (월드 스페이스 UI 버튼을 XR 레이로 클릭 가능하게)
    // ============================================================

    private static void EnsureEventSystem()
    {
        var existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing == null)
        {
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            existing = go.AddComponent<EventSystem>();
            go.AddComponent<XRUIInputModule>();
            Debug.Log("[FoxEncounterBuilder] 씬에 EventSystem이 없어 XRUIInputModule과 함께 새로 생성했습니다.");
        }
        else if (existing.GetComponent<BaseInputModule>() == null)
        {
            Undo.AddComponent<XRUIInputModule>(existing.gameObject);
            Debug.Log("[FoxEncounterBuilder] 기존 EventSystem에 XRUIInputModule을 추가했습니다.");
        }
    }

    // ============================================================
    // 여우 루트 + 플레이스홀더 비주얼 (캡슐 + 큐브)
    // ============================================================

    private static GameObject BuildFoxRoot()
    {
        var fox = new GameObject(FoxRootName);
        Undo.RegisterCreatedObjectUndo(fox, "Create Fox_Encounter");
        // 임시 배치 위치. 지형(Terrain) 높이에 맞춰 수동으로 옮겨야 한다 (PR 수동 단계 참고).
        fox.transform.position = new Vector3(5f, 0f, 5f);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            fox.layer = interactableLayer;
        }
        else
        {
            Debug.LogWarning("[FoxEncounterBuilder] 'Interactable' 레이어를 찾지 못했습니다 (TagManager 설정 확인 필요).");
        }

        var collider = fox.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.35f, 0f);
        collider.radius = 0.3f;
        collider.height = 0.7f;

        BuildFoxVisual(fox.transform);

        return fox;
    }

    private static void BuildFoxVisual(Transform parent)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Visual_Body";
        Undo.RegisterCreatedObjectUndo(body, "Create Fox Visual Body");
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        body.transform.localScale = new Vector3(0.4f, 0.35f, 0.45f);
        StripCollider(body);
        ApplyPlaceholderColor(body, new Color(0.85f, 0.42f, 0.13f));

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Visual_Head";
        Undo.RegisterCreatedObjectUndo(head, "Create Fox Visual Head");
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 0.62f, 0.32f);
        head.transform.localScale = new Vector3(0.22f, 0.22f, 0.28f);
        StripCollider(head);
        ApplyPlaceholderColor(head, new Color(0.95f, 0.55f, 0.2f));
    }

    private static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    private static void ApplyPlaceholderColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        // 씬/에셋에 남기지 않는 런타임 전용 머티리얼 (모델 교체 시 자연히 사라짐).
        var mat = new Material(shader) { name = "Mat_FoxPlaceholder_Runtime" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        renderer.sharedMaterial = mat;
    }

    // ============================================================
    // 불안의 막 VFX 구체
    // ============================================================

    private static (GameObject go, Renderer renderer) BuildMembrane(Transform parent, Material material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "AnxietyMembrane";
        Undo.RegisterCreatedObjectUndo(sphere, "Create AnxietyMembrane");
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        sphere.transform.localScale = Vector3.one * 1.15f;

        StripCollider(sphere); // 순수 VFX — 물리/상호작용에 영향을 주지 않는다.

        var renderer = sphere.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;

        // 명세 5.4 이전에는 보이지 않아야 하므로 기본 비활성화. Revealed 단계에서 컨트롤러가 켠다.
        sphere.SetActive(false);
        return (sphere, renderer);
    }

    // ============================================================
    // 동료 추종 (NavMeshAgent + 폴백)
    // ============================================================

    private static FoxCompanionFollower BuildCompanionFollower(GameObject fox)
    {
        var agent = fox.AddComponent<NavMeshAgent>();
        agent.radius = 0.3f;
        agent.height = 0.7f;
        agent.baseOffset = 0f;
        agent.speed = 2.2f;
        agent.stoppingDistance = 1.2f;
        // NavMesh가 구워져 있지 않으면 isOnNavMesh가 항상 false가 되어
        // FoxCompanionFollower가 자동으로 단순 Transform 추종으로 폴백한다.
        agent.enabled = false;

        var follower = fox.AddComponent<FoxCompanionFollower>();
        var so = new SerializedObject(follower);
        so.FindProperty("agent").objectReferenceValue = agent;
        so.ApplyModifiedPropertiesWithoutUndo();

        return follower;
    }

    // ============================================================
    // 월드 스페이스 UI 빌드 헬퍼
    // ============================================================

    private static Canvas CreateWorldSpaceCanvas(
        string name, Transform parent, Vector3 localPos, Vector2 sizeDelta, float worldScale, bool interactive)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * worldScale;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = sizeDelta;

        if (interactive)
        {
            // XRI 레이 인터랙터로 월드 스페이스 버튼을 클릭할 수 있게 한다.
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            // worldCamera가 비어 있으면 TrackedDeviceGraphicRaycaster가 Camera.main으로 대체하지만,
            // 명시적으로 채워두는 편이 더 안전하다.
            if (Camera.main != null) canvas.worldCamera = Camera.main;
        }

        // 플레이어가 어느 방향에 있든 항상 읽을 수 있도록 카메라를 바라보게 한다 (명세 5.2).
        go.AddComponent<FaceCamera>();

        return canvas;
    }

    private static TMP_Text CreateTMPText(
        string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta,
        float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        // 폰트를 지정하지 않으면 TMP_Settings.defaultFontAsset을 사용한다 — 한글은
        // KoreanTextSupport(Installer)가 등록해 둔 NotoSansKR SDF 폴백으로 표시된다.
        return text;
    }

    private static Image CreateUIImage(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var image = go.AddComponent<Image>();
        image.color = color;
        // sprite는 비워둔다 — BreathCircleUI.Awake()의 EnsureSprites()가 재생 시작 시
        // CircleSpriteFactory로 자동 생성해 채운다 (원본 설계 그대로).
        return image;
    }

    // ============================================================
    // 상태 UI (여우 머리 위, 명세 5.2/5.4/5.6)
    // ============================================================

    private static (GameObject root, TMP_Text text) BuildStatusUI(Transform parent)
    {
        var canvas = CreateWorldSpaceCanvas(
            "UI_Status", parent, new Vector3(0f, 1.6f, 0f), new Vector2(400f, 100f), 0.0035f, interactive: false);
        var text = CreateTMPText(
            "StatusText", canvas.transform, Vector2.zero, new Vector2(380f, 90f), 36f, TextAlignmentOptions.Center, Color.white);

        canvas.gameObject.SetActive(false); // Wary 진입 전까지는 숨김
        return (canvas.gameObject, text);
    }

    // ============================================================
    // 상호작용 UI (감지 프롬프트 + 안내 지시문 + 액션 버튼)
    // ============================================================

    private class FoxInteractionUIRefs
    {
        public GameObject Root;
        public GameObject PromptPanelRoot;
        public TMP_Text PromptNameText;
        public TMP_Text PromptDescText;
        public GameObject InstructionPanelRoot;
        public TMP_Text InstructionText;
        public GameObject ActionButtonRoot;
        public Button ActionButton;
        public TMP_Text ActionButtonLabel;
    }

    private static FoxInteractionUIRefs BuildInteractionUI(Transform parent)
    {
        var canvas = CreateWorldSpaceCanvas(
            "UI_Interaction", parent, new Vector3(0f, 1.0f, 0.15f), new Vector2(420f, 260f), 0.0032f, interactive: true);
        var root = canvas.gameObject;

        // --- 감지 프롬프트 (명세 5.1: "ANIMAL / FOX") ---
        var promptPanel = new GameObject("PromptPanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(promptPanel, "Create PromptPanel");
        promptPanel.transform.SetParent(root.transform, false);
        StretchFull(promptPanel.GetComponent<RectTransform>());
        var promptBg = promptPanel.AddComponent<Image>();
        promptBg.color = new Color(0f, 0f, 0f, 0.55f);

        var nameText = CreateTMPText(
            "NameText", promptPanel.transform, new Vector2(0f, 40f), new Vector2(380f, 60f),
            32f, TextAlignmentOptions.Center, Color.white);
        var descText = CreateTMPText(
            "DescText", promptPanel.transform, new Vector2(0f, -20f), new Vector2(380f, 80f),
            22f, TextAlignmentOptions.Center, new Color(0.85f, 0.85f, 0.85f, 1f));
        promptPanel.SetActive(false);

        // --- 안내 지시문 + 액션 버튼 (명세 5.2~5.6 공용, 단계별로 라벨/동작만 바뀜) ---
        var instructionPanel = new GameObject("InstructionPanel", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(instructionPanel, "Create InstructionPanel");
        instructionPanel.transform.SetParent(root.transform, false);
        StretchFull(instructionPanel.GetComponent<RectTransform>());
        var instrBg = instructionPanel.AddComponent<Image>();
        instrBg.color = new Color(0f, 0f, 0f, 0.55f);

        var instructionText = CreateTMPText(
            "InstructionText", instructionPanel.transform, new Vector2(0f, 40f), new Vector2(380f, 120f),
            24f, TextAlignmentOptions.Center, Color.white);

        var actionButtonRoot = new GameObject("ActionButton", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(actionButtonRoot, "Create ActionButton");
        actionButtonRoot.transform.SetParent(instructionPanel.transform, false);
        var btnRt = actionButtonRoot.GetComponent<RectTransform>();
        btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, -70f);
        btnRt.sizeDelta = new Vector2(240f, 60f);

        var btnImage = actionButtonRoot.AddComponent<Image>();
        btnImage.color = new Color(0.3f, 0.6f, 0.9f, 0.9f);
        var button = actionButtonRoot.AddComponent<Button>();
        button.targetGraphic = btnImage;

        var buttonLabel = CreateTMPText(
            "Label", actionButtonRoot.transform, Vector2.zero, new Vector2(220f, 50f),
            24f, TextAlignmentOptions.Center, Color.white);

        instructionPanel.SetActive(false);

        return new FoxInteractionUIRefs
        {
            Root = root,
            PromptPanelRoot = promptPanel,
            PromptNameText = nameText,
            PromptDescText = descText,
            InstructionPanelRoot = instructionPanel,
            InstructionText = instructionText,
            ActionButtonRoot = actionButtonRoot,
            ActionButton = button,
            ActionButtonLabel = buttonLabel,
        };
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ============================================================
    // 공용 호흡 UI (BreathCircleUI 재사용 — 명세 5.3/5.5)
    // ============================================================

    private static BreathCircleUI BuildBreathCircleUI(Transform parent, BreathEventsSO events)
    {
        var canvas = CreateWorldSpaceCanvas(
            "UI_BreathCircle", parent, new Vector3(0f, 1.2f, 0.2f), new Vector2(300f, 300f), 0.0035f, interactive: false);
        var root = canvas.gameObject;

        var group = root.AddComponent<CanvasGroup>();

        var large = CreateUIImage("LargeCircleOutline", root.transform, Vector2.zero, new Vector2(180f, 180f), Color.white);
        var small = CreateUIImage("SmallCircleOutline", root.transform, Vector2.zero, new Vector2(130f, 130f), Color.white);
        var bead = CreateUIImage("Bead", root.transform, Vector2.zero, new Vector2(70f, 70f), Color.white);

        var slotsRoot = new GameObject("Slots", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(slotsRoot, "Create Slots");
        slotsRoot.transform.SetParent(root.transform, false);
        var slotsRt = slotsRoot.GetComponent<RectTransform>();
        slotsRt.anchorMin = slotsRt.anchorMax = new Vector2(0.5f, 0.5f);
        slotsRt.anchoredPosition = new Vector2(0f, 110f);

        const int slotCount = 3; // BreathManager.targetLoopCount 기본값(3회 성공)과 동일
        var slotRings = new Image[slotCount];
        var slotFills = new Image[slotCount];
        const float spacing = 55f;
        float startX = -spacing * (slotCount - 1) / 2f;

        for (int i = 0; i < slotCount; i++)
        {
            var slotGo = new GameObject($"Slot{i}", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(slotGo, "Create Slot");
            slotGo.transform.SetParent(slotsRoot.transform, false);
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            slotRt.sizeDelta = new Vector2(40f, 40f);

            slotRings[i] = CreateUIImage("Ring", slotGo.transform, Vector2.zero, new Vector2(40f, 40f), Color.white);
            slotFills[i] = CreateUIImage("Fill", slotGo.transform, Vector2.zero, new Vector2(40f, 40f), Color.white);
            slotFills[i].enabled = false;
        }

        var breathUI = root.AddComponent<BreathCircleUI>();
        var so = new SerializedObject(breathUI);
        so.FindProperty("events").objectReferenceValue = events;
        so.FindProperty("largeCircleOutline").objectReferenceValue = large;
        so.FindProperty("smallCircleOutline").objectReferenceValue = small;
        so.FindProperty("bead").objectReferenceValue = bead;
        so.FindProperty("group").objectReferenceValue = group;

        var slotRingsProp = so.FindProperty("slotRings");
        slotRingsProp.arraySize = slotCount;
        for (int i = 0; i < slotCount; i++)
            slotRingsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotRings[i];

        var slotFillsProp = so.FindProperty("slotFills");
        slotFillsProp.arraySize = slotCount;
        for (int i = 0; i < slotCount; i++)
            slotFillsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotFills[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false); // 호흡 시퀀스가 시작될 때만 컨트롤러가 Show()
        return breathUI;
    }

    // ============================================================
    // 컴포넌트 배선
    // ============================================================

    private static void WireFoxInteractable(
        FoxInteractable foxInteractable, InteractableDataSO data,
        FoxInteractionUIRefs interactionUI, FoxEncounterController controller)
    {
        var so = new SerializedObject(foxInteractable);
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("promptPanelRoot").objectReferenceValue = interactionUI.PromptPanelRoot;
        so.FindProperty("promptNameText").objectReferenceValue = interactionUI.PromptNameText;
        so.FindProperty("promptDescText").objectReferenceValue = interactionUI.PromptDescText;
        so.FindProperty("encounterController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireFoxEncounterController(
        FoxEncounterController controller, InteractableDataSO data, BreathEventsSO breathEvents,
        GameObject statusRoot, TMP_Text statusText, FoxInteractionUIRefs interactionUI,
        BreathCircleUI breathUI, GameObject membraneGo, Renderer membraneRenderer,
        Animator animator, FoxCompanionFollower companionFollower)
    {
        var so = new SerializedObject(controller);
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("breathEvents").objectReferenceValue = breathEvents;
        so.FindProperty("statusPanelRoot").objectReferenceValue = statusRoot;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("instructionPanelRoot").objectReferenceValue = interactionUI.InstructionPanelRoot;
        so.FindProperty("instructionText").objectReferenceValue = interactionUI.InstructionText;
        so.FindProperty("actionButtonRoot").objectReferenceValue = interactionUI.ActionButtonRoot;
        so.FindProperty("actionButton").objectReferenceValue = interactionUI.ActionButton;
        so.FindProperty("actionButtonLabel").objectReferenceValue = interactionUI.ActionButtonLabel;
        so.FindProperty("breathCircleUI").objectReferenceValue = breathUI;
        so.FindProperty("membraneObject").objectReferenceValue = membraneGo;
        so.FindProperty("membraneRenderer").objectReferenceValue = membraneRenderer;
        so.FindProperty("foxAnimator").objectReferenceValue = animator;
        so.FindProperty("companionFollower").objectReferenceValue = companionFollower;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 기존 공용 InteractionDetector(Systems 프리팹 인스턴스)의 Interactable 레이어 비트와
    /// 시야 기준(viewOrigin)이 비어있으면 채워 넣는다 — 채워져 있지 않으면 감지 자체가
    /// 전혀 동작하지 않는다(현재 프리팹 기본값은 레이어 마스크 0, viewOrigin 없음).
    /// 씬 인스턴스에 대한 오버라이드만 남기므로 다른 씬/원본 프리팹에는 영향이 없다.
    /// </summary>
    private static void ConfigureSharedInteractionDetector()
    {
        var detector = Object.FindFirstObjectByType<InteractionDetector>();
        if (detector == null)
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] 씬에서 InteractionDetector를 찾지 못했습니다. " +
                "Systems 프리팹이 씬에 배치되어 있는지 확인하세요 — 여우 감지가 동작하지 않습니다.");
            return;
        }

        var so = new SerializedObject(detector);

        var layerProp = so.FindProperty("interactableLayer");
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            layerProp.intValue |= 1 << interactableLayer;
        }
        else
        {
            Debug.LogWarning("[FoxEncounterBuilder] 'Interactable' 레이어를 찾지 못해 감지 레이어 마스크를 설정하지 못했습니다.");
        }

        var viewOriginProp = so.FindProperty("viewOrigin");
        if (viewOriginProp.objectReferenceValue == null)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cams.Length > 0) cam = cams[0];
            }
            if (cam != null)
            {
                viewOriginProp.objectReferenceValue = cam.transform;
            }
            else
            {
                Debug.LogWarning(
                    "[FoxEncounterBuilder] 씬에서 카메라를 찾지 못해 InteractionDetector.viewOrigin을 " +
                    "자동으로 채우지 못했습니다. XR 메인 카메라를 수동으로 연결하세요.");
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(detector);
    }
}

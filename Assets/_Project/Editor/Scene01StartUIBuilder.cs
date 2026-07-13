using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// 스타팅 화면(Scene_01_Start)의 메인 UI, 환경설정 패널, 우주선 불시착 컷신 시스템을
/// 현재 열려 있는 씬에 자동으로 구성하는 에디터 툴입니다(기능 명세 1장).
/// SOOM &gt; Build Scene01 Start UI 메뉴로 실행하세요.
///
/// 이 스크립트는 씬 파일을 직접 건드리지 않습니다 — 메뉴를 실행하는 사람이 열어 둔 씬의
/// 런타임 오브젝트 그래프에 GameObject/컴포넌트를 생성·배선할 뿐이며, 씬 저장은 사용자가 직접 해야 합니다.
/// XR Origin, Turn Provider, 잔해 파티클처럼 씬마다 다를 수 있는 참조는 있으면 자동으로 배선하고,
/// 없으면 옵션으로 비워 둔 채 경고 로그만 남깁니다(null 가드는 런타임 스크립트 쪽에서 보장).
/// </summary>
internal static class Scene01StartUIBuilder
{
    private const string RootName = "StartScreen_Systems";
    private const string CanvasName = "StartScreenCanvas";
    private const string CliCanvasName = "CliLoadingCanvas";
    private const string TopViewPoseName = "Scene01_TopViewPose";
    private const string RecenterPoseName = "Scene01_RecenterOriginPoint";
    private const string KoreanFontPath = "Assets/_Project/Resources/Fonts/NotoSansKR SDF.asset";

    private static readonly DefaultControls.Resources UIResources = new DefaultControls.Resources();

    [MenuItem("SOOM/Build Scene01 Start UI")]
    private static void Build()
    {
        if (!PrepareCleanRoot())
            return; // 사용자가 재생성을 취소함

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Scene01 Start UI");

        EnsureEventSystem();

        // ---- UI 구성 ----
        var canvasGO = BuildMainCanvas(root.transform);
        var mainMenuPanel = BuildMainMenuPanel(canvasGO.transform,
            out var startBtn, out var openSettingsBtn, out var teamIntroBtn, out var closeBtn);
        var settingsPanel = BuildSettingsPanel(canvasGO.transform,
            out var recenterBtn, out var masterSlider, out var bgmSlider, out var sfxSlider, out var voiceSlider,
            out var turnToggle, out var closeSettingsBtn);
        var teamIntroPanel = BuildTeamIntroPanel(canvasGO.transform, out var teamBackBtn);

        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
        teamIntroPanel.SetActive(false);

        var cliCanvasGO = BuildCliLoadingCanvas(out var cliLogText, out var cliCanvasGroup);
        cliCanvasGO.SetActive(false);

        var topViewPose = CreatePoseMarker(TopViewPoseName, new Vector3(0f, 8f, 0f), Quaternion.Euler(90f, 0f, 0f));
        var recenterPose = CreatePoseMarker(RecenterPoseName, Vector3.zero, Quaternion.identity);

        // ---- 로직 컴포넌트 ----
        root.AddComponent<SceneEntryFade>();
        var cameraShake = root.AddComponent<CameraShakeNoise>();
        var landingSequence = root.AddComponent<CutsceneLandingSequence>();
        var director = root.AddComponent<PlayableDirector>();
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;

        var cliLoadingUI = cliCanvasGO.AddComponent<CliLoadingUI>();
        var crashCutscene = root.AddComponent<CrashCutsceneController>();
        var turnModeSwitcher = root.AddComponent<TurnModeSwitcher>();
        var startScreenController = root.AddComponent<StartScreenController>();
        var environmentSettingsPanel = settingsPanel.AddComponent<EnvironmentSettingsPanel>();

        // ---- 씬에 이미 존재하는 XR/로코모션 오브젝트 자동 탐색 (없으면 null로 남김) ----
        var xrOrigin = Object.FindFirstObjectByType<XROrigin>();
        var continuousTurn = Object.FindFirstObjectByType<ContinuousTurnProvider>();
        var snapTurn = Object.FindFirstObjectByType<SnapTurnProvider>();

        if (xrOrigin == null)
            Debug.LogWarning("[Scene01StartUIBuilder] 씬에서 XROrigin을 찾지 못했습니다. EnvironmentSettingsPanel의 xrOriginTransform을 직접 배선해주세요.");
        if (continuousTurn == null || snapTurn == null)
            Debug.LogWarning("[Scene01StartUIBuilder] ContinuousTurnProvider/SnapTurnProvider를 찾지 못했습니다. TurnModeSwitcher에 직접 배선해주세요.");

        // ---- 배선 ----
        WireCliLoadingUI(cliLoadingUI, cliLogText, cliCanvasGroup);
        WireLandingSequence(landingSequence, topViewPose);
        WireCrashCutscene(crashCutscene, director, cameraShake, cliLoadingUI, landingSequence);
        WireTurnModeSwitcher(turnModeSwitcher, continuousTurn, snapTurn);
        WireEnvironmentSettingsPanel(environmentSettingsPanel, xrOrigin, recenterPose, recenterBtn,
            masterSlider, bgmSlider, sfxSlider, voiceSlider, turnToggle, turnModeSwitcher);
        WireStartScreenController(startScreenController, startBtn, crashCutscene,
            openSettingsBtn, closeSettingsBtn, teamIntroBtn, teamBackBtn, closeBtn,
            mainMenuPanel, settingsPanel, teamIntroPanel);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = root;
        Debug.Log("[Scene01StartUIBuilder] Scene_01_Start UI 구성 완료. " +
            "XR Origin/Turn Provider/우주선 Timeline/잔해 파티클처럼 씬마다 달라지는 옵션 참조는 필요시 직접 배선한 뒤 씬을 저장해주세요.");
    }

    // =====================================================================================
    // 정리(재실행 대비)
    // =====================================================================================

    private static bool PrepareCleanRoot()
    {
        var existingRoot = GameObject.Find(RootName);
        var existingCli = GameObject.Find(CliCanvasName);
        var existingTopView = GameObject.Find(TopViewPoseName);
        var existingRecenter = GameObject.Find(RecenterPoseName);

        if (existingRoot == null && existingCli == null && existingTopView == null && existingRecenter == null)
            return true;

        bool rebuild = EditorUtility.DisplayDialog(
            "Scene01 Start UI 재생성",
            "이미 '" + RootName + "' 등 스타팅 화면 오브젝트가 씬에 존재합니다. 삭제하고 새로 만들까요?\n" +
            "(수동으로 배선한 옵션 참조는 초기화됩니다.)",
            "삭제 후 다시 만들기",
            "취소");

        if (!rebuild) return false;

        if (existingRoot != null) Undo.DestroyObjectImmediate(existingRoot);
        if (existingCli != null) Undo.DestroyObjectImmediate(existingCli);
        if (existingTopView != null) Undo.DestroyObjectImmediate(existingTopView);
        if (existingRecenter != null) Undo.DestroyObjectImmediate(existingRecenter);

        return true;
    }

    // =====================================================================================
    // EventSystem
    // =====================================================================================

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    // =====================================================================================
    // 메인 캔버스 / 패널
    // =====================================================================================

    private static GameObject BuildMainCanvas(Transform root)
    {
        var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Create StartScreenCanvas");
        go.transform.SetParent(root, false);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1000f, 1200f);

        PlaceInFrontOfCamera(go.transform, distance: 2.2f, fallbackHeight: 1.4f, worldScale: 0.0012f);

        // XRI 컨트롤러 레이 인터랙션용 레이캐스터 (마우스/게임패드용 GraphicRaycaster와 공존 가능)
        go.AddComponent<TrackedDeviceGraphicRaycaster>();
        go.AddComponent<FaceCamera>();

        return go;
    }

    private static GameObject BuildMainMenuPanel(Transform canvasTransform,
        out Button startBtn, out Button openSettingsBtn, out Button teamIntroBtn, out Button closeBtn)
    {
        var panel = CreateFillPanel(canvasTransform, "MainMenuPanel");

        CreateLabel(panel.transform, "Title", "SOOM", 72, TextAlignmentOptions.Center, preferredHeight: 110f);

        startBtn = CreateButtonElement(panel.transform, "StartGameButton", "게임 시작");
        openSettingsBtn = CreateButtonElement(panel.transform, "OpenSettingsButton", "환경 설정");
        teamIntroBtn = CreateButtonElement(panel.transform, "TeamIntroButton", "팀 소개");
        closeBtn = CreateButtonElement(panel.transform, "CloseButton", "종료");

        return panel;
    }

    private static GameObject BuildSettingsPanel(Transform canvasTransform,
        out Button recenterBtn, out Slider masterSlider, out Slider bgmSlider, out Slider sfxSlider, out Slider voiceSlider,
        out Toggle turnToggle, out Button closeSettingsBtn)
    {
        var panel = CreateFillPanel(canvasTransform, "SettingsPanel");

        CreateLabel(panel.transform, "Title", "환경 설정", 56, TextAlignmentOptions.Center, preferredHeight: 90f);

        recenterBtn = CreateButtonElement(panel.transform, "RecenterButton", "시점 초기화");

        masterSlider = CreateSliderRow(panel.transform, "MasterVolumeRow", "Master");
        bgmSlider = CreateSliderRow(panel.transform, "BgmVolumeRow", "BGM");
        sfxSlider = CreateSliderRow(panel.transform, "SfxVolumeRow", "SFX");
        voiceSlider = CreateSliderRow(panel.transform, "VoiceVolumeRow", "Voice");

        turnToggle = CreateToggleElement(panel.transform, "ContinuousTurnToggle", "연속 회전(Continuous Turn) 사용");

        closeSettingsBtn = CreateButtonElement(panel.transform, "CloseSettingsButton", "뒤로가기");

        return panel;
    }

    private static GameObject BuildTeamIntroPanel(Transform canvasTransform, out Button backBtn)
    {
        var panel = CreateFillPanel(canvasTransform, "TeamIntroPanel");

        CreateLabel(panel.transform, "Title", "팀 소개", 56, TextAlignmentOptions.Center, preferredHeight: 90f);
        CreateLabel(panel.transform, "Body",
            "SOOM 개발팀\n(팀 소개 내용을 채워주세요)", 32, TextAlignmentOptions.TopLeft, preferredHeight: 500f);

        backBtn = CreateButtonElement(panel.transform, "TeamIntroBackButton", "뒤로가기");

        return panel;
    }

    private static GameObject CreateFillPanel(Transform parent, string name, float margin = 30f)
    {
        var go = DefaultControls.CreatePanel(UIResources);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name;
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);

        var image = go.GetComponent<Image>();
        image.sprite = null;
        image.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        var layoutGroup = go.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.spacing = 24f;
        layoutGroup.padding = new RectOffset(20, 20, 40, 40);
        // childControl*을 켜야 자식들의 LayoutElement.preferred 값이 실제 렌더 크기에 반영됩니다.
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        return go;
    }

    // =====================================================================================
    // CLI 로딩 캔버스
    // =====================================================================================

    private static GameObject BuildCliLoadingCanvas(out TMP_Text logText, out CanvasGroup canvasGroup)
    {
        var go = new GameObject(CliCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(go, "Create CliLoadingCanvas");

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1200f, 800f);

        // ScreenFader의 페이드 쿼드와 동일한 방식: 카메라 자식으로 붙여서 항상 시야를 덮게 만듭니다.
        var cam = Camera.main;
        if (cam != null)
        {
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.001f;
        }
        else
        {
            Debug.LogWarning("[Scene01StartUIBuilder] Camera.main을 찾지 못해 CliLoadingCanvas를 카메라 자식으로 붙이지 못했습니다. 직접 Main Camera 아래로 옮겨주세요.");
            go.transform.localPosition = new Vector3(0f, 1.4f, 0.6f);
            go.transform.localScale = Vector3.one * 0.001f;
        }

        var background = CreateUIObject("Background", go.transform, typeof(Image));
        var backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        var backgroundImage = background.GetComponent<Image>();
        backgroundImage.sprite = null;
        backgroundImage.color = new Color(0f, 0f, 0f, 0.96f);

        var textGO = new GameObject("LogText", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(60f, 60f);
        textRect.offsetMax = new Vector2(-60f, -60f);

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = string.Empty;
        tmp.fontSize = 30;
        tmp.color = new Color(0.35f, 1f, 0.55f); // CLI 터미널 느낌의 초록색
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        AssignKoreanFont(tmp);

        logText = tmp;
        canvasGroup = go.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        return go;
    }

    // =====================================================================================
    // 포즈 마커 (탑뷰 / 리센터 기준점)
    // =====================================================================================

    private static Transform CreatePoseMarker(string name, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetPositionAndRotation(position, rotation);
        return go.transform;
    }

    // =====================================================================================
    // 배선 (SerializedObject)
    // =====================================================================================

    private static void WireCliLoadingUI(CliLoadingUI cliLoadingUI, TMP_Text logText, CanvasGroup group)
    {
        var so = new SerializedObject(cliLoadingUI);
        so.FindProperty("logText").objectReferenceValue = logText;
        so.FindProperty("group").objectReferenceValue = group;
        so.ApplyModifiedProperties();
    }

    private static void WireLandingSequence(CutsceneLandingSequence landingSequence, Transform topViewPose)
    {
        var so = new SerializedObject(landingSequence);
        so.FindProperty("topViewPose").objectReferenceValue = topViewPose;
        so.ApplyModifiedProperties();
    }

    private static void WireCrashCutscene(CrashCutsceneController crashCutscene, PlayableDirector director,
        CameraShakeNoise cameraShake, CliLoadingUI loadingUI, CutsceneLandingSequence landingSequence)
    {
        var so = new SerializedObject(crashCutscene);
        so.FindProperty("director").objectReferenceValue = director;
        so.FindProperty("cameraShake").objectReferenceValue = cameraShake;
        so.FindProperty("loadingUI").objectReferenceValue = loadingUI;
        so.FindProperty("landingSequence").objectReferenceValue = landingSequence;
        so.ApplyModifiedProperties();
    }

    private static void WireTurnModeSwitcher(TurnModeSwitcher switcher, ContinuousTurnProvider continuous, SnapTurnProvider snap)
    {
        var so = new SerializedObject(switcher);
        so.FindProperty("continuousTurnProvider").objectReferenceValue = continuous;
        so.FindProperty("snapTurnProvider").objectReferenceValue = snap;
        so.ApplyModifiedProperties();
    }

    private static void WireEnvironmentSettingsPanel(EnvironmentSettingsPanel panel, XROrigin xrOrigin, Transform recenterPose,
        Button recenterBtn, Slider masterSlider, Slider bgmSlider, Slider sfxSlider, Slider voiceSlider,
        Toggle turnToggle, TurnModeSwitcher turnModeSwitcher)
    {
        var so = new SerializedObject(panel);
        so.FindProperty("xrOriginTransform").objectReferenceValue = xrOrigin != null ? xrOrigin.transform : null;
        so.FindProperty("recenterTargetPose").objectReferenceValue = recenterPose;
        so.FindProperty("recenterButton").objectReferenceValue = recenterBtn;
        so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
        so.FindProperty("bgmVolumeSlider").objectReferenceValue = bgmSlider;
        so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("voiceVolumeSlider").objectReferenceValue = voiceSlider;
        so.FindProperty("continuousTurnToggle").objectReferenceValue = turnToggle;
        so.FindProperty("turnModeSwitcher").objectReferenceValue = turnModeSwitcher;
        so.ApplyModifiedProperties();
    }

    private static void WireStartScreenController(StartScreenController controller, Button startBtn, CrashCutsceneController crashCutscene,
        Button openSettingsBtn, Button closeSettingsBtn, Button teamIntroBtn, Button teamIntroBackBtn, Button closeBtn,
        GameObject mainMenuPanel, GameObject settingsPanel, GameObject teamIntroPanel)
    {
        var so = new SerializedObject(controller);
        so.FindProperty("startGameButton").objectReferenceValue = startBtn;
        so.FindProperty("crashCutscene").objectReferenceValue = crashCutscene;
        so.FindProperty("openSettingsButton").objectReferenceValue = openSettingsBtn;
        so.FindProperty("closeSettingsButton").objectReferenceValue = closeSettingsBtn;
        so.FindProperty("teamIntroButton").objectReferenceValue = teamIntroBtn;
        so.FindProperty("teamIntroBackButton").objectReferenceValue = teamIntroBackBtn;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("mainMenuPanel").objectReferenceValue = mainMenuPanel;
        so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
        so.FindProperty("teamIntroPanel").objectReferenceValue = teamIntroPanel;
        so.ApplyModifiedProperties();
    }

    // =====================================================================================
    // UI 엘리먼트 생성 헬퍼
    // =====================================================================================

    private static GameObject CreateUIObject(string name, Transform parent, params System.Type[] components)
    {
        var go = new GameObject(name, components);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateLabel(Transform parent, string name, string text, int fontSize,
        TextAlignmentOptions alignment, float preferredHeight)
    {
        var go = CreateUIObject(name, parent, typeof(RectTransform));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        AssignKoreanFont(tmp);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;

        return go;
    }

    private static void AssignKoreanFont(TMP_Text tmp)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font != null) tmp.font = font;
    }

    private static Button CreateButtonElement(Transform parent, string name, string label)
    {
        var go = DefaultControls.CreateButton(UIResources);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name;
        go.transform.SetParent(parent, false);

        // 레거시 Text(Legacy) 자식을 지우고 한글 표기를 위한 TMP 라벨로 교체합니다.
        var legacyText = go.GetComponentInChildren<Text>();
        if (legacyText != null) Object.DestroyImmediate(legacyText.gameObject);

        var image = go.GetComponent<Image>();
        image.sprite = null;
        image.color = new Color(0.85f, 0.85f, 0.9f, 1f);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        AssignKoreanFont(tmp);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = 90f;
        layout.preferredWidth = 560f;

        return go.GetComponent<Button>();
    }

    private static Toggle CreateToggleElement(Transform parent, string name, string label)
    {
        var go = DefaultControls.CreateToggle(UIResources);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.name = name;
        go.transform.SetParent(parent, false);

        // 레거시 Label(Text)을 지우고 한글 표기를 위한 TMP 라벨로 교체합니다.
        var legacyLabel = go.transform.Find("Label");
        if (legacyLabel != null) Object.DestroyImmediate(legacyLabel.gameObject);

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(36f, 1f);
        labelRect.offsetMax = new Vector2(-5f, -2f);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        AssignKoreanFont(tmp);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = 50f;
        layout.preferredWidth = 560f;

        return go.GetComponent<Toggle>();
    }

    private static Slider CreateSliderRow(Transform parent, string rowName, string label)
    {
        var row = CreateUIObject(rowName, parent, typeof(RectTransform));

        var rowLayoutGroup = row.AddComponent<HorizontalLayoutGroup>();
        rowLayoutGroup.spacing = 16f;
        rowLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        // childControl*을 켜야 Label/Slider의 LayoutElement.preferredWidth가 실제로 적용됩니다.
        rowLayoutGroup.childControlWidth = true;
        rowLayoutGroup.childControlHeight = true;
        rowLayoutGroup.childForceExpandWidth = false;
        rowLayoutGroup.childForceExpandHeight = false;

        var rowElement = row.AddComponent<LayoutElement>();
        rowElement.preferredHeight = 50f;
        rowElement.preferredWidth = 560f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(row.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = Color.white;
        AssignKoreanFont(tmp);
        var labelLayout = labelGO.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 140f;
        labelLayout.preferredHeight = 50f;

        var sliderGO = DefaultControls.CreateSlider(UIResources);
        sliderGO.name = "Slider";
        sliderGO.transform.SetParent(row.transform, false);
        var sliderLayout = sliderGO.AddComponent<LayoutElement>();
        sliderLayout.preferredWidth = 380f;
        sliderLayout.preferredHeight = 30f;

        var slider = sliderGO.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.value = 100f;

        return slider;
    }

    private static void PlaceInFrontOfCamera(Transform target, float distance, float fallbackHeight, float worldScale)
    {
        var cam = Camera.main;
        if (cam != null)
        {
            var camTransform = cam.transform;
            target.position = camTransform.position + camTransform.forward * distance;

            // FaceCamera.cs와 동일한 billboard 공식 (카메라 → 오브젝트 방향을 바라보도록 회전).
            Vector3 toCamera = target.position - camTransform.position;
            target.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }
        else
        {
            target.position = new Vector3(0f, fallbackHeight, distance);
            target.rotation = Quaternion.identity;
        }
        target.localScale = Vector3.one * worldScale;
    }
}

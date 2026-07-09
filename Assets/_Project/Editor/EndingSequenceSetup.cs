// EndingSequenceSetup.cs (Editor-only)
// 기능 명세 6장(엔딩 화면) 씬 배선: 현재 열려 있는 씬에 EndingSequenceController, 크레딧
// World Space Canvas(프로젝트명 + 크레딧 텍스트), 길잡이 등불 플레이스홀더를 생성하고 서로 연결한다.
//
//   SOOM ▸ Build Ending Sequence
//
// Idempotent: "EndingSequence" 루트가 이미 있으면 지우고 새로 만든다.
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

static class EndingSequenceSetup
{
    const string RootName = "EndingSequence";
    const string GlowMaterialPath = "Assets/_Project/Arts/Mat_GlowingOrb.mat";
    const string KoreanFontPath = "Assets/_Project/Resources/Fonts/NotoSansKR SDF.asset";

    const string DefaultCreditsText =
        "Directed & Produced by\nSOOM Team\n\n" +
        "기획\n(팀원 이름)\n\n" +
        "프로그래밍\n(팀원 이름)\n\n" +
        "아트\n(팀원 이름)\n\n" +
        "사운드\n(팀원 이름)\n\n" +
        "플레이해주셔서 감사합니다.";

    [MenuItem("SOOM/Build Ending Sequence")]
    static void Build()
    {
        // 멱등성: 이미 존재하는 EndingSequence 루트가 있으면 제거하고 새로 만든다.
        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create EndingSequence");

        var lantern = BuildLanternPlaceholder(root.transform);
        var (canvasGo, content, viewport) = BuildCreditsCanvas(root.transform);

        var controller = root.AddComponent<EndingSequenceController>();
        Wire(controller, so =>
        {
            so.FindProperty("lanternTransform").objectReferenceValue = lantern;
            so.FindProperty("creditsPanelRoot").objectReferenceValue = canvasGo;
            so.FindProperty("creditsContent").objectReferenceValue = content;
            so.FindProperty("creditsViewport").objectReferenceValue = viewport;
        });

        canvasGo.SetActive(false); // 크레딧 단계 전까지는 숨겨둔다 (EndingSequenceController가 활성화한다).

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = root;

        Debug.Log("[SOOM] 'EndingSequence'를 빌드했습니다. " +
            "root의 EndingSequenceController.StartEnding()을 호출하거나 컨텍스트 메뉴 'Start Ending'으로 테스트하세요.");
    }

    static Transform BuildLanternPlaceholder(Transform parent)
    {
        var go = new GameObject("GuidingLanternPlaceholder");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 1.5f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.82f, 0.5f);
        light.range = 8f;
        light.intensity = 3f;

        // 눈에 보이는 몸체(선택 사항): 프로젝트에 이미 있는 Glowing Orb 머티리얼을 재사용한다.
        var glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        if (glowMat != null)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            var col = body.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col); // 시각적 표시용이므로 물리 충돌 불필요
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = Vector3.one * 0.15f;
            body.GetComponent<Renderer>().sharedMaterial = glowMat;
        }
        else
        {
            Debug.LogWarning("[SOOM] Mat_GlowingOrb 머티리얼을 찾지 못해 등불 플레이스홀더에 포인트라이트만 생성했습니다.");
        }

        return go.transform;
    }

    static (GameObject canvas, RectTransform content, RectTransform viewport) BuildCreditsCanvas(Transform parent)
    {
        var cam = Camera.main; // 씬에 카메라가 없을 수도 있음(런타임에 XR Origin이 스폰됨) — null이어도 안전
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (font == null)
            Debug.LogWarning("[SOOM] 한글 TMP 폰트(NotoSansKR SDF)를 찾지 못했습니다. 기본 폰트로 생성합니다.");

        var canvasGo = new GameObject("EndingCreditsCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(parent, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        var canvasRt = (RectTransform)canvasGo.transform;
        canvasRt.sizeDelta = new Vector2(1800f, 2000f);
        canvasRt.localScale = Vector3.one * 0.001f; // 1 유닛 = 1mm 컨벤션 (BreathCircleUI World Canvas와 동일)
        canvasGo.AddComponent<FaceCamera>(); // 플레이어를 향해 항상 정면을 보도록 빌보드 처리

        // --- 배경(반투명 검정) ---------------------------------------------------
        var backgroundGo = new GameObject("Background", typeof(Image));
        backgroundGo.transform.SetParent(canvasGo.transform, false);
        var backgroundImg = backgroundGo.GetComponent<Image>();
        backgroundImg.color = new Color(0f, 0f, 0f, 0.82f);
        var backgroundRt = backgroundImg.rectTransform;
        backgroundRt.anchorMin = Vector2.zero;
        backgroundRt.anchorMax = Vector2.one;
        backgroundRt.offsetMin = Vector2.zero;
        backgroundRt.offsetMax = Vector2.zero;

        // --- 뷰포트(스크롤 가시 영역, RectMask2D로 클리핑) --------------------------
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(canvasGo.transform, false);
        var viewportRt = (RectTransform)viewportGo.transform;
        viewportRt.sizeDelta = new Vector2(1600f, 1800f);
        viewportRt.anchoredPosition = Vector2.zero;

        // --- 콘텐츠(제목 + 크레딧, 위로 스크롤되는 대상) ----------------------------
        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = (RectTransform)contentGo.transform;
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(1f, 0f);
        contentRt.pivot = new Vector2(0.5f, 0f);
        contentRt.sizeDelta = new Vector2(0f, 0f);

        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 60f;
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeText(contentGo.transform, "Title", "SOOM", font, 140f, FontStyles.Bold, Color.white);
        MakeText(contentGo.transform, "Subtitle", "숨", font, 60f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.8f));
        MakeText(contentGo.transform, "Body", DefaultCreditsText, font, 46f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.9f));

        // 레이아웃을 즉시 계산해 초기 스크롤 위치(뷰포트 아래로 완전히 숨긴 상태)를 정확히 잡는다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        float contentHeight = contentRt.rect.height;
        contentRt.anchoredPosition = new Vector2(0f, -contentHeight);

        return (canvasGo, contentRt, viewportRt);
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, string text, TMP_FontAsset font, float fontSize, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
        return tmp;
    }

    static void Wire(Object component, System.Action<SerializedObject> set)
    {
        var so = new SerializedObject(component);
        set(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

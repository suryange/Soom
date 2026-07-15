using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 03 Tutorial_ex와 B 버튼 안내를 별도 Transform으로 묶은 MissionReady UI 프리팹을 만들고,
/// Tutorial은 Main Camera에, B 안내는 런타임에 오른손 컨트롤러 위에 배치한다.
/// </summary>
internal static class Scene03MissionReadyUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string SourcePrefabPath = "Assets/UI component/03 Tutorial_ex.prefab";
    private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/Scene03_MissionReadyUI.prefab";
    private const string InstanceName = "Scene03_MissionReadyUI";
    private const string KoreanFontPath = "Assets/_Project/Resources/Fonts/NotoSansKR SDF.asset";
    private const string ControllerPromptPrefabPath = "Assets/UI component/08 interact_button.prefab";

    [MenuItem("SOOM/Scene 03/Build and Setup MissionReady UI")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[Scene03MissionReadyUISetup] {ScenePath} 씬을 연 상태에서 실행해주세요.");
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
        Debug.Log("[Scene03MissionReadyUISetup] Scene 03 저장 완료.");
    }

    private static void Setup(Scene scene)
    {
        Camera camera = Camera.main;
        if (camera == null)
            throw new MissingReferenceException("Scene 03에서 Main Camera를 찾지 못했습니다.");

        GameObject prefab = BuildOrUpdatePrefab();
        Transform rightController = FindRightController(scene);
        if (rightController == null)
            throw new MissingReferenceException("Scene 03에서 Right Controller를 찾지 못했습니다.");

        HologramMessage hologram = Object.FindFirstObjectByType<HologramMessage>(FindObjectsInactive.Include);
        if (hologram == null)
            throw new MissingReferenceException("Scene 03에서 HologramMessage를 찾지 못했습니다.");

        GameObject controllerPromptPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPromptPrefabPath);
        if (controllerPromptPrefab == null)
            throw new MissingReferenceException($"조작 안내 프리팹을 찾지 못했습니다: {ControllerPromptPrefabPath}");

        hologram.controllerPromptPrefab = controllerPromptPrefab;
        hologram.rightController = rightController;
        Transform existing = camera.transform.Find(InstanceName);
        GameObject instance;
        if (existing != null)
        {
            instance = existing.gameObject;
        }
        else
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = InstanceName;
            instance.transform.SetParent(camera.transform, false);
        }

        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.localPosition = new Vector3(0f, -0.08f, 0.7f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.001f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);

        MissionReadyUIController controller = instance.GetComponent<MissionReadyUIController>();
        GameObject tutorial = instance.transform.Find("TutorialContent")?.gameObject;
        GameObject prompt = instance.transform.Find("MissionReadyPromptAnchor")?.gameObject;
        if (controller == null || tutorial == null)
            throw new MissingComponentException("MissionReady UI 프리팹 구조가 올바르지 않습니다.");

        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("tutorialRoot").objectReferenceValue = tutorial;
        controllerObject.FindProperty("controllerPromptRoot").objectReferenceValue = prompt;
        controllerObject.FindProperty("promptOwner").objectReferenceValue = hologram;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        // 구독 루트는 활성, 실제 UI 내용은 MissionReady 진입 전까지 비활성이다.
        instance.SetActive(true);
        tutorial.SetActive(false);
        if (prompt != null)
            prompt.SetActive(false);

        EditorUtility.SetDirty(instance);
        EditorUtility.SetDirty(tutorial);
        if (prompt != null)
            EditorUtility.SetDirty(prompt);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(hologram);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = instance;

        Debug.Log(
            "[Scene03MissionReadyUISetup] 완료: TutorialContent와 MissionReadyPromptAnchor 분리, " +
            $"오른손 컨트롤러={(rightController != null ? rightController.name : "Camera Fallback")}, " +
            "MissionReady 상태 구독 연결.");
    }

    private static GameObject BuildOrUpdatePrefab()
    {
        EnsurePrefabFolder();

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        if (source == null)
            throw new MissingReferenceException($"원본 튜토리얼 프리팹을 찾지 못했습니다: {SourcePrefabPath}");

        GameObject root = new GameObject(InstanceName, typeof(RectTransform), typeof(MissionReadyUIController));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(1000f, 650f);

        GameObject content = (GameObject)PrefabUtility.InstantiatePrefab(source);
        content.name = "TutorialContent";
        content.transform.SetParent(root.transform, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = rootRect.sizeDelta;
        contentRect.localScale = Vector3.one;

        Canvas canvas = content.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;
        canvas.sortingOrder = 30;

        Transform oldPrompt = content.transform.Find("BreathingStartPrompt");
        if (oldPrompt != null)
            Object.DestroyImmediate(oldPrompt.gameObject);
        MissionReadyUIController controller = root.GetComponent<MissionReadyUIController>();
        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("tutorialRoot").objectReferenceValue = content;
        controllerObject.FindProperty("controllerPromptRoot").objectReferenceValue = null;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        content.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static GameObject BuildBPrompt(Transform parent)
    {
        GameObject anchor = new GameObject(
            "MissionReadyPromptAnchor",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(FaceCamera));
        anchor.transform.SetParent(parent, false);
        anchor.layer = LayerMask.NameToLayer("UI");

        RectTransform anchorRect = anchor.GetComponent<RectTransform>();
        anchorRect.sizeDelta = new Vector2(760f, 120f);

        Canvas canvas = anchor.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = null;
        canvas.sortingOrder = 40;

        GameObject prompt = new GameObject("BreathingStartPromptText", typeof(RectTransform), typeof(TextMeshProUGUI));
        prompt.transform.SetParent(anchor.transform, false);
        prompt.layer = LayerMask.NameToLayer("UI");

        TextMeshProUGUI text = prompt.GetComponent<TextMeshProUGUI>();
        text.text = "B: 호흡 훈련 시작";
        text.fontSize = 52f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        TMP_FontAsset koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (koreanFont != null)
            text.font = koreanFont;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return anchor;
    }

    private static Transform FindRightController(Scene scene)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.gameObject.scene == scene && candidate.name == "Right Controller")
                return candidate;
        }

        Debug.LogWarning(
            "[Scene03MissionReadyUISetup] Right Controller를 찾지 못했습니다. " +
            "런타임에는 Main Camera 폴백 위치를 사용합니다.");
        return null;
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
            AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
    }
}

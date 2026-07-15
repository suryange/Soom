using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene 03의 중복 Breath Circle을 정리하고 실제 XR B 버튼과 Simulator 입력을
/// 함께 지원하는 전용 InputActionReference를 PlayerInputHandler에 연결한다.
/// </summary>
internal static class Scene03BreathInputSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string InputFolder = "Assets/_Project/Input";
    private const string InputAssetPath = InputFolder + "/Scene03ClueInputActions.asset";
    private const string ActionMapName = "Scene03";
    private const string BActionName = "B Button";

    [MenuItem("SOOM/Scene 03/Setup Breath Circle and B Input")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[Scene03BreathInputSetup] {ScenePath} 씬을 연 상태에서 실행해주세요.");
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
        Debug.Log("[Scene03BreathInputSetup] Scene 03 저장 완료.");
    }

    private static void Setup(Scene scene)
    {
        BreathCircleUI outsideCircle = FindSharedBreathCircle();
        if (outsideCircle == null)
            throw new MissingReferenceException(
                "Scene 03에서 SoomUI/BreathCircle을 찾지 못했습니다. " +
                "SOOM > Build Breath Circle UI를 먼저 실행해 주세요.");

        BreathMissionGuideController guideController =
            Object.FindFirstObjectByType<BreathMissionGuideController>(FindObjectsInactive.Include);
        if (guideController == null)
            throw new MissingReferenceException("Scene 03에서 BreathMissionGuideController를 찾지 못했습니다.");

        BreathManager[] managers = Object.FindObjectsByType<BreathManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (managers.Length != 1)
            throw new System.InvalidOperationException(
                $"Scene 03의 BreathManager는 정확히 하나여야 합니다. 현재 개수: {managers.Length}");
        BreathManager breathManager = managers[0];

        // 기존 수동 생성 UI는 제거하고 상태 컨트롤러에 연결된 Outside UI 하나만 유지한다.
        BreathCircleUI[] circles = Object.FindObjectsByType<BreathCircleUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (BreathCircleUI circle in circles)
        {
            if (circle != null && circle != outsideCircle)
                Undo.DestroyObjectImmediate(circle.gameObject);
        }

        SerializedObject circleObject = new SerializedObject(outsideCircle);
        BreathEventsSO circleEvents =
            circleObject.FindProperty("events").objectReferenceValue as BreathEventsSO;
        if (circleEvents == null)
            throw new MissingReferenceException("SoomUI/BreathCircle.events 참조가 비어 있습니다.");

        Transform leftController = FindSceneTransform("Left Controller", scene);
        Transform rightController = FindSceneTransform("Right Controller", scene);
        if (leftController == null || rightController == null)
            throw new MissingReferenceException(
                $"XR Controller Transform을 찾지 못했습니다. " +
                $"Left={(leftController != null ? leftController.name : "NULL")}, " +
                $"Right={(rightController != null ? rightController.name : "NULL")}");

        Undo.RecordObject(breathManager, "Configure Scene03 Breath Manager");
        breathManager.breathEventsChannel = circleEvents;
        breathManager.leftController = leftController;
        breathManager.rightController = rightController;
        breathManager.targetLoopCount = 3;
        breathManager.maxBreathAngle = 30f;
        breathManager.inhaleThreshold = 0.7f;
        breathManager.exhaleThreshold = 0.3f;

        SerializedObject guideObject = new SerializedObject(guideController);
        guideObject.FindProperty("breathCircleUI").objectReferenceValue = outsideCircle;
        guideObject.ApplyModifiedPropertiesWithoutUndo();
        outsideCircle.gameObject.SetActive(false);

        InputActionReference bActionReference = CreateOrLoadBActionReference();
        PlayerInputHandler inputHandler =
            Object.FindFirstObjectByType<PlayerInputHandler>(FindObjectsInactive.Include);
        if (inputHandler == null)
            throw new MissingReferenceException("Scene 03에서 PlayerInputHandler를 찾지 못했습니다.");

        SerializedObject inputObject = new SerializedObject(inputHandler);
        SerializedProperty actionProperty = inputObject.FindProperty("bButtonAction");
        actionProperty.FindPropertyRelative("m_UseReference").boolValue = true;
        actionProperty.FindPropertyRelative("m_Reference").objectReferenceValue = bActionReference;
        inputObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(guideController);
        EditorUtility.SetDirty(outsideCircle);
        EditorUtility.SetDirty(inputHandler);
        EditorUtility.SetDirty(breathManager);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = outsideCircle.gameObject;

        Debug.Log(
            "[Scene03BreathInputSetup] 완료: SoomUI/BreathCircle 하나만 유지, " +
            "B Button=<XRController>{RightHand}/secondaryButton + <Keyboard>/2 연결, " +
            "BreathManager 좌/우 Controller 및 공통 BreathEventsChannel 배선.");
    }

    private static BreathCircleUI FindSharedBreathCircle()
    {
        return Object.FindObjectsByType<BreathCircleUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(circle =>
                circle != null &&
                circle.gameObject.name == "BreathCircle" &&
                circle.transform.parent != null &&
                circle.transform.parent.name == "SoomUI");
    }

    private static Transform FindSceneTransform(string objectName, Scene scene)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        return transforms.FirstOrDefault(
            candidate => candidate != null &&
                         candidate.gameObject.scene == scene &&
                         candidate.name == objectName);
    }

    private static InputActionReference CreateOrLoadBActionReference()
    {
        EnsureInputFolder();

        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "Scene03ClueInputActions";

            InputActionMap map = asset.AddActionMap(ActionMapName);
            InputAction action = map.AddAction(BActionName, InputActionType.Button, expectedControlLayout: "Button");
            action.AddBinding("<XRController>{RightHand}/secondaryButton");
            action.AddBinding("<Keyboard>/2");

            AssetDatabase.CreateAsset(asset, InputAssetPath);

            InputActionReference reference = InputActionReference.Create(action);
            reference.name = BActionName;
            AssetDatabase.AddObjectToAsset(reference, asset);
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(reference);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(InputAssetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        InputAction targetAction = asset.FindAction($"{ActionMapName}/{BActionName}", true);
        InputActionReference existingReference = AssetDatabase.LoadAllAssetsAtPath(InputAssetPath)
            .OfType<InputActionReference>()
            .FirstOrDefault(reference => reference.action != null && reference.action.id == targetAction.id);

        if (existingReference == null)
        {
            existingReference = InputActionReference.Create(targetAction);
            existingReference.name = BActionName;
            AssetDatabase.AddObjectToAsset(existingReference, asset);
            EditorUtility.SetDirty(existingReference);
            AssetDatabase.SaveAssets();
        }

        return existingReference;
    }

    private static void EnsureInputFolder()
    {
        if (!AssetDatabase.IsValidFolder(InputFolder))
            AssetDatabase.CreateFolder("Assets/_Project", "Input");
    }
}

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Scene 03의 열린 메시지에 Grab 확인 및 재확인 Hover World Space UI를 생성하고 연결한다.
/// 기존 모델과 컴포넌트는 보존하며 반복 실행해도 UI가 중복되지 않는다.
/// </summary>
internal static class Scene03GrabInteractionSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string GrabUiName = "MessageViewingPromptUI";
    private const string LegacyGrabUiName = "GrabConfirmUI";
    private const string ReopenUiName = "ReopenPromptUI";
    private const string ControllerPromptPrefabPath = "Assets/UI component/08 interact_button.prefab";
    private const string GrabInstruction = "메시지를 확인한 뒤 G를 놓으세요";
    private const string ReopenInstruction = "G: 메시지 열기";

    [MenuItem("SOOM/Scene 03/Setup Clue Grab, Return and Reopen UI")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[Scene03GrabInteractionSetup] {ScenePath} 씬을 연 상태에서 실행해주세요.");
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
        Debug.Log("[Scene03GrabInteractionSetup] Scene 03 저장 완료.");
    }

    internal static void Setup(Scene scene)
    {
        ConfigureNearFarRaycastMasks(scene);

        GameObject clue = GameObject.Find("ClueObject");
        if (clue == null)
            throw new MissingReferenceException("Scene 03에서 ClueObject를 찾지 못했습니다.");

        HologramMessage hologram = clue.GetComponent<HologramMessage>();
        if (hologram == null)
            throw new MissingComponentException("ClueObject에 HologramMessage가 없습니다.");

        ConfigureControllerAttach(clue.transform, hologram);

        Transform messageOpen = clue.transform.Find("messageOpen");
        if (messageOpen == null)
            throw new MissingReferenceException("ClueObject/messageOpen을 찾지 못했습니다.");

        GameObject controllerPromptPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(ControllerPromptPrefabPath);
        if (controllerPromptPrefab == null)
            throw new MissingReferenceException($"조작 안내 프리팹을 찾지 못했습니다: {ControllerPromptPrefabPath}");

        Transform rightController = FindRightController(scene);
        if (rightController == null)
            throw new MissingReferenceException("Scene 03에서 Right Controller를 찾지 못했습니다.");

        DisableLegacyPrompt(messageOpen, GrabUiName);
        DisableLegacyPrompt(messageOpen, LegacyGrabUiName);
        DisableLegacyPrompt(messageOpen, ReopenUiName);

        hologram.grabConfirmUI = null;
        hologram.reopenPromptUI = null;
        hologram.controllerPromptPrefab = controllerPromptPrefab;
        hologram.rightController = rightController;

        EditorUtility.SetDirty(hologram);
        ValidateWiring(hologram);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = clue;

        Debug.Log(
            "[Scene03GrabInteractionSetup] 완료: 좌/우 Near-Far Ray에 Interactable Layer 추가, " +
            "Far Attach=Near/ClueAttachPoint, " +
            "08 interact_button/Right Controller 연결 및 전체 참조 검증.");
    }

    private static void DisableLegacyPrompt(Transform parent, string promptName)
    {
        Transform prompt = parent.Find(promptName);
        if (prompt != null)
            prompt.gameObject.SetActive(false);
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

        return null;
    }

    private static void ConfigureControllerAttach(Transform clue, HologramMessage hologram)
    {
        XRGrabInteractable grab = clue.GetComponent<XRGrabInteractable>();
        if (grab == null)
            throw new MissingComponentException("ClueObject에 XRGrabInteractable이 없습니다.");

        Transform attachPoint = clue.Find("ClueAttachPoint");
        if (attachPoint == null)
        {
            GameObject attachObject = new GameObject("ClueAttachPoint");
            Undo.RegisterCreatedObjectUndo(attachObject, "Create Clue Attach Point");
            attachPoint = attachObject.transform;
            attachPoint.SetParent(clue, false);
            attachPoint.localPosition = new Vector3(0f, 0f, 0.4f);
            attachPoint.localRotation = Quaternion.Euler(0f, 180f, 0f);
            attachPoint.localScale = Vector3.one;
        }

        Undo.RecordObject(grab, "Configure Clue Controller Attach");
        Undo.RecordObject(hologram, "Wire Clue Attach Point");
        grab.attachTransform = attachPoint;
        grab.useDynamicAttach = false;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.snapToColliderVolume = false;
        grab.farAttachMode = InteractableFarAttachMode.Near;
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.trackPosition = true;
        grab.trackRotation = true;
        grab.retainTransformParent = true;
        grab.throwOnDetach = false;
        hologram.clueAttachPoint = attachPoint;

        PrefabUtility.RecordPrefabInstancePropertyModifications(grab);
        EditorUtility.SetDirty(grab);
        EditorUtility.SetDirty(hologram);
    }

    private static void ConfigureNearFarRaycastMasks(Scene scene)
    {
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer < 0)
            throw new MissingReferenceException("Project Settings에 Interactable Layer가 없습니다.");

        int interactableBit = 1 << interactableLayer;
        int configuredCount = 0;
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.gameObject.scene != scene) continue;
            if (behaviour.gameObject.name != "Near-Far Interactor") continue;

            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty raycastMask = serialized.FindProperty("m_RaycastMask.m_Bits");
            if (raycastMask == null) continue; // NearFarInteractor 본체가 아닌 Far Caster만 해당

            int updatedMask = raycastMask.intValue | interactableBit;
            if (raycastMask.intValue != updatedMask)
            {
                Undo.RecordObject(behaviour, "Include Interactable Layer In Near-Far Ray");
                raycastMask.intValue = updatedMask;
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                EditorUtility.SetDirty(behaviour);
            }
            configuredCount++;
        }

        if (configuredCount < 2)
        {
            Debug.LogWarning(
                $"[Scene03GrabInteractionSetup] Near-Far Far Caster를 {configuredCount}개만 찾았습니다. " +
                "좌/우 컨트롤러 프리팹 구성을 확인하세요.");
        }
    }

    private static GameObject FindOrBuildPrompt(
        Transform parent,
        string uiName,
        string legacyName,
        string instruction,
        Vector3 localPosition)
    {
        Transform existing = parent.Find(uiName);
        if (existing == null && !string.IsNullOrEmpty(legacyName))
            existing = parent.Find(legacyName);

        if (existing != null && existing.name != uiName)
        {
            Undo.RecordObject(existing.gameObject, $"Migrate {legacyName} To {uiName}");
            existing.name = uiName;
        }

        GameObject ui = existing != null
            ? existing.gameObject
            : BuildWorldPrompt(parent, uiName, instruction, localPosition);

        // 이전 도구 실행이나 수동 복제로 같은 역할의 UI가 여러 개면 하나만 남긴다.
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == ui.transform) continue;

            if (child.name == uiName ||
                (!string.IsNullOrEmpty(legacyName) && child.name == legacyName))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        TextMeshProUGUI instructionText = ui.GetComponentInChildren<TextMeshProUGUI>(true);
        if (instructionText != null)
        {
            instructionText.text = instruction;
            instructionText.raycastTarget = false;
            EditorUtility.SetDirty(instructionText);
        }

        RectTransform rect = ui.GetComponent<RectTransform>();
        if (rect != null)
            rect.localPosition = localPosition;

        return ui;
    }

    private static void ValidateWiring(HologramMessage hologram)
    {
        if (hologram.messageClose == null || hologram.messageOpen == null)
            throw new MissingReferenceException("HologramMessage의 messageClose/messageOpen 참조가 비어 있습니다.");
        if (hologram.controllerPromptPrefab == null || hologram.rightController == null)
            throw new MissingReferenceException("HologramMessage의 08 interact_button/Right Controller 참조가 비어 있습니다.");
        if (hologram.breathEvents == null)
            throw new MissingReferenceException("HologramMessage.breathEvents 참조가 비어 있습니다.");
        if (hologram.guidingLightPrefab == null)
            throw new MissingReferenceException("HologramMessage.guidingLightPrefab 참조가 비어 있습니다.");
        if (hologram.spawnPoint == null)
            throw new MissingReferenceException("HologramMessage.spawnPoint 참조가 비어 있습니다.");
        if (hologram.missionWaypoints == null || hologram.missionWaypoints.Length != 14)
            throw new MissingReferenceException("HologramMessage.missionWaypoints는 정확히 14개여야 합니다.");

        for (int i = 0; i < hologram.missionWaypoints.Length; i++)
        {
            if (hologram.missionWaypoints[i] == null)
                throw new MissingReferenceException($"HologramMessage.missionWaypoints[{i}] 참조가 비어 있습니다.");
        }
    }

    private static GameObject BuildWorldPrompt(
        Transform parent,
        string uiName,
        string instruction,
        Vector3 localPosition)
    {
        GameObject canvasObject = new GameObject(uiName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasObject, $"Create {uiName}");
        canvasObject.transform.SetParent(parent, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(760f, 140f);
        canvasRect.localPosition = localPosition;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = CompensateScale(parent.lossyScale, 0.001f);

        canvasObject.AddComponent<FaceCamera>();

        GameObject textObject = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(canvasObject.transform, false);
        textObject.layer = LayerMask.NameToLayer("UI");

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = instruction;
        text.fontSize = 48f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        canvasObject.SetActive(false);
        return canvasObject;
    }

    private static Vector3 CompensateScale(Vector3 parentLossyScale, float worldScale)
    {
        return new Vector3(
            Mathf.Approximately(parentLossyScale.x, 0f) ? worldScale : worldScale / parentLossyScale.x,
            Mathf.Approximately(parentLossyScale.y, 0f) ? worldScale : worldScale / parentLossyScale.y,
            Mathf.Approximately(parentLossyScale.z, 0f) ? worldScale : worldScale / parentLossyScale.z);
    }
}

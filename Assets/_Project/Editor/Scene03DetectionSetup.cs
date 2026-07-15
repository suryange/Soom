using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene 03의 InteractionDetector와 3.2 감지 UI만 안전하게 배선한다.
/// 기존 DetectionUI와 ClueObject 배치는 보존하며 반복 실행해도 중복 생성하지 않는다.
/// </summary>
internal static class Scene03DetectionSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string IndicatorPrefabPath = "Assets/UI component/05 detect_Somthing.prefab";
    private const string IndicatorInstanceName = "DetectionIndicatorUI";

    [MenuItem("SOOM/Scene 03/Setup Detection Camera Layer and UI")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[Scene03DetectionSetup] {ScenePath} 씬을 연 상태에서 실행해주세요.");
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
        Debug.Log("[Scene03DetectionSetup] Scene 03 저장 완료.");
    }

    private static void Setup(Scene scene)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            throw new MissingReferenceException("Scene 03에서 Main Camera를 찾지 못했습니다.");
        }

        InteractionDetector detector = Object.FindFirstObjectByType<InteractionDetector>(FindObjectsInactive.Include);
        if (detector == null)
        {
            throw new MissingReferenceException("Scene 03에서 InteractionDetector를 찾지 못했습니다.");
        }

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer < 0)
        {
            throw new System.InvalidOperationException("Interactable Layer가 ProjectSettings에 없습니다.");
        }

        SerializedObject detectorObject = new SerializedObject(detector);
        detectorObject.FindProperty("viewOrigin").objectReferenceValue = camera.transform;
        detectorObject.FindProperty("interactableLayer").intValue = 1 << interactableLayer;
        detectorObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject clue = GameObject.Find("ClueObject");
        if (clue == null)
        {
            throw new MissingReferenceException("Scene 03에서 ClueObject를 찾지 못했습니다.");
        }

        HologramMessage hologram = clue.GetComponent<HologramMessage>();
        if (hologram == null)
        {
            throw new MissingComponentException("ClueObject에 HologramMessage가 없습니다.");
        }

        InteractableWorldUI descriptionUI = clue.GetComponentInChildren<InteractableWorldUI>(true);
        if (descriptionUI == null)
        {
            throw new MissingComponentException("ClueObject 아래에서 DetectionUI/InteractableWorldUI를 찾지 못했습니다.");
        }

        // 감지 UI는 단서와 함께 관리될 수 있도록 반드시 ClueObject의 자식으로 둔다.
        // 이전 설정에서 SoomUI 아래에 생성된 인스턴스도 기존 참조를 통해 재사용하고 이동한다.
        Transform uiParent = clue.transform;
        Transform existing = uiParent.Find(IndicatorInstanceName);
        GameObject indicator = hologram.hologramUI;
        if (indicator != null)
        {
            indicator.name = IndicatorInstanceName;
        }
        else if (existing != null)
        {
            indicator = existing.gameObject;
        }
        else
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndicatorPrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"감지 UI 프리팹을 찾지 못했습니다: {IndicatorPrefabPath}");
            }

            indicator = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            indicator.name = IndicatorInstanceName;
            indicator.transform.SetParent(uiParent, false);
        }

        if (indicator.transform.parent != uiParent)
        {
            Undo.SetTransformParent(indicator.transform, uiParent, "Parent Detection UI To ClueObject");
            indicator.transform.localPosition = Vector3.zero;
            indicator.transform.localRotation = Quaternion.identity;
            indicator.transform.localScale = Vector3.one;
        }

        RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
        if (indicatorRect != null)
        {
            // 제공 프리팹 원본의 루트 scale이 0이므로 씬 인스턴스에서 보이도록 교정한다.
            Undo.RecordObject(indicatorRect, "Set Detection Indicator Scale");
            indicatorRect.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(indicatorRect);
            EditorUtility.SetDirty(indicatorRect);
        }

        RectTransform trackedCircle = indicator.transform.Find("Panel") as RectTransform;
        if (trackedCircle == null)
        {
            throw new MissingReferenceException(
                "DetectionIndicatorUI 프리팹에서 추적할 Panel RectTransform을 찾지 못했습니다.");
        }

        DetectionIndicatorTracker tracker = indicator.GetComponent<DetectionIndicatorTracker>();
        if (tracker == null)
            tracker = Undo.AddComponent<DetectionIndicatorTracker>(indicator);

        Vector3 targetWorldCenter = CalculateClosedMessageCenter(hologram, clue.transform);
        Vector3 targetLocalOffset = clue.transform.InverseTransformPoint(targetWorldCenter);
        Undo.RecordObject(tracker, "Configure Detection Indicator Tracker");
        tracker.Configure(camera, clue.transform, trackedCircle, targetLocalOffset);
        EditorUtility.SetDirty(tracker);

        indicator.SetActive(false);

        descriptionUI.gameObject.SetActive(false);
        hologram.hologramUI = indicator;
        hologram.worldUI = descriptionUI;

        EditorUtility.SetDirty(detector);
        EditorUtility.SetDirty(hologram);
        EditorUtility.SetDirty(indicator);
        EditorUtility.SetDirty(descriptionUI.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = clue;

        Debug.Log(
            "[Scene03DetectionSetup] 완료: Main Camera 기준, Interactable LayerMask, " +
            "ClueObject 화면 추적 위치 UI, Far Ray Hover 인계, DetectionUI 설명 배선.");
    }

    private static Vector3 CalculateClosedMessageCenter(HologramMessage hologram, Transform clue)
    {
        Transform closedMessage = hologram.messageClose != null
            ? hologram.messageClose.transform
            : clue.Find("memo_close");
        Transform boundsRoot = closedMessage != null ? closedMessage : clue;
        Renderer[] renderers = boundsRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        Collider[] colliders = boundsRoot.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);
            return bounds.center;
        }

        return clue.position;
    }

}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Scene 03의 기존 ClueObject 모델/배치를 보존하면서 물리 및 XRI Grab 구성만 보완한다.
/// 여러 번 실행해도 컴포넌트가 중복되지 않는다.
/// </summary>
internal static class Scene03CluePhysicalSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";

    [MenuItem("SOOM/Scene 03/Setup Clue Physical and XRI")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[Scene03CluePhysicalSetup] {ScenePath} 씬을 연 상태에서 실행해주세요.");
            return;
        }

        Setup(scene);
    }

    // Unity batchmode에서 특정 단계만 안전하게 적용하기 위한 진입점.
    public static void ApplyToScene03()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Setup(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Scene03CluePhysicalSetup] Scene 03 저장 완료.");
    }

    private static void Setup(Scene scene)
    {
        GameObject clue = GameObject.Find("ClueObject");
        if (clue == null)
        {
            throw new MissingReferenceException("Scene 03에서 ClueObject를 찾지 못했습니다.");
        }

        Transform messageClose = FindSceneObject(scene, "memo_close");
        Transform messageOpen = FindSceneObject(scene, "memo_open");
        if (messageClose == null || messageOpen == null)
        {
            throw new MissingReferenceException(
                "Scene 03 하이어라키에 memo_close와 memo_open이 모두 있어야 합니다.");
        }

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer < 0)
        {
            throw new System.InvalidOperationException("Interactable Layer가 ProjectSettings에 없습니다.");
        }

        SetLayerRecursively(clue.transform, interactableLayer);

        // 기존 자식 Collider를 XRGrabInteractable이 자동 수집한다.
        Collider[] colliders = clue.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            BoxCollider fallback = Undo.AddComponent<BoxCollider>(clue);
            fallback.isTrigger = false;
            colliders = clue.GetComponentsInChildren<Collider>(true);
            Debug.LogWarning("[Scene03CluePhysicalSetup] Collider가 없어 루트에 임시 BoxCollider를 추가했습니다.");
        }

        Rigidbody body = clue.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = Undo.AddComponent<Rigidbody>(clue);
        }
        body.mass = 0.2f;
        body.useGravity = false;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        XRGrabInteractable grab = clue.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = Undo.AddComponent<XRGrabInteractable>(clue);
        }
        grab.colliders.Clear();
        foreach (Collider clueCollider in colliders)
        {
            if (clueCollider != null && !clueCollider.isTrigger)
                grab.colliders.Add(clueCollider);
        }
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        Transform attachPoint = EnsureAttachPoint(clue.transform);
        grab.attachTransform = attachPoint;
        grab.useDynamicAttach = false;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.snapToColliderVolume = false;
        grab.farAttachMode = InteractableFarAttachMode.Near;
        grab.trackPosition = true;
        grab.trackRotation = true;
        grab.retainTransformParent = true;
        grab.throwOnDetach = false;

        HologramMessage hologram = clue.GetComponent<HologramMessage>();
        if (hologram == null)
        {
            hologram = Undo.AddComponent<HologramMessage>(clue);
        }
        hologram.messageClose = messageClose.gameObject;
        hologram.messageOpen = messageOpen.gameObject;
        hologram.clueAttachPoint = attachPoint;

        messageClose.gameObject.SetActive(true);
        messageOpen.gameObject.SetActive(false);

        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(grab);
        EditorUtility.SetDirty(hologram);
        EditorUtility.SetDirty(clue);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = clue;

        Debug.Log(
            $"[Scene03CluePhysicalSetup] 완료: Layer=Interactable, Collider={colliders.Length}, " +
            "Far Attach=Near, ClueAttachPoint, Rigidbody/XRGrabInteractable/HologramMessage 구성, " +
            "memo_close 활성/memo_open 비활성.");
    }

    private static Transform FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            Transform[] hierarchy = rootObject.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in hierarchy)
            {
                if (candidate.name == objectName)
                    return candidate;
            }
        }

        return null;
    }

    private static Transform EnsureAttachPoint(Transform clue)
    {
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

        return attachPoint;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}

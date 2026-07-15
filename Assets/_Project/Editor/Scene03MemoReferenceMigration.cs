using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[InitializeOnLoad]
internal static class Scene03MemoReferenceMigration
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string CloseAssetPath = "Assets/_Project/Prefabs/MyMesh/holomemo/memo_close.fbx";
    private const string OpenAssetPath = "Assets/_Project/Prefabs/MyMesh/holomemo/memo_open.fbx";
    private const string SessionKey = "SOOM.Scene03MemoReferenceMigration.Completed";

    static Scene03MemoReferenceMigration()
    {
        EditorApplication.delayCall += RunOnce;
    }

    public static void RunFromCommandLine()
    {
        SessionState.EraseBool(SessionKey);
        RunOnce();
    }

    private static void RunOnce()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorApplication.delayCall += RunOnce;
            return;
        }

        SessionState.SetBool(SessionKey, true);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject clue = FindInScene(scene, "ClueObject");
            if (clue == null)
                throw new InvalidOperationException("Scene 03에서 ClueObject를 찾지 못했습니다.");

            GameObject memoClose = FindInScene(scene, "memo_close");
            if (memoClose == null)
                memoClose = InstantiateModel(CloseAssetPath, scene, "memo_close");

            GameObject memoOpen = FindInScene(scene, "memo_open");
            if (memoOpen == null)
                memoOpen = InstantiateModel(OpenAssetPath, scene, "memo_open");

            PlaceUnderClue(memoClose, clue.transform);
            PlaceUnderClue(memoOpen, clue.transform);
            CopyLocalPose(memoClose.transform, memoOpen.transform);

            SetLayerRecursively(memoClose, clue.layer);
            SetLayerRecursively(memoOpen, clue.layer);

            BoxCollider closeCollider = EnsureFittedBoxCollider(memoClose);
            BoxCollider openCollider = EnsureFittedBoxCollider(memoOpen);

            HologramMessage hologram = clue.GetComponent<HologramMessage>();
            if (hologram == null)
                throw new MissingComponentException("ClueObject에 HologramMessage가 없습니다.");

            SerializedObject hologramObject = new SerializedObject(hologram);
            hologramObject.FindProperty("messageClose").objectReferenceValue = memoClose;
            hologramObject.FindProperty("messageOpen").objectReferenceValue = memoOpen;
            hologramObject.ApplyModifiedPropertiesWithoutUndo();

            XRGrabInteractable grab = clue.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.colliders.Clear();
                grab.colliders.Add(closeCollider);
                grab.colliders.Add(openCollider);
                EditorUtility.SetDirty(grab);
            }

            memoClose.SetActive(true);
            memoOpen.SetActive(false);
            EditorUtility.SetDirty(hologram);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SOOM] Scene 03 ClueObject를 memo_close/memo_open으로 교체하고 참조 및 Collider를 연결했습니다.");
        }
        catch (Exception exception)
        {
            SessionState.EraseBool(SessionKey);
            Debug.LogException(exception);
        }
        finally
        {
            if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == objectName)
                    return candidate.gameObject;
            }
        }

        return null;
    }

    private static GameObject InstantiateModel(string assetPath, Scene scene, string objectName)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            throw new MissingReferenceException($"모델 에셋을 찾지 못했습니다: {assetPath}");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        instance.name = objectName;
        return instance;
    }

    private static void PlaceUnderClue(GameObject model, Transform clue)
    {
        Transform modelTransform = model.transform;
        modelTransform.SetParent(clue, true);
        modelTransform.position = clue.position;
    }

    private static void CopyLocalPose(Transform source, Transform destination)
    {
        destination.localPosition = source.localPosition;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    private static BoxCollider EnsureFittedBoxCollider(GameObject model)
    {
        BoxCollider collider = model.GetComponent<BoxCollider>();
        if (collider == null)
            collider = model.AddComponent<BoxCollider>();

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return collider;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        collider.center = model.transform.InverseTransformPoint(worldBounds.center);
        Vector3 scale = model.transform.lossyScale;
        collider.size = new Vector3(
            DivideSafely(worldBounds.size.x, scale.x),
            DivideSafely(worldBounds.size.y, scale.y),
            DivideSafely(worldBounds.size.z, scale.z));
        return collider;
    }

    private static float DivideSafely(float value, float scale)
    {
        return value / Mathf.Max(Mathf.Abs(scale), 0.0001f);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }
}

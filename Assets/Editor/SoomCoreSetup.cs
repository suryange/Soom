// SoomCoreSetup.cs
// Editor utility: one-click creation of the persistent "SoomCore" root that carries
// ScreenFader and SoomAudioManager, plus a helper to make sure the three project scenes
// are registered in Build Settings (scenes themselves are not created here).
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SoomCoreSetup
{
    const string CoreRootName = "SoomCore";

    static readonly string[] RequiredScenePaths =
    {
        "Assets/_Project/Scenes/Scene_01_Start.unity",
        "Assets/_Project/Scenes/Scene_02_InGame_Inside.unity",
        "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity",
    };

    [MenuItem("SOOM/Build Core (Fader + Audio)")]
    public static void BuildCore()
    {
        // 멱등성: 이미 존재하는 SoomCore 루트가 있으면 제거하고 새로 만든다.
        var existing = GameObject.Find(CoreRootName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject(CoreRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create SoomCore");

        root.AddComponent<ScreenFader>();
        root.AddComponent<SoomAudioManager>();

        EnsureScenesRegistered();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[SoomCoreSetup] 'SoomCore' 루트를 생성하고 ScreenFader + SoomAudioManager를 부착했습니다.");
        Selection.activeGameObject = root;
    }

    /// <summary>
    /// EditorBuildSettings에 3개 씬이 등록되어 있지 않으면 등록한다. 기존에 등록된 씬 목록은 보존한다.
    /// </summary>
    static void EnsureScenesRegistered()
    {
        var current = EditorBuildSettings.scenes;
        var currentPaths = new System.Collections.Generic.HashSet<string>();
        foreach (var s in current) currentPaths.Add(s.path);

        bool missingAny = false;
        foreach (var path in RequiredScenePaths)
        {
            if (!currentPaths.Contains(path)) missingAny = true;
        }

        if (!missingAny) return;

        var newList = new System.Collections.Generic.List<EditorBuildSettingsScene>(current);
        foreach (var path in RequiredScenePaths)
        {
            if (currentPaths.Contains(path)) continue;
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[SoomCoreSetup] 씬을 찾을 수 없어 Build Settings에 등록하지 못했습니다: {path}");
                continue;
            }
            newList.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = newList.ToArray();
        Debug.Log("[SoomCoreSetup] Build Settings에 누락된 씬을 등록했습니다.");
    }
}

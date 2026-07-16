using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SoomAudioSetup
{
    private const string Scene01Path = "Assets/_Project/Scenes/Scene_01_Start.unity";
    private const string Scene03Path = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string LibraryPath = "Assets/_Project/Settings/SoomAudioLibrary.asset";
    private const string BreathEventsPath = "Assets/_Project/Scripts/System/BreathEventsChannel.asset";

    [MenuItem("SOOM/Audio/Setup Audio Manager In Scenes 01-03")]
    public static void SetupAudioManagerInScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene previousScene = SceneManager.GetActiveScene();
        string previousScenePath = previousScene.IsValid() ? previousScene.path : string.Empty;

        SoomAudioLibrarySO library = GetOrCreateLibrary();
        BreathEventsSO breathEvents = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(BreathEventsPath);
        if (breathEvents == null)
            throw new MissingReferenceException($"호흡 이벤트 채널을 찾을 수 없습니다: {BreathEventsPath}");

        int createdManagers = 0;
        int updatedManagers = 0;
        SetupScene(Scene01Path, true, library, breathEvents, ref createdManagers, ref updatedManagers);
        SetupScene(Scene03Path, false, library, breathEvents, ref createdManagers, ref updatedManagers);

        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != Scene03Path)
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        Debug.Log(
            $"[SoomAudioSetup] 완료: 매니저 {createdManagers}개 생성, {updatedManagers}개 갱신. " +
            "기존 Library의 AudioClip과 volume 값은 유지했습니다.");
    }

    private static SoomAudioLibrarySO GetOrCreateLibrary()
    {
        SoomAudioLibrarySO library = AssetDatabase.LoadAssetAtPath<SoomAudioLibrarySO>(LibraryPath);
        if (library != null)
            return library;

        library = ScriptableObject.CreateInstance<SoomAudioLibrarySO>();
        AssetDatabase.CreateAsset(library, LibraryPath);
        return library;
    }

    private static void SetupScene(
        string scenePath,
        bool createRoot,
        SoomAudioLibrarySO library,
        BreathEventsSO breathEvents,
        ref int createdManagers,
        ref int updatedManagers)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        List<SoomAudioManager> managers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<SoomAudioManager>(true))
            .ToList();

        SoomAudioManager manager = managers.FirstOrDefault();
        if (manager == null)
        {
            GameObject host = createRoot
                ? new GameObject("SoomAudioRoot")
                : FindOrCreateSoomCore(scene);
            manager = Undo.AddComponent<SoomAudioManager>(host);
            createdManagers++;
        }
        else
        {
            updatedManagers++;
        }

        for (int i = 1; i < managers.Count; i++)
            Undo.DestroyObjectImmediate(managers[i]);

        AudioSource bgmSource = EnsureSource(manager.transform, "BGMSource", true);
        AudioSource sfxSource = EnsureSource(manager.transform, "SFXSource", false);
        AudioSource voiceSource = EnsureSource(manager.transform, "VoiceSource", false);

        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("audioLibrary").objectReferenceValue = library;
        serializedManager.FindProperty("breathEventsChannel").objectReferenceValue = breathEvents;
        serializedManager.FindProperty("bgmSource").objectReferenceValue = bgmSource;
        serializedManager.FindProperty("sfxSource").objectReferenceValue = sfxSource;
        serializedManager.FindProperty("voiceSource").objectReferenceValue = voiceSource;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject FindOrCreateSoomCore(Scene scene)
    {
        GameObject core = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "SoomCore");
        return core != null ? core : new GameObject("SoomCore");
    }

    private static AudioSource EnsureSource(Transform parent, string sourceName, bool loop)
    {
        Transform child = parent.Find(sourceName);
        GameObject sourceObject;
        if (child == null)
        {
            sourceObject = new GameObject(sourceName);
            Undo.RegisterCreatedObjectUndo(sourceObject, $"Create {sourceName}");
            sourceObject.transform.SetParent(parent, false);
        }
        else
        {
            sourceObject = child.gameObject;
        }

        AudioSource source = sourceObject.GetComponent<AudioSource>();
        if (source == null)
            source = Undo.AddComponent<AudioSource>(sourceObject);

        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        EditorUtility.SetDirty(source);
        return source;
    }
}

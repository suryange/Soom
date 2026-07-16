using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Applies the asset/scene wiring described in plan.md. The operation is idempotent so it can
/// also be run manually after replacing either source FBX.
/// </summary>
internal static class Scene03PlanSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Scene_03_InGame_Outside.unity";
    private const string MemoModelPath =
        "Assets/_Project/Prefabs/MyMesh/holomemo/memo_open/memo_open 1.fbx";
    private const string ButterflyModelPath =
        "Assets/_Project/Prefabs/MyMesh/butterfly/Butterflyy_final/butterfly_2.fbx";
    private const string GuidingLightPrefabPath = "Assets/_Project/Prefabs/GuidingLight.prefab";
    private const string ButterflyControllerPath =
        "Assets/_Project/Prefabs/GuidingLightButterfly.controller";
    private const string DustMaterialPath = "Assets/_Project/Prefabs/GuidingLightDust.mat";

    [MenuItem("SOOM/Scene 03/Apply plan.md Setup")]
    private static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
    }

    private static void Apply()
    {
        ConfigureButterflyImport();
        AnimatorController controller = EnsureButterflyController();
        EnsureDustMaterial();
        ConfigureGuidingLightPrefab(controller);

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.path == ScenePath)
        {
            ConfigureScene03(scene);
            EditorSceneManager.SaveScene(scene);
        }
        else
        {
            Scene setupScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                ConfigureScene03(setupScene);
                EditorSceneManager.SaveScene(setupScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(setupScene, true);
            }
        }

        AssetDatabase.SaveAssets();
    }

    private static void ConfigureButterflyImport()
    {
        ModelImporter importer = AssetImporter.GetAtPath(ButterflyModelPath) as ModelImporter;
        if (importer == null)
            throw new MissingReferenceException($"나비 FBX를 찾을 수 없습니다: {ButterflyModelPath}");

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            if (clip.loopTime) continue;
            clip.loopTime = true;
            changed = true;
        }

        if (changed || importer.clipAnimations == null || importer.clipAnimations.Length == 0)
        {
            importer.importAnimation = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip GetButterflyClip()
    {
        return AssetDatabase.LoadAllAssetsAtPath(ButterflyModelPath)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__") && clip.length > 0.01f)
            .OrderByDescending(clip => clip.length)
            .FirstOrDefault();
    }

    private static AnimatorController EnsureButterflyController()
    {
        AnimationClip clip = GetButterflyClip();
        if (clip == null)
            throw new MissingReferenceException("butterfly_2.fbx에서 날개짓 AnimationClip을 찾지 못했습니다.");

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ButterflyControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ButterflyControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = stateMachine.states
            .Select(item => item.state)
            .FirstOrDefault(item => item.name == "Wing Flap");
        if (state == null)
            state = stateMachine.AddState("Wing Flap");

        state.motion = clip;
        stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static Material EnsureDustMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            throw new MissingReferenceException("GuidingLight 파티클용 Shader를 찾지 못했습니다.");

        if (material == null)
        {
            material = new Material(shader) { name = "GuidingLightDust" };
            AssetDatabase.CreateAsset(material, DustMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        Color color = new Color(1f, 0.86f, 0.42f, 0.9f);
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureGuidingLightPrefab(AnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GuidingLightPrefabPath);
        if (root == null)
            throw new MissingReferenceException($"GuidingLight 프리팹을 찾을 수 없습니다: {GuidingLightPrefabPath}");

        try
        {
            Transform glowOrb = root.transform.Find("GlowOrb");
            if (glowOrb != null)
                Object.DestroyImmediate(glowOrb.gameObject);

            Transform visual = root.transform.Find("ButterflyVisual");
            if (visual == null)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ButterflyModelPath);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
                instance.name = "ButterflyVisual";
                visual = instance.transform;
                visual.SetParent(root.transform, false);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            ConfigureTinkerDust(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, GuidingLightPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureTinkerDust(Transform root)
    {
        Transform dustTransform = root.Find("TinkerDust");
        GameObject dustObject;
        if (dustTransform == null)
        {
            dustObject = new GameObject("TinkerDust", typeof(ParticleSystem));
            dustTransform = dustObject.transform;
            dustTransform.SetParent(root, false);
            dustTransform.localPosition = new Vector3(0f, 0f, -0.18f);
        }
        else
        {
            dustObject = dustTransform.gameObject;
        }

        ParticleSystem particles = dustObject.GetComponent<ParticleSystem>();
        if (particles == null) particles = dustObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.06f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.78f, 0.25f, 0.95f),
            new Color(0.45f, 1f, 0.82f, 0.8f));
        main.maxParticles = 128;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 24f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.86f, 0.38f), 0f),
                new GradientColorKey(new Color(0.55f, 1f, 0.82f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.7f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = fadeGradient;

        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.65f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.12f;

        ParticleSystemRenderer renderer = dustObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = EnsureDustMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void ConfigureScene03(Scene scene)
    {
        BreathCircleUI sharedUI = scene.GetRootGameObjects()
            .Select(item => item.transform)
            .Where(item => item.name == "SoomUI")
            .Select(item => item.GetComponentInChildren<BreathCircleUI>(true))
            .FirstOrDefault(item => item != null);
        if (sharedUI == null)
        {
            Debug.LogError("[Scene03PlanSetup] SoomUI/BreathCircle 공용 UI를 찾지 못했습니다.");
            return;
        }

        FoxEncounterController[] foxControllers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<FoxEncounterController>(true))
            .ToArray();
        foreach (FoxEncounterController fox in foxControllers)
        {
            SerializedObject serializedFox = new SerializedObject(fox);
            serializedFox.FindProperty("breathCircleUI").objectReferenceValue = sharedUI;
            serializedFox.ApplyModifiedPropertiesWithoutUndo();

            BreathCircleUI[] duplicateUIs = fox.GetComponentsInChildren<BreathCircleUI>(true);
            foreach (BreathCircleUI duplicate in duplicateUIs)
                if (duplicate != sharedUI)
                    Undo.DestroyObjectImmediate(duplicate.gameObject);
            EditorUtility.SetDirty(fox);
        }

        AssignSharedUI<BreathMissionGuideController>(scene, "breathCircleUI", sharedUI);
        AssignSharedUI<SandstormController>(scene, "breathCircleUI", sharedUI);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void AssignSharedUI<T>(Scene scene, string propertyName, BreathCircleUI sharedUI)
        where T : MonoBehaviour
    {
        T[] controllers = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
        foreach (T controller in controllers)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty property = serializedController.FindProperty(propertyName);
            if (property == null) continue;
            property.objectReferenceValue = sharedUI;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }
    }
}

using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// 여우와의 조우 (기능 명세서 5장) — 씬 배선용 에디터 빌더.
///
/// 씬에 이미 배치된 실제 여우 모델(fox_sample_2 인스턴스)을 찾아 그 위에 조우 로직 전체를 부착한다.
/// (모델을 못 찾으면 캡슐+큐브 플레이스홀더로 폴백)
///  • 실제 모델의 Renderer bounds를 계산해 콜라이더 / 상태 UI / 상호작용 UI / 호흡 UI / 불안의 막 구체를
///    모델 크기·스케일(예: 리그에 맞춘 ×3)에 맞게 자동 배치한다.
///  • fox_sample_2.fbx의 실제 클립(Action1_Alert / Action4_Standing_Happy / Action5_Walking)을 참조하는
///    Fox_Encounter.controller를 생성해 Animator에 연결한다. (Wary/Joy/Walk 상태)
///  • FoxInteractable / FoxEncounterController / FoxCompanionFollower / InteractableDataSO / 막 머티리얼을
///    모두 생성·배선하고, 공용 InteractionDetector의 레이어/viewOrigin을 패치한다.
///
/// 사용법: Scene_03_InGame_Outside 씬을 열어둔 상태에서 메뉴 SOOM/Build Fox Encounter 실행.
/// 이 스크립트는 씬을 자동 저장하지 않는다 — 실행 후 결과가 마음에 들면 씬을 직접 저장해야 한다.
/// </summary>
public static class FoxEncounterBuilder
{
    private const string RealFoxObjectName = "fox_sample_2";
    private const string FoxFbxPath = "Assets/_Project/Prefabs/MyMesh/fox/fox_sample_2.fbx";
    private const string FoxControllerPath = "Assets/_Project/Prefabs/MyMesh/fox/animcontroller/Fox_Encounter.controller";
    private const string DataAssetPath = "Assets/_Project/Data/Fox_InteractableData.asset";
    private const string MembraneMaterialPath = "Assets/_Project/Arts/Mat_AnxietyMembrane.mat";
    private const string GlassMaterialPath = "Assets/_Project/Arts/Mat_UIFrostedGlass.mat";
    private const string BreathEventsAssetPath = "Assets/_Project/Scripts/System/BreathEventsChannel.asset";

    // ---- UI 테마 (visionOS 라이트 글래스: 반투명 유리 + 얇은 화이트 엣지 + 블루 액센트) ----
    private static readonly Color PanelFill = new Color(0.88f, 0.90f, 0.94f, 0.60f);   // 라이트 쿨 글래스
    private static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.55f);          // 얇은 화이트 엣지
    private static readonly Color TextMain = new Color(0.10f, 0.12f, 0.16f, 1f);       // 다크 텍스트(라이트 글래스 위)
    private static readonly Color TextMuted = new Color(0.34f, 0.38f, 0.44f, 1f);      // 흐린 텍스트(설명)
    private static readonly Color ButtonFill = new Color(0.16f, 0.50f, 0.95f, 0.92f);  // 반투명 블루 필 버튼
    private static readonly Color ButtonBorder = new Color(1f, 1f, 1f, 0.5f);          // 버튼 화이트 엣지
    private static readonly Color ButtonText = new Color(1f, 1f, 1f, 1f);              // 버튼 라벨(화이트)

    // 이번 빌드에서 유리 카드에 적용할 프로스티드 글래스 머티리얼(셰이더 없으면 null → 반투명색 폴백)
    private static Material _glassMat;

    [MenuItem("SOOM/Build Fox Encounter")]
    private static void BuildFoxEncounter()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.name.Contains("Scene_03"))
        {
            Debug.LogWarning(
                $"[FoxEncounterBuilder] 현재 활성 씬이 'Scene_03_InGame_Outside'가 아닙니다(현재: '{activeScene.name}'). " +
                "여우와의 조우는 해당 씬에서 동작하도록 설계되었습니다. 계속 진행합니다.");
        }

        if (Object.FindFirstObjectByType<FoxEncounterController>() != null)
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] 씬에 이미 FoxEncounterController가 배선되어 있습니다. " +
                "다시 생성하려면 기존 여우 배선(콜라이더/UI/컨트롤러 등)을 먼저 제거한 뒤 실행하세요.");
            return;
        }

        Undo.SetCurrentGroupName("Build Fox Encounter");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureFolder("Assets/_Project", "Data");

        InteractableDataSO data = CreateOrLoadInteractableData();
        BreathEventsSO breathEvents = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(BreathEventsAssetPath);
        if (breathEvents == null)
        {
            Debug.LogWarning(
                $"[FoxEncounterBuilder] '{BreathEventsAssetPath}'에서 BreathEventsSO를 찾지 못했습니다. " +
                "호흡 이벤트 연동 없이 진행합니다 — FoxEncounterController.breathEvents를 수동으로 연결하세요.");
        }
        Material membraneMaterial = CreateOrLoadMembraneMaterial();
        _glassMat = CreateOrLoadGlassMaterial();

        EnsureEventSystem();

        GameObject fox = ResolveFoxRoot(out bool isPlaceholder);
        Animator animator = EnsureFoxAnimator(fox);

        // 실제 모델의 크기/스케일에 맞춰 UI·막·콜라이더를 배치하기 위한 기준값.
        Bounds bounds = GetFoxWorldBounds(fox);
        float ps = fox.transform.lossyScale.x;
        if (Mathf.Approximately(ps, 0f)) ps = 1f;
        float foxHeight = Mathf.Max(bounds.size.y, 0.3f);
        float uiWorldScale = Mathf.Clamp(foxHeight, 0.6f, 3f) / 520f; // 패널을 여우 키에 맞춰 살짝 작게

        // 감지 콜라이더 (실제 모델은 콜라이더가 없으므로 bounds에 맞춰 추가; 플레이스홀더는 이미 보유)
        if (!isPlaceholder && fox.GetComponent<Collider>() == null)
            AddDetectionCollider(fox, bounds, ps);

        // 패널을 여우 위로 띄워 모델과 겹치지 않게 스택 (아래→위: 머리 → 상태 알약 → 카드/호흡).
        float top = bounds.max.y;
        float gap = 0.06f * foxHeight;
        float WorldH(float px) => px * uiWorldScale;
        float statusY = top + WorldH(96f) * 0.5f + gap;
        float cardY = statusY + WorldH(96f) * 0.5f + WorldH(300f) * 0.5f + WorldH(44f);
        Vector3 statusPos = new Vector3(bounds.center.x, statusY, bounds.center.z);
        Vector3 cardPos = new Vector3(bounds.center.x, cardY, bounds.center.z);

        var (membraneGo, membraneRenderer) = BuildMembrane(fox.transform, membraneMaterial, bounds, ps);
        BreathCircleUI breathUI = BuildBreathCircleUI(fox.transform, breathEvents, cardPos, uiWorldScale, ps);
        var (statusRoot, statusText) = BuildStatusUI(fox.transform, statusPos, uiWorldScale, ps);
        FoxInteractionUIRefs interactionUI = BuildInteractionUI(fox.transform, cardPos, uiWorldScale, ps);
        FoxCompanionFollower companionFollower = BuildCompanionFollower(fox, animator, bounds, ps);

        FoxInteractable foxInteractable = Undo.AddComponent<FoxInteractable>(fox);
        FoxEncounterController controller = Undo.AddComponent<FoxEncounterController>(fox);

        WireFoxInteractable(foxInteractable, data, interactionUI, controller);
        WireFoxEncounterController(
            controller, data, breathEvents, statusRoot, statusText, interactionUI,
            breathUI, membraneGo, membraneRenderer, animator, companionFollower);

        ConfigureSharedInteractionDetector();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(fox.scene);
        Selection.activeGameObject = fox;

        Debug.Log(
            "[FoxEncounterBuilder] 여우와의 조우 배선 완료" +
            (isPlaceholder ? " (⚠️ 실제 fox_sample_2를 못 찾아 플레이스홀더 사용)." : " (실제 fox_sample_2 모델).") +
            " 씬을 저장하세요. 애니메이터/막/UI 크기는 모델 bounds를 기준으로 자동 배치했습니다.");
    }

    /// <summary>기존 여우 배선(UI/막/컴포넌트)을 제거하고 최신 스타일로 다시 만든다.</summary>
    [MenuItem("SOOM/Rebuild Fox Encounter (Restyle)")]
    private static void RebuildFoxEncounter()
    {
        RemoveFoxEncounter();
        BuildFoxEncounter();
    }

    /// <summary>
    /// 배선한 뒤 곧바로 활성 씬을 저장한다. 배선은 메모리에만 남고 씬을 저장하지 않으면
    /// 디스크(.unity)에는 반영되지 않아 "여우에 아무 스크립트도 안 붙어 있는" 상태가 되므로,
    /// 클릭 한 번으로 배선+저장까지 끝내고 싶을 때 이 메뉴를 쓴다.
    /// </summary>
    [MenuItem("SOOM/Build Fox Encounter (and Save)")]
    private static void BuildFoxEncounterAndSave()
    {
        // 아직 배선이 없을 때만 새로 만든다. 이미 있으면(메모리에만 있고 미저장일 수 있음) 저장만 한다.
        if (Object.FindFirstObjectByType<FoxEncounterController>() == null)
            BuildFoxEncounter();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (Object.FindFirstObjectByType<FoxEncounterController>() != null)
        {
            bool ok = EditorSceneManager.SaveScene(scene);
            Debug.Log(ok
                ? $"[FoxEncounterBuilder] '{scene.name}' 저장 완료 — 여우 조우 배선이 디스크에 반영되었습니다."
                : "[FoxEncounterBuilder] 씬 저장에 실패했습니다. 수동으로 저장(Ctrl/Cmd+S)하세요.");
        }
        else
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] 배선이 생성되지 않아 저장을 건너뜁니다. 위 콘솔 경고(fox_sample_2/레이어/BreathEvents 등)를 확인하세요.");
        }
    }

    private static void RemoveFoxEncounter()
    {
        var controller = Object.FindFirstObjectByType<FoxEncounterController>();
        if (controller == null) return;

        var fox = controller.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(fox, "Remove Fox Encounter");

        foreach (var childName in new[] { "UI_Status", "UI_Interaction", "UI_BreathCircle", "AnxietyMembrane" })
        {
            var t = fox.transform.Find(childName);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        DestroyIfPresent<FoxInteractable>(fox);
        DestroyIfPresent<FoxEncounterController>(fox);
        DestroyIfPresent<FoxCompanionFollower>(fox);
        DestroyIfPresent<NavMeshAgent>(fox);
        DestroyIfPresent<CapsuleCollider>(fox);
    }

    private static void DestroyIfPresent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null) Object.DestroyImmediate(c);
    }

    // ============================================================
    // 여우 루트 확보 (실제 모델 우선, 없으면 플레이스홀더)
    // ============================================================

    private static GameObject ResolveFoxRoot(out bool isPlaceholder)
    {
        var existing = GameObject.Find(RealFoxObjectName);
        if (existing != null)
        {
            isPlaceholder = false;
            Undo.RegisterFullObjectHierarchyUndo(existing, "Configure Fox Encounter");

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
                existing.layer = interactableLayer;
            else
                Debug.LogWarning("[FoxEncounterBuilder] 'Interactable' 레이어를 찾지 못했습니다 (TagManager 확인 필요).");

            Debug.Log("[FoxEncounterBuilder] 씬의 실제 여우 모델(fox_sample_2)에 조우 로직을 부착합니다.");
            return existing;
        }

        Debug.LogWarning(
            "[FoxEncounterBuilder] 씬에서 'fox_sample_2'를 찾지 못해 임시 캡슐 플레이스홀더로 대체합니다. " +
            "실제 모델을 씬에 배치한 뒤 다시 실행하면 그 모델에 부착됩니다.");
        isPlaceholder = true;
        return BuildFoxRoot();
    }

    private static Animator EnsureFoxAnimator(GameObject fox)
    {
        var animator = fox.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(fox);

        RuntimeAnimatorController controller = CreateOrLoadFoxController();
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(animator);
        }
        return animator;
    }

    // ============================================================
    // 애니메이터 컨트롤러 생성 (실제 FBX 클립 참조)
    // ============================================================

    private static RuntimeAnimatorController CreateOrLoadFoxController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(FoxControllerPath);
        if (existing != null) return existing;

        Object[] reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(FoxFbxPath);
        AnimationClip alert = FindClip(reps, n => n.Contains("alert") && !n.Contains("standing"))
                           ?? FindClip(reps, n => n.Contains("alert"));                 // Action1_Alert
        AnimationClip happy = FindClip(reps, n => n.Contains("standing") && n.Contains("happy"))
                           ?? FindClip(reps, n => n.Contains("happy"));                 // Action4_Standing_Happy
        AnimationClip walk = FindClip(reps, n => n.Contains("walk"));                    // Action5_Walking

        if (alert == null && happy == null && walk == null)
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] fox_sample_2.fbx에서 애니메이션 클립을 찾지 못했습니다. " +
                "애니메이터 없이 진행합니다(흐름은 정상 동작). 발견된 클립: " + DescribeClips(reps));
            return null;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(FoxControllerPath);
        var sm = controller.layers[0].stateMachine;

        var waryState = sm.AddState("Wary");
        waryState.motion = alert;
        var joyState = sm.AddState("Joy");
        joyState.motion = happy;
        var walkState = sm.AddState("Walk");
        walkState.motion = walk;

        sm.defaultState = waryState; // 감지 전에도 T-포즈 대신 경계 자세를 유지

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[FoxEncounterBuilder] Fox_Encounter 컨트롤러 생성 — Wary={ClipName(alert)}, Joy={ClipName(happy)}, Walk={ClipName(walk)}");
        return controller;
    }

    private static AnimationClip FindClip(Object[] reps, System.Func<string, bool> match)
    {
        foreach (var o in reps)
            if (o is AnimationClip c && match(c.name.ToLowerInvariant()))
                return c;
        return null;
    }

    private static string ClipName(AnimationClip c) => c != null ? c.name : "(없음)";

    private static string DescribeClips(Object[] reps)
    {
        string s = "";
        foreach (var o in reps)
            if (o is AnimationClip c) s += (s.Length > 0 ? ", " : "") + c.name;
        return s.Length > 0 ? s : "(클립 없음)";
    }

    // ============================================================
    // 모델 크기(bounds) 기반 배치 헬퍼
    // ============================================================

    private static Bounds GetFoxWorldBounds(GameObject fox)
    {
        bool has = false;
        Bounds b = new Bounds(fox.transform.position, Vector3.zero);
        foreach (var r in fox.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has) b = new Bounds(fox.transform.position + Vector3.up * 0.5f, Vector3.one);
        return b;
    }

    /// <summary>모델 중심에서 키의 frac 비율만큼 위로 올린 월드 좌표.</summary>
    private static Vector3 WorldOffsetPos(Bounds b, float frac, float foxHeight)
        => new Vector3(b.center.x, b.center.y + frac * foxHeight, b.center.z);

    /// <summary>모델 정수리 위 frac*키 만큼 띄운 월드 좌표(상태 UI용).</summary>
    private static Vector3 WorldTopPos(Bounds b, float frac, float foxHeight)
        => new Vector3(b.center.x, b.max.y + frac * foxHeight, b.center.z);

    private static void AddDetectionCollider(GameObject fox, Bounds bounds, float ps)
    {
        var col = Undo.AddComponent<CapsuleCollider>(fox);
        col.direction = 1; // Y축
        col.center = fox.transform.InverseTransformPoint(bounds.center);
        col.height = bounds.size.y / ps;
        col.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f / ps;
    }

    // ============================================================
    // 데이터 / 머티리얼 에셋
    // ============================================================

    private static InteractableDataSO CreateOrLoadInteractableData()
    {
        var existing = AssetDatabase.LoadAssetAtPath<InteractableDataSO>(DataAssetPath);
        if (existing != null) return existing;

        var so = ScriptableObject.CreateInstance<InteractableDataSO>();
        so.objectName = "ANIMAL / FOX";
        so.descriptionText = "낯선 기척에 몸을 웅크린 여우";
        so.missionGuideText = "숨의 호흡 능력은 동료의 집중된 마음을 알아볼 수 있습니다. 깊게 호흡하세요";
        so.requiresBreathing = true;
        so.targetBreathCount = 3;

        AssetDatabase.CreateAsset(so, DataAssetPath);
        AssetDatabase.SaveAssets();
        return so;
    }

    private static Material CreateOrLoadMembraneMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MembraneMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("SOOM/AnxietyMembrane");
        if (shader == null)
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] 'SOOM/AnxietyMembrane' 셰이더를 찾지 못했습니다(아직 임포트되지 않았을 수 있음). " +
                "URP 기본 Lit 셰이더로 대체합니다 — 한 번 더 임포트된 뒤 다시 실행하면 정상 셰이더로 만들어집니다.");
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null) return null;

        var mat = new Material(shader) { name = "Mat_AnxietyMembrane" };
        if (mat.HasProperty("_Alpha")) mat.SetFloat("_Alpha", 1f);

        AssetDatabase.CreateAsset(mat, MembraneMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// <summary>프로스티드 글래스 UI 머티리얼을 만들거나 로드한다. 셰이더가 없으면 null(반투명색 폴백).</summary>
    private static Material CreateOrLoadGlassMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("SOOM/UIFrostedGlass");
        if (shader == null)
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] 'SOOM/UIFrostedGlass' 셰이더를 찾지 못했습니다(아직 임포트 전일 수 있음). " +
                "이번엔 배경 블러 없이 반투명 유리색으로 진행합니다 — 임포트 후 다시 Rebuild하면 진짜 프로스티드 글래스가 적용됩니다.");
            return null;
        }

        var mat = new Material(shader) { name = "Mat_UIFrostedGlass" };
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.92f, 0.94f, 0.98f, 0.42f));
        if (mat.HasProperty("_BlurRadius")) mat.SetFloat("_BlurRadius", 0.014f);
        if (mat.HasProperty("_Brightness")) mat.SetFloat("_Brightness", 1.08f);
        if (mat.HasProperty("_Saturation")) mat.SetFloat("_Saturation", 0.9f);

        AssetDatabase.CreateAsset(mat, GlassMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void EnsureFolder(string parentFolder, string newFolderName)
    {
        string fullPath = parentFolder + "/" + newFolderName;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }
    }

    // ============================================================
    // EventSystem (월드 스페이스 UI 버튼을 XR 레이로 클릭 가능하게)
    // ============================================================

    private static void EnsureEventSystem()
    {
        var existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing == null)
        {
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            existing = go.AddComponent<EventSystem>();
            go.AddComponent<XRUIInputModule>();
            Debug.Log("[FoxEncounterBuilder] 씬에 EventSystem이 없어 XRUIInputModule과 함께 새로 생성했습니다.");
        }
        else if (existing.GetComponent<BaseInputModule>() == null)
        {
            Undo.AddComponent<XRUIInputModule>(existing.gameObject);
            Debug.Log("[FoxEncounterBuilder] 기존 EventSystem에 XRUIInputModule을 추가했습니다.");
        }
    }

    // ============================================================
    // 여우 루트 플레이스홀더 (실제 모델이 없을 때만)
    // ============================================================

    private static GameObject BuildFoxRoot()
    {
        var fox = new GameObject(RealFoxObjectName);
        Undo.RegisterCreatedObjectUndo(fox, "Create Fox_Encounter");
        fox.transform.position = new Vector3(5f, 0f, 5f);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            fox.layer = interactableLayer;
        }
        else
        {
            Debug.LogWarning("[FoxEncounterBuilder] 'Interactable' 레이어를 찾지 못했습니다 (TagManager 설정 확인 필요).");
        }

        var collider = fox.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.35f, 0f);
        collider.radius = 0.3f;
        collider.height = 0.7f;

        BuildFoxVisual(fox.transform);

        return fox;
    }

    private static void BuildFoxVisual(Transform parent)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Visual_Body";
        Undo.RegisterCreatedObjectUndo(body, "Create Fox Visual Body");
        body.transform.SetParent(parent, false);
        body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        body.transform.localScale = new Vector3(0.4f, 0.35f, 0.45f);
        StripCollider(body);
        ApplyPlaceholderColor(body, new Color(0.85f, 0.42f, 0.13f));

        var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Visual_Head";
        Undo.RegisterCreatedObjectUndo(head, "Create Fox Visual Head");
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 0.62f, 0.32f);
        head.transform.localScale = new Vector3(0.22f, 0.22f, 0.28f);
        StripCollider(head);
        ApplyPlaceholderColor(head, new Color(0.95f, 0.55f, 0.2f));
    }

    private static void StripCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    private static void ApplyPlaceholderColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        var mat = new Material(shader) { name = "Mat_FoxPlaceholder_Runtime" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        renderer.sharedMaterial = mat;
    }

    // ============================================================
    // 불안의 막 VFX 구체 (bounds로 모델을 감싸도록 크기 자동 설정)
    // ============================================================

    private static (GameObject go, Renderer renderer) BuildMembrane(
        Transform parent, Material material, Bounds bounds, float ps)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "AnxietyMembrane";
        Undo.RegisterCreatedObjectUndo(sphere, "Create AnxietyMembrane");
        sphere.transform.SetParent(parent, false);

        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 1.12f;
        sphere.transform.localScale = Vector3.one * (size / ps);
        sphere.transform.position = bounds.center; // 월드 중심에 맞춤(스케일 부모 아래에서도 정확)

        StripCollider(sphere); // 순수 VFX — 물리/상호작용에 영향을 주지 않는다.

        var renderer = sphere.GetComponent<Renderer>();
        if (material != null) renderer.sharedMaterial = material;

        sphere.SetActive(false); // 명세 5.4 이전에는 숨김. Revealed 단계에서 컨트롤러가 켠다.
        return (sphere, renderer);
    }

    // ============================================================
    // 동료 추종 (NavMeshAgent + 폴백 + 걷기/정지 애니메이션)
    // ============================================================

    private static FoxCompanionFollower BuildCompanionFollower(GameObject fox, Animator animator, Bounds bounds, float ps)
    {
        var agent = fox.GetComponent<NavMeshAgent>();
        if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(fox);
        agent.radius = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
        agent.height = Mathf.Max(bounds.size.y, 0.3f);
        agent.baseOffset = 0f;
        agent.speed = 2.2f;
        agent.stoppingDistance = 1.2f;
        // NavMesh가 구워져 있지 않으면 isOnNavMesh가 항상 false가 되어
        // FoxCompanionFollower가 자동으로 단순 Transform 추종으로 폴백한다.
        agent.enabled = false;

        var follower = Undo.AddComponent<FoxCompanionFollower>(fox);
        var so = new SerializedObject(follower);
        so.FindProperty("agent").objectReferenceValue = agent;
        so.FindProperty("foxAnimator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();

        return follower;
    }

    // ============================================================
    // 월드 스페이스 UI 빌드 헬퍼 (월드 좌표 + 부모 스케일 보정)
    // ============================================================

    private static Canvas CreateWorldSpaceCanvas(
        string name, Transform parent, Vector3 worldPos, Vector2 sizeDelta, float worldScale, float ps, bool interactive)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        // 부모가 ×ps로 스케일돼 있어도 최종 월드 스케일이 worldScale이 되도록 보정.
        go.transform.localScale = Vector3.one * (worldScale / ps);
        go.transform.position = worldPos; // 월드 좌표 지정 → Unity가 부모 기준 로컬 좌표를 역산
        go.transform.localRotation = Quaternion.identity;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = sizeDelta;

        if (interactive)
        {
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (Camera.main != null) canvas.worldCamera = Camera.main;
        }

        // 플레이어가 어느 방향에 있든 항상 읽을 수 있도록 카메라를 바라보게 한다 (명세 5.2).
        go.AddComponent<FaceCamera>();

        return canvas;
    }

    private static TMP_Text CreateTMPText(
        string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta,
        float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = fontSize;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Image CreateUIImage(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Sprite _uiSprite;

    /// <summary>Unity 내장 둥근 모서리 UI 스프라이트(9-slice).</summary>
    private static Sprite UISprite()
    {
        if (_uiSprite == null)
            _uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        return _uiSprite;
    }

    private static Image CreateRoundedImage(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Color color)
    {
        var image = CreateUIImage(name, parent, anchoredPos, size, color);
        var sprite = UISprite();
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1.2f; // visionOS 느낌의 큰 라운딩
        }
        return image;
    }

    /// <summary>액센트 테두리 + 어두운 채움의 둥근 카드. 콘텐츠를 얹을 안쪽(Fill) RectTransform을 반환한다.</summary>
    private static RectTransform CreateCard(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var border = CreateRoundedImage(name, parent, anchoredPos, size, PanelBorder);
        var fill = CreateRoundedImage("Fill", border.transform, Vector2.zero, new Vector2(size.x - 4f, size.y - 4f), PanelFill);
        // 프로스티드 글래스 머티리얼이 있으면 배경 블러 유리로, 없으면 반투명색 그대로 폴백.
        if (_glassMat != null)
        {
            fill.material = _glassMat;
            fill.color = Color.white; // 셰이더가 _TintColor로 유리색을 결정 → 이미지 색은 흰색(알파 1)
        }
        return fill.rectTransform;
    }

    /// <summary>절차적 아이콘(GlassUIKit) Image + 런타임 바인더(GlassIcon)를 만든다. Play에서 스프라이트가 채워진다.</summary>
    private static Image CreateIcon(string name, Transform parent, Vector2 anchoredPos, float size, GlassIcon.Kind kind, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = true;

        var binder = go.AddComponent<GlassIcon>();
        binder.SetKind(kind); // 에디터 즉시 표시(런타임엔 Awake가 재설정)
        EditorUtility.SetDirty(binder);
        return img;
    }

    // ============================================================
    // 상태 UI (여우 머리 위, 명세 5.2/5.4/5.6)
    // ============================================================

    private static (GameObject root, TMP_Text text) BuildStatusUI(
        Transform parent, Vector3 worldPos, float worldScale, float ps)
    {
        var canvas = CreateWorldSpaceCanvas(
            "UI_Status", parent, worldPos, new Vector2(440f, 96f), worldScale, ps, interactive: false);

        // 유리 알약(nameplate) + 발자국 아이콘.
        var pill = CreateCard("Pill", canvas.transform, Vector2.zero, new Vector2(440f, 96f));
        CreateIcon("Icon", pill, new Vector2(-166f, 0f), 42f, GlassIcon.Kind.Paw, GlassUIKit.Amber);

        var text = CreateTMPText(
            "StatusText", pill, new Vector2(28f, 0f), new Vector2(356f, 80f), 38f, TextAlignmentOptions.Center, TextMain);
        text.fontStyle = FontStyles.Bold;

        canvas.gameObject.SetActive(false); // Wary 진입 전까지는 숨김
        return (canvas.gameObject, text);
    }

    // ============================================================
    // 상호작용 UI (감지 프롬프트 + 안내 지시문 + 액션 버튼)
    // ============================================================

    private class FoxInteractionUIRefs
    {
        public GameObject Root;
        public GameObject PromptPanelRoot;
        public TMP_Text PromptNameText;
        public TMP_Text PromptDescText;
        public GameObject InstructionPanelRoot;
        public TMP_Text InstructionText;
        public GameObject ActionButtonRoot;
        public Button ActionButton;
        public TMP_Text ActionButtonLabel;
        public Image PhaseIcon;
    }

    private static FoxInteractionUIRefs BuildInteractionUI(
        Transform parent, Vector3 worldPos, float worldScale, float ps)
    {
        var canvas = CreateWorldSpaceCanvas(
            "UI_Interaction", parent, worldPos, new Vector2(480f, 300f), worldScale, ps, interactive: true);
        var root = canvas.gameObject;

        // --- 감지 프롬프트 카드 (명세 5.1: 발자국 아이콘 + "ANIMAL / FOX") ---
        var promptFill = CreateCard("PromptPanel", root.transform, Vector2.zero, new Vector2(480f, 300f));
        var promptPanel = promptFill.parent.gameObject;
        CreateIcon("Icon", promptFill, new Vector2(0f, 92f), 58f, GlassIcon.Kind.Paw, GlassUIKit.Amber);
        var nameText = CreateTMPText(
            "NameText", promptFill, new Vector2(0f, 22f), new Vector2(440f, 70f),
            44f, TextAlignmentOptions.Center, TextMain);
        nameText.fontStyle = FontStyles.Bold;
        var descText = CreateTMPText(
            "DescText", promptFill, new Vector2(0f, -54f), new Vector2(440f, 110f),
            24f, TextAlignmentOptions.Center, TextMuted);
        promptPanel.SetActive(false);

        // --- 지시문 + 액션 버튼 카드 (명세 5.2~5.6, 단계별 아이콘/라벨 교체) ---
        var instrFill = CreateCard("InstructionPanel", root.transform, Vector2.zero, new Vector2(480f, 300f));
        var instructionPanel = instrFill.parent.gameObject;

        // 상단 히어로 아이콘 — 컨트롤러가 단계별로 호흡/체크/하트로 교체.
        var phaseIcon = CreateIcon("PhaseIcon", instrFill, new Vector2(0f, 94f), 60f, GlassIcon.Kind.Breath, GlassUIKit.Accent);

        var instructionText = CreateTMPText(
            "InstructionText", instrFill, new Vector2(0f, 14f), new Vector2(440f, 120f),
            25f, TextAlignmentOptions.Center, TextMain);

        // 3점 진행 표시(장식)
        for (int i = 0; i < 3; i++)
        {
            Color dc = i == 0 ? GlassUIKit.Accent : new Color(0.45f, 0.5f, 0.58f, 0.5f);
            CreateRoundedImage($"Dot{i}", instrFill, new Vector2(-22f + i * 22f, -64f), new Vector2(11f, 11f), dc);
        }

        // 아이콘(셰브런) + 짧은 라벨의 둥근 알약 버튼
        var btnBorder = CreateRoundedImage("ActionButton", instrFill, new Vector2(0f, -114f), new Vector2(300f, 74f), ButtonBorder);
        var btnFill = CreateRoundedImage("Fill", btnBorder.transform, Vector2.zero, new Vector2(292f, 66f), ButtonFill);
        var button = btnBorder.gameObject.AddComponent<Button>();
        button.targetGraphic = btnFill;
        var cb = button.colors;
        cb.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        button.colors = cb;

        CreateIcon("Icon", btnFill.transform, new Vector2(-96f, 0f), 26f, GlassIcon.Kind.Chevron, ButtonText);
        var buttonLabel = CreateTMPText(
            "Label", btnFill.transform, new Vector2(16f, 0f), new Vector2(210f, 56f),
            26f, TextAlignmentOptions.Center, ButtonText);
        buttonLabel.fontStyle = FontStyles.Bold;

        instructionPanel.SetActive(false);

        return new FoxInteractionUIRefs
        {
            Root = root,
            PromptPanelRoot = promptPanel,
            PromptNameText = nameText,
            PromptDescText = descText,
            InstructionPanelRoot = instructionPanel,
            InstructionText = instructionText,
            ActionButtonRoot = btnBorder.gameObject,
            ActionButton = button,
            ActionButtonLabel = buttonLabel,
            PhaseIcon = phaseIcon,
        };
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ============================================================
    // 공용 호흡 UI (BreathCircleUI 재사용 — 명세 5.3/5.5)
    // ============================================================

    private static BreathCircleUI BuildBreathCircleUI(
        Transform parent, BreathEventsSO events, Vector3 worldPos, float worldScale, float ps)
    {
        var canvas = CreateWorldSpaceCanvas(
            "UI_BreathCircle", parent, worldPos, new Vector2(300f, 300f), worldScale, ps, interactive: false);
        var root = canvas.gameObject;

        var group = root.AddComponent<CanvasGroup>();

        var large = CreateUIImage("LargeCircleOutline", root.transform, Vector2.zero, new Vector2(180f, 180f), Color.white);
        var small = CreateUIImage("SmallCircleOutline", root.transform, Vector2.zero, new Vector2(130f, 130f), Color.white);
        var bead = CreateUIImage("Bead", root.transform, Vector2.zero, new Vector2(70f, 70f), Color.white);

        var slotsRoot = new GameObject("Slots", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(slotsRoot, "Create Slots");
        slotsRoot.transform.SetParent(root.transform, false);
        var slotsRt = slotsRoot.GetComponent<RectTransform>();
        slotsRt.anchorMin = slotsRt.anchorMax = new Vector2(0.5f, 0.5f);
        slotsRt.anchoredPosition = new Vector2(0f, 110f);

        const int slotCount = 3; // BreathManager.targetLoopCount 기본값(3회 성공)과 동일
        var slotRings = new Image[slotCount];
        var slotFills = new Image[slotCount];
        const float spacing = 55f;
        float startX = -spacing * (slotCount - 1) / 2f;

        for (int i = 0; i < slotCount; i++)
        {
            var slotGo = new GameObject($"Slot{i}", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(slotGo, "Create Slot");
            slotGo.transform.SetParent(slotsRoot.transform, false);
            var slotRt = slotGo.GetComponent<RectTransform>();
            slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            slotRt.sizeDelta = new Vector2(40f, 40f);

            slotRings[i] = CreateUIImage("Ring", slotGo.transform, Vector2.zero, new Vector2(40f, 40f), Color.white);
            slotFills[i] = CreateUIImage("Fill", slotGo.transform, Vector2.zero, new Vector2(40f, 40f), Color.white);
            slotFills[i].enabled = false;
        }

        var breathUI = root.AddComponent<BreathCircleUI>();
        var so = new SerializedObject(breathUI);
        so.FindProperty("events").objectReferenceValue = events;
        so.FindProperty("largeCircleOutline").objectReferenceValue = large;
        so.FindProperty("smallCircleOutline").objectReferenceValue = small;
        so.FindProperty("bead").objectReferenceValue = bead;
        so.FindProperty("group").objectReferenceValue = group;

        var slotRingsProp = so.FindProperty("slotRings");
        slotRingsProp.arraySize = slotCount;
        for (int i = 0; i < slotCount; i++)
            slotRingsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotRings[i];

        var slotFillsProp = so.FindProperty("slotFills");
        slotFillsProp.arraySize = slotCount;
        for (int i = 0; i < slotCount; i++)
            slotFillsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotFills[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false); // 호흡 시퀀스가 시작될 때만 컨트롤러가 Show()
        return breathUI;
    }

    // ============================================================
    // 컴포넌트 배선
    // ============================================================

    private static void WireFoxInteractable(
        FoxInteractable foxInteractable, InteractableDataSO data,
        FoxInteractionUIRefs interactionUI, FoxEncounterController controller)
    {
        var so = new SerializedObject(foxInteractable);
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("promptPanelRoot").objectReferenceValue = interactionUI.PromptPanelRoot;
        so.FindProperty("promptNameText").objectReferenceValue = interactionUI.PromptNameText;
        so.FindProperty("promptDescText").objectReferenceValue = interactionUI.PromptDescText;
        so.FindProperty("encounterController").objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireFoxEncounterController(
        FoxEncounterController controller, InteractableDataSO data, BreathEventsSO breathEvents,
        GameObject statusRoot, TMP_Text statusText, FoxInteractionUIRefs interactionUI,
        BreathCircleUI breathUI, GameObject membraneGo, Renderer membraneRenderer,
        Animator animator, FoxCompanionFollower companionFollower)
    {
        var so = new SerializedObject(controller);
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("breathEvents").objectReferenceValue = breathEvents;
        so.FindProperty("statusPanelRoot").objectReferenceValue = statusRoot;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("instructionPanelRoot").objectReferenceValue = interactionUI.InstructionPanelRoot;
        so.FindProperty("instructionText").objectReferenceValue = interactionUI.InstructionText;
        so.FindProperty("actionButtonRoot").objectReferenceValue = interactionUI.ActionButtonRoot;
        so.FindProperty("actionButton").objectReferenceValue = interactionUI.ActionButton;
        so.FindProperty("actionButtonLabel").objectReferenceValue = interactionUI.ActionButtonLabel;
        so.FindProperty("phaseIcon").objectReferenceValue = interactionUI.PhaseIcon;
        so.FindProperty("breathCircleUI").objectReferenceValue = breathUI;
        so.FindProperty("membraneObject").objectReferenceValue = membraneGo;
        so.FindProperty("membraneRenderer").objectReferenceValue = membraneRenderer;
        so.FindProperty("foxAnimator").objectReferenceValue = animator;
        so.FindProperty("companionFollower").objectReferenceValue = companionFollower;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 기존 공용 InteractionDetector(Systems 프리팹 인스턴스)의 Interactable 레이어 비트와
    /// 시야 기준(viewOrigin)이 비어있으면 채워 넣는다 — 채워져 있지 않으면 감지 자체가
    /// 전혀 동작하지 않는다(현재 프리팹 기본값은 레이어 마스크 0, viewOrigin 없음).
    /// 씬 인스턴스에 대한 오버라이드만 남기므로 다른 씬/원본 프리팹에는 영향이 없다.
    /// </summary>
    private static void ConfigureSharedInteractionDetector()
    {
        var detector = Object.FindFirstObjectByType<InteractionDetector>();
        if (detector == null)
        {
            Debug.LogWarning(
                "[FoxEncounterBuilder] 씬에서 InteractionDetector를 찾지 못했습니다. " +
                "Systems 프리팹이 씬에 배치되어 있는지 확인하세요 — 여우 감지가 동작하지 않습니다.");
            return;
        }

        var so = new SerializedObject(detector);

        var layerProp = so.FindProperty("interactableLayer");
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
        {
            layerProp.intValue |= 1 << interactableLayer;
        }
        else
        {
            Debug.LogWarning("[FoxEncounterBuilder] 'Interactable' 레이어를 찾지 못해 감지 레이어 마스크를 설정하지 못했습니다.");
        }

        var viewOriginProp = so.FindProperty("viewOrigin");
        if (viewOriginProp.objectReferenceValue == null)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cams.Length > 0) cam = cams[0];
            }
            if (cam != null)
            {
                viewOriginProp.objectReferenceValue = cam.transform;
            }
            else
            {
                Debug.LogWarning(
                    "[FoxEncounterBuilder] 씬에서 카메라를 찾지 못해 InteractionDetector.viewOrigin을 " +
                    "자동으로 채우지 못했습니다. XR 메인 카메라를 수동으로 연결하세요.");
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(detector);
    }
}

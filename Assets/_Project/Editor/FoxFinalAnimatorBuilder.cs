using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// fox_final.fbx 자체 애니메이션 클립으로 여우 애니메이터를 새로 구성한다.
///
/// fox_final 은 자기 스켈레톤 + 자기 클립(fox_rig|Action1_Alert 등)을 갖고 있으므로,
/// fox_sample_2 용으로 만든 기존 Fox_Encounter 컨트롤러(뼈대 경로가 달라 재생 안 됨) 대신,
/// fox_final 자기 클립을 물린 새 컨트롤러(Fox_Encounter_Final)를 만들어 씬 여우 Animator 에 할당한다.
///
/// 상태 이름은 기존과 동일하게 Wary / Joy / Walk 로 두어(FoxEncounterController 는 "Wary"/"Joy",
/// FoxCompanionFollower 는 "Walk"/"Joy" 를 이름으로 재생) 게임플레이 코드를 건드리지 않는다.
///
/// 명세 매핑:
///   Wary(경계, 5.2.2 Sit_Growl)  -> fox_rig|Action1_Alert
///   Joy (동료, 5.6.1 Stand_Joy)   -> fox_rig|Action4_Standing_Happy
///   Walk(걷기, 5.6.2)             -> fox_rig|Action5_Walking
///
/// 순서: "여우 모델 교체 (fox_final)" 로 모델을 바꾼 뒤 이 메뉴를 실행한다.
/// </summary>
public static class FoxFinalAnimatorBuilder
{
    private const string FoxFinalPath = "Assets/_Project/Prefabs/MyMesh/fox/fox_final/fox_final.fbx";
    private const string OutputControllerPath = "Assets/_Project/Prefabs/MyMesh/fox/animcontroller/Fox_Encounter_Final.controller";

    [MenuItem("Tools/여우 애니메이터 재구성 (fox_final 자체 클립)")]
    public static void RebuildFoxFinalAnimator()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("여우 애니메이터 재구성", "플레이 모드에서는 실행할 수 없습니다. 정지 후 다시 시도하세요.", "확인");
            return;
        }

        // 0) 클립들이 정지하지 않고 계속 미세하게 움직이도록 Loop Time 켜기 (idle 흔들림 느낌)
        EnsureClipsLoop();

        // 1) fox_final.fbx 의 애니메이션 클립 로드
        var assets = AssetDatabase.LoadAllAssetsAtPath(FoxFinalPath);
        var clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
        if (clips.Count == 0)
        {
            EditorUtility.DisplayDialog("여우 애니메이터 재구성",
                "fox_final.fbx 에서 애니메이션 클립을 찾지 못했습니다.\n" +
                "Inspector > fox_final > Animation 탭에서 Import Animation 이 켜져 있는지 확인하세요.", "확인");
            return;
        }

        AnimationClip Find(string key) => clips.FirstOrDefault(c => c.name.Contains(key));
        var waryClip = Find("Action1_Alert");
        var joyClip = Find("Action4_Standing_Happy");
        var walkClip = Find("Action5_Walking");

        if (waryClip == null || joyClip == null || walkClip == null)
        {
            EditorUtility.DisplayDialog("여우 애니메이터 재구성",
                $"필요한 클립을 못 찾았습니다.\n" +
                $"Wary(Action1_Alert)={(waryClip != null ? "OK" : "없음")}\n" +
                $"Joy(Action4_Standing_Happy)={(joyClip != null ? "OK" : "없음")}\n" +
                $"Walk(Action5_Walking)={(walkClip != null ? "OK" : "없음")}\n\n" +
                "로드된 클립: " + string.Join(", ", clips.Select(c => c.name)), "확인");
            return;
        }

        // 2) 새 애니메이터 컨트롤러 생성 (Wary=기본, Joy, Walk)
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(OutputControllerPath);
        var sm = ctrl.layers[0].stateMachine;

        var wary = sm.AddState("Wary");
        wary.motion = waryClip;
        wary.writeDefaultValues = true;

        var joy = sm.AddState("Joy");
        joy.motion = joyClip;
        joy.writeDefaultValues = true;

        var walk = sm.AddState("Walk");
        walk.motion = walkClip;
        walk.writeDefaultValues = true;

        sm.defaultState = wary; // 조우 시작 시 경계 자세
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        // 3) 씬 여우 Animator 에 새 컨트롤러 + fox_final 아바타 할당
        var foxCtrl = Object.FindFirstObjectByType<FoxEncounterController>(FindObjectsInactive.Include);
        if (foxCtrl == null)
        {
            EditorUtility.DisplayDialog("여우 애니메이터 재구성",
                "컨트롤러 자산은 생성됐지만 씬에서 여우(FoxEncounterController)를 못 찾아 할당은 못 했습니다.\n" +
                "여우 Animator 에 Fox_Encounter_Final 를 수동 할당하세요:\n" + OutputControllerPath, "확인");
            return;
        }

        var anim = foxCtrl.GetComponent<Animator>();
        if (anim == null) anim = Undo.AddComponent<Animator>(foxCtrl.gameObject);
        Undo.RecordObject(anim, "Assign Fox_Final Animator");
        anim.runtimeAnimatorController = ctrl;

        // fox_final 자체 아바타가 있으면 할당 (Generic 클립이 자기 스켈레톤에 확실히 바인딩되도록)
        var avatar = assets.OfType<Avatar>().FirstOrDefault();
        if (avatar != null) anim.avatar = avatar;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EditorUtility.SetDirty(anim);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(foxCtrl.gameObject.scene);
        Selection.activeGameObject = foxCtrl.gameObject;

        Debug.Log($"[FoxFinalAnim] 완료: {OutputControllerPath} 생성 및 할당.\n" +
                  $"  Wary={waryClip.name}, Joy={joyClip.name}, Walk={walkClip.name}, Avatar={(avatar != null ? avatar.name : "None(경로매칭)")}\n" +
                  "저장 후 플레이하면 여우가 경계(앉아 그르렁) 자세로 시작합니다.");
    }

    /// <summary>fox_final 의 모든 클립에 Loop Time 을 켜서 상태가 정지하지 않고 계속 미세 재생되게 한다.</summary>
    private static void EnsureClipsLoop()
    {
        var importer = AssetImporter.GetAtPath(FoxFinalPath) as ModelImporter;
        if (importer == null) return;

        var defs = importer.clipAnimations;
        if (defs == null || defs.Length == 0) defs = importer.defaultClipAnimations; // [] 이면 자동 생성 목록에서 시작

        bool changed = false;
        for (int i = 0; i < defs.Length; i++)
        {
            if (!defs[i].loopTime) { defs[i].loopTime = true; changed = true; }
        }
        if (changed)
        {
            importer.clipAnimations = defs;
            importer.SaveAndReimport();
        }
    }
}

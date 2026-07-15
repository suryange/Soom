using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 여우와의 조우(명세 5장)에서 사용하는 여우 모델을 fox_sample_2 -> fox_final 로 교체하는 에디터 유틸.
///
/// 씬의 여우는 fox_sample_2.fbx 프리팹 인스턴스 위에 게임플레이 컴포넌트(FoxEncounterController /
/// FoxInteractable / FoxCompanionFollower / NavMeshAgent / CapsuleCollider / Animator)와 UI 자식
/// (상태/지시문/호흡/불안의 막 패널)이 얹혀 있는 구조다. 이 메뉴는:
///   1. fox_final.fbx 를 같은 위치/부모로 새로 인스턴스화
///   2. UI 자식(= 소스 프리팹에 없는 추가 자식)을 새 여우로 이동
///   3. 콜라이더/에이전트 -> 스크립트 순으로 컴포넌트를 복사(직렬화 참조 보존)
///   4. Animator 는 fox_final 자체 아바타를 유지한 채 Fox_Encounter 컨트롤러만 이식
///      (뼈대 경로가 fox_sample_2 와 같으면 기존 Wary/Joy/Walk 클립이 그대로 재생됨 — 재사용 시도)
///   5. 내부 상호참조(foxAnimator/companionFollower/agent/encounterController)를 새 컴포넌트로 교정
///   6. 옛 여우 삭제
///
/// 외부(다른 씬 오브젝트)에서 여우를 직렬화 참조하는 곳은 없음(감지는 런타임 콜라이더로 처리)이 확인되어,
/// 이 교체는 자기완결적이다. Undo 로 한 번에 되돌릴 수 있다.
/// </summary>
public static class FoxModelSwapper
{
    private const string FoxFinalPath = "Assets/_Project/Prefabs/MyMesh/fox/fox_final/fox_final.fbx";

    [MenuItem("Tools/여우 모델 교체 (fox_final)")]
    public static void SwapFoxModel()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("여우 모델 교체", "플레이 모드에서는 실행할 수 없습니다. 정지 후 다시 시도하세요.", "확인");
            return;
        }

        var oldController = Object.FindFirstObjectByType<FoxEncounterController>(FindObjectsInactive.Include);
        if (oldController == null)
        {
            EditorUtility.DisplayDialog("여우 모델 교체", "현재 씬에서 FoxEncounterController를 찾지 못했습니다.\n여우가 있는 씬을 연 뒤 다시 실행하세요.", "확인");
            return;
        }
        GameObject oldFox = oldController.gameObject;

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FoxFinalPath);
        if (fbx == null)
        {
            EditorUtility.DisplayDialog("여우 모델 교체", "fox_final.fbx 로드 실패:\n" + FoxFinalPath, "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        // 1) 새 여우 인스턴스 (fox_final) — 옛 여우와 동일한 부모/트랜스폼/이름/레이어/태그
        GameObject newFox = (GameObject)PrefabUtility.InstantiatePrefab(fbx, oldFox.scene);
        Undo.RegisterCreatedObjectUndo(newFox, "Swap Fox Model");

        Transform oldT = oldFox.transform;
        newFox.transform.SetParent(oldT.parent, false);
        newFox.transform.localPosition = oldT.localPosition;
        newFox.transform.localRotation = oldT.localRotation;
        newFox.transform.localScale = oldT.localScale;
        newFox.name = oldFox.name;
        newFox.layer = oldFox.layer;
        newFox.tag = oldFox.tag;
        int siblingIndex = oldT.GetSiblingIndex();

        // 2) 추가된 자식(UI 패널 등 = 소스 프리팹에 없는 자식)을 새 여우로 이동.
        //    world position stays 로 옮기므로 화면상 위치는 그대로 유지된다.
        var childrenToMove = new List<Transform>();
        foreach (Transform child in oldT)
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == null)
                childrenToMove.Add(child);
        }
        foreach (var child in childrenToMove)
            Undo.SetTransformParent(child, newFox.transform, "Move Fox Child");

        // 3) Animator: 새 모델(fox_final) 자체 Animator 를 유지하고 Fox_Encounter 컨트롤러만 이식.
        var oldAnim = oldFox.GetComponent<Animator>();
        var newAnim = newFox.GetComponent<Animator>();
        if (newAnim == null) newAnim = Undo.AddComponent<Animator>(newFox);
        if (oldAnim != null)
        {
            newAnim.runtimeAnimatorController = oldAnim.runtimeAnimatorController;
            newAnim.applyRootMotion = oldAnim.applyRootMotion;
            newAnim.updateMode = oldAnim.updateMode;
            newAnim.cullingMode = oldAnim.cullingMode;
        }

        // 4) 나머지 컴포넌트 복사. 콜라이더/에이전트를 먼저 → 스크립트 순서로 붙여
        //    FoxInteractable 의 [RequireComponent(Collider)] 가 엉뚱한 콜라이더를 자동 추가하지 않게 한다.
        var nonBehaviours = new List<Component>();
        var behaviours = new List<Component>();
        foreach (var comp in oldFox.GetComponents<Component>())
        {
            if (comp is Transform || comp is Animator) continue;
            if (comp is MonoBehaviour) behaviours.Add(comp);
            else nonBehaviours.Add(comp);
        }
        foreach (var comp in nonBehaviours) CopyToNewFox(comp, newFox);
        foreach (var comp in behaviours) CopyToNewFox(comp, newFox);

        // 5) 내부 상호참조를 새 컴포넌트로 교정 (복사본은 옛 컴포넌트를 가리키므로)
        var newController = newFox.GetComponent<FoxEncounterController>();
        var newFollower = newFox.GetComponent<FoxCompanionFollower>();
        var newAgent = newFox.GetComponent<NavMeshAgent>();
        var newInteractable = newFox.GetComponent<FoxInteractable>();

        SetObjectRef(newController, "foxAnimator", newAnim);
        SetObjectRef(newController, "companionFollower", newFollower);
        SetObjectRef(newFollower, "foxAnimator", newAnim);
        SetObjectRef(newFollower, "agent", newAgent);
        SetObjectRef(newInteractable, "encounterController", newController);

        // 6) 형제 순서 맞추고 옛 여우 삭제
        newFox.transform.SetSiblingIndex(siblingIndex);
        Undo.DestroyObjectImmediate(oldFox);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(newFox.scene);
        Selection.activeGameObject = newFox;

        string ctrlName = newAnim.runtimeAnimatorController != null ? newAnim.runtimeAnimatorController.name : "(없음)";
        Debug.Log($"[FoxSwap] fox_final 로 교체 완료. Animator 컨트롤러='{ctrlName}', 이동한 UI 자식={childrenToMove.Count}개. " +
                  $"애니메이션이 안 나오면 fox_final 뼈대 경로가 fox_sample_2 와 달라서이니 알려주세요.");
    }

    private static void CopyToNewFox(Component source, GameObject newFox)
    {
        if (source == null) return;
        ComponentUtility.CopyComponent(source);
        var existing = newFox.GetComponent(source.GetType());
        if (existing != null)
            ComponentUtility.PasteComponentValues(existing);
        else
            ComponentUtility.PasteComponentAsNew(newFox);
    }

    private static void SetObjectRef(Object target, string fieldName, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

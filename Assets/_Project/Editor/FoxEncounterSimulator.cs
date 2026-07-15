using UnityEditor;
using UnityEngine;

/// <summary>
/// 여우와의 조우(명세 5장)를 VR 없이 플레이 모드에서 버튼으로 단계별 진행/확인하는 시뮬레이터 창.
///
/// 실제 흐름은 감지(시야) → 호흡(컨트롤러 기울기) 입력에 의존하지만, 에디터에서 바로 보기 위해
/// FoxEncounterController 의 public 통지(NotifyDetected/NotifyInteractBegin)와 BreathEventsSO 의
/// 이벤트 발행(RaiseLoopCompleted/RaiseMissionSuccess)을 직접 호출해 각 단계를 강제로 넘긴다.
///
/// 사용: Tools > 여우 조우 시뮬레이터 → 플레이 진입 → "다음 단계" 버튼으로 진행.
/// </summary>
public class FoxEncounterSimulator : EditorWindow
{
    private const string BreathChannelPath = "Assets/_Project/Scripts/System/BreathEventsChannel.asset";

    private BreathEventsSO _breath;
    private int _membraneLoop;

    [MenuItem("Tools/여우 조우 시뮬레이터")]
    public static void Open()
    {
        var w = GetWindow<FoxEncounterSimulator>("여우 조우 시뮬");
        w.minSize = new Vector2(300, 360);
    }

    private void OnInspectorUpdate() => Repaint(); // 단계 표시 실시간 갱신

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("여우 조우 시뮬레이터", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 사용하세요. ▶ 를 눌러 실행한 뒤 아래 버튼으로 단계를 진행합니다.", MessageType.Info);
            return;
        }

        var fox = Object.FindFirstObjectByType<FoxEncounterController>(FindObjectsInactive.Include);
        if (fox == null)
        {
            EditorGUILayout.HelpBox("씬에서 FoxEncounterController 를 찾지 못했습니다.", MessageType.Warning);
            return;
        }

        if (_breath == null)
            _breath = AssetDatabase.LoadAssetAtPath<BreathEventsSO>(BreathChannelPath);

        var phase = fox.CurrentPhase;
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("현재 단계", phase.ToString(), EditorStyles.helpBox);
        EditorGUILayout.Space(6);

        // ---- 문맥에 맞는 "다음 단계" 큰 버튼 ----
        string nextLabel = NextStepLabel(phase);
        using (new EditorGUI.DisabledScope(nextLabel == null))
        {
            if (GUILayout.Button(nextLabel ?? "완료 — 동료가 되어 따라다닙니다", GUILayout.Height(38)))
                DoNextStep(fox, phase);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("개별 제어", EditorStyles.boldLabel);

        if (GUILayout.Button("감지/경계 진입 (NotifyDetected)"))
            fox.NotifyDetected();

        if (GUILayout.Button("상호작용 버튼 누르기 (NotifyInteractBegin)"))
            fox.NotifyInteractBegin();

        using (new EditorGUI.DisabledScope(_breath == null))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("호흡 이벤트 (BreathEventsChannel)", EditorStyles.miniBoldLabel);
            if (_breath == null)
            {
                EditorGUILayout.HelpBox("BreathEventsChannel.asset 을 찾지 못했습니다.", MessageType.Warning);
            }
            else
            {
                if (GUILayout.Button("호흡 1회 성공 발행 (RaiseLoopCompleted)"))
                {
                    _membraneLoop++;
                    _breath.RaiseLoopCompleted(_membraneLoop);
                }
                if (GUILayout.Button("호흡 미션 성공 발행 (RaiseMissionSuccess)"))
                {
                    _breath.RaiseMissionSuccess();
                    _membraneLoop = 0;
                }
                if (GUILayout.Button("세션 리셋 (ResetSession)"))
                {
                    _breath.ResetSession();
                    _membraneLoop = 0;
                }
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "권장 순서:\n" +
            "1) 감지/경계 진입 → 경계 자세 + '호흡 시작'\n" +
            "2) 상호작용 버튼 → 호흡(집중) 시작\n" +
            "3) 호흡 미션 성공 → 불안의 막 등장\n" +
            "4) 상호작용 버튼 → 막 제거 호흡\n" +
            "5) 호흡 1회 성공 ×3 (막이 옅어짐) → 호흡 미션 성공 → 막 제거\n" +
            "6) 상호작용 버튼 → 동료 되기(따라다님)", MessageType.None);
    }

    private static string NextStepLabel(FoxEncounterController.EncounterPhase phase)
    {
        switch (phase)
        {
            case FoxEncounterController.EncounterPhase.Detected: return "① 감지 → 경계 진입";
            case FoxEncounterController.EncounterPhase.Wary: return "② 호흡 시작";
            case FoxEncounterController.EncounterPhase.FocusBreath: return "③ 호흡 미션 성공 → 불안의 막";
            case FoxEncounterController.EncounterPhase.Revealed: return "④ 막 제거 호흡 시작";
            case FoxEncounterController.EncounterPhase.MembraneBreath: return "⑤ 막 제거 호흡 3회 + 성공";
            case FoxEncounterController.EncounterPhase.Cleared: return "⑥ 동료 되기 (따라다님)";
            case FoxEncounterController.EncounterPhase.Companion: return null;
            default: return null;
        }
    }

    private void DoNextStep(FoxEncounterController fox, FoxEncounterController.EncounterPhase phase)
    {
        switch (phase)
        {
            case FoxEncounterController.EncounterPhase.Detected:
                fox.NotifyDetected();
                break;
            case FoxEncounterController.EncounterPhase.Wary:
                fox.NotifyInteractBegin();
                break;
            case FoxEncounterController.EncounterPhase.FocusBreath:
                if (_breath != null) _breath.RaiseMissionSuccess();
                break;
            case FoxEncounterController.EncounterPhase.Revealed:
                fox.NotifyInteractBegin();
                break;
            case FoxEncounterController.EncounterPhase.MembraneBreath:
                if (_breath != null)
                {
                    // 막이 3단계로 옅어지는 걸 보여주고 마지막에 성공
                    _breath.RaiseLoopCompleted(1);
                    _breath.RaiseLoopCompleted(2);
                    _breath.RaiseLoopCompleted(3);
                    _breath.RaiseMissionSuccess();
                }
                _membraneLoop = 0;
                break;
            case FoxEncounterController.EncounterPhase.Cleared:
                fox.NotifyInteractBegin();
                break;
        }
    }
}

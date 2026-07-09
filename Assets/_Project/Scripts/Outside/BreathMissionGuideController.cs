using UnityEngine;

/// <summary>
/// 콘텐츠 B(길 찾기) 호흡 시퀀스의 UI 배선 담당.
/// PlayerStateManager가 BreathingActive 상태로 들어오고 나갈 때 BreathCircleUI를 표시/숨김 처리하고,
/// 안내 문구("숨의 호흡능력은 올바른 길을 찾아낼 수 있습니다...")를 MissionGuideTextUI로 출력한다.
/// 호흡 미션이 성공(OnMissionSuccess)하면 길잡이 등불이 출발했음을 알리는 "길라잡이" 문구를 띄운다.
/// PlayerStateManager 구독은 BreathManager와 동일하게 Start/OnDestroy에서 처리해
/// Awake 순서 경합(Instance가 아직 null인 상태로 OnEnable이 먼저 도는 경우)을 피한다.
/// </summary>
public class BreathMissionGuideController : MonoBehaviour
{
    [Header("Event Channel (SO)")]
    [SerializeField] private BreathEventsSO breathEvents;

    [Header("Breathing UI")]
    [SerializeField] private BreathCircleUI breathCircleUI;

    [Header("안내 문구 (3.4 콘텐츠 B)")]
    [TextArea]
    [SerializeField] private string breathingGuideMessage =
        "숨의 호흡능력은 올바른 길을 찾아낼 수 있습니다. 길을 밝히기 위해 숨을 깊게 들이마셔요";

    [TextArea]
    [SerializeField] private string guidingLightMessage = "길라잡이";

    private void Start()
    {
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter += HandleStateEnter;
            PlayerStateManager.Instance.OnStateExit += HandleStateExit;
        }
    }

    private void OnDestroy()
    {
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter -= HandleStateEnter;
            PlayerStateManager.Instance.OnStateExit -= HandleStateExit;
        }
    }

    private void OnEnable()
    {
        if (breathEvents != null) breathEvents.OnMissionSuccess += HandleMissionSuccess;
    }

    private void OnDisable()
    {
        if (breathEvents != null) breathEvents.OnMissionSuccess -= HandleMissionSuccess;
    }

    private void HandleStateEnter(PlayerState state)
    {
        if (state != PlayerState.BreathingActive) return;

        if (breathCircleUI != null) breathCircleUI.Show();
        MissionGuideTextUI.Instance?.ShowMessage(breathingGuideMessage);
    }

    private void HandleStateExit(PlayerState state)
    {
        if (state != PlayerState.BreathingActive) return;

        if (breathCircleUI != null) breathCircleUI.Hide();
    }

    private void HandleMissionSuccess()
    {
        MissionGuideTextUI.Instance?.ShowMessage(guidingLightMessage);
    }
}

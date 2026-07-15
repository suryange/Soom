using UnityEngine;

/// <summary>
/// 콘텐츠 B(길 찾기) 호흡 시퀀스의 UI 배선 담당.
/// PlayerStateManager가 BreathingActive 상태로 들어오고 나갈 때 BreathCircleUI를 표시/숨김 처리하고,
/// 안내 문구("숨의 호흡능력은 올바른 길을 찾아낼 수 있습니다...")를 MissionGuideTextUI로 출력한다.
/// 해당 단서가 실제로 길잡이 등불을 스폰하면 "길라잡이" 문구를 띄운다.
/// PlayerStateManager 구독은 BreathManager와 동일하게 Start/OnDestroy에서 처리해
/// Awake 순서 경합(Instance가 아직 null인 상태로 OnEnable이 먼저 도는 경우)을 피한다.
/// </summary>
public class BreathMissionGuideController : MonoBehaviour
{
    [Header("Guiding Mission Owner")]
    [SerializeField] private HologramMessage guidingMissionOwner;

    [Header("Breathing UI")]
    [SerializeField] private BreathCircleUI breathCircleUI;

    [Header("안내 문구 (3.4 콘텐츠 B)")]
    [TextArea]
    [SerializeField] private string breathingGuideMessage =
        "숨의 호흡능력은 올바른 길을 찾아낼 수 있습니다. 길을 밝히기 위해 숨을 깊게 들이마셔요";

    [TextArea]
    [SerializeField] private string guidingLightMessage = "길라잡이";

    private bool isOwnerSubscribed;
    private bool isStateSubscribed;
    private bool? lastRequestedBreathCircleVisibility;

    private void Start()
    {
        TrySubscribeStateManager();
        TrySubscribeOwner();
        SyncCurrentState();
    }

    private void OnEnable()
    {
        TrySubscribeStateManager();
        TrySubscribeOwner();
    }

    private void Update()
    {
        // PlayerStateManager가 이 오브젝트보다 늦게 생성되거나 교체되어도 다시 구독한다.
        // 상태 이벤트를 놓친 경우에도 현재 상태를 기준으로 UI 활성 상태를 복구한다.
        TrySubscribeStateManager();
        TrySubscribeOwner();
        SyncCurrentState();
    }

    private void OnDisable()
    {
        UnsubscribeStateManager();
        UnsubscribeOwner();
        SetBreathCircleVisible(false);
    }

    private void TrySubscribeStateManager()
    {
        if (isStateSubscribed || PlayerStateManager.Instance == null) return;

        PlayerStateManager.Instance.OnStateEnter += HandleStateEnter;
        PlayerStateManager.Instance.OnStateExit += HandleStateExit;
        isStateSubscribed = true;
    }

    private void UnsubscribeStateManager()
    {
        if (!isStateSubscribed) return;

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter -= HandleStateEnter;
            PlayerStateManager.Instance.OnStateExit -= HandleStateExit;
        }
        isStateSubscribed = false;
    }

    private void SyncCurrentState()
    {
        bool shouldShow = PlayerStateManager.Instance != null &&
                          PlayerStateManager.Instance.CurrentState == PlayerState.BreathingActive &&
                          PlayerStateManager.Instance.IsBreathMissionOwner(BreathMissionId.GuidingLight);
        SetBreathCircleVisible(shouldShow);
    }

    private void SetBreathCircleVisible(bool visible)
    {
        if (breathCircleUI == null)
        {
            if (visible && lastRequestedBreathCircleVisibility != true)
                Debug.LogError("[BreathMissionGuideController] BreathingActive이지만 BreathCircleUI 참조가 없습니다.", this);

            lastRequestedBreathCircleVisibility = visible;
            return;
        }

        bool hierarchyMatches = breathCircleUI.IsVisibilityRequested == visible &&
                                (!visible || breathCircleUI.gameObject.activeInHierarchy);

        if (lastRequestedBreathCircleVisibility == visible && hierarchyMatches)
            return;

        if (visible)
            breathCircleUI.Show();
        else
            breathCircleUI.Hide();

        lastRequestedBreathCircleVisibility = visible;
        Debug.Log($"[BreathMissionGuideController] Breath Circle {(visible ? "표시" : "숨김")}. " +
                  $"State={PlayerStateManager.Instance?.CurrentState}, Active={breathCircleUI.gameObject.activeInHierarchy}", this);
    }

    private void TrySubscribeOwner()
    {
        if (isOwnerSubscribed) return;

        if (guidingMissionOwner == null)
            guidingMissionOwner = FindFirstObjectByType<HologramMessage>(FindObjectsInactive.Include);
        if (guidingMissionOwner == null) return;

        guidingMissionOwner.OnGuidingLightSpawned += HandleGuidingLightSpawned;
        isOwnerSubscribed = true;
    }

    private void UnsubscribeOwner()
    {
        if (!isOwnerSubscribed) return;

        if (guidingMissionOwner != null)
            guidingMissionOwner.OnGuidingLightSpawned -= HandleGuidingLightSpawned;
        isOwnerSubscribed = false;
    }

    private void HandleStateEnter(PlayerState state)
    {
        if (state != PlayerState.BreathingActive) return;
        if (PlayerStateManager.Instance == null ||
            !PlayerStateManager.Instance.IsBreathMissionOwner(BreathMissionId.GuidingLight)) return;

        SetBreathCircleVisible(true);
        MissionGuideTextUI.Instance?.ShowMessage(breathingGuideMessage);
    }

    private void HandleStateExit(PlayerState state)
    {
        if (state != PlayerState.BreathingActive) return;

        SetBreathCircleVisible(false);
    }

    private void HandleGuidingLightSpawned(GuidingLightController spawnedLight)
    {
        MissionGuideTextUI.Instance?.ShowMessage(guidingLightMessage);
    }
}

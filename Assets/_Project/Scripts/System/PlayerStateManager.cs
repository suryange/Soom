using System;
using UnityEngine;

// 플레이어 상태 정의 
public enum PlayerState
{
    Idle,
    Move,
    Interact,
    MissionReady,
    BreathingActive
}

public enum BreathMissionId
{
    None,
    GuidingLight,
    Sandstorm
}

public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    [Header("현재 Player State")]
    [SerializeField] private PlayerState currentState = PlayerState.Idle;
    public PlayerState CurrentState => currentState;

    [Header("현재 호흡 미션 소유자")]
    [SerializeField] private BreathMissionId activeBreathMission = BreathMissionId.None;
    public BreathMissionId ActiveBreathMission => activeBreathMission;

    // 특정 상태에 진입했을 때 방송
    public event Action<PlayerState> OnStateEnter;

    // 특정 상태에서 나갈 때 방송
    public event Action<PlayerState> OnStateExit;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    // =========================================================
    // 상태 변경 (State Machine Transition)
    // =========================================================

    /// 상태 전이를 통제하는 핵심 게이트웨이.
    /// 내부의 구역 제어 함수를 통해 호출.
    public void ChangeState(PlayerState newState)
    {
        // 예외 처리
        if (currentState == newState) return;
        if (currentState == PlayerState.Interact && newState == PlayerState.Interact) return;
        if (currentState == PlayerState.BreathingActive && newState == PlayerState.Move)
        {
            Debug.LogWarning("호흡 미션 중에는 강제로 이동 상태로 변경할 수 없습니다.");
            return;
        }

        // 기존 상태 탈출 (Exit)
        Debug.Log($"[PlayerStateManager] Exit State : {currentState}");
        OnStateExit?.Invoke(currentState); // OnStateExit 방송

        // 상태 업데이트
        currentState = newState;

        // 새로운 상태 진입 (Enter)
        Debug.Log($"[PlayerStateManager] Enter State : {currentState}");
        OnStateEnter?.Invoke(currentState); // OnStateEnter 방송
    }

    // =========================================================
    // 외부 호출용 함수 
    // =========================================================

    /// 조이스틱 이동 제어
    public void SetMoving(bool isMoving)
    {
        if (isMoving && currentState == PlayerState.Idle)
        {
            ChangeState(PlayerState.Move);
        }
        else if (!isMoving && currentState == PlayerState.Move)
        {
            ChangeState(PlayerState.Idle);
        }
    }

    /// Layer 1 (XR Input) 계층에서 B버튼을 눌렀을 때 호출.
    public void OnInteractButtonPressed()
    {
        if (currentState == PlayerState.MissionReady)
        {
            // 미션 대기 중 -> 호흡 시작
            ChangeState(PlayerState.BreathingActive);
        }
        else if (currentState == PlayerState.BreathingActive)
        {
            // 호흡 중 -> 미션 취소 (Idle로 복귀)
            ChangeState(PlayerState.Idle);
        }
    }

    /// 미션 구역 Trigger에 들어오거나 나갈 때 호출.
    public void SetMissionZone(bool isInside)
    {
        if (isInside && (currentState == PlayerState.Idle || currentState == PlayerState.Move || currentState == PlayerState.Interact))
        {
            ChangeState(PlayerState.MissionReady);
        }
        else if (!isInside && currentState == PlayerState.MissionReady)
        {
            ChangeState(PlayerState.Idle);
        }
    }

    public bool TryAcquireBreathMission(BreathMissionId missionId)
    {
        if (missionId == BreathMissionId.None)
            return false;

        if (activeBreathMission != BreathMissionId.None && activeBreathMission != missionId)
        {
            Debug.LogWarning(
                $"[PlayerStateManager] {missionId} 미션이 소유권을 요청했지만 " +
                $"{activeBreathMission} 미션이 이미 활성 상태입니다.");
            return false;
        }

        activeBreathMission = missionId;
        return true;
    }

    public bool IsBreathMissionOwner(BreathMissionId missionId)
    {
        return missionId != BreathMissionId.None && activeBreathMission == missionId;
    }

    public void ReleaseBreathMission(BreathMissionId missionId)
    {
        if (missionId != BreathMissionId.None && activeBreathMission == missionId)
            activeBreathMission = BreathMissionId.None;
    }

    /// 호흡 3회 성공 시 BreathCore 등에서 이 함수를 호출.
    public void CompleteBreathingMission()
    {
        if (currentState == PlayerState.BreathingActive)
        {
            ChangeState(PlayerState.Idle);
        }
    }
}

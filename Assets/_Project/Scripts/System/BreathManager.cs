using UnityEngine;

/// <summary>
/// 현재 Scene 03 호흡 입력 Provider.
/// 실제 호흡/마이크 센서가 아니라 좌우 컨트롤러 중 더 크게 기울어진 쪽의 회전 변화량을 사용한다.
/// </summary>
public class BreathManager : MonoBehaviour
{
    [Header("Event Channels (SO)")]
    public BreathEventsSO breathEventsChannel;

    [Header("Tracking Target")]
    public Transform leftController;
    public Transform rightController;

    [Header("Settings")]
    public int targetLoopCount = 3;
    public float maxBreathAngle = 30f;

    [Header("Thresholds")]
    [Range(0f, 1f)] public float inhaleThreshold = 0.7f;
    [Range(0f, 1f)] public float exhaleThreshold = 0.3f;
    [SerializeField, Min(0f)] private float minimumPhaseDuration = 0.15f;

    [Header("Runtime Diagnostics (Read Only)")]
    [SerializeField] private bool isMeasuring;
    [SerializeField] private bool isStateSubscribed;
    [SerializeField] private int breathingStateEnterCount;
    [SerializeField] private int measurementSessionCount;
    [SerializeField] private int currentLoopCount;
    [SerializeField] private float leftAngleDelta;
    [SerializeField] private float rightAngleDelta;
    [SerializeField] private float activeInputAngle;
    [SerializeField] private float currentBreathValue;

    private Quaternion leftZeroRotation;
    private Quaternion rightZeroRotation;
    private float phaseChangedAt;
    private bool dependencyErrorReported;

    private enum BreathPhase
    {
        WaitingForInhale,
        WaitingForExhale
    }

    [SerializeField] private BreathPhase currentPhase = BreathPhase.WaitingForInhale;

    private void OnEnable()
    {
        TrySubscribeStateManager();
    }

    private void Start()
    {
        TrySubscribeStateManager();
        SyncCurrentState();
    }

    private void OnDisable()
    {
        UnsubscribeStateManager();
        StopMeasurement();
    }

    private void TrySubscribeStateManager()
    {
        if (isStateSubscribed || PlayerStateManager.Instance == null)
            return;

        PlayerStateManager.Instance.OnStateEnter += HandleStateEnter;
        PlayerStateManager.Instance.OnStateExit += HandleStateExit;
        isStateSubscribed = true;
    }

    private void UnsubscribeStateManager()
    {
        if (!isStateSubscribed)
            return;

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter -= HandleStateEnter;
            PlayerStateManager.Instance.OnStateExit -= HandleStateExit;
        }

        isStateSubscribed = false;
    }

    private void SyncCurrentState()
    {
        if (PlayerStateManager.Instance != null &&
            PlayerStateManager.Instance.CurrentState == PlayerState.BreathingActive &&
            !isMeasuring)
        {
            StartCalibrationAndMeasurement();
        }
    }

    private void HandleStateEnter(PlayerState state)
    {
        if (state != PlayerState.BreathingActive)
            return;

        breathingStateEnterCount++;
        StartCalibrationAndMeasurement();
    }

    private void HandleStateExit(PlayerState state)
    {
        if (state == PlayerState.BreathingActive)
            StopMeasurement();
    }

    private void StartCalibrationAndMeasurement()
    {
        if (isMeasuring)
        {
            Debug.LogWarning("[BreathManager] 중복 측정 시작 요청을 무시합니다.");
            return;
        }

        if (!ValidateAndResolveDependencies())
            return;

        leftZeroRotation = leftController.localRotation;
        rightZeroRotation = rightController.localRotation;

        currentLoopCount = 0;
        leftAngleDelta = 0f;
        rightAngleDelta = 0f;
        activeInputAngle = 0f;
        currentBreathValue = 0f;
        currentPhase = BreathPhase.WaitingForInhale;
        phaseChangedAt = Time.unscaledTime;
        dependencyErrorReported = false;
        isMeasuring = true;
        measurementSessionCount++;

        breathEventsChannel.RaiseBreathValue(0f);
        Debug.Log(
            $"[BreathManager] 측정 세션 #{measurementSessionCount} 시작. " +
            $"State={PlayerStateManager.Instance.CurrentState}, Active={isActiveAndEnabled}, " +
            $"Left={leftController.name}, Right={rightController.name}, " +
            $"Inhale={inhaleThreshold:0.00}({maxBreathAngle * inhaleThreshold:0.#}°), " +
            $"Exhale={exhaleThreshold:0.00}({maxBreathAngle * exhaleThreshold:0.#}°)");
    }

    private bool ValidateAndResolveDependencies()
    {
        if (leftController == null)
            leftController = FindSceneTransform("Left Controller");
        if (rightController == null)
            rightController = FindSceneTransform("Right Controller");

        if (breathEventsChannel == null)
        {
            Debug.LogError("[BreathManager] BreathEventsChannel 참조가 없어 측정을 시작할 수 없습니다.");
            return false;
        }

        if (leftController == null || rightController == null)
        {
            Debug.LogError(
                $"[BreathManager] Controller 참조가 없어 측정을 시작할 수 없습니다. " +
                $"Left={(leftController != null ? leftController.name : "NULL")}, " +
                $"Right={(rightController != null ? rightController.name : "NULL")}");
            return false;
        }

        if (maxBreathAngle <= 0f || exhaleThreshold >= inhaleThreshold)
        {
            Debug.LogError(
                $"[BreathManager] 임계값 설정이 올바르지 않습니다. " +
                $"MaxAngle={maxBreathAngle}, Inhale={inhaleThreshold}, Exhale={exhaleThreshold}");
            return false;
        }

        targetLoopCount = Mathf.Max(1, targetLoopCount);
        return true;
    }

    private void StopMeasurement()
    {
        bool wasMeasuring = isMeasuring;
        isMeasuring = false;
        currentLoopCount = 0;
        leftAngleDelta = 0f;
        rightAngleDelta = 0f;
        activeInputAngle = 0f;
        currentBreathValue = 0f;
        currentPhase = BreathPhase.WaitingForInhale;

        if (breathEventsChannel != null)
            breathEventsChannel.RaiseBreathValue(0f);

        if (wasMeasuring)
            Debug.Log("[BreathManager] 측정 세션 종료.");
    }

    private void Update()
    {
        if (!isStateSubscribed)
        {
            TrySubscribeStateManager();
            if (isStateSubscribed)
                SyncCurrentState();
        }

        if (!isMeasuring)
            return;

        if (leftController == null || rightController == null || breathEventsChannel == null)
        {
            if (!dependencyErrorReported)
            {
                dependencyErrorReported = true;
                Debug.LogError("[BreathManager] 측정 중 필수 참조가 사라져 세션을 중단합니다.");
            }
            StopMeasurement();
            return;
        }

        leftAngleDelta = Quaternion.Angle(leftZeroRotation, leftController.localRotation);
        rightAngleDelta = Quaternion.Angle(rightZeroRotation, rightController.localRotation);

        // 양손 평균은 한쪽만 조작할 때 두 배 각도를 요구했다. Simulator/실기기 모두
        // 어느 한 컨트롤러든 의도적으로 기울이면 입력할 수 있도록 큰 쪽을 사용한다.
        activeInputAngle = Mathf.Max(leftAngleDelta, rightAngleDelta);
        currentBreathValue = Mathf.Clamp01(activeInputAngle / maxBreathAngle);

        breathEventsChannel.RaiseBreathValue(currentBreathValue);
        CheckBreathLoop(currentBreathValue);
    }

    private void CheckBreathLoop(float normalizedValue)
    {
        if (Time.unscaledTime - phaseChangedAt < minimumPhaseDuration)
            return;

        if (currentPhase == BreathPhase.WaitingForInhale)
        {
            if (normalizedValue < inhaleThreshold)
                return;

            currentPhase = BreathPhase.WaitingForExhale;
            phaseChangedAt = Time.unscaledTime;
            Debug.Log(
                $"[BreathManager] 들숨 감지. Value={normalizedValue:0.00}, " +
                $"Left={leftAngleDelta:0.#}°, Right={rightAngleDelta:0.#}°");
            return;
        }

        if (normalizedValue > exhaleThreshold)
            return;

        currentPhase = BreathPhase.WaitingForInhale;
        phaseChangedAt = Time.unscaledTime;
        currentLoopCount++;

        Debug.Log($"[BreathManager] 호흡 {currentLoopCount}/{targetLoopCount}회 성공.");
        breathEventsChannel.RaiseLoopCompleted(currentLoopCount);

        if (currentLoopCount < targetLoopCount)
            return;

        isMeasuring = false;
        Debug.Log("[BreathManager] 목표 횟수 완료. MissionSuccess를 한 번 방송합니다.");
        breathEventsChannel.RaiseMissionSuccess();

        if (PlayerStateManager.Instance != null)
            PlayerStateManager.Instance.CompleteBreathingMission();
        else
            Debug.LogError("[BreathManager] 완료 시 PlayerStateManager가 없어 Idle로 복귀하지 못했습니다.");
    }

    private static Transform FindSceneTransform(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                return candidate;
        }

        return null;
    }
}

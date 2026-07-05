using UnityEngine;

public class BreathManager : MonoBehaviour
{
    [Header("Event Channels (SO)")]
    public BreathEventsSO breathEventsChannel;

    [Header("Tracking Target")]
    public Transform leftController;
    public Transform rightController;

    [Header("Settings")]
    public int targetLoopCount = 3; 
    public float maxBreathAngle = 30f; // 최대 들숨으로 간주할 각도 차이

    [Header("Thresholds")]
    public float inhaleThreshold = 0.7f;
    public float exhaleThreshold = 0.3f;

    private bool isMeasuring = false;
    private Quaternion leftZeroRotation;
    private Quaternion rightZeroRotation;

    private int currentLoopCount = 0;
    private float currentBreathValue = 0f;

    // 호흡 판별을 위한 내부 상태 (들숨 대기 중 / 날숨 대기 중)
    private enum BreathPhase { WaitingForInhale, WaitingForExhale }
    private BreathPhase currentPhase = BreathPhase.WaitingForInhale;
    private void Start()
    {
        PlayerStateManager.Instance.OnStateEnter += HandleStateEnter;
        PlayerStateManager.Instance.OnStateExit += HandleStateExit;
    }

    private void OnDestroy()
    {
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter -= HandleStateEnter;
            PlayerStateManager.Instance.OnStateExit -= HandleStateExit;
        }
    }

    private void HandleStateEnter(PlayerState state)
    {
        if (state == PlayerState.BreathingActive)
        {
            StartCalibrationAndMeasurement();
        }
    }

    private void HandleStateExit(PlayerState state)
    {
        if (state == PlayerState.BreathingActive)
        {
            StopMeasurement();
        }
    }

    private void StartCalibrationAndMeasurement()
    {
        // 1. 0점 조절 / 캘리브레이션 (양손 모두)
        leftZeroRotation = leftController.localRotation;
        rightZeroRotation = rightController.localRotation;

        currentLoopCount = 0;
        currentBreathValue = 0f;
        currentPhase = BreathPhase.WaitingForInhale; // 시작 시 들숨 대기 상태로 초기화
        isMeasuring = true;

        Debug.Log("0점 조절 완료: 양손 컨트롤러 캘리브레이션 됨");
    }

    private void StopMeasurement()
    {
        isMeasuring = false;
        currentLoopCount = 0;
        currentBreathValue = 0f;

        if (breathEventsChannel != null) breathEventsChannel.RaiseBreathValue(0f);
    }

    private void Update()
    {
        if (!isMeasuring || leftController == null || rightController == null || breathEventsChannel == null) return;

        // 2. 호흡 데이터 추출 및 정규화 로직
        // TODO: 추후 이 부분의 수학적 로직을 고도화하거나 ML 모델 입력값으로 사용
        float leftDelta = Quaternion.Angle(leftZeroRotation, leftController.localRotation); // 양손의 각도 변화량 평균 계산
        float rightDelta = Quaternion.Angle(rightZeroRotation, rightController.localRotation);
        float avgDeltaAngle = (leftDelta + rightDelta) / 2f;

        currentBreathValue = Mathf.Clamp01(avgDeltaAngle / maxBreathAngle); // 0.0 ~ 1.0 사이로 정규화
        // Debug.Log(currentBreathValue);

        // BreathEventsSO로 호흡 정규화 값(0.0 ~ 1.0) 실시간 송신
        breathEventsChannel.RaiseBreathValue(currentBreathValue);

        // 3. 호흡 루프 판별 로직 호출
        CheckBreathLoop(currentBreathValue);
    }

    /// 호흡 루프 (들숨 / 날숨) 판별 함수
    private void CheckBreathLoop(float normalizedValue)
    {
        if (currentPhase == BreathPhase.WaitingForInhale)
        {
            // 숨을 들이마셔서 수치가 inhaleThreshold를 넘겼을 때
            if (normalizedValue >= inhaleThreshold)
            {
                currentPhase = BreathPhase.WaitingForExhale;
                Debug.Log("들숨 감지");
            }
        }
        else if (currentPhase == BreathPhase.WaitingForExhale)
        {
            // 숨을 내쉬어서 수치가 다시 exhaleThreshold 밑으로 내려왔을 때
            if (normalizedValue <= exhaleThreshold)
            {
                currentPhase = BreathPhase.WaitingForInhale;
                currentLoopCount++; 

                Debug.Log($"호흡 {currentLoopCount}회 성공");
                breathEventsChannel.RaiseLoopCompleted(currentLoopCount); // 횟수 증가 방송

                // 3회 달성 시 종료
                if (currentLoopCount >= targetLoopCount)
                {
                    isMeasuring = false;
                    breathEventsChannel.RaiseMissionSuccess(); // 미션 성공 방송

                    // 상태 머신 복귀 요청
                    PlayerStateManager.Instance.CompleteBreathingMission();
                }
            }
        }
    }
}
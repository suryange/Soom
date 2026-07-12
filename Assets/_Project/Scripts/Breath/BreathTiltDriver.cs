using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// BreathManager(컨트롤러 회전 기반)의 대체(alternative) 입력 드라이버.
// HMD Transform의 pitch(고개/상체 젖힘)를 읽어 호흡 정규화값을 만든다는 입력 방식만 다르고,
// 출력 계약(같은 BreathEventsSO 채널에 RaiseBreathValue / RaiseLoopCompleted / RaiseMissionSuccess 발행,
// PlayerStateManager 수명주기 연동)은 BreathManager와 동일하게 맞춘다.
// 같은 BreathEventsChannel에 발행하므로 둘을 동시에 활성화하지 말 것.
public class BreathTiltDriver : MonoBehaviour
{
    [Header("Event Channels (SO)")]
    [SerializeField] private BreathEventsSO breathEventsChannel;

    [Header("Tracking Target")]
    [Tooltip("Main Camera / CenterEyeAnchor 등 XR Origin 하위의 HMD Transform. " +
             "TrackedPoseDriver가 매 프레임 헤드셋 포즈로 갱신해준다.")]
    [SerializeField] private Transform hmdTransform;

    [Header("Settings")]
    [SerializeField] private int targetLoopCount = 3;
    [Tooltip("캘리브레이션 기준(0도)에서 이 각도만큼 뒤로 젖히면 호흡값 1.0으로 정규화된다.")]
    [SerializeField] private float maxTiltDegrees = 15f;
    [Tooltip("BreathingActive 상태 진입 시 자동으로 현재 pitch를 0점으로 재캘리브레이션할지 여부.")]
    [SerializeField] private bool calibrateOnStateEnter = true;
    [Tooltip("원시 정규화값에 적용할 SmoothDamp 필터 시간(초).")]
    [SerializeField] private float smoothTime = 0.15f;

    [Header("Thresholds")]
    [SerializeField] private float inhaleThreshold = 0.7f;
    [SerializeField] private float exhaleThreshold = 0.3f;

    [Header("Editor Test Fallback")]
    [Tooltip("HMD Transform이 할당되지 않았을 때 방향키로 호흡값을 직접 움직여 데스크톱에서 테스트.")]
    [SerializeField] private bool allowKeyboardFallback = true;
    [Tooltip("키보드 폴백 사용 시 초당 호흡값 변화량.")]
    [SerializeField] private float keyboardRate = 0.6f;

    private bool isMeasuring = false;
    private float baselinePitchDeg;
    private float smoothedBreathValue;
    private float smoothVelocity;

    private int currentLoopCount = 0;
    private float currentBreathValue = 0f;

    // 호흡 판정을 위한 상태 머신 (들숨 대기 중 / 날숨 대기 중) - BreathManager와 동일한 컨벤션
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
        // 0점 보정 / 캘리브레이션 (HMD pitch 기준)
        if (calibrateOnStateEnter)
        {
            Recalibrate();
        }

        currentLoopCount = 0;
        currentBreathValue = 0f;
        smoothedBreathValue = 0f;
        smoothVelocity = 0f;
        currentPhase = BreathPhase.WaitingForInhale; // 항상 들숨 대기 상태로 초기화
        isMeasuring = true;

        Debug.Log("0도 보정 완료: HMD 기울기 캘리브레이션 완료");
    }

    private void StopMeasurement()
    {
        isMeasuring = false;
        currentLoopCount = 0;
        currentBreathValue = 0f;

        if (breathEventsChannel != null) breathEventsChannel.RaiseBreathValue(0f);
    }

    /// 현재 pitch를 "가장 편안한" 0.0 기준으로 재캘리브레이션.
    /// 착용 위치가 바뀌었을 때 등 외부(캘리브레이션 버튼/메뉴)에서 호출 가능.
    public void Recalibrate()
    {
        baselinePitchDeg = CurrentPitchDegrees();
    }

    private void Update()
    {
        if (!isMeasuring || breathEventsChannel == null) return;

        float rawBreathValue = ReadTiltBreathValue(out bool haveHmdSample);

        // HMD Transform이 없을 때만 키보드 폴백 사용
        if (allowKeyboardFallback && !haveHmdSample)
        {
            rawBreathValue = ReadKeyboardBreathValue();
        }

        // 노이즈 저감용 SmoothDamp 필터
        smoothedBreathValue = Mathf.SmoothDamp(smoothedBreathValue, rawBreathValue, ref smoothVelocity, smoothTime);
        currentBreathValue = Mathf.Clamp01(smoothedBreathValue);

        // BreathEventsSO로 호흡 정규화값(0.0 ~ 1.0) 실시간 송신
        breathEventsChannel.RaiseBreathValue(currentBreathValue);

        // 호흡 루프 판정 로직 호출
        CheckBreathLoop(currentBreathValue);
    }

    /// 호흡 루프 (들숨 / 날숨) 판정 함수 - BreathManager와 동일한 임계값 컨벤션
    private void CheckBreathLoop(float normalizedValue)
    {
        if (currentPhase == BreathPhase.WaitingForInhale)
        {
            // 들숨으로 올라가서 수치가 inhaleThreshold를 넘겼을 때
            if (normalizedValue >= inhaleThreshold)
            {
                currentPhase = BreathPhase.WaitingForExhale;
                Debug.Log("들숨 감지");
            }
        }
        else if (currentPhase == BreathPhase.WaitingForExhale)
        {
            // 다시 낮아진 수치가 exhaleThreshold 이하로 떨어졌을 때
            if (normalizedValue <= exhaleThreshold)
            {
                currentPhase = BreathPhase.WaitingForInhale;
                currentLoopCount++;

                Debug.Log($"호흡 {currentLoopCount}회 완료");
                breathEventsChannel.RaiseLoopCompleted(currentLoopCount); // 횟수 갱신 방송

                // targetLoopCount회 도달 시 종료
                if (currentLoopCount >= targetLoopCount)
                {
                    isMeasuring = false;
                    breathEventsChannel.RaiseMissionSuccess(); // 미션 성공 방송

                    // 상태 머신에 완료 요청
                    PlayerStateManager.Instance.CompleteBreathingMission();
                }
            }
        }
    }

    /// 이번 프레임의 정규화된 뒤로 젖힘(backward tilt) 값(0..1)과, 실제로 HMD/트래킹 Transform이
    /// 할당되어 있었는지(haveSample)를 반환한다. 미할당 시 호출부에서 키보드 폴백으로 전환.
    private float ReadTiltBreathValue(out bool haveSample)
    {
        haveSample = hmdTransform != null;
        if (!haveSample) return 0f;

        float pitchDeg = CurrentPitchDegrees();
        float deltaDeg = pitchDeg - baselinePitchDeg; // 기준 대비 양수 = 뒤로 젖혀짐

        if (maxTiltDegrees <= 0.01f) return 0f;
        return Mathf.Clamp01(deltaDeg / maxTiltDegrees); // 음수(앞으로 숙임)는 0으로 클램프
    }

    /// 부호가 보정된 pitch(도). 고개를 뒤로 젖힐수록(하늘을 볼수록) 값이 커지도록 만든다.
    /// Unity의 localEulerAngles는 0..360으로 랩되므로, 우선 180 기준으로 재정렬해 -180..180 범위로 만든다.
    private float CurrentPitchDegrees()
    {
        if (!hmdTransform) return 0f;

        float rawPitch = hmdTransform.localEulerAngles.x; // 0..360, X축 = pitch
        float signedPitch = rawPitch > 180f ? rawPitch - 360f : rawPitch;

        // 카메라 pitch 컨벤션: 고개를 들어 위를 볼 때 Unity 좌표계에서는 X각도가 음수로 읽힌다
        // (예: 위를 20도 보면 -20). 뒤로 젖힘이 양수가 되도록 부호를 뒤집는다.
        return -signedPitch;
    }

    private float ReadKeyboardBreathValue()
    {
        float current = currentBreathValue;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            float delta = 0f;
            if (kb.upArrowKey.isPressed) delta += 1f;
            if (kb.downArrowKey.isPressed) delta -= 1f;
            if (Mathf.Abs(delta) > 0.001f)
                return Mathf.Clamp01(current + delta * keyboardRate * Time.deltaTime);
        }
#endif
        return current;
    }
}

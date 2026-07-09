using System.Collections;
using UnityEngine;

/// <summary>
/// Scene_02_InGame_Inside(우주선 내부) 튜토리얼 전체를 관장하는 단계형 컨트롤러.
/// 기상 연출(2.1.1) → 지시문(2.1.2) → 호흡 캘리브레이션 미션(2.2) → 성공 안내 →
/// 해치 개방(2.3) → 다음 씬 전환 순서로 진행한다.
///
/// 각 스텝은 참조가 비어 있어도(음성 클립/해치 모델 등 옵션 에셋 미준비) 예외 없이
/// 건너뛰고 다음 스텝으로 진행하도록 방어적으로 작성되어 있다.
/// </summary>
public class InsideTutorialSequence : MonoBehaviour
{
    [Header("이벤트 채널 (SO)")]
    [SerializeField] private BreathEventsSO breathEventsChannel;

    [Header("자동 시작 여부")]
    [Tooltip("씬 로드 후(Start) 자동으로 시퀀스를 시작할지 여부. 끄면 StartSequence()를 외부에서 호출해야 한다.")]
    [SerializeField] private bool autoStartOnEnable = true;

    [Header("2.1.1 기상 연출")]
    [SerializeField] private WakeUpVignetteEffect wakeUpEffect;

    [Header("공용 월드 스페이스 안내 UI")]
    [SerializeField] private TutorialMessagePanel messagePanel;
    [Tooltip("메시지를 띄울 때 이 거리(m)만큼 플레이어 정면에 배치한다.")]
    [SerializeField] private float messageDistanceFromPlayer = 2f;
    [Tooltip("비워두면 Camera.main을 플레이어 카메라로 사용한다.")]
    [SerializeField] private Transform playerCameraOverride;

    [Header("2.1.1 도착 안내 문구")]
    [SerializeField] private string arrivalMessage = "???별 불시착";
    [SerializeField] private float arrivalMessageDuration = 2.5f;
    [SerializeField] private float postArrivalFadeDuration = 0.6f;

    [Header("2.1.2 지시문")]
    [TextArea]
    [SerializeField] private string instructionMessage = "숨을 깊게 들이쉬고 내쉬어 비상탈출장치를 가동하세요!";
    [Tooltip("옵션: 지시문 안내 음성. 비워두면 텍스트만 표시한다.")]
    [SerializeField] private AudioClip instructionVoiceClip;
    [Tooltip("음성 클립이 없을 때 지시문을 띄워두는 시간(초).")]
    [SerializeField] private float instructionFallbackDuration = 3.5f;

    [Header("2.2 호흡 캘리브레이션")]
    [SerializeField] private BreathCircleUI breathCircleUI;
    [Tooltip("BreathEventsSO/BreathManager가 배선되지 않았을 때 테스트 진행을 위해 자동 성공 처리할지 여부.")]
    [SerializeField] private bool autoSucceedIfNoBreathChannel = true;
    [SerializeField] private float autoSucceedFallbackDelay = 1f;

    [TextArea]
    [SerializeField] private string missionSuccessMessage = "비상탈출장치가 성공적으로 가동되었습니다! 곧 해치가 개방됩니다";
    [SerializeField] private float missionSuccessMessageDuration = 3f;

    [Header("2.3 해치 개방")]
    [SerializeField] private HatchController hatchController;
    [TextArea]
    [SerializeField] private string hatchOpenCompleteMessage = "해치 개방 완료";
    [SerializeField] private float hatchCompleteMessageDuration = 2f;

    [Header("씬 전환")]
    [SerializeField] private SceneType nextScene = SceneType.Scene_03_InGame_Outside;

    private bool _missionSucceeded;

    private void OnEnable()
    {
        if (breathEventsChannel != null)
            breathEventsChannel.OnMissionSuccess += HandleMissionSuccess;
    }

    private void OnDisable()
    {
        if (breathEventsChannel != null)
            breathEventsChannel.OnMissionSuccess -= HandleMissionSuccess;
    }

    private void Start()
    {
        if (autoStartOnEnable) StartSequence();
    }

    private void HandleMissionSuccess()
    {
        _missionSucceeded = true;
    }

    /// <summary>외부(QA 스킵 버튼, 디버그 등)에서 시퀀스를 (재)시작하고 싶을 때 호출.</summary>
    public void StartSequence()
    {
        StopAllCoroutines();
        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // 미션이 시작되기 전까지는 호흡 UI를 확실히 숨겨둔다(빌드 직후 기본 활성 상태여도 안전).
        if (breathCircleUI != null) breathCircleUI.Hide();
        HideMessage();

        yield return PlayWakeUp();
        yield return PlayArrivalTitle();
        yield return PlayInstruction();
        yield return PlayBreathingMission();
        yield return PlayMissionSuccessMessage();
        yield return PlayHatchOpen();
        TransitionToNextScene();
    }

    // ===================================================================
    // 2.1.1 기상 연출
    // ===================================================================
    private IEnumerator PlayWakeUp()
    {
        if (wakeUpEffect == null)
        {
            Debug.LogWarning("[InsideTutorialSequence] WakeUpVignetteEffect가 연결되지 않아 기상 연출을 건너뜁니다.");
            yield break;
        }

        bool done = false;
        wakeUpEffect.PlayWakeUpAnimation(() => done = true);
        yield return new WaitUntil(() => done);
    }

    private IEnumerator PlayArrivalTitle()
    {
        ShowMessage(arrivalMessage);
        yield return new WaitForSeconds(arrivalMessageDuration);

        // 도착 문구 → 화면 페이드 아웃 → 문구 숨김 → 페이드 인 순서로,
        // "잠시 후 Fade-out" 이후 자연스럽게 다음(지시문) 스텝으로 넘어가도록 한다.
        if (ScreenFader.Instance != null)
        {
            bool faded = false;
            ScreenFader.Instance.FadeOut(postArrivalFadeDuration, () => faded = true);
            yield return new WaitUntil(() => faded);
        }

        HideMessage();

        if (ScreenFader.Instance != null)
        {
            bool faded = false;
            ScreenFader.Instance.FadeIn(postArrivalFadeDuration, () => faded = true);
            yield return new WaitUntil(() => faded);
        }
    }

    // ===================================================================
    // 2.1.2 지시문
    // ===================================================================
    private IEnumerator PlayInstruction()
    {
        if (instructionVoiceClip != null && SoomAudioManager.Instance != null)
            SoomAudioManager.Instance.PlayVoice(instructionVoiceClip);

        ShowMessage(instructionMessage);

        float waitDuration = instructionVoiceClip != null ? instructionVoiceClip.length : instructionFallbackDuration;
        yield return new WaitForSeconds(waitDuration);

        HideMessage();
    }

    // ===================================================================
    // 2.2 호흡 캘리브레이션
    // ===================================================================
    private IEnumerator PlayBreathingMission()
    {
        _missionSucceeded = false;

        if (breathCircleUI != null) breathCircleUI.Show();

        if (PlayerStateManager.Instance != null)
            PlayerStateManager.Instance.ChangeState(PlayerState.BreathingActive);
        else
            Debug.LogWarning("[InsideTutorialSequence] PlayerStateManager 인스턴스를 찾을 수 없어 상태 전환 없이 진행합니다.");

        if (breathEventsChannel == null)
        {
            Debug.LogWarning("[InsideTutorialSequence] BreathEventsSO가 연결되지 않았습니다. " +
                (autoSucceedIfNoBreathChannel
                    ? "테스트 진행을 위해 잠시 후 자동으로 미션을 성공 처리합니다."
                    : "미션 성공 신호를 받을 수 없어 이 스텝에서 대기 상태로 멈춥니다."));

            if (autoSucceedIfNoBreathChannel)
            {
                yield return new WaitForSeconds(autoSucceedFallbackDelay);
                _missionSucceeded = true;
            }
            else
            {
                yield return new WaitUntil(() => _missionSucceeded);
            }
        }
        else
        {
            // 실제 성공 신호(OnMissionSuccess)는 BreathManager/BreathTiltDriver가
            // 3회 호흡 루프를 완료했을 때 breathEventsChannel을 통해 방송한다.
            yield return new WaitUntil(() => _missionSucceeded);
        }

        if (breathCircleUI != null) breathCircleUI.Hide();
    }

    private IEnumerator PlayMissionSuccessMessage()
    {
        ShowMessage(missionSuccessMessage);
        yield return new WaitForSeconds(missionSuccessMessageDuration);
        HideMessage();
    }

    // ===================================================================
    // 2.3 해치 개방
    // ===================================================================
    private IEnumerator PlayHatchOpen()
    {
        if (hatchController != null)
        {
            bool done = false;
            hatchController.OpenHatch(() => done = true);
            yield return new WaitUntil(() => done);
        }
        else
        {
            Debug.LogWarning("[InsideTutorialSequence] HatchController가 연결되지 않아 해치 연출 없이 진행합니다.");
        }

        ShowMessage(hatchOpenCompleteMessage);
        yield return new WaitForSeconds(hatchCompleteMessageDuration);
        HideMessage();
    }

    private void TransitionToNextScene()
    {
        if (SOOMSceneManager.Instance != null)
            SOOMSceneManager.Instance.LoadScene(nextScene);
        else
            Debug.LogWarning("[InsideTutorialSequence] SOOMSceneManager 인스턴스가 없어 씬 전환을 수행할 수 없습니다.");
    }

    // ===================================================================
    // 공용 헬퍼
    // ===================================================================
    private void ShowMessage(string message)
    {
        if (messagePanel == null) return;
        messagePanel.PositionInFrontOfCamera(GetPlayerCamera(), messageDistanceFromPlayer);
        messagePanel.Show(message);
    }

    private void HideMessage()
    {
        if (messagePanel != null) messagePanel.Hide();
    }

    private Transform GetPlayerCamera()
    {
        if (playerCameraOverride != null) return playerCameraOverride;
        return Camera.main != null ? Camera.main.transform : null;
    }
}

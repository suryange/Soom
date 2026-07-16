using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class StartSequenceController : MonoBehaviour
{
    private const float Scene01UIRayDistance = 20f;

    [Header("UI & Video Setup")]
    public GameObject startPanel;       // 시작 버튼이 있는 UI 패널
    public GameObject videoPanel;       // RawImage가 있는 비디오 패널
    public VideoPlayer videoPlayer;     // 영상 재생기

    [Header("Target Scene")]
    public SceneType nextScene = SceneType.Scene_02_InGame_Inside;

    private bool sequenceStarted;

    private void Start()
    {
        // 초기 상태 설정: 시작 패널 활성화, 비디오 패널 비활성화
        startPanel.SetActive(true);
        videoPanel.SetActive(false);

        ConfigureXRUIInteraction();

        // XRI 레이 호버는 되지만 UI Press가 누락되는 런타임 환경에서도
        // 컨트롤러 Trigger로 시작 버튼을 누를 수 있게 보완 입력을 설치한다.
        Button startButton = startPanel.GetComponentInChildren<Button>(true);
        if (startButton != null && startButton.GetComponent<XRTriggerButtonFallback>() == null)
            startButton.gameObject.AddComponent<XRTriggerButtonFallback>();

        // 비디오 재생 완료 이벤트 구독
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void ConfigureXRUIInteraction()
    {
        // Scene01의 월드 스페이스 Canvas는 XR Origin에서 약 15m 떨어져 있다.
        // Starter Assets Near-Far Interactor의 기본 Cast Distance(10m)로는 UI에 닿지 않으므로
        // 이 씬에서만 실제 캐스트 거리와 라인 표시 거리를 늘린다.
        NearFarInteractor[] interactors = FindObjectsByType<NearFarInteractor>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (NearFarInteractor interactor in interactors)
        {
            interactor.enableUIInteraction = true;

            if (interactor.farInteractionCaster is CurveInteractionCaster caster)
                caster.castDistance = Mathf.Max(caster.castDistance, Scene01UIRayDistance);

            CurveVisualController visual = interactor.GetComponentInChildren<CurveVisualController>(true);
            if (visual != null)
                visual.maxVisualCurveDistance = Mathf.Max(visual.maxVisualCurveDistance, Scene01UIRayDistance);
        }

        Canvas startCanvas = startPanel.GetComponentInParent<Canvas>();
        if (startCanvas != null && startCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            startCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
    }

    /// <summary>
    /// 시작 버튼의 OnClick 이벤트에 연결할 메서드
    /// </summary>
    public void OnStartButtonClicked()
    {
        if (sequenceStarted)
            return;

        sequenceStarted = true;

        SoomAudioManager.Instance?.PlayInteractionSfx();

        // UI 패널 전환
        startPanel.SetActive(false);
        videoPanel.SetActive(true);

        // 비디오 재생 시작
        videoPlayer.Play();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    /// <summary>
    /// 비디오 재생이 끝났을 때 자동 호출되는 콜백 메서드
    /// </summary>
    private void OnVideoFinished(VideoPlayer vp)
    {
        // 이벤트 구독 해제 (메모리 누수 및 중복 호출 방지)
        vp.loopPointReached -= OnVideoFinished;

        // 기존에 작성된 씬 매니저를 통해 다음 씬으로 전환
        SOOMSceneManager.Instance.LoadScene(nextScene);
    }
}

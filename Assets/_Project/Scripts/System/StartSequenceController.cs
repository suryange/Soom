using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class StartSequenceController : MonoBehaviour
{
    [Header("UI & Video Setup")]
    public GameObject startPanel;       // 시작 버튼이 있는 UI 패널
    public GameObject videoPanel;       // RawImage가 있는 비디오 패널
    public VideoPlayer videoPlayer;     // 영상 재생기

    [Header("Target Scene")]
    public SceneType nextScene = SceneType.Scene_02_InGame_Inside;

    private void Start()
    {
        // 초기 상태 설정: 시작 패널 활성화, 비디오 패널 비활성화
        startPanel.SetActive(true);
        videoPanel.SetActive(false);

        // 비디오 재생 완료 이벤트 구독
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    /// <summary>
    /// 시작 버튼의 OnClick 이벤트에 연결할 메서드
    /// </summary>
    public void OnStartButtonClicked()
    {
        // UI 패널 전환
        startPanel.SetActive(false);
        videoPanel.SetActive(true);

        // 비디오 재생 시작
        videoPlayer.Play();
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
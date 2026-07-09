using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스타팅 화면(Scene_01_Start) 메인 UI 컨트롤러(기능 명세 1.2).
/// 게임 시작 → 추락 컷신 재생 → 다음 씬 전환, 환경 설정/팀 소개 패널 전환, 종료 버튼을 담당합니다.
/// </summary>
public class StartScreenController : MonoBehaviour
{
    [Header("1.2.1 게임 시작")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private CrashCutsceneController crashCutscene;
    [SerializeField] private SceneType nextScene = SceneType.Scene_02_InGame_Inside;

    [Header("1.2.2 환경 설정 패널 열기/닫기")]
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button closeSettingsButton;

    [Header("1.2.3 팀 소개 패널 열기/닫기")]
    [SerializeField] private Button teamIntroButton;
    [SerializeField] private Button teamIntroBackButton;

    [Header("1.2.4 종료")]
    [SerializeField] private Button closeButton;

    [Header("패널 참조")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject teamIntroPanel;

    private bool _isStarting;

    private void OnEnable()
    {
        if (startGameButton != null) startGameButton.onClick.AddListener(OnStartGameClicked);
        if (openSettingsButton != null) openSettingsButton.onClick.AddListener(OnOpenSettingsClicked);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnCloseSettingsClicked);
        if (teamIntroButton != null) teamIntroButton.onClick.AddListener(OnTeamIntroClicked);
        if (teamIntroBackButton != null) teamIntroBackButton.onClick.AddListener(OnTeamIntroBackClicked);
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDisable()
    {
        if (startGameButton != null) startGameButton.onClick.RemoveListener(OnStartGameClicked);
        if (openSettingsButton != null) openSettingsButton.onClick.RemoveListener(OnOpenSettingsClicked);
        if (closeSettingsButton != null) closeSettingsButton.onClick.RemoveListener(OnCloseSettingsClicked);
        if (teamIntroButton != null) teamIntroButton.onClick.RemoveListener(OnTeamIntroClicked);
        if (teamIntroBackButton != null) teamIntroBackButton.onClick.RemoveListener(OnTeamIntroBackClicked);
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    // ------------------------------------------------------------ 1.2.1 게임 시작
    private void OnStartGameClicked()
    {
        if (_isStarting) return;
        _isStarting = true;

        SwitchPanel(null); // 컷신 재생 중에는 메인 UI 패널을 전부 닫습니다.

        if (crashCutscene != null)
        {
            crashCutscene.Play(OnCutsceneComplete);
        }
        else
        {
            Debug.LogWarning("[StartScreenController] CrashCutsceneController가 연결되지 않아 컷신 없이 바로 씬을 전환합니다.");
            OnCutsceneComplete();
        }
    }

    private void OnCutsceneComplete()
    {
        if (SOOMSceneManager.Instance != null)
        {
            SOOMSceneManager.Instance.LoadScene(nextScene);
        }
        else
        {
            Debug.LogError("[StartScreenController] SOOMSceneManager 인스턴스를 찾을 수 없어 씬을 전환할 수 없습니다.");
        }
    }

    // ------------------------------------------------------------ 1.2.2 환경 설정
    private void OnOpenSettingsClicked() => SwitchPanel(settingsPanel);
    private void OnCloseSettingsClicked() => SwitchPanel(mainMenuPanel);

    // ------------------------------------------------------------ 1.2.3 팀 소개
    private void OnTeamIntroClicked() => SwitchPanel(teamIntroPanel);
    private void OnTeamIntroBackClicked() => SwitchPanel(mainMenuPanel);

    /// <summary>메인/설정/팀소개 패널 중 target 하나만 활성화합니다. null이면 전부 비활성화합니다.</summary>
    private void SwitchPanel(GameObject target)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(target == mainMenuPanel);
        if (settingsPanel != null) settingsPanel.SetActive(target == settingsPanel);
        if (teamIntroPanel != null) teamIntroPanel.SetActive(target == teamIntroPanel);
    }

    // ------------------------------------------------------------ 1.2.4 종료
    private void OnCloseClicked()
    {
        Debug.Log("[StartScreenController] 게임 종료 요청");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

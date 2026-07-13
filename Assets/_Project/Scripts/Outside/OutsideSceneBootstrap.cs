using UnityEngine;

/// <summary>
/// Scene_03_InGame_Outside 진입 시 최소한의 초기화를 담당하는 가벼운 부트스트랩.
/// ScreenFader로 페이드 인을 실행하고, 필요하다면 PlayerStateManager를 Idle로 정렬한다.
/// SOOMSceneManager를 거쳐 들어오는 경우 이미 페이드 인이 진행되지만, 에디터에서 씬을 바로
/// 재생(Play)했을 때도 검은 화면으로 시작하지 않도록 폴백 역할을 한다.
/// </summary>
public class OutsideSceneBootstrap : MonoBehaviour
{
    [Header("Fade In Settings")]
    [SerializeField] private float fadeInDuration = 0.5f;

    private void Start()
    {
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.EnsureFadeQuad();
            ScreenFader.Instance.FadeIn(fadeInDuration);
        }
        else
        {
            Debug.LogWarning("[OutsideSceneBootstrap] ScreenFader.Instance를 찾을 수 없어 페이드 없이 진행합니다.");
        }

        // Scene_03 진입 시 이동 가능한 Idle 상태로 정렬 (이미 Idle이면 ChangeState가 아무 것도 하지 않음)
        if (PlayerStateManager.Instance != null &&
            PlayerStateManager.Instance.CurrentState != PlayerState.Idle)
        {
            PlayerStateManager.Instance.ChangeState(PlayerState.Idle);
        }
    }
}

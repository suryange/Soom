using UnityEngine;

/// <summary>
/// 씬 진입 연출(기능 명세 1.1): 씬이 시작되면 화면을 검은색으로 고정한 뒤 서서히 밝게(Fade In) 전환합니다.
/// 실제 페이드 연산은 ScreenFader 싱글턴이 담당하며, ScreenFader가 씬에 없으면 조용히 아무 것도 하지 않습니다.
/// </summary>
public class SceneEntryFade : MonoBehaviour
{
    [Header("Fade In 소요 시간(초)")]
    [SerializeField] private float fadeInDuration = 1.0f;

    private void Start()
    {
        if (ScreenFader.Instance == null)
        {
            Debug.LogWarning("[SceneEntryFade] ScreenFader 인스턴스를 찾을 수 없어 씬 진입 페이드를 건너뜁니다.");
            return;
        }

        // 씬 시작 시 즉시 암전(SetBlack) 후, Fade In으로 서서히 밝아집니다.
        ScreenFader.Instance.SetBlack();
        ScreenFader.Instance.FadeIn(fadeInDuration);
    }
}

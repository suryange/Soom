// EndingSequenceController.cs
// 기능 명세서 6장(엔딩 화면) 구현: 길잡이 등불 시선 트래킹 -> 페이드 아웃 -> 프로젝트명/크레딧 순으로
// 진행되는 엔딩 시퀀스 총괄 컨트롤러.
//
// 다른 시스템(예: 여우 동료 합류 후 최종 트리거)이 StartEnding()을 호출하면 시퀀스가 시작된다.
// 씬 배선(등불 플레이스홀더, 크레딧 World Space Canvas)은 Assets/_Project/Editor/의
// "SOOM > Build Ending Sequence" 메뉴 아이템으로 생성한다. 참조가 비어 있어도(옵션) 죽지 않도록
// 각 단계마다 null 가드를 두고, 등불 참조가 없으면 런타임에 포인트라이트 플레이스홀더를 즉석 생성한다.
using System.Collections;
using UnityEngine;

/// <summary>엔딩 크레딧 이후 처리 방식.</summary>
public enum EndingFinishBehavior
{
    ReturnToStart, // Scene_01_Start로 복귀
    Stop           // 크레딧 화면에서 정지
}

public class EndingSequenceController : MonoBehaviour
{
    [Header("6.1 길잡이 등불 참조 (없으면 포인트라이트 플레이스홀더 자동 생성)")]
    [Tooltip("길잡이 등불 트랜스폼. 비워두면 StartEnding() 시점에 포인트라이트 플레이스홀더를 생성한다.")]
    [SerializeField] private Transform lanternTransform;

    [Header("카메라 / 플레이어 참조 (비우면 자동 탐색)")]
    [Tooltip("플레이어 시점 카메라. 비우면 Camera.main을 사용한다.")]
    [SerializeField] private Transform playerCameraOverride;
    [Tooltip("플레이어 루트(위치 기준점). 비우면 'Player' 태그 오브젝트, 그마저 없으면 카메라 아래 대략적인 발밑 위치를 사용한다.")]
    [SerializeField] private Transform playerRootOverride;

    [Header("6.1 길잡이 등불 시선 트래킹")]
    [Tooltip("등불이 플레이어 주위를 돌며 하늘로 떠오르는 데 걸리는 시간(초).")]
    [SerializeField] private float ascendDuration = 6f;
    [Tooltip("떠오르는 움직임의 완급 커브 (0=시작, 1=종료).")]
    [SerializeField] private AnimationCurve ascendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("떠오르기 시작할 때 플레이어와의 수평 거리.")]
    [SerializeField] private float orbitStartRadius = 1.2f;
    [Tooltip("떠오르기가 끝날 때 플레이어와의 수평 거리.")]
    [SerializeField] private float orbitEndRadius = 3.5f;
    [Tooltip("떠오르는 동안 플레이어 주위를 도는 각도(도).")]
    [SerializeField] private float orbitDegrees = 220f;
    [Tooltip("하늘로 떠오른 뒤 최종 높이(플레이어 기준, m).")]
    [SerializeField] private float ascendHeight = 18f;
    [Tooltip("등불이 시야에서 충분히 멀어질 때까지 대기하는 시간(초). 페이드 아웃 시작 전 여유 시간.")]
    [SerializeField] private float preFadeHoldDuration = 1.2f;

    [Header("6.2 페이드 아웃")]
    [SerializeField] private float fadeOutDuration = 3f;

    [Header("6.3 프로젝트명 / 크레딧")]
    [Tooltip("크레딧 World Space Canvas 루트 (에디터 빌더가 생성/배선).")]
    [SerializeField] private GameObject creditsPanelRoot;
    [Tooltip("스크롤되는 크레딧 텍스트 콘텐츠 RectTransform.")]
    [SerializeField] private RectTransform creditsContent;
    [Tooltip("크레딧이 보이는 영역(뷰포트) RectTransform. 스크롤 총 이동 거리 계산에 사용.")]
    [SerializeField] private RectTransform creditsViewport;
    [Tooltip("플레이어 정면 기준 크레딧 패널 배치 거리(m).")]
    [SerializeField] private float creditsDistance = 2f;
    [Tooltip("암전 상태에서 크레딧 패널을 배치한 뒤 다시 밝아지는 데 걸리는 시간(초).")]
    [SerializeField] private float fadeInAfterBlackDuration = 2f;
    [Tooltip("크레딧 텍스트가 위로 스크롤되는 속도(캔버스 단위/초).")]
    [SerializeField] private float creditsScrollSpeed = 40f;
    [Tooltip("크레딧 스크롤이 끝난 뒤 다음 동작까지 대기 시간(초).")]
    [SerializeField] private float postCreditsDelay = 2f;
    [Tooltip("크레딧 종료 후 처리 방식.")]
    [SerializeField] private EndingFinishBehavior finishBehavior = EndingFinishBehavior.ReturnToStart;
    [Tooltip("ReturnToStart 선택 시 복귀할 씬.")]
    [SerializeField] private SceneType returnSceneType = SceneType.Scene_01_Start;

    private bool _isPlaying;
    private bool _createdPlaceholderLantern;
    private Vector2 _creditsStartAnchoredPos;
    private bool _creditsStartCaptured;

    /// <summary>
    /// 엔딩 시퀀스 진입점. 다른 시스템(예: 여우 동료 합류 후 최종 트리거)에서 호출한다.
    /// LockPlayer -> LanternAscend -> FadeOut -> Credits 순으로 진행된다.
    /// </summary>
    public void StartEnding()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[EndingSequenceController] StartEnding()은 플레이 모드에서만 동작합니다.");
            return;
        }

        if (_isPlaying)
        {
            Debug.LogWarning("[EndingSequenceController] 이미 엔딩 시퀀스가 진행 중입니다.");
            return;
        }

        _isPlaying = true;
        StartCoroutine(EndingRoutine());
    }

    [ContextMenu("Start Ending")]
    private void StartEndingFromContextMenu()
    {
        StartEnding();
    }

    private IEnumerator EndingRoutine()
    {
        Debug.Log("[EndingSequenceController] 엔딩 시퀀스 시작");

        LockPlayer();

        yield return StartCoroutine(LanternAscendRoutine());

        if (preFadeHoldDuration > 0f)
            yield return new WaitForSeconds(preFadeHoldDuration);

        yield return StartCoroutine(FadeOutRoutine());

        yield return StartCoroutine(CreditsRoutine());

        yield return StartCoroutine(FinishRoutine());

        _isPlaying = false;
        Debug.Log("[EndingSequenceController] 엔딩 시퀀스 완료");
    }

    // ------------------------------------------------------------ 6.1 LockPlayer
    private void LockPlayer()
    {
        // LocomotionStateController(파일명 LocomotionStaticController.cs)가 들고 있는
        // locomotionComponents 배열을 직접 비활성화하여 이동을 잠근다.
        var locomotion = FindFirstObjectByType<LocomotionStateController>();
        if (locomotion != null && locomotion.locomotionComponents != null)
        {
            foreach (var comp in locomotion.locomotionComponents)
            {
                if (comp != null) comp.enabled = false;
            }
            Debug.Log("[EndingSequenceController] 플레이어 이동을 잠갔습니다.");
        }
        else
        {
            Debug.LogWarning("[EndingSequenceController] LocomotionStateController를 찾지 못해 이동 잠금을 건너뜁니다.");
        }
    }

    // ------------------------------------------------------------ 6.1 LanternAscend
    private IEnumerator LanternAscendRoutine()
    {
        Transform lantern = ResolveLantern();
        if (lantern == null)
        {
            Debug.LogWarning("[EndingSequenceController] 등불 트랜스폼을 확보하지 못해 시선 트래킹 연출을 건너뜁니다.");
            yield break;
        }

        Transform cam = ResolveCamera();
        Vector3 center = ResolvePlayerCenter(cam);

        // 카메라 정면 방향을 시작 각도로 삼아, 시퀀스 시작 시 플레이어가 바로 등불을 볼 수 있게 한다.
        Vector3 forwardFlat = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up) : Vector3.forward;
        if (forwardFlat.sqrMagnitude < 0.0001f) forwardFlat = Vector3.forward;
        forwardFlat.Normalize();
        float startAngle = Mathf.Atan2(forwardFlat.x, forwardFlat.z) * Mathf.Rad2Deg;

        float eyeHeight = cam != null ? Mathf.Max(cam.position.y - center.y, 0.5f) : 1.6f;

        float t = 0f;
        while (t < ascendDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / ascendDuration);
            float curved = ascendCurve.Evaluate(k);

            float angle = startAngle + orbitDegrees * curved;
            float radius = Mathf.Lerp(orbitStartRadius, orbitEndRadius, curved);
            float height = Mathf.Lerp(eyeHeight, ascendHeight, curved);

            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
            lantern.position = center + offset + Vector3.up * height;

            yield return null;
        }

        // 마지막 프레임의 오차 없이 최종 위치를 정확히 고정한다.
        float finalAngle = startAngle + orbitDegrees;
        Vector3 finalOffset = Quaternion.Euler(0f, finalAngle, 0f) * Vector3.forward * orbitEndRadius;
        lantern.position = center + finalOffset + Vector3.up * ascendHeight;
    }

    private Transform ResolveLantern()
    {
        if (lanternTransform != null) return lanternTransform;

        // 옵션 참조가 비어 있으면 포인트라이트 플레이스홀더를 즉석에서 생성한다.
        var go = new GameObject("GuidingLanternPlaceholder (Runtime)");
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.82f, 0.5f);
        light.range = 8f;
        light.intensity = 3f;

        lanternTransform = go.transform;
        _createdPlaceholderLantern = true;
        return lanternTransform;
    }

    private Transform ResolveCamera()
    {
        if (playerCameraOverride != null) return playerCameraOverride;
        return Camera.main != null ? Camera.main.transform : null;
    }

    private Vector3 ResolvePlayerCenter(Transform cam)
    {
        if (playerRootOverride != null) return playerRootOverride.position;

        GameObject playerGo = null;
        try
        {
            playerGo = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            playerGo = null; // "Player" 태그가 프로젝트에 정의되어 있지 않은 경우 대비
        }

        if (playerGo != null) return playerGo.transform.position;
        if (cam != null) return new Vector3(cam.position.x, cam.position.y - 1.6f, cam.position.z); // 대략적인 발밑 높이
        return transform.position;
    }

    // ------------------------------------------------------------ 6.2 FadeOut
    private IEnumerator FadeOutRoutine()
    {
        if (ScreenFader.Instance == null)
        {
            Debug.LogWarning("[EndingSequenceController] ScreenFader.Instance가 없어 페이드 없이 진행합니다.");
            yield break;
        }

        bool done = false;
        ScreenFader.Instance.FadeOut(fadeOutDuration, () => done = true);
        yield return new WaitUntil(() => done);
    }

    // ------------------------------------------------------------ 6.3 Credits
    private IEnumerator CreditsRoutine()
    {
        // 등불(플레이스홀더 포함)은 크레딧 단계에서 더 이상 필요 없다.
        // 런타임 생성 플레이스홀더는 정리하고, 디자이너가 미리 배치한 실제 참조는 비활성화만 한다.
        if (lanternTransform != null)
        {
            if (_createdPlaceholderLantern) Destroy(lanternTransform.gameObject);
            else lanternTransform.gameObject.SetActive(false);
            lanternTransform = null;
        }

        if (creditsPanelRoot != null)
        {
            creditsPanelRoot.SetActive(true);
            PositionCreditsPanelInFrontOfPlayer();
            ResetCreditsScroll();
        }
        else
        {
            Debug.LogWarning("[EndingSequenceController] 크레딧 패널이 연결되어 있지 않습니다. " +
                "'SOOM > Build Ending Sequence' 메뉴로 씬을 먼저 배선해주세요.");
        }

        // 화면은 여전히 암전 상태 — 크레딧 패널을 배치한 뒤 다시 밝혀서 자연스럽게 드러낸다.
        // (ScreenFader의 페이드 쿼드는 카메라에 매우 가깝게 붙어 있어, 완전 암전 상태에서는
        //  World Space 크레딧 패널도 함께 가려진다. 그래서 준비를 마친 뒤 FadeIn으로 걷어낸다.)
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.EnsureFadeQuad();
            bool faded = false;
            ScreenFader.Instance.FadeIn(fadeInAfterBlackDuration, () => faded = true);
            yield return new WaitUntil(() => faded);
        }

        if (creditsPanelRoot != null && creditsContent != null)
        {
            yield return StartCoroutine(ScrollCreditsRoutine());
        }
    }

    private void PositionCreditsPanelInFrontOfPlayer()
    {
        Transform cam = ResolveCamera();
        if (cam == null || creditsPanelRoot == null) return;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.0001f) forwardFlat = cam.forward;
        forwardFlat.Normalize();

        Transform panel = creditsPanelRoot.transform;
        panel.position = cam.position + forwardFlat * creditsDistance;
        panel.rotation = Quaternion.LookRotation(panel.position - cam.position, Vector3.up);
        // 이후 프레임부터는 FaceCamera 컴포넌트가 회전을 계속 보정한다.
    }

    private void ResetCreditsScroll()
    {
        if (creditsContent == null) return;
        if (!_creditsStartCaptured)
        {
            _creditsStartAnchoredPos = creditsContent.anchoredPosition;
            _creditsStartCaptured = true;
        }
        creditsContent.anchoredPosition = _creditsStartAnchoredPos;
    }

    private IEnumerator ScrollCreditsRoutine()
    {
        float viewportHeight = creditsViewport != null ? creditsViewport.rect.height : 0f;
        float distance = creditsContent.rect.height + viewportHeight;
        if (distance <= 0f) yield break;

        float speed = Mathf.Max(creditsScrollSpeed, 1f);
        Vector2 start = creditsContent.anchoredPosition;
        float travelled = 0f;

        while (travelled < distance)
        {
            travelled += speed * Time.deltaTime;
            creditsContent.anchoredPosition = start + new Vector2(0f, Mathf.Min(travelled, distance));
            yield return null;
        }
    }

    // ------------------------------------------------------------ 종료 처리
    private IEnumerator FinishRoutine()
    {
        if (postCreditsDelay > 0f)
            yield return new WaitForSeconds(postCreditsDelay);

        switch (finishBehavior)
        {
            case EndingFinishBehavior.ReturnToStart:
                if (SOOMSceneManager.Instance != null)
                {
                    SOOMSceneManager.Instance.LoadScene(returnSceneType);
                }
                else
                {
                    Debug.LogWarning("[EndingSequenceController] SOOMSceneManager.Instance가 없어 씬 복귀를 건너뜁니다.");
                }
                break;

            case EndingFinishBehavior.Stop:
                Debug.Log("[EndingSequenceController] 엔딩 정지 옵션 — 크레딧 화면을 유지합니다.");
                break;
        }
    }
}

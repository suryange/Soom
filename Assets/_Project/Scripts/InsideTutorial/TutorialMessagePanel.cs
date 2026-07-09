using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 우주선 내부 튜토리얼 전용 공용 안내 문구 World Space UI.
/// "???별 불시착", 지시문, 미션 성공 안내, 해치 개방 완료 등 텍스트만 바뀌는
/// 모든 안내 메시지를 이 하나의 패널로 재사용한다. BreathCircleUI와 동일하게
/// CanvasGroup 알파값을 코루틴으로 Lerp하여 Show/Hide 페이드를 처리한다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TutorialMessagePanel : MonoBehaviour
{
    [Header("표시 대상")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup group;

    [Header("페이드 설정")]
    [SerializeField] private float fadeDuration = 0.35f;

    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 카메라(플레이어) 정면 distance(m) 지점으로 패널을 이동시킨다.
    /// 카메라의 높이(y)는 그대로 따라가고, 수평 방향(forward)만 사용해 눕거나 젖혀도
    /// 패널이 바닥/천장으로 튀지 않게 한다.
    /// </summary>
    public void PositionInFrontOfCamera(Transform cameraTransform, float distance)
    {
        if (cameraTransform == null) return;

        Vector3 flatForward = cameraTransform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward; // 카메라가 수직을 볼 때의 예외 처리

        flatForward.Normalize();
        transform.position = cameraTransform.position + flatForward * distance;
    }

    /// <summary>문구를 설정하고 페이드 인으로 패널을 띄운다.</summary>
    public void Show(string message)
    {
        if (messageText != null) messageText.text = message;
        gameObject.SetActive(true);
        Fade(1f);
    }

    /// <summary>페이드 아웃 후 비활성화한다.</summary>
    public void Hide()
    {
        if (!gameObject.activeInHierarchy) return; // 이미 꺼져 있으면 코루틴을 돌릴 수 없으니 종료
        Fade(0f);
    }

    private void Fade(float target)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        float start = group != null ? group.alpha : target;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            if (group != null) group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        if (group != null) group.alpha = target;
        if (target <= 0f) gameObject.SetActive(false);
        _fadeRoutine = null;
    }
}

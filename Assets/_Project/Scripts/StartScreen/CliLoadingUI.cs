using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// 컷신 도중 화면 전체를 덮는 CLI 터미널 스타일 로딩 UI(기능 명세 1.3.2).
/// 미리 정의된 시스템 로그 라인들을 타자기(typewriter) 효과로 한 글자씩 출력합니다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CliLoadingUI : MonoBehaviour
{
    [Header("표시 대상 텍스트")]
    [SerializeField] private TMP_Text logText;

    [Header("페이드")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("타자기 효과")]
    [SerializeField] private float charactersPerSecond = 40f;
    [SerializeField] private string cursorGlyph = "_";

    [Header("시스템 로그 라인 (순서대로 출력)")]
    [TextArea(1, 3)]
    [SerializeField]
    private string[] logLines =
    {
        "> SOOM_OS v1.0 부팅 중...",
        "[OK] 생명 유지 장치 점검 완료",
        "[OK] 대기권 진입 각도 계산 완료",
        "[WARN] 추진 엔진 출력 불안정",
        "[ERROR] 착륙 시퀀스 강제 실행",
        "> 비상 착륙 절차를 개시합니다_",
    };

    private Coroutine _typeRoutine;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 로딩 UI를 화면에 표시하고, totalDuration 동안 로그 라인들을 타자기 효과로 출력합니다.
    /// logText가 없으면 조용히 무시합니다(null 가드).
    /// </summary>
    public void PlayBootSequence(float totalDuration)
    {
        gameObject.SetActive(true);
        Fade(1f);

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = StartCoroutine(TypeRoutine(totalDuration));
    }

    /// <summary>로딩 UI를 즉시 숨깁니다.</summary>
    public void Hide()
    {
        if (_typeRoutine != null)
        {
            StopCoroutine(_typeRoutine);
            _typeRoutine = null;
        }
        if (!gameObject.activeInHierarchy) return; // 이미 꺼져 있으면 페이드 코루틴을 돌릴 수 없습니다.
        Fade(0f);
    }

    private IEnumerator TypeRoutine(float totalDuration)
    {
        if (logText == null || logLines == null || logLines.Length == 0) yield break;

        float perLine = totalDuration > 0f ? totalDuration / logLines.Length : 0f;
        float charDelay = charactersPerSecond > 0f ? 1f / charactersPerSecond : 0f;

        string completedLines = string.Empty;
        foreach (var line in logLines)
        {
            var current = new StringBuilder();
            foreach (char c in line)
            {
                current.Append(c);
                logText.text = completedLines + current + cursorGlyph;
                if (charDelay > 0f) yield return new WaitForSeconds(charDelay);
            }
            completedLines += line + "\n";
            logText.text = completedLines;

            // 이번 줄에 배정된 시간 중 타이핑에 쓰지 않은 나머지는 대기합니다.
            float typedTime = charDelay * line.Length;
            float remain = perLine - typedTime;
            if (remain > 0f) yield return new WaitForSeconds(remain);
        }

        _typeRoutine = null;
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

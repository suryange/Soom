using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 우주선 외부 씬에서 사용하는 공용 지시문/안내 문구 HUD.
/// "오아시스로 가야 함을 알려주는 단서 + 호흡능력을 통해 길을 찾으세요", "숨의 호흡능력은...",
/// "길라잡이" 같은 짧은 안내 텍스트를 그때그때 띄우는 용도. 씬에 하나만 존재하는 싱글턴이며,
/// 다른 스크립트에서는 MissionGuideTextUI.Instance?.ShowMessage(...) 형태로 null 가드 후 호출한다.
/// </summary>
public class MissionGuideTextUI : MonoBehaviour
{
    public static MissionGuideTextUI Instance { get; private set; }

    [Header("Text Slot")]
    [SerializeField] private TMP_Text messageText;

    [Header("Fade")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Auto Hide")]
    [Tooltip("0 이하로 두면 Hide()를 직접 호출하기 전까지 계속 표시됩니다.")]
    [SerializeField] private float defaultDisplayDuration = 5f;

    private Coroutine fadeRoutine;
    private Coroutine autoHideRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!group) group = GetComponent<CanvasGroup>();
        if (group) group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>기본 지속 시간(defaultDisplayDuration)만큼 문구를 표시한다.</summary>
    public void ShowMessage(string message)
    {
        ShowMessage(message, defaultDisplayDuration);
    }

    /// <summary>지정한 시간(초) 동안 문구를 표시한다. displayDuration이 0 이하면 자동으로 숨기지 않는다.</summary>
    public void ShowMessage(string message, float displayDuration)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (messageText != null) messageText.text = message;

        gameObject.SetActive(true);
        Fade(1f);

        if (autoHideRoutine != null) StopCoroutine(autoHideRoutine);
        if (displayDuration > 0f) autoHideRoutine = StartCoroutine(AutoHideAfter(displayDuration));
    }

    public void Hide()
    {
        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }
        if (!gameObject.activeInHierarchy) return;
        Fade(0f);
    }

    private IEnumerator AutoHideAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Fade(0f);
        autoHideRoutine = null;
    }

    private void Fade(float target)
    {
        if (group == null)
        {
            if (target <= 0f) gameObject.SetActive(false);
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;
        if (target <= 0f) gameObject.SetActive(false);
        fadeRoutine = null;
    }
}

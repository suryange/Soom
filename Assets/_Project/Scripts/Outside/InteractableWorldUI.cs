using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 감지된 사물(IInteractable)의 InteractableDataSO 데이터를 읽어 표시하는 재사용 가능한
/// 월드 스페이스 UI. "UNKNOWN DEVICE DETECTED / Origin: Unknown" 같은 감지 텍스트를
/// objectName / descriptionText로 렌더링한다. 카메라를 향해 계속 정면을 바라보도록
/// FaceCamera 컴포넌트와 함께 사용하는 것을 전제로 한다(같은 오브젝트에 부착).
/// 호출부(IInteractable 구현체)는 ShowUI()/HideUI()에서 Show(data)/Hide()만 호출하면 된다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class InteractableWorldUI : MonoBehaviour
{
    [Header("Text Slots")]
    [SerializeField] private TMP_Text objectNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Fade")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeDuration = 0.2f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
    }

    /// <summary>InteractableDataSO의 objectName/descriptionText를 반영해 패널을 표시한다.</summary>
    public void Show(InteractableDataSO data)
    {
        if (data != null)
        {
            if (objectNameText != null) objectNameText.text = data.objectName;
            if (descriptionText != null) descriptionText.text = data.descriptionText;
        }

        gameObject.SetActive(true);
        Fade(1f);
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy) return; // 이미 꺼져 있으면 페이드 코루틴을 돌릴 수 없음
        Fade(0f);
    }

    private void Fade(float target)
    {
        if (group == null)
        {
            // CanvasGroup이 없는 예외적인 경우에도 최소한 표시/숨김은 동작하도록 폴백
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

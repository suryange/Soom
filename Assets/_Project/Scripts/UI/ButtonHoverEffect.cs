using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [Tooltip("레이가 닿았을 때 커질 목표 크기 비율")]
    public Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1.1f);
    
    [Tooltip("크기가 변하는 속도")]
    public float scaleSpeed = 10f;

    private Vector3 originalScale;
    private Vector3 targetScale;

    private void Start()
    {
        // 시작 시점의 원래 크기를 저장
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    private void Update()
    {
        // 현재 크기와 목표 크기가 다를 경우 부드럽게(Lerp) 크기를 변경
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }
    }

    /// <summary>
    /// 레이(포인터)가 UI 요소 위로 들어왔을 때 호출
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = hoverScale;
    }

    /// <summary>
    /// 레이(포인터)가 UI 요소 밖으로 나갔을 때 호출
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }
    
    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 크기를 강제로 원상복구 (버그 방지)
        transform.localScale = originalScale;
        targetScale = originalScale;
    }
}
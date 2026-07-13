using UnityEngine;

/// <summary>
/// Screen Space 감지 원을 대상의 월드 위치에 맞춘다.
/// 표시 여부는 HologramMessage가 소유하고 이 컴포넌트는 위치만 갱신한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DetectionIndicatorTracker : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform target;
    [SerializeField] private RectTransform indicatorRect;
    [SerializeField] private Vector3 targetLocalOffset;

    public void Configure(
        Camera camera,
        Transform trackedTarget,
        RectTransform trackedIndicator,
        Vector3 localOffset)
    {
        targetCamera = camera;
        target = trackedTarget;
        indicatorRect = trackedIndicator;
        targetLocalOffset = localOffset;
    }

    private void LateUpdate()
    {
        if (targetCamera == null || target == null || indicatorRect == null)
            return;

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(target.TransformPoint(targetLocalOffset));
        bool isInFront = screenPoint.z > 0f;

        if (indicatorRect.gameObject.activeSelf != isInFront)
            indicatorRect.gameObject.SetActive(isInFront);

        if (isInFront)
            indicatorRect.position = new Vector3(screenPoint.x, screenPoint.y, indicatorRect.position.z);
    }
}

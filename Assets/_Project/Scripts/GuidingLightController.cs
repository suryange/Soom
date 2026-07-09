using System.Collections;
using UnityEngine;

public class GuidingLightController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 2.0f;           // 빛무리의 이동 속도
    public float waypointTolerance = 0.5f; // 목적지 도달 판정 거리

    [Header("Light Settings")]
    [Tooltip("빛 강도를 제어할 자식 Light. 비워두면 자식 오브젝트에서 자동으로 찾는다.")]
    public Light guidingLight;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;
    private bool isMoving = false;

    private float baseLightIntensity = 1f;
    private Coroutine lightFadeRoutine;

    void Awake()
    {
        if (guidingLight == null) guidingLight = GetComponentInChildren<Light>();
        if (guidingLight != null) baseLightIntensity = guidingLight.intensity;
    }

    void Update()
    {
        // 이동 상태가 아니거나 웨이포인트가 없으면 대기
        if (!isMoving || waypoints == null || waypoints.Length == 0) return;

        // 현재 향해야 할 웨이포인트 타겟 설정
        Transform target = waypoints[currentWaypointIndex];

        // 타겟을 향해 부드럽게 이동
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // 타겟에 충분히 가까워졌다면 다음 웨이포인트로 인덱스 증가
        if (Vector3.Distance(transform.position, target.position) < waypointTolerance)
        {
            currentWaypointIndex++;

            // 모든 웨이포인트를 다 돌았다면 이동 종료 (추후 소멸 로직 추가 가능)
            if (currentWaypointIndex >= waypoints.Length)
            {
                isMoving = false;
                // Destroy(gameObject, 2f); // 2초 뒤 서서히 사라짐
            }
        }
    }

    // ClueObject에서 스폰 직후 경로를 주입해줄 퍼블릭 메서드
    public void StartGuiding(Transform[] pathWaypoints)
    {
        waypoints = pathWaypoints;
        currentWaypointIndex = 0;
        isMoving = true;
    }

    // =========================================================
    // 등불 빛 강도 제어 (모래바람 구역 연출 등에서 호출)
    // =========================================================

    /// 등불 빛 강도를 duration(초) 동안 targetIntensity로 서서히 Lerp.
    public void FadeLightIntensity(float targetIntensity, float duration)
    {
        if (guidingLight == null) return;

        if (lightFadeRoutine != null) StopCoroutine(lightFadeRoutine);
        lightFadeRoutine = StartCoroutine(FadeLightRoutine(targetIntensity, duration));
    }

    /// 빛 강도를 0으로 서서히 Lerp (모래폭풍 연출 진입 시 사용).
    public void FadeOutLight(float duration)
    {
        FadeLightIntensity(0f, duration);
    }

    /// 빛 강도를 원래(Awake 시점 캘리브레이션) 값으로 서서히 복구 (모래폭풍 해제 시 사용).
    public void RestoreLightIntensity(float duration)
    {
        FadeLightIntensity(baseLightIntensity, duration);
    }

    private IEnumerator FadeLightRoutine(float targetIntensity, float duration)
    {
        if (guidingLight == null) yield break;

        float startIntensity = guidingLight.intensity;

        if (duration <= 0f)
        {
            guidingLight.intensity = targetIntensity;
            lightFadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            guidingLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, elapsed / duration);
            yield return null;
        }

        guidingLight.intensity = targetIntensity;
        lightFadeRoutine = null;
    }
}
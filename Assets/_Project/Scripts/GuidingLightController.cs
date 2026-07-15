using System.Collections;
using UnityEngine;

public class GuidingLightController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 2.2f;           // 플레이어 기본 이동 속도와 비슷하게 유지
    public float waypointTolerance = 0.35f; // 좁은 코너에서도 다음 지점을 건너뛰지 않도록 설정
    [Tooltip("플레이어보다 이 거리 이상 앞서면 등불이 기다린다. 0 이하면 사용하지 않는다.")]
    public float maxLeadDistance = 7f;
    [Tooltip("기다리던 등불이 다시 이동을 시작하는 거리. maxLeadDistance보다 작게 둔다.")]
    public float resumeLeadDistance = 4.5f;
    [Tooltip("이동 방향을 바라보는 회전 속도(도/초). 구체 외형이어도 후속 파티클 방향에 사용한다.")]
    public float turnSpeed = 360f;

    [Header("Sine Movement")]
    [Tooltip("활성화하면 Waypoint 기준 경로를 따라가면서 글로벌 Y축 위아래로 사인 곡선을 그린다.")]
    public bool useSineMovement = true;
    [Min(0f)]
    [Tooltip("글로벌 Y축 기준 위아래 최대 거리(월드 단위). 0이면 직선으로 이동한다.")]
    public float sineAmplitude = 0.75f;
    [Min(0.01f)]
    [Tooltip("사인 곡선 한 주기의 길이(월드 단위). 값이 클수록 곡선의 너비가 넓어진다.")]
    public float sineWavelength = 5f;

    [Header("Light Settings")]
    [Tooltip("빛 강도를 제어할 자식 Light. 비워두면 자식 오브젝트에서 자동으로 찾는다.")]
    public Light guidingLight;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;
    private bool isMoving = false;
    private bool isWaitingForPlayer = false;
    private Transform playerTransform;
    private Vector3 pathPosition;
    private float travelledPathDistance;

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

        UpdatePlayerWaitState();
        if (isWaitingForPlayer) return;

        // 현재 향해야 할 웨이포인트 타겟 설정
        Transform target = waypoints[currentWaypointIndex];
        if (target == null)
        {
            AdvanceWaypoint();
            return;
        }

        // Waypoint를 잇는 기준선은 일정 속도로 진행하고, 실제 표시 위치에만
        // 글로벌 Y축 방향의 사인 오프셋을 적용한다.
        Vector3 pathDirection = target.position - pathPosition;
        Vector3 previousVisualPosition = transform.position;
        Vector3 previousPathPosition = pathPosition;
        pathPosition = Vector3.MoveTowards(pathPosition, target.position, speed * Time.deltaTime);
        travelledPathDistance += Vector3.Distance(previousPathPosition, pathPosition);

        Vector3 visualPosition = pathPosition;
        if (useSineMovement && sineAmplitude > 0f && pathDirection.sqrMagnitude > 0.0001f)
        {
            float wavelength = Mathf.Max(0.01f, sineWavelength);
            float phase = travelledPathDistance * Mathf.PI * 2f / wavelength;
            visualPosition += Vector3.up * (Mathf.Sin(phase) * sineAmplitude);
        }
        transform.position = visualPosition;

        Vector3 motionDirection = visualPosition - previousVisualPosition;
        if (motionDirection.sqrMagnitude > 0.0001f && turnSpeed > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(motionDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        // 흔들리는 실제 위치가 아니라 기준선 위치로 Waypoint 도착을 판정한다.
        if (Vector3.Distance(pathPosition, target.position) < waypointTolerance)
        {
            pathPosition = target.position;
            AdvanceWaypoint();
        }
    }

    // ClueObject에서 스폰 직후 경로를 주입해줄 퍼블릭 메서드
    public void StartGuiding(Transform[] pathWaypoints)
    {
        waypoints = pathWaypoints;
        currentWaypointIndex = 0;
        isWaitingForPlayer = false;
        playerTransform = Camera.main != null ? Camera.main.transform : null;
        isMoving = waypoints != null && waypoints.Length > 0;
        pathPosition = transform.position;
        travelledPathDistance = 0f;

        if (!isMoving)
            Debug.LogWarning("[GuidingLightController] 이동할 Waypoint가 없어 현재 위치에서 대기합니다.", this);
    }

    private void UpdatePlayerWaitState()
    {
        if (maxLeadDistance <= 0f) return;
        if (playerTransform == null && Camera.main != null) playerTransform = Camera.main.transform;
        if (playerTransform == null) return;

        Vector3 offset = transform.position - playerTransform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (!isWaitingForPlayer && distance >= maxLeadDistance)
            isWaitingForPlayer = true;
        else if (isWaitingForPlayer && distance <= Mathf.Min(resumeLeadDistance, maxLeadDistance))
            isWaitingForPlayer = false;
    }

    private void AdvanceWaypoint()
    {
        currentWaypointIndex++;
        if (waypoints == null || currentWaypointIndex >= waypoints.Length)
        {
            isMoving = false;
            isWaitingForPlayer = false;
            transform.position = pathPosition;
        }
    }

    private void OnValidate()
    {
        sineAmplitude = Mathf.Max(0f, sineAmplitude);
        sineWavelength = Mathf.Max(0.01f, sineWavelength);
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

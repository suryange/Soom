using UnityEngine;

public class GuidingLightController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 2.0f;           // 빛무리의 이동 속도
    public float waypointTolerance = 0.5f; // 목적지 도달 판정 거리

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;
    private bool isMoving = false;

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
}
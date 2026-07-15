using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 여우와의 조우 (명세 5.6) — 동료가 된 여우가 플레이어를 따라다니게 하는 추종 로직.
///
/// NavMeshAgent가 있고 실제로 NavMesh 위에 있으면(NavMeshAgent.isOnNavMesh) 정식으로
/// SetDestination을 사용해 따라가고, NavMesh가 구워져 있지 않은 경우(대부분의 개발 초기 상태)에는
/// 단순 Transform 보간 추종으로 폴백한다. 두 경로 모두 NullReferenceException이 나지 않도록
/// 매번 null/상태 가드를 거친다.
/// </summary>
public class FoxCompanionFollower : MonoBehaviour
{
    [Header("추종 대상 (비워두면 Camera.main을 사용)")]
    [SerializeField] private Transform player;

    [Header("NavMeshAgent 경로 (옵션 — NavMesh가 없으면 자동으로 폴백)")]
    [SerializeField] private NavMeshAgent agent;
    [Tooltip("플레이어 위치를 다시 목적지로 갱신하는 주기(초)")]
    [SerializeField] private float destinationUpdateInterval = 0.25f;

    [Header("폴백: 단순 Transform 추종")]
    [SerializeField] private float fallbackMoveSpeed = 2f;
    [SerializeField] private float fallbackTurnSpeed = 360f;

    [Header("공통")]
    [Tooltip("플레이어와 이 거리 이하로 가까워지면 더 이상 다가가지 않음 (여우 크기상 2~2.5 권장)")]
    [SerializeField] private float stoppingDistance = 2.2f;
    [Tooltip("모델의 정면(코) 방향이 +Z가 아닐 때 회전 보정(도). 옆을 보고 따라오면 90 또는 -90, " +
             "등을 보이면 180 을 넣는다. 플레이 중 값을 바꿔가며 정면이 맞을 때를 찾으면 된다.")]
    [SerializeField] private float facingYawOffset = 0f;
    [Tooltip("이 여유 이내로 거리가 맞으면 재배치하지 않음(미세 떨림 방지). 보통 0.2~0.4")]
    [SerializeField] private float keepDistanceDeadzone = 0.3f;

    [Header("이동 애니메이션 (옵션 — Fox_Encounter 컨트롤러의 Walk/Joy 상태)")]
    [Tooltip("걷기/정지 상태를 재생할 Animator. 비워두면 애니메이션 없이 이동만 한다.")]
    [SerializeField] private Animator foxAnimator;
    [Tooltip("이동 중 재생할 상태 이름 (Action5_Walking 클립)")]
    [SerializeField] private string movingStateName = "Walk";
    [Tooltip("멈춰 있을 때 재생할 상태 이름 (Action4_Standing_Happy 클립)")]
    [SerializeField] private string idleStateName = "Joy";
    [Tooltip("걷기↔정지 전환 크로스페이드 시간(초)")]
    [SerializeField] private float animCrossFade = 0.2f;

    private bool _isFollowing;
    private float _destinationTimer;

    // 이동 애니메이션 상태 변화 감지용 (매 프레임 재생 호출을 막기 위함)
    private bool _animStateInitialized;
    private bool _animMoving;

    /// <summary>동료 되기(5.6)가 확정되면 컨트롤러가 호출 — 추종을 시작한다.</summary>
    public void BeginFollowing()
    {
        if (!player) player = ResolvePlayerTransform();

        if (agent != null)
        {
            // NavMesh가 실제로 존재해 여우 위치를 샘플링할 수 있을 때만 정식 NavMeshAgent를 켠다.
            // NavMesh가 안 구워진 상태에서 에이전트를 켜면 Transform을 점유해 아래의 폴백 추종
            // (transform.position 직접 이동)과 충돌하고 콘솔에 'not on NavMesh' 에러가 남으므로,
            // 이 경우엔 에이전트를 꺼둔 채 단순 Transform 추종으로 넘어간다.
            bool hasNavMesh = NavMesh.SamplePosition(transform.position, out _, 2f, NavMesh.AllAreas);
            agent.enabled = hasNavMesh;
            if (hasNavMesh && agent.isOnNavMesh) agent.stoppingDistance = stoppingDistance;
        }

        _isFollowing = true;
        _destinationTimer = 0f;
    }

    public void StopFollowing()
    {
        _isFollowing = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
        UpdateLocomotionAnimation(false);
    }

    private void Update()
    {
        if (!_isFollowing || player == null) return;

        // 플레이어와의 수평 거리/방향
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        // 유지해야 할 링(반경 stoppingDistance) 위의 목표 지점.
        // 너무 멀면 이 지점이 플레이어 쪽(앞)이라 다가가고, 너무 가까우면 반대쪽(뒤)이라 물러난다.
        Vector3 dirFromPlayer = distance > 0.0001f ? -toPlayer / distance : -transform.forward;
        Vector3 ringTarget = player.position + dirFromPlayer * stoppingDistance;
        ringTarget.y = transform.position.y;

        // 현재 거리가 목표에서 데드존 이상 벗어났을 때만 재배치(미세 떨림 방지).
        bool repositioning = Mathf.Abs(distance - stoppingDistance) > keepDistanceDeadzone;
        UpdateLocomotionAnimation(repositioning);

        // 항상 플레이어를 정면으로 바라보게 회전한다(모델 전방축 보정 포함).
        // 멈춰 있을 때도, 뒤로 물러날 때도 계속 플레이어를 향한다.
        if (distance > 0.0001f)
        {
            Vector3 dir = toPlayer / distance;
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, facingYawOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, fallbackTurnSpeed * Time.deltaTime);
        }

        if (!repositioning) return;

        // NavMeshAgent가 실제로 NavMesh 위에서 활성화되어 있을 때만 정식 경로를 사용한다.
        // (씬에 NavMesh가 아직 구워져 있지 않으면 isOnNavMesh가 항상 false이므로 자동으로
        // 아래의 단순 Transform 추종 폴백으로 넘어간다.)
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.updateRotation = false; // 회전은 위에서 직접 처리(항상 플레이어를 향함)
            agent.stoppingDistance = 0f;  // 링 지점 자체가 목표라 0
            _destinationTimer += Time.deltaTime;
            if (_destinationTimer >= destinationUpdateInterval)
            {
                _destinationTimer = 0f;
                agent.SetDestination(ringTarget);
            }
        }
        else
        {
            // 폴백: 링 지점까지 직접 보간(앞으로 다가가거나 뒤로 물러남).
            transform.position = Vector3.MoveTowards(transform.position, ringTarget, fallbackMoveSpeed * Time.deltaTime);
        }
    }

    /// <summary>이동/정지 상태가 바뀔 때만 걷기/정지 상태로 크로스페이드한다.</summary>
    private void UpdateLocomotionAnimation(bool moving)
    {
        if (foxAnimator == null || foxAnimator.runtimeAnimatorController == null) return;
        if (_animStateInitialized && _animMoving == moving) return;

        _animStateInitialized = true;
        _animMoving = moving;

        string state = moving ? movingStateName : idleStateName;
        if (!string.IsNullOrEmpty(state) && foxAnimator.HasState(0, Animator.StringToHash(state)))
            foxAnimator.CrossFadeInFixedTime(state, animCrossFade);
    }

    private static Transform ResolvePlayerTransform()
    {
        // "Player" 태그가 프로젝트에 등록되어 있지 않을 수 있어 FindGameObjectWithTag는
        // 사용하지 않는다(등록되지 않은 태그로 호출하면 예외가 발생함). 대신 FaceCamera와
        // 동일하게 Camera.main(플레이어 HMD 카메라)을 추종 기준점으로 사용한다.
        return Camera.main != null ? Camera.main.transform : null;
    }
}

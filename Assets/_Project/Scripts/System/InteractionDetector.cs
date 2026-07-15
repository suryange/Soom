using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 특정 시야각과 반경 내에 있는 모든 IInteractable 오브젝트를 감지하고 UI를 제어합니다.
/// </summary>
public class InteractionDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("시선 및 감지의 기준점 (Main Camera를 연결)")]
    [SerializeField] private Transform viewOrigin;

    [Tooltip("상호작용 가능한 물체를 감지할 최대 반경")]
    [SerializeField] private float detectionRadius = 5f;

    [Tooltip("물체를 감지할 플레이어의 시야각")]
    [Range(0f, 180f)]
    [SerializeField] private float viewAngle = 90f;

    [Tooltip("상호작용 가능한 오브젝트가 속한 레이어")]
    [SerializeField] private LayerMask interactableLayer;

    // 현재 시야 범위 내에 존재하여 UI가 켜져 있는 오브젝트들을 추적하는 셋
    private HashSet<IInteractable> interactablesInRange = new HashSet<IInteractable>();

    // 범위 내의 여러 오브젝트 중, 플레이어가 트리거를 당겼을 때 실제 조준/상호작용할 중심 타겟
    private IInteractable currentTarget;

    /// <summary>
    /// InteractionManager가 최종 명령을 내릴 때 참고할 시선 중심의 타겟 프로퍼티
    /// </summary>
    public IInteractable CurrentTarget => currentTarget;

    private void Start()
    {
        if (viewOrigin == null)
        {
            viewOrigin = transform;
        }
    }

    private void Update()
    {
        DetectInteractablesInView();
    }

    /// <summary>
    /// 매 프레임 플레이어 시야 영역 내의 모든 오브젝트를 감지하고 UI 상태를 일괄 업데이트합니다.
    /// </summary>
    private void DetectInteractablesInView()
    {
        // 단서 감지는 자유 이동 상태에서만 허용한다. 다른 상태로 전환되면
        // 이미 표시 중인 감지 UI도 즉시 정리한다.
        if (PlayerStateManager.Instance != null &&
            PlayerStateManager.Instance.CurrentState != PlayerState.Idle &&
            PlayerStateManager.Instance.CurrentState != PlayerState.Move)
        {
            HideAllDetectedUI();
            return;
        }

        // 플레이어 주변 반경 내에 있는 지정 레이어의 모든 콜라이더를 1차 검출
        Collider[] colliders = Physics.OverlapSphere(viewOrigin.position, detectionRadius, interactableLayer);

        // 이번 프레임에 시야각과 거리 조건을 모두 만족한 오브젝트들을 담을 임시 셋
        HashSet<IInteractable> currentFrameInteractables = new HashSet<IInteractable>();

        IInteractable bestTarget = null;
        float minAngle = float.MaxValue;

        // 1차 검출된 오브젝트들이 플레이어의 시야각 내에 있는지 2차 필터링
        foreach (var col in colliders)
        {
            // ClueObject처럼 Collider는 모델 자식에, IInteractable은 루트에 있는
            // 계층도 감지할 수 있어야 한다.
            IInteractable interactable = col.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            // 플레이어 시선 정면과 오브젝트를 향한 방향 벡터 사이의 각도 계산
            Vector3 directionToTarget = (col.transform.position - viewOrigin.position).normalized;
            float angle = Vector3.Angle(viewOrigin.forward, directionToTarget);

            // 설정한 시야각 절반 범위 내에 들어와 있다면
            if (angle <= viewAngle * 0.5f)
            {
                currentFrameInteractables.Add(interactable);

                // 여러 개가 동시에 보여도, 트리거를 누를 때 선택될 시선 중심과 가장 가까운 1개를 판정
                if (angle < minAngle)
                {
                    minAngle = angle;
                    bestTarget = interactable;
                }
            }
        }

        // UI 갱신 로직 (개수 상관없이 독립적으로 동작)

        // 시야에 잘 있다가 이번 프레임에 범위를 벗어난 물체들 -> HideUI()
        foreach (var oldInteractable in interactablesInRange)
        {
            if (!currentFrameInteractables.Contains(oldInteractable))
            {
                oldInteractable.HideUI();
            }
        }

        // 새로 시야 범위 안으로 들어온 물체들 -> ShowUI()
        foreach (var newInteractable in currentFrameInteractables)
        {
            if (!interactablesInRange.Contains(newInteractable))
            {
                newInteractable.ShowUI();
            }
        }

        // 데이터를 다음 프레임을 위해 저장 및 최종 상호작용 대상 업데이트
        interactablesInRange = currentFrameInteractables;
        currentTarget = bestTarget;
    }

    private void HideAllDetectedUI()
    {
        foreach (IInteractable interactable in interactablesInRange)
        {
            interactable.HideUI();
        }

        interactablesInRange.Clear();
        currentTarget = null;
    }

    /// <summary>
    /// 에디터의 Scene 뷰에서 기획에 맞는 시야각과 거리를 눈으로 보며 조절할 수 있도록 디버그 라인을 그립니다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (viewOrigin == null) return;

        // 감지 반경 그리기 (반투명 구체)
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawWireSphere(viewOrigin.position, detectionRadius);

        // 좌우 시야각 가이드라인 그리기
        Vector3 leftRayDirection = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * viewOrigin.forward;
        Vector3 rightRayDirection = Quaternion.Euler(0, viewAngle * 0.5f, 0) * viewOrigin.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(viewOrigin.position, leftRayDirection * detectionRadius);
        Gizmos.DrawRay(viewOrigin.position, rightRayDirection * detectionRadius);
    }
}

using UnityEngine;

/// <summary>
/// 모래바람 구역 진입 감지 (기능 명세 4.1).
/// Box Collider(Is Trigger) 구역에 플레이어가 들어오면
///  1) PlayerStateManager.SetMissionZone(true) 호출로 미션 대기 상태 진입
///  2) SandstormController에 구역 진입을 통지해 챕터/구역 텍스트 UI 표시 및 폭풍 연출 시작
/// 를 담당한다. 실제 폭풍 연출/호흡 시퀀스 로직은 전부 SandstormController가 소유하고,
/// 이 컴포넌트는 순수하게 "트리거 감지 + 통지" 역할만 한다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SandstormZoneTrigger : MonoBehaviour
{
    [Header("연출 대상")]
    [Tooltip("이 구역이 시작할 모래폭풍 연출 컨트롤러.")]
    [SerializeField] private SandstormController sandstormController;

    [Header("트리거 설정")]
    [Tooltip("플레이어(XR Origin 등)에 부여된 태그. Project Settings > Tags and Layers에 등록되어 있어야 한다.")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("한 번 진입 후 다시 발동하지 않도록 막을지 여부.")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasEntered = false;

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasEntered && triggerOnce) return;
        if (other == null || !other.CompareTag(playerTag)) return;

        hasEntered = true;

        // 4.1: 미션 구역 진입 상태로 전이 (PlayerStateManager 싱글턴 존재 여부 방어)
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.SetMissionZone(true);
        }

        // 4.1~4.2: 챕터/구역 UI 노출 및 모래폭풍 연출 시작 통지
        if (sandstormController != null)
        {
            sandstormController.EnterZone();
        }
        else
        {
            Debug.LogWarning("[SandstormZoneTrigger] sandstormController가 연결되어 있지 않습니다.", this);
        }
    }

    // 씬 뷰에서 트리거 영역을 쉽게 확인할 수 있도록 기즈모 표시
    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = new Color(0.9f, 0.7f, 0.3f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.9f, 0.7f, 0.3f, 0.8f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}

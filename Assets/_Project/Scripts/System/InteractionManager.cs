using UnityEngine;

/// <summary>
/// 컨트롤러를 통해 선택된 오브젝트와의 상호작용 세션을 관리하는 총괄 매니저입니다.
/// 시퀀스 다이어그램에 따라 상태 변경은 각 사물이 직접 수행합니다.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Current Status")]
    [SerializeField] private MonoBehaviour activeInteractableObject;
    private IInteractable activeInteractable;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 플레이어가 컨트롤러 레이캐스트로 UI나 사물을 클릭(트리거)했을 때 호출됩니다.
    /// (XRI의 SelectEntered 이벤트나 UI Button의 OnClick에 연결)
    /// </summary>
    /// <param name="target">클릭한 대상 오브젝트</param>
    public void BeginInteraction(IInteractable target)
    {
        // 이미 다른 상호작용이 진행 중이라면 중복 실행 방지
        if (activeInteractable != null)
        {
            Debug.LogWarning("[InteractionManager] 이미 다른 상호작용이 진행 중입니다.");
            return;
        }

        if (target != null)
        {
            // 현재 타겟 등록
            activeInteractable = target;
            activeInteractableObject = target as MonoBehaviour; // 인스펙터 확인용

            Debug.Log($"[InteractionManager] 상호작용 시작: {target.GetType().Name}");

            // 타겟에게 상호작용 시작을 알림
            activeInteractable.OnInteractBegin();
        }
    }

    /// <summary>
    /// 대상 사물에 대한 모든 인터랙션 시퀀스가 완전히 끝났을 때
    /// 해당 사물이 직접 호출하여 세션을 종료합니다.
    /// </summary>
    public void EndInteraction()
    {
        if (activeInteractable != null)
        {
            Debug.Log($"[InteractionManager] 상호작용 최종 종료: {activeInteractable.GetType().Name}");

            // 사물에게 상호작용이 완전히 종료되었음을 알림
            activeInteractable.OnInteractEnd();

            // 타겟 초기화
            activeInteractable = null;
            activeInteractableObject = null;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 컨트롤러 트리거 버튼 입력을 InteractionManager.BeginInteraction으로 연결하는 브리지.
/// PlayerInputHandler(B버튼 -> PlayerStateManager)와 같은 패턴으로, InputActionProperty를
/// 인스펙터에서 XRI 트리거 액션(예: XRI RightHand Interaction/Activate)에 연결해서 사용한다.
/// </summary>
public class TriggerInteractionInputBridge : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("컨트롤러 트리거(Activate) 버튼 액션. 인스펙터에서 XRI 입력 액션을 연결하세요.")]
    public InputActionProperty triggerAction;

    [Header("Detection")]
    [Tooltip("비워두면 씬에서 InteractionDetector를 자동으로 찾습니다.")]
    public InteractionDetector detector;

    private void OnEnable()
    {
        if (detector == null)
        {
#if UNITY_2023_1_OR_NEWER
            detector = FindFirstObjectByType<InteractionDetector>();
#else
            detector = FindObjectOfType<InteractionDetector>();
#endif
        }

        if (triggerAction.action != null)
        {
            triggerAction.action.Enable();
            triggerAction.action.performed += OnTriggerPressed;
        }
    }

    private void OnDisable()
    {
        if (triggerAction.action != null)
        {
            triggerAction.action.performed -= OnTriggerPressed;
            triggerAction.action.Disable();
        }
    }

    // 트리거가 눌렸을 때 현재 조준 중인 대상으로 상호작용 시작
    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (InteractionManager.Instance == null || detector == null) return;

        IInteractable target = detector.CurrentTarget;
        if (target == null) return;

        Debug.Log("트리거 버튼 눌림 감지 → 상호작용 시작");
        InteractionManager.Instance.BeginInteraction(target);
    }
}

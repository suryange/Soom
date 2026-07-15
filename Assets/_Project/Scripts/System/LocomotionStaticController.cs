using UnityEngine;

public class LocomotionStateController : MonoBehaviour
{
    [Header("Locomotion Components")]
    public MonoBehaviour[] locomotionComponents;

    private PlayerStateManager subscribedStateManager;

    private void Start()
    {
        BindStateManager();
        ApplyCurrentState();
    }

    private void Update()
    {
        // PlayerStateManager가 씬 전환 등으로 나중에 생성되거나 교체될 수 있다.
        if (subscribedStateManager != PlayerStateManager.Instance)
        {
            BindStateManager();
            ApplyCurrentState();
        }
    }

    private void OnDestroy()
    {
        UnbindStateManager();
    }

    private void BindStateManager()
    {
        UnbindStateManager();

        subscribedStateManager = PlayerStateManager.Instance;
        if (subscribedStateManager != null)
            subscribedStateManager.OnStateEnter += HandleStateEnter;
    }

    private void UnbindStateManager()
    {
        if (subscribedStateManager != null)
            subscribedStateManager.OnStateEnter -= HandleStateEnter;

        subscribedStateManager = null;
    }

    private void ApplyCurrentState()
    {
        // 상태 매니저가 아직 없다면 기본 플레이 상태로 보고 이동을 허용한다.
        PlayerState state = subscribedStateManager != null
            ? subscribedStateManager.CurrentState
            : PlayerState.Idle;

        HandleStateEnter(state);
    }

    private void HandleStateEnter(PlayerState state)
    {
        bool shouldEnableLocomotion =
            state != PlayerState.Interact &&
            state != PlayerState.BreathingActive;

        SetLocomotionActive(shouldEnableLocomotion);
    }

    private void SetLocomotionActive(bool isActive)
    {
        if (locomotionComponents == null)
            return;

        foreach (MonoBehaviour component in locomotionComponents)
        {
            if (component != null && component.enabled != isActive)
                component.enabled = isActive;
        }

        Debug.Log($"[LocomotionStateController] 이동 기능 활성화 여부: {isActive}", this);
    }
}

using UnityEngine;

public class LocomotionStateController : MonoBehaviour
{
    [Header("Locomotion Components")]
    public MonoBehaviour[] locomotionComponents;

    private void Start()
    {
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter += HandleStateEnter;
        }
    }

    private void OnDestroy()
    {
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter -= HandleStateEnter;
        }
    }

    // 상태가 변할 때마다 자동으로 호출됨
    private void HandleStateEnter(PlayerState state)
    {
        // 호흡 미션 중일 때 이동 불가
        if (state == PlayerState.BreathingActive)
        {
            SetLocomotionActive(false);
        }
        else
        {
            // 그 외의 상태에서는 이동 허용
            SetLocomotionActive(true);
        }
    }

    private void SetLocomotionActive(bool isActive)
    {
        foreach (var comp in locomotionComponents)
        {
            if (comp != null)
            {
                // 해당 컴포넌트의 체크박스를 끄거나 켭니다.
                comp.enabled = isActive;
            }
        }

        Debug.Log($"[LocomotionStateController] 이동 기능 활성화 여부: {isActive}");
    }
}
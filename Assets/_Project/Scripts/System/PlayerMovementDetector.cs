using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementDetector : MonoBehaviour
{
    [Header("Input Action")]
    public InputActionProperty moveAction;
    public float moveThreshold = 0.1f;

    private void Update()
    {
        if (PlayerStateManager.Instance == null) return;

        // 조이스틱 입력값 확인
        Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
        bool isMoving = moveInput.magnitude > moveThreshold;

        // PlayerStateManager에 이동 여부 전달
        PlayerStateManager.Instance.SetMoving(isMoving);
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    public InputActionProperty bButtonAction;

    private void OnEnable()
    {
        if (bButtonAction.action != null)
        {
            bButtonAction.action.Enable();
            bButtonAction.action.performed += OnBButtonPressed;
        }
    }

    private void OnDisable()
    {
        if (bButtonAction.action != null)
        {
            bButtonAction.action.performed -= OnBButtonPressed;
            bButtonAction.action.Disable();
        }
    }

    // B버튼이 눌렸을 때 State 변경 (MissionReady <=> BreathingActive)
    private void OnBButtonPressed(InputAction.CallbackContext context)
    {
        if (PlayerStateManager.Instance != null)
        {
            Debug.Log("B버튼 눌림 감지");
            PlayerStateManager.Instance.OnInteractButtonPressed();
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// XRI 레이 호버는 동작하지만 UI Press가 Button 클릭으로 전달되지 않는 경우를 위한 보완 입력 경로.
/// XR 포인터가 이 버튼 위에 있을 때 좌/우 컨트롤러 Trigger를 누르면 Button.onClick을 실행한다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class XRTriggerButtonFallback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const string LeftTriggerPath = "<XRController>{LeftHand}/triggerPressed";
    private const string RightTriggerPath = "<XRController>{RightHand}/triggerPressed";

    private readonly HashSet<int> hoveredXRPointers = new HashSet<int>();

    private Button button;
    private InputAction triggerAction;

    private void Awake()
    {
        button = GetComponent<Button>();

        triggerAction = new InputAction("XR UI Trigger", InputActionType.Button);
        triggerAction.AddBinding(LeftTriggerPath);
        triggerAction.AddBinding(RightTriggerPath);
    }

    private void OnEnable()
    {
        triggerAction.performed += OnTriggerPressed;
        triggerAction.Enable();
    }

    private void OnDisable()
    {
        triggerAction.performed -= OnTriggerPressed;
        triggerAction.Disable();
        hoveredXRPointers.Clear();
    }

    private void OnDestroy()
    {
        triggerAction?.Dispose();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (eventData is TrackedDeviceEventData)
            hoveredXRPointers.Add(eventData.pointerId);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData is TrackedDeviceEventData)
            hoveredXRPointers.Remove(eventData.pointerId);
    }

    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (hoveredXRPointers.Count == 0 || button == null || !button.IsActive() || !button.interactable)
            return;

        button.onClick.Invoke();
    }
}

using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PoC stand-in for the breathing pipeline. Pushes Clarity up/down so you can SEE the
/// sandstorm clear/thicken without any sensor.
///   - In headset: right thumbstick up = clearer, down = sandstorm.
///   - In editor:  Up / Down arrow keys (new Input System), OR just drag the Clarity
///                 slider on the BreathSignal asset while in Play mode.
///
/// When the real pipeline is ready, delete this component. Your BreathProcessor writes
/// signal.Clarity instead — nothing else changes.
/// </summary>
public class ClarityPoCDriver : MonoBehaviour
{
    [SerializeField] BreathSignalSO signal;
    [Tooltip("Clarity change per second at full input.")]
    [SerializeField] float rate = 0.6f;
    [SerializeField] XRNode controllerNode = XRNode.RightHand;

    void Update()
    {
        if (!signal) return;
        float delta = 0f;

        // In-headset thumbstick (works on Quest Touch via UnityEngine.XR)
        var dev = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
            delta += axis.y;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed)   delta += 1f;
            if (kb.downArrowKey.isPressed) delta -= 1f;
        }
#endif

        if (Mathf.Abs(delta) > 0.001f)
            signal.Clarity = Mathf.Clamp01(signal.Clarity + delta * rate * Time.deltaTime);
    }
}

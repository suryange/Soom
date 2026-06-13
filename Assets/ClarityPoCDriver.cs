using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PoC stand-in for the breathing pipeline. Pushes Clarity up/down so you can SEE the
/// sandstorm clear/thicken without any sensor, AND fakes the discrete breath structure
/// (phase + rep + session) so the UI can be built and tested before the real sensor exists.
///   - In headset: right thumbstick up = inhale (clearer), down = exhale (sandstorm).
///   - In editor:  Up / Down arrow keys.
///
/// Breath model (PoC): pushing up = Inhale, down = Exhale, neutral = Hold/Idle. One rep is
/// counted each time the user finishes an inhale (reached at least `repInhaleTarget` clarity)
/// and starts exhaling. After `repsTotal` reps the session is complete.
///
/// When the real pipeline is ready, delete this component. Your BreathProcessor writes
/// signal.Clarity / signal.Phase and calls events.Raise* — nothing on the UI side changes.
/// </summary>
public class ClarityPoCDriver : MonoBehaviour
{
    [Header("State seam")]
    [SerializeField] BreathSignalSO signal;
    [SerializeField] BreathEventsSO events;

    [Header("Tuning")]
    [Tooltip("Clarity change per second at full input.")]
    [SerializeField] float rate = 0.6f;
    [Tooltip("Reps needed to complete the session (= number of icons lit left→right).")]
    [SerializeField] int repsTotal = 3;
    [Tooltip("Inhale must reach at least this clarity to count the breath as a real rep.")]
    [Range(0f, 1f)] [SerializeField] float repInhaleTarget = 0.85f;
    [Tooltip("Below this input magnitude the breath is treated as neutral (Hold/Idle).")]
    [SerializeField] float neutralDeadzone = 0.05f;
    [SerializeField] XRNode controllerNode = XRNode.RightHand;

    [Header("Guide copy")]
    [TextArea] [SerializeField] string openingGuide = "숨을 깊게 들이쉬고 천천히 내쉬어…";

    int _repsDone;
    bool _reachedInhaleTarget;   // did the current inhale climb high enough to count?
    BreathPhase _lastPhase = BreathPhase.Idle;
    bool _sessionDone;

    void OnEnable()
    {
        _repsDone = 0;
        _reachedInhaleTarget = false;
        _lastPhase = BreathPhase.Idle;
        _sessionDone = false;
    }

    void Start()
    {
        // One persistent coaching line at the top of the session.
        if (events && !string.IsNullOrEmpty(openingGuide)) events.RaiseGuide(openingGuide);
    }

    void Update()
    {
        if (!signal) return;

        float delta = ReadBreathInput();

        // Drive the continuous clarity exactly as before.
        if (Mathf.Abs(delta) > 0.001f)
            signal.Clarity = Mathf.Clamp01(signal.Clarity + delta * rate * Time.deltaTime);

        // Classify the breath phase from input direction + current clarity.
        BreathPhase phase;
        if (delta > neutralDeadzone)       phase = BreathPhase.Inhale;
        else if (delta < -neutralDeadzone) phase = BreathPhase.Exhale;
        else                               phase = signal.Clarity > 0.5f ? BreathPhase.Hold : BreathPhase.Idle;
        signal.Phase = phase;

        // Track whether this inhale climbed high enough to "count". (No need to be inhaling
        // right now — just to have reached the target at some point since the last rep.)
        if (signal.Clarity >= repInhaleTarget)
            _reachedInhaleTarget = true;

        // Rep lands the moment we ENTER an exhale after a deep-enough inhale. We key off
        // "entered Exhale" (not a strict Inhale→Exhale frame pair) because releasing Up before
        // pressing Down inserts a neutral Hold frame in between, which the strict check missed.
        if (!_sessionDone && phase == BreathPhase.Exhale && _lastPhase != BreathPhase.Exhale && _reachedInhaleTarget)
            CompleteRep();

        _lastPhase = phase;
    }

    void CompleteRep()
    {
        _repsDone++;
        _reachedInhaleTarget = false;
        if (events) events.RaiseRep(_repsDone, repsTotal);

        if (_repsDone >= repsTotal)
        {
            _sessionDone = true;
            if (events) events.RaiseSessionComplete();
        }
    }

    float ReadBreathInput()
    {
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
        return delta;
    }
}

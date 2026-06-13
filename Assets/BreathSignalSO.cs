using UnityEngine;

/// <summary>
/// Where in a single breath the user currently is. Continuous-state companion to Clarity:
/// UI that coaches the breath (guide text, gauge) reads this every frame, the same way the
/// sandstorm reads Clarity. Discrete one-shot moments (a rep landed, the session is done)
/// do NOT live here — those go through BreathEventsSO so a per-frame field never has to be
/// polled for "did it just change?".
/// </summary>
public enum BreathPhase { Idle, Inhale, Hold, Exhale }

/// <summary>
/// The single seam between INPUT (breathing / PoC driver) and OUTPUT (sandstorm visuals).
/// Anything that produces a "how stable / clear" value writes Clarity here;
/// anything that visualizes it reads Clarity here. They never reference each other.
///
/// PoC: a ClarityPoCDriver writes this.
/// Later: your breathing pipeline (belt / controller IMU) writes the exact same field,
///        and nothing on the visual side has to change.
///
/// Create one asset: Assets > Create > SOOM > Breath Signal
/// </summary>
[CreateAssetMenu(menuName = "SOOM/Breath Signal", fileName = "BreathSignal")]
public class BreathSignalSO : ScriptableObject
{
    [Tooltip("0 = unstable breathing → full sandstorm.  1 = stable breathing → clear view.")]
    [Range(0f, 1f)] public float Clarity = 1f;

    [Tooltip("Current point in the breath cycle. Coaching UI reads this; the sandstorm ignores it.")]
    public BreathPhase Phase = BreathPhase.Idle;

    // Reset to a known state every Play so editing the asset at runtime doesn't persist.
    void OnEnable()
    {
        Clarity = 0f; // start obscured so you can 'breathe it clear'
        Phase   = BreathPhase.Idle;
    }
}

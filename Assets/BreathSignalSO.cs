using UnityEngine;

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

    // Reset to a known state every Play so editing the asset at runtime doesn't persist.
    void OnEnable() => Clarity = 0f; // start obscured so you can 'breathe it clear'
}

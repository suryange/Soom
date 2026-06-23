using UnityEngine;
using System;

/// <summary>
/// The DISCRETE-event companion to BreathSignalSO. BreathSignalSO carries continuous state
/// (Clarity, Phase) that things poll every frame; this carries one-shot moments that things
/// react to once: a breath rep landed, the whole session succeeded, a guide line should show.
///
/// Same seam philosophy as BreathSignalSO: the INPUT side (PoC driver now, real breath
/// pipeline later) calls the Raise* methods; the OUTPUT side (every UI piece) subscribes to
/// the events. Neither side references the other — they only share this asset. When the real
/// sensor pipeline arrives it raises the same events and no UI changes.
///
/// Create one asset: Assets > Create > SOOM > Breath Events
/// </summary>
[CreateAssetMenu(menuName = "SOOM/Breath Events", fileName = "BreathEvents")]
public class BreathEventsSO : ScriptableObject
{
    /// <summary>A single breath rep was completed successfully. (repsDone, repsTotal)</summary>
    public event Action<int, int> RepSucceeded;

    /// <summary>Every rep in the session landed — fire the "SUCCESS!" celebration.</summary>
    public event Action SessionCompleted;

    /// <summary>Show a transient guide/instruction line for `duration` seconds. (text, duration)</summary>
    public event Action<string, float> MessageRequested;

    /// <summary>Show a persistent coaching line that stays until replaced. (text)</summary>
    public event Action<string> GuideChanged;

    // ---- INPUT side calls these (driver / breath pipeline) ----------------------------
    public void RaiseRep(int repsDone, int repsTotal) => RepSucceeded?.Invoke(repsDone, repsTotal);
    public void RaiseSessionComplete()                => SessionCompleted?.Invoke();
    public void RequestMessage(string text, float duration) => MessageRequested?.Invoke(text, duration);
    public void RaiseGuide(string text)               => GuideChanged?.Invoke(text);

    // C# events on a ScriptableObject keep their subscriber list across Play sessions in the
    // editor (the asset isn't reloaded), which would leak dead listeners. Clear on enable so
    // every Play starts with a clean subscriber list.
    void OnEnable()
    {
        RepSucceeded     = null;
        SessionCompleted = null;
        MessageRequested = null;
        GuideChanged     = null;
    }
}

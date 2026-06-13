using UnityEngine;
using TMPro;

/// <summary>
/// A persistent coaching line that mirrors the current breath phase: "들이쉬어요…" while
/// inhaling, "내쉬어요…" while exhaling, and falls back to a base guide line (set via
/// BreathEventsSO.GuideChanged) when idle. Reads BreathSignalSO.Phase every frame but only
/// pushes text to TextMeshPro when it actually changes — a per-frame string assignment forces
/// a canvas rebuild, which is wasteful on the Quest GPU.
///
/// Unlike WorldPopup this does NOT auto-dismiss; it's the always-there breath coach.
/// </summary>
public class BreathGuidePanel : MonoBehaviour
{
    [Header("State seam")]
    [SerializeField] BreathSignalSO signal;
    [SerializeField] BreathEventsSO events;

    [SerializeField] TextMeshProUGUI label;

    [Header("Phase copy")]
    [SerializeField] string inhaleText = "천천히 들이쉬어요…";
    [SerializeField] string exhaleText = "부드럽게 내쉬어요…";
    [SerializeField] string holdText   = "잠시 멈춰요…";

    string _baseGuide = "";
    BreathPhase _shownPhase = (BreathPhase)(-1); // force first update
    string _shownText;

    void OnEnable()
    {
        if (events) events.GuideChanged += OnGuideChanged;
        _shownPhase = (BreathPhase)(-1);
    }

    void OnDisable()
    {
        if (events) events.GuideChanged -= OnGuideChanged;
    }

    void OnGuideChanged(string text)
    {
        _baseGuide = text;
        _shownPhase = (BreathPhase)(-1); // re-evaluate so an Idle panel shows the new base line
    }

    void Update()
    {
        if (!signal) return;
        if (signal.Phase == _shownPhase) return; // only touch TMP on a real change
        _shownPhase = signal.Phase;
        SetText(TextFor(signal.Phase));
    }

    string TextFor(BreathPhase phase) => phase switch
    {
        BreathPhase.Inhale => inhaleText,
        BreathPhase.Exhale => exhaleText,
        BreathPhase.Hold   => holdText,
        _                  => _baseGuide,
    };

    void SetText(string text)
    {
        if (text == _shownText) return;
        _shownText = text;
        if (label) label.text = text;
    }
}

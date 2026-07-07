using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared circular breathing UI: a central bead grows/shrinks between a small and large outline
/// ring in step with BreathEventsSO.OnBreathValueNormalized, and top slots fill in as
/// OnBreathLoopCompleted lands. OnMissionSuccess plays a small pulse. Callers only ever call
/// Show()/Hide(); nothing here references a driver, only the single new-architecture event SO.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BreathCircleUI : MonoBehaviour
{
    [Header("State seam")]
    [SerializeField] BreathEventsSO events;

    [Header("Outlines")]
    [SerializeField] Image largeCircleOutline;
    [SerializeField] Image smallCircleOutline;
    [SerializeField] Image bead; // central filled circle that scales with the live breath value

    [Header("Bead scale (breath value = 0 .. 1)")]
    [SerializeField] float smallScale = 0.4f;
    [SerializeField] float largeScale = 1f;
    [Tooltip("Smaller = snappier tracking of the breath value. Larger = softer/laggier.")]
    [SerializeField] float smoothTime = 0.12f;

    [Header("Top slots (left → right, filled as loops complete)")]
    [SerializeField] Image[] slotRings;
    [SerializeField] Image[] slotFills;

    [Header("Bead-to-slot travel")]
    [SerializeField] float travelDuration = 0.5f;

    [Header("Mission success pulse")]
    [SerializeField] float pulseScale = 1.25f;
    [SerializeField] float pulseDuration = 0.3f;

    [Header("Fade")]
    [SerializeField] CanvasGroup group;
    [SerializeField] float fadeDuration = 0.3f;

    float _scaleVel;
    float _cachedBreathValue;
    int _slotsFilled;
    Vector3 _beadHomeLocalPos;
    Coroutine _travelRoutine;
    Coroutine _pulseRoutine;
    Coroutine _fadeRoutine;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        if (bead) _beadHomeLocalPos = bead.rectTransform.localPosition;
        EnsureSprites();
    }

    // The editor builder assigns in-memory procedural sprites that don't survive a scene reload
    // (they were never saved as assets, on purpose — no texture files). Regenerate any that came
    // back missing so the circles never degrade to plain white squares.
    void EnsureSprites()
    {
        if (largeCircleOutline && !largeCircleOutline.sprite)
            largeCircleOutline.sprite = CircleSpriteFactory.CreateRing(Color.white, 0.06f);
        if (smallCircleOutline && !smallCircleOutline.sprite)
            smallCircleOutline.sprite = CircleSpriteFactory.CreateRing(Color.white, 0.1f);

        Sprite filled = null;
        if (bead && !bead.sprite)
            bead.sprite = filled = CircleSpriteFactory.CreateFilledCircle(Color.white);

        Sprite ringSprite = null;
        if (slotRings != null)
            foreach (var ring in slotRings)
                if (ring && !ring.sprite)
                {
                    if (!ringSprite) ringSprite = CircleSpriteFactory.CreateRing(Color.white, 0.12f);
                    ring.sprite = ringSprite;
                }
        if (slotFills != null)
            foreach (var fill in slotFills)
                if (fill && !fill.sprite)
                {
                    if (!filled) filled = CircleSpriteFactory.CreateFilledCircle(Color.white);
                    fill.sprite = filled;
                }
    }

    void OnEnable()
    {
        if (events)
        {
            events.OnBreathValueNormalized += OnBreathValue;
            events.OnBreathLoopCompleted += OnLoopCompleted;
            events.OnMissionSuccess += OnMissionSuccess;
        }
    }

    void OnDisable()
    {
        if (events)
        {
            events.OnBreathValueNormalized -= OnBreathValue;
            events.OnBreathLoopCompleted -= OnLoopCompleted;
            events.OnMissionSuccess -= OnMissionSuccess;
        }
    }

    void OnBreathValue(float value)
    {
        _cachedBreathValue = value;
    }

    void Update()
    {
        if (!bead) return;
        // Travel/pulse coroutines own the bead's scale+position while they run; don't fight them.
        if (_travelRoutine != null || _pulseRoutine != null) return;
        float target = Mathf.Lerp(smallScale, largeScale, _cachedBreathValue);
        float current = bead.rectTransform.localScale.x;
        float next = Mathf.SmoothDamp(current, target, ref _scaleVel, smoothTime);
        bead.rectTransform.localScale = Vector3.one * next;
    }

    void OnLoopCompleted(int loopCount)
    {
        int i = _slotsFilled;
        if (slotFills == null || i < 0 || i >= slotFills.Length) return;
        if (_travelRoutine != null) StopCoroutine(_travelRoutine);
        _travelRoutine = StartCoroutine(TravelToSlot(i));
    }

    void OnMissionSuccess()
    {
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(Pulse());
    }

    IEnumerator TravelToSlot(int slotIndex)
    {
        if (!bead || slotFills == null || !slotFills[slotIndex]) yield break;

        var beadRt = bead.rectTransform;
        Vector3 from = _beadHomeLocalPos;
        // Slot fills live several levels deeper (Slots/SlotN/Fill) than the bead, so their
        // localPosition isn't in the same space — convert via world space into the bead's parent.
        Vector3 to = beadRt.parent.InverseTransformPoint(slotFills[slotIndex].rectTransform.position);
        Vector3 startScale = beadRt.localScale;
        // Shrink down to the slot's size on the way up so the bead lands flush on the ring.
        float beadW = beadRt.sizeDelta.x;
        float endScale = beadW > 0f ? slotFills[slotIndex].rectTransform.sizeDelta.x / beadW : smallScale;

        for (float t = 0f; t < travelDuration; t += Time.deltaTime)
        {
            float k = t / travelDuration;
            beadRt.localPosition = Vector3.Lerp(from, to, k);
            beadRt.localScale = Vector3.Lerp(startScale, Vector3.one * endScale, k);
            yield return null;
        }
        beadRt.localPosition = from; // bead resets home for the next rep
        beadRt.localScale = startScale;

        slotFills[slotIndex].enabled = true;
        _slotsFilled = Mathf.Min(_slotsFilled + 1, slotFills.Length);
        _travelRoutine = null;
    }

    IEnumerator Pulse()
    {
        if (!bead) yield break;
        var rt = bead.rectTransform;
        Vector3 baseScale = rt.localScale;
        for (float t = 0f; t < pulseDuration; t += Time.deltaTime)
        {
            float k = t / pulseDuration;
            float punch = 1f + (pulseScale - 1f) * Mathf.Sin(k * Mathf.PI);
            rt.localScale = baseScale * punch;
            yield return null;
        }
        rt.localScale = baseScale;
        _pulseRoutine = null;
    }

    /// <summary>Fade the whole UI in and reset the slots for a fresh session.</summary>
    public void Show()
    {
        ResetSlots();
        gameObject.SetActive(true);
        Fade(1f);
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy) return; // already hidden; can't run a fade coroutine
        Fade(0f);
    }

    void ResetSlots()
    {
        _slotsFilled = 0;
        if (slotFills != null)
            foreach (var fill in slotFills)
                if (fill) fill.enabled = false;
        if (bead)
        {
            bead.rectTransform.localPosition = _beadHomeLocalPos;
            bead.rectTransform.localScale = Vector3.one * smallScale;
        }
    }

    void Fade(float target)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(target));
    }

    IEnumerator FadeTo(float target)
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;
        if (target <= 0f) gameObject.SetActive(false);
        _fadeRoutine = null;
    }
}

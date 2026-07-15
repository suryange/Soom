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

    [Header("XR display")]
    [SerializeField] bool followMainCamera = true;
    [SerializeField] Vector3 cameraLocalPosition = new Vector3(0f, -0.08f, 0.75f);

    [Header("Visibility")]
    [Range(0f, 1f)] [SerializeField] float minimumOutlineAlpha = 0.95f;
    [Range(0f, 1f)] [SerializeField] float minimumSlotAlpha = 0.8f;
    [Range(0f, 1f)] [SerializeField] float minimumBeadAlpha = 1f;

    float _scaleVel;
    [Header("Runtime diagnostics (Read Only)")]
    [SerializeField] bool _eventChannelSubscribed;
    [SerializeField] float _cachedBreathValue;
    [SerializeField] int _receivedLoopCount;

    int _slotsFilled;
    Vector3 _beadHomeLocalPos;
    Coroutine _travelRoutine;
    Coroutine _pulseRoutine;
    Coroutine _fadeRoutine;
    bool _visibleRequested;
    int _observedBreathValueVersion = -1;
    int _observedLoopVersion = -1;

    public bool IsVisibilityRequested => _visibleRequested;
    public bool IsEventChannelSubscribed => _eventChannelSubscribed;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        if (bead) _beadHomeLocalPos = bead.rectTransform.localPosition;
        EnsureSprites();
        EnsureHighVisibility();
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
            _eventChannelSubscribed = true;

            // UI가 비활성 상태였던 동안 발행된 최신 값을 즉시 복구한다.
            _observedBreathValueVersion = events.BreathValueVersion;
            _observedLoopVersion = events.LoopVersion;
            _cachedBreathValue = events.CurrentBreathValue;

            Debug.Log(
                $"[BreathCircleUI] 이벤트 채널 연결 완료. Channel={events.name}, " +
                $"Value={_cachedBreathValue:0.00}, Bead={(bead ? bead.name : "NULL")}", this);
        }
        else
        {
            _eventChannelSubscribed = false;
            Debug.LogError("[BreathCircleUI] BreathEventsChannel 참조가 없습니다.", this);
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

        _eventChannelSubscribed = false;

        // 비활성화되면 Unity가 Coroutine을 중단하므로 다음 세션을 위해 캐시도 비운다.
        _travelRoutine = null;
        _pulseRoutine = null;
        _fadeRoutine = null;
    }

    void OnBreathValue(float value)
    {
        _cachedBreathValue = value;
        if (events) _observedBreathValueVersion = events.BreathValueVersion;
    }

    void Update()
    {
        if (_visibleRequested)
            UpdateCameraPlacement();

        SyncEventChannelSnapshot();

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
        _receivedLoopCount = loopCount;
        if (events) _observedLoopVersion = events.LoopVersion;

        if (slotFills == null || slotFills.Length == 0) return;

        int i = Mathf.Clamp(loopCount - 1, 0, slotFills.Length - 1);

        // 이벤트가 연출 시간보다 빠르게 와도 이전 완료 슬롯은 누락하지 않는다.
        for (int completedIndex = 0; completedIndex < i; completedIndex++)
        {
            if (slotFills[completedIndex])
                slotFills[completedIndex].enabled = true;
        }
        _slotsFilled = Mathf.Max(_slotsFilled, i);

        if (_travelRoutine != null) StopCoroutine(_travelRoutine);
        _travelRoutine = StartCoroutine(TravelToSlot(i));
        Debug.Log($"[BreathCircleUI] 호흡 UI {loopCount}회차 갱신.", this);
    }

    void SyncEventChannelSnapshot()
    {
        if (!events) return;

        if (_observedBreathValueVersion != events.BreathValueVersion)
        {
            _observedBreathValueVersion = events.BreathValueVersion;
            _cachedBreathValue = events.CurrentBreathValue;
        }

        if (_observedLoopVersion != events.LoopVersion)
        {
            _observedLoopVersion = events.LoopVersion;
            if (events.CurrentLoopCount > 0)
                OnLoopCompleted(events.CurrentLoopCount);
        }
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
        _slotsFilled = Mathf.Max(_slotsFilled, Mathf.Min(slotIndex + 1, slotFills.Length));
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
        if (_visibleRequested && gameObject.activeInHierarchy)
            return;

        _visibleRequested = true;
        EnsureRenderConfiguration();
        ResetSlots();
        gameObject.SetActive(true);
        Fade(1f);
    }

    public void Hide()
    {
        _visibleRequested = false;
        if (!gameObject.activeInHierarchy) return; // already hidden; can't run a fade coroutine
        Fade(0f);
    }

    void EnsureRenderConfiguration()
    {
        if (!group) group = GetComponent<CanvasGroup>();

        Canvas canvas = GetComponent<Canvas>();
        if (canvas)
        {
            canvas.enabled = true;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvas.worldCamera = Camera.main;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
            }
        }

        UpdateCameraPlacement();
        EnsureSprites();
        EnsureHighVisibility();
    }

    void EnsureHighVisibility()
    {
        SetMinimumAlpha(largeCircleOutline, minimumOutlineAlpha);
        SetMinimumAlpha(smallCircleOutline, minimumOutlineAlpha);
        SetMinimumAlpha(bead, minimumBeadAlpha);

        if (slotRings != null)
            foreach (Image ring in slotRings)
                SetMinimumAlpha(ring, minimumSlotAlpha);

        if (slotFills != null)
            foreach (Image fill in slotFills)
                SetMinimumAlpha(fill, minimumBeadAlpha);

        if (group)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            group.ignoreParentGroups = true;
        }
    }

    static void SetMinimumAlpha(Image image, float minimumAlpha)
    {
        if (!image) return;

        Color color = image.color;
        color.a = Mathf.Max(color.a, minimumAlpha);
        image.color = color;
        image.raycastTarget = false;
    }

    void UpdateCameraPlacement()
    {
        if (!followMainCamera) return;

        Camera camera = Camera.main;
        if (!camera) return;

        // SoomUI 아래 공용 World Space Canvas를 유지하면서 HMD 앞에 고정한다.
        transform.position = camera.transform.TransformPoint(cameraLocalPosition);
        transform.rotation = camera.transform.rotation;

        FaceCamera faceCamera = GetComponent<FaceCamera>();
        if (faceCamera && faceCamera.enabled)
            faceCamera.enabled = false;
    }

    void ResetSlots()
    {
        _slotsFilled = 0;
        _receivedLoopCount = 0;
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
        if (!group)
        {
            Debug.LogError("[BreathCircleUI] CanvasGroup이 없어 UI 페이드를 실행할 수 없습니다.", this);
            return;
        }

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

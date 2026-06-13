using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and places WorldPopup cards in front of the headset, and is the single subscriber
/// that turns BreathEventsSO moments into popups:
///   - MessageRequested  → a transient instruction line that auto-dismisses.
///   - SessionCompleted   → the big "SUCCESS!" celebration.
///
/// Placement is comfort-first: an anchor lazily follows the player's gaze (flattened forward,
/// SmoothDamp) so cards sit ahead of you without being head-locked. The card itself billboards
/// (see WorldPopup). UI subscribes here; it never knows who raised the event.
/// </summary>
public class PopupSystem : MonoBehaviour
{
    [Header("State seam")]
    [SerializeField] BreathEventsSO events;

    [Header("Prefab & pool")]
    [Tooltip("A WorldPopup prefab (World Space canvas + CanvasGroup + TextMeshProUGUI).")]
    [SerializeField] WorldPopup popupPrefab;
    [SerializeField] int poolSize = 4;

    [Header("Placement (relative to the headset)")]
    [Tooltip("Metres in front of the eyes.")]
    [SerializeField] float distance = 1.8f;
    [Tooltip("Vertical offset for ordinary instruction lines (slightly below eye line).")]
    [SerializeField] float messageHeight = -0.15f;
    [Tooltip("Vertical offset for the SUCCESS celebration (around eye line).")]
    [SerializeField] float successHeight = 0.1f;
    [Tooltip("Bigger = the anchor follows your gaze more lazily (more stable, less sticky).")]
    [SerializeField] float followSmoothTime = 0.25f;

    [Header("Success copy")]
    [SerializeField] string successText = "SUCCESS!";
    [SerializeField] float successDuration = 2.5f;

    readonly List<WorldPopup> _pool = new();
    Transform _cam;
    Transform _anchor;
    Vector3 _anchorVel;

    void Awake()
    {
        if (Camera.main) _cam = Camera.main.transform;

        // Anchor that lazy-follows the gaze; popups are parented here so they share placement.
        _anchor = new GameObject("PopupAnchor").transform;
        _anchor.SetParent(transform, false);

        if (popupPrefab)
            for (int i = 0; i < poolSize; i++)
            {
                var p = Instantiate(popupPrefab, _anchor);
                p.gameObject.SetActive(false);
                _pool.Add(p);
            }
    }

    void OnEnable()
    {
        if (!events) return;
        events.MessageRequested += OnMessage;
        events.SessionCompleted += OnSessionComplete;
    }

    void OnDisable()
    {
        if (!events) return;
        events.MessageRequested -= OnMessage;
        events.SessionCompleted -= OnSessionComplete;
    }

    void OnMessage(string text, float duration) => ShowAt(text, duration, messageHeight);
    void OnSessionComplete()                     => ShowAt(successText, successDuration, successHeight);

    void ShowAt(string text, float duration, float height)
    {
        var popup = GetFree();
        if (!popup) return;
        popup.transform.localPosition = new Vector3(0f, height, 0f);
        popup.Show(text, duration);
    }

    WorldPopup GetFree()
    {
        foreach (var p in _pool)
            if (!p.IsActive && !p.gameObject.activeSelf) return p;
        // All busy: reuse the oldest by just taking the first.
        return _pool.Count > 0 ? _pool[0] : null;
    }

    void LateUpdate()
    {
        if (_cam == null)
        {
            if (Camera.main == null) return;
            _cam = Camera.main.transform;
        }
        // Flatten forward so cards don't drift up/down when the player looks at their feet.
        Vector3 fwd = _cam.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 target = _cam.position + fwd * distance;
        _anchor.position = Vector3.SmoothDamp(_anchor.position, target, ref _anchorVel, followSmoothTime);
    }
}

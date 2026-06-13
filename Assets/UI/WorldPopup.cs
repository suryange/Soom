using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// One world-space text card: fade in → hold → fade out, with a small scale "pop" and a
/// comfort-first billboard toward the headset. This is the reusable primitive every transient
/// UI in SOOM is built from (instruction lines, "SUCCESS!"). PopupSystem owns placement and
/// content; this component owns only how a single card animates and faces the player.
///
/// VR notes baked in here:
///   - Lives on a World Space canvas (never Screen Space Overlay — that doesn't render in VR).
///   - Billboards with Slerp damping instead of snapping, so a card that appears off-axis
///     doesn't jerk the eye.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class WorldPopup : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] CanvasGroup group;

    [Header("Timing (seconds)")]
    [SerializeField] float fadeIn  = 0.25f;
    [SerializeField] float fadeOut = 0.4f;

    [Header("Feel")]
    [Tooltip("Scale at the start of the pop, eased to 1 over fadeIn.")]
    [SerializeField] float popFromScale = 0.6f;
    [Tooltip("How fast the card turns to face the player. Higher = snappier.")]
    [SerializeField] float billboardSharpness = 8f;

    Transform _cam;
    Coroutine _run;
    Vector3 _baseScale;

    public bool IsActive => _run != null;

    void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        _baseScale = transform.localScale;
        if (Camera.main) _cam = Camera.main.transform;
        group.alpha = 0f;
        // NOTE: don't SetActive(false) here. Pooled copies are created inactive and only Awake
        // when Show() first activates them — deactivating again here would kill the coroutine.
    }

    /// <summary>Show `text`. If duration ≤ 0 the card stays until Show is called again or Hide().</summary>
    public void Show(string text, float duration)
    {
        if (label) label.text = text;
        gameObject.SetActive(true);
        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Run(duration));
    }

    public void Hide()
    {
        if (!isActiveAndEnabled) return;
        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(FadeOutThenDisable());
    }

    IEnumerator Run(float duration)
    {
        // Fade + pop in.
        for (float t = 0f; t < fadeIn; t += Time.deltaTime)
        {
            float k = fadeIn > 0f ? t / fadeIn : 1f;
            group.alpha = k;
            transform.localScale = _baseScale * Mathf.Lerp(popFromScale, 1f, EaseOutBack(k));
            yield return null;
        }
        group.alpha = 1f;
        transform.localScale = _baseScale;

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
            yield return FadeOutThenDisable();
        }
        else
        {
            _run = null; // persistent: leave it up
        }
    }

    IEnumerator FadeOutThenDisable()
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeOut; t += Time.deltaTime)
        {
            group.alpha = Mathf.Lerp(start, 0f, t / fadeOut);
            yield return null;
        }
        group.alpha = 0f;
        gameObject.SetActive(false);
        _run = null;
    }

    void LateUpdate()
    {
        if (_cam == null)
        {
            if (Camera.main == null) return;
            _cam = Camera.main.transform;
        }
        // Face the player: a canvas reads correctly when its forward points away from the eye.
        Vector3 toCard = transform.position - _cam.position;
        if (toCard.sqrMagnitude < 1e-4f) return;
        Quaternion target = Quaternion.LookRotation(toCard, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, target, 1f - Mathf.Exp(-billboardSharpness * Time.deltaTime));
    }

    // Gentle overshoot so the pop feels alive without being cartoonish.
    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }
}

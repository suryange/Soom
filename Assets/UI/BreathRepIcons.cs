using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A left→right row of icons that fill in as the player lands each breath rep. Subscribes to
/// BreathEventsSO.RepSucceeded(done, total): rep N tints icon N from the inactive to the active
/// colour with a short colour lerp and a little scale "punch". The left→right order is emergent
/// — reps arrive one at a time, so icon 0 fills, then 1, then 2 — not a staggered all-at-once
/// sweep. Lives on a World Space canvas with a HorizontalLayoutGroup.
/// </summary>
public class BreathRepIcons : MonoBehaviour
{
    [Header("State seam")]
    [SerializeField] BreathEventsSO events;

    [Header("Icons (left → right)")]
    [SerializeField] Image[] icons;

    [Header("Colours")]
    [SerializeField] Color inactiveColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] Color activeColor   = new Color(1f, 0.85f, 0.4f, 1f);

    [Header("Feel")]
    [SerializeField] float fillDuration = 0.3f;
    [SerializeField] float punchScale   = 1.35f;

    Coroutine[] _running;

    void Awake() => _running = new Coroutine[icons != null ? icons.Length : 0];

    void OnEnable()
    {
        ResetAll();
        if (events) events.RepSucceeded += OnRep;
    }

    void OnDisable()
    {
        if (events) events.RepSucceeded -= OnRep;
    }

    void ResetAll()
    {
        if (icons == null) return;
        foreach (var icon in icons)
        {
            if (!icon) continue;
            icon.color = inactiveColor;
            icon.rectTransform.localScale = Vector3.one;
        }
    }

    // repsDone is 1-based; the icon that just landed is index repsDone-1.
    void OnRep(int repsDone, int repsTotal)
    {
        int i = repsDone - 1;
        if (icons == null || i < 0 || i >= icons.Length || !icons[i]) return;
        if (_running[i] != null) StopCoroutine(_running[i]);
        _running[i] = StartCoroutine(Fill(icons[i]));
    }

    IEnumerator Fill(Image icon)
    {
        var rt = icon.rectTransform;
        for (float t = 0f; t < fillDuration; t += Time.deltaTime)
        {
            float k = t / fillDuration;
            icon.color = Color.Lerp(inactiveColor, activeColor, k);
            // Punch: scale up to punchScale at the midpoint, settle back to 1.
            float punch = 1f + (punchScale - 1f) * Mathf.Sin(k * Mathf.PI);
            rt.localScale = Vector3.one * punch;
            yield return null;
        }
        icon.color = activeColor;
        rt.localScale = Vector3.one;
    }
}

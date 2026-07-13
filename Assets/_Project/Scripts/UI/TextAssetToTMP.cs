using TMPro;
using UnityEngine;

/// <summary>
/// Copies a Unity TextAsset into a TextMesh Pro component.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextAssetToTMP : MonoBehaviour
{
    [SerializeField] private TextAsset sourceText;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private bool setOnEnable = true;

    private void Reset()
    {
        targetText = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TMP_Text>();

        if (!setOnEnable)
            ApplyText();
    }

    private void OnEnable()
    {
        if (setOnEnable)
            ApplyText();
    }

    public void ApplyText()
    {
        if (targetText == null || sourceText == null)
            return;

        targetText.text = sourceText.text;
    }
}

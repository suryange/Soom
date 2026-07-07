// ScreenFader.cs
// VR-safe full-screen fade to/from black.
//
// Screen Space Overlay canvases don't render in VR (there's no single "screen" — each eye has
// its own render target), so the standard UI-Image fade trick doesn't work on Quest. Instead we
// parent a small unlit quad directly to the camera, sized/positioned to fill the frustum just
// past the near clip plane, and drive its material alpha. Because it rides along with the
// camera it works identically whether the camera is a desktop Camera or an XR Origin's eye
// camera, and it survives scene loads via DontDestroyOnLoad.
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton fade-to-black controller for VR. Attach to a persistent root (see
/// SoomCoreSetup); it will create/find its own fade quad as a child of Camera.main.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Fade quad (camera child)")]
    [Tooltip("Assigned automatically if left empty: created as a child of Camera.main.")]
    [SerializeField] Transform fadeQuad;
    [SerializeField] float quadDistance = 0.15f; // metres in front of the eye, past near clip
    [SerializeField] float quadScale = 0.4f;     // large enough to fill the FOV at quadDistance

    [Header("State")]
    [SerializeField] Color fadeColor = Color.black;

    Renderer _quadRenderer;
    Material _material;
    Coroutine _fadeRoutine;

    /// <summary>Current fade alpha, 0 = fully clear, 1 = fully black.</summary>
    public float CurrentAlpha { get; private set; }

    public event Action FadeOutComplete;
    public event Action FadeInComplete;

    static readonly int ColorProp = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureFadeQuad();
    }

    /// <summary>
    /// Makes sure the fade quad exists and is parented to the current Camera.main. Safe to
    /// call again after a scene load in case the camera instance changed (e.g. new XR Origin).
    /// </summary>
    public void EnsureFadeQuad()
    {
        var cam = Camera.main;
        if (cam == null) return; // no camera yet (e.g. very first frame of a load) — retry later

        if (fadeQuad == null)
        {
            var existing = transform.Find("ScreenFaderQuad");
            fadeQuad = existing != null ? existing : CreateQuad();
        }

        if (fadeQuad.parent != cam.transform)
            fadeQuad.SetParent(cam.transform, false);

        fadeQuad.localPosition = new Vector3(0f, 0f, quadDistance);
        fadeQuad.localRotation = Quaternion.identity;
        fadeQuad.localScale    = Vector3.one * quadScale;

        if (_quadRenderer == null) _quadRenderer = fadeQuad.GetComponent<Renderer>();
        if (_material == null) _material = BuildUnlitTransparentMaterial();
        _quadRenderer.sharedMaterial = _material;
        _quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _quadRenderer.receiveShadows = false;

        ApplyAlpha(CurrentAlpha);
    }

    Transform CreateQuad()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "ScreenFaderQuad";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col); // no physics on an overlay
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    // Procedural URP Unlit material, transparent surface, so no separate shader asset is needed.
    Material BuildUnlitTransparentMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("[ScreenFader] 'Universal Render Pipeline/Unlit' shader not found. " +
                "Is URP installed/active?");
            return null;
        }
        var mat = new Material(shader) { name = "ScreenFaderMaterial" };

        // Transparent surface type (URP Unlit keywords/props).
        mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
        mat.SetFloat("_Blend", 0f);   // 0 = Alpha blend
        mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetColor(ColorProp, new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f));
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        return mat;
    }

    void ApplyAlpha(float a)
    {
        CurrentAlpha = Mathf.Clamp01(a);
        if (_material == null) return;
        var c = fadeColor;
        c.a = CurrentAlpha;
        _material.SetColor(ColorProp, c);
        // Fully clear: disable the renderer so it costs nothing and never occludes.
        if (_quadRenderer != null) _quadRenderer.enabled = CurrentAlpha > 0.0001f;
    }

    /// <summary>Snap instantly to fully black (no fade). Useful before the very first load.</summary>
    public void SetBlack()
    {
        EnsureFadeQuad();
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        ApplyAlpha(1f);
    }

    /// <summary>Snap instantly to fully clear (no fade).</summary>
    public void SetClear()
    {
        EnsureFadeQuad();
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        ApplyAlpha(0f);
    }

    /// <summary>Fade from clear to black over <paramref name="duration"/> seconds.</summary>
    public Coroutine FadeOut(float duration, Action onComplete = null)
    {
        EnsureFadeQuad();
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(1f, duration, () =>
        {
            onComplete?.Invoke();
            FadeOutComplete?.Invoke();
        }));
        return _fadeRoutine;
    }

    /// <summary>Fade from black to clear over <paramref name="duration"/> seconds.</summary>
    public Coroutine FadeIn(float duration, Action onComplete = null)
    {
        EnsureFadeQuad();
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(0f, duration, () =>
        {
            onComplete?.Invoke();
            FadeInComplete?.Invoke();
        }));
        return _fadeRoutine;
    }

    IEnumerator FadeRoutine(float target, float duration, Action onComplete)
    {
        float start = CurrentAlpha;
        if (duration <= 0f)
        {
            ApplyAlpha(target);
        }
        else
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                ApplyAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            ApplyAlpha(target);
        }
        _fadeRoutine = null;
        onComplete?.Invoke();
    }
}

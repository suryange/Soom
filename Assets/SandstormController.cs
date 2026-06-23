using UnityEngine;

/// <summary>
/// Reads BreathSignalSO.Clarity and drives all visual layers from it:
///   1. Dust overlay quad (DustVignette shader)  - the concentric clear-center effect
///   2. World-space sand ParticleSystem          - blowing sand, parallax/depth
///   3. URP fog                                   - cheap overall haze
///   4. Ground dust plane (GroundDust shader)     - wind-blown sand drifting over the floor
///
/// Never bind visuals to the raw signal: everything goes through SmoothDamp so the
/// sandstorm "settles" instead of flickering, preserving the felt cause→effect link.
/// </summary>
public class SandstormController : MonoBehaviour
{
    [Header("State seam")]
    [SerializeField] BreathSignalSO signal;

    [Header("1. Dust overlay (quad parented to camera)")]
    [SerializeField] Renderer dustOverlay;          // quad using a DustVignette material

    [Header("2. World sand particles")]
    [SerializeField] ParticleSystem sandParticles;
    [SerializeField] float maxEmission = 600f;

    [Header("3. Fog")]
    [SerializeField] bool driveFog = true;
    [SerializeField] float maxFogDensity = 0.06f;

    [Header("4. Ground dust (GroundDust plane)")]
    [SerializeField] Renderer groundDust;           // large plane using a GroundDust material
    [Tooltip("Surface sand still drifts a little when clear; ramps to max in the storm.")]
    [SerializeField] float minGroundDust = 0.12f;
    [SerializeField] float maxGroundDust = 0.85f;

    [Header("Mapping & damping")]
    [Tooltip("clarity (x) -> obscurity (y). Tune the feel here. Default: linear inverse.")]
    [SerializeField] AnimationCurve clarityToObscurity = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [Tooltip("Bigger = sandstorm reacts more slowly / smoothly.")]
    [SerializeField] float smoothTime = 0.6f;

    MaterialPropertyBlock _mpb;
    ParticleSystem.EmissionModule _emission;
    float _shownClarity, _vel;

    static readonly int ClarityID   = Shader.PropertyToID("_Clarity");
    static readonly int DustAmountID = Shader.PropertyToID("_DustAmount");
    static readonly int StrengthID   = Shader.PropertyToID("_Strength");

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (sandParticles) _emission = sandParticles.emission;
        _shownClarity = signal ? signal.Clarity : 1f;
    }

    void Update()
    {
        float target = signal ? Mathf.Clamp01(signal.Clarity) : 1f;
        _shownClarity = Mathf.SmoothDamp(_shownClarity, target, ref _vel, smoothTime);

        // 0 = clear, 1 = fully obscured. One curve drives all layers for a coherent feel.
        float obscurity = Mathf.Clamp01(clarityToObscurity.Evaluate(_shownClarity));

        // 1. Overlay: _Clarity shapes the radial clear zone, _DustAmount scales intensity.
        if (dustOverlay)
        {
            dustOverlay.GetPropertyBlock(_mpb);
            _mpb.SetFloat(ClarityID, _shownClarity);
            _mpb.SetFloat(DustAmountID, obscurity);
            dustOverlay.SetPropertyBlock(_mpb);
        }

        // 2. Particles
        if (sandParticles) _emission.rateOverTime = maxEmission * obscurity;

        // 3. Fog (URP reads RenderSettings fog)
        if (driveFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogDensity = maxFogDensity * obscurity;
        }

        // 4. Ground dust: always a little drift, ramping up with the storm.
        if (groundDust)
        {
            groundDust.GetPropertyBlock(_mpb);
            _mpb.SetFloat(StrengthID, Mathf.Lerp(minGroundDust, maxGroundDust, obscurity));
            groundDust.SetPropertyBlock(_mpb);
        }
    }
}

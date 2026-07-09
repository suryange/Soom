using System.Collections;
using UnityEngine;

/// <summary>
/// 카메라에 다중 채널(Multi-channel) Perlin Noise 기반 셰이크를 적용합니다(기능 명세 1.3.1).
/// 위치(XYZ) 3채널 + 회전(XYZ) 3채널, 총 6개의 독립적인 Perlin Noise 샘플을 사용해
/// 기계적이지 않은 자연스러운 흔들림을 만들고, 시간이 지날수록(intensityOverTime) 강도가 커집니다.
///
/// targetTransform을 비워두면 런타임에 Camera.main 위에 오프셋 전용 앵커를 자동으로 만들어 사용합니다.
/// (VR에서는 HMD 트래킹이 카메라의 localPosition/localRotation을 매 프레임 덮어쓰므로, 카메라 자체가 아니라
/// 그 위의 별도 부모를 흔들어야 트래킹과 충돌하지 않습니다.)
/// </summary>
public class CameraShakeNoise : MonoBehaviour
{
    private const string RuntimeAnchorName = "CutsceneShakeAnchor (Runtime)";
    private const int ChannelCount = 6;

    [Header("셰이크 대상 (비우면 런타임에 Camera.main 위에 자동 생성)")]
    [SerializeField] private Transform targetTransform;

    [Header("위치 진폭 (m)")]
    [SerializeField] private Vector3 positionAmplitude = new Vector3(0.05f, 0.05f, 0.03f);

    [Header("회전 진폭 (deg)")]
    [SerializeField] private Vector3 rotationAmplitude = new Vector3(2f, 2f, 3f);

    [Tooltip("Perlin Noise 샘플링 주파수(Hz). 클수록 빠르게 흔들립니다.")]
    [SerializeField] private float frequency = 2.5f;

    [Tooltip("셰이크 진행도(0~1)에 대한 강도 배율 곡선. 추락 컷신처럼 시간이 지날수록 격해지는 연출에 사용합니다.")]
    [SerializeField] private AnimationCurve intensityOverTime = AnimationCurve.Linear(0f, 0.25f, 1f, 1f);

    private readonly float[] _seeds = new float[ChannelCount];
    private Coroutine _shakeRoutine;

    private void Awake()
    {
        for (int i = 0; i < _seeds.Length; i++)
            _seeds[i] = Random.Range(0f, 1000f);
    }

    private void OnDisable()
    {
        StopShake();
    }

    /// <summary>duration(초) 동안 셰이크를 재생합니다. duration이 0 이하이면 StopShake가 호출될 때까지 계속됩니다.</summary>
    public void BeginShake(float duration)
    {
        var target = ResolveTarget();
        if (target == null)
        {
            Debug.LogWarning("[CameraShakeNoise] 셰이크를 적용할 카메라를 찾지 못해 건너뜁니다.");
            return;
        }

        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(target, duration));
    }

    /// <summary>셰이크를 즉시 멈추고 대상의 위치/회전을 원점(오프셋 0)으로 되돌립니다.</summary>
    public void StopShake()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }
        if (targetTransform != null)
        {
            targetTransform.localPosition = Vector3.zero;
            targetTransform.localRotation = Quaternion.identity;
        }
    }

    private Transform ResolveTarget()
    {
        if (targetTransform != null) return targetTransform;

        var cam = Camera.main;
        if (cam == null) return null;

        var camTransform = cam.transform;

        // 이미 이전에 만들어둔 런타임 앵커가 부모라면 재사용합니다.
        if (camTransform.parent != null && camTransform.parent.name == RuntimeAnchorName)
        {
            targetTransform = camTransform.parent;
            return targetTransform;
        }

        // 카메라와 기존 부모 사이에 오프셋 전용 앵커를 삽입합니다. 월드 포즈는 그대로 유지됩니다.
        var anchorGO = new GameObject(RuntimeAnchorName);
        var originalParent = camTransform.parent;
        anchorGO.transform.SetParent(originalParent, false);
        camTransform.SetParent(anchorGO.transform, true);

        targetTransform = anchorGO.transform;
        return targetTransform;
    }

    private IEnumerator ShakeRoutine(Transform target, float duration)
    {
        bool infinite = duration <= 0f;
        float t = 0f;
        while (infinite || t < duration)
        {
            t += Time.deltaTime;
            float normalized = infinite ? 1f : Mathf.Clamp01(t / duration);
            float intensity = intensityOverTime.Evaluate(normalized);
            ApplyNoise(target, intensity);
            yield return null;
        }

        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        _shakeRoutine = null;
    }

    private void ApplyNoise(Transform target, float intensity)
    {
        float time = Time.time * frequency;

        float px = (Mathf.PerlinNoise(_seeds[0], time) * 2f - 1f) * positionAmplitude.x * intensity;
        float py = (Mathf.PerlinNoise(_seeds[1], time) * 2f - 1f) * positionAmplitude.y * intensity;
        float pz = (Mathf.PerlinNoise(_seeds[2], time) * 2f - 1f) * positionAmplitude.z * intensity;

        float rx = (Mathf.PerlinNoise(_seeds[3], time) * 2f - 1f) * rotationAmplitude.x * intensity;
        float ry = (Mathf.PerlinNoise(_seeds[4], time) * 2f - 1f) * rotationAmplitude.y * intensity;
        float rz = (Mathf.PerlinNoise(_seeds[5], time) * 2f - 1f) * rotationAmplitude.z * intensity;

        target.localPosition = new Vector3(px, py, pz);
        target.localRotation = Quaternion.Euler(rx, ry, rz);
    }
}

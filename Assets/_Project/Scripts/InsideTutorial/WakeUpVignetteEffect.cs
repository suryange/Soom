using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 명세 2.1.1 기상 연출: 런타임에 전역 Volume + VolumeProfile을 코드로 생성해
/// URP Vignette의 Intensity를 1(눈 감음) → 0(완전히 뜸)으로 점점 얕아지는
/// Ping-Pong 패턴(눈 깜빡임 효과)으로 애니메이션한다.
/// 별도의 .asset을 미리 만들 필요 없이 ScriptableObject.CreateInstance로
/// VolumeProfile을 생성하므로 씬/에셋 준비 없이 바로 동작한다.
/// </summary>
public class WakeUpVignetteEffect : MonoBehaviour
{
    [Header("Vignette 설정")]
    [SerializeField] private Color vignetteColor = Color.black;
    [SerializeField] private float volumePriority = 100f;

    [Header("눈 깜빡임 애니메이션")]
    [Tooltip("완전히 눈을 뜨기 전까지 반복할 깜빡임(뜸→다시 감음) 횟수")]
    [SerializeField] private int blinkCount = 3;
    [Tooltip("깜빡임 1회(뜸 + 다시 감음)에 걸리는 시간(초)")]
    [SerializeField] private float blinkCycleDuration = 0.6f;
    [Tooltip("마지막에 완전히 눈을 뜨는 데 걸리는 시간(초)")]
    [SerializeField] private float finalOpenDuration = 0.8f;

    private Volume _volume;
    private VolumeProfile _profile;
    private Vignette _vignette;
    private Coroutine _routine;

    // Awake가 아닌 Start에서 Volume을 만드는 이유: 에디터 빌더 스크립트가 AddComponent
    // 직후 SerializedObject로 필드를 배선할 수 있는데, Awake는 AddComponent 시점에
    // 즉시(에디터 모드에서도) 실행되어 배선/저장 흐름과 불필요하게 얽힐 수 있다.
    // Start는 플레이 모드 진입 시에만 실행되므로 항상 실제 런타임에만 Volume을 생성한다.
    private void Start()
    {
        // EnsureVolumeCreated()가 프로파일을 처음 만들 때 Intensity를 1(눈 감음)로 초기화한다.
        // (InsideTutorialSequence가 Start 순서상 이보다 먼저 PlayWakeUpAnimation()을 호출해
        // EnsureVolumeCreated()가 먼저 실행되더라도, 멱등 처리 덕분에 초기값은 항상 1로 시작한다.)
        EnsureVolumeCreated();
    }

    /// <summary>기상 연출(눈 깜빡임 애니메이션)을 재생하고, 완전히 눈을 뜨면 콜백을 호출한다.</summary>
    public Coroutine PlayWakeUpAnimation(Action onComplete = null)
    {
        EnsureVolumeCreated();
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(WakeUpRoutine(onComplete));
        return _routine;
    }

    private void EnsureVolumeCreated()
    {
        if (_volume != null && _profile != null && _vignette != null) return;

        var go = new GameObject("WakeUpVignetteVolume");
        go.transform.SetParent(transform, false);

        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = volumePriority;
        _volume.weight = 1f;

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.name = "WakeUpVignetteProfile";

        _vignette = _profile.Add<Vignette>(true); // overrides=true → 모든 파라미터를 즉시 오버라이드 상태로
        _vignette.color.value = vignetteColor;
        _vignette.intensity.overrideState = true;
        _vignette.intensity.value = 1f; // 최초 생성 시점 = 씬 시작 시점 → 눈을 감은 상태(완전한 비네트)로 대기
        _vignette.smoothness.overrideState = true;
        _vignette.smoothness.value = 1f;

        _volume.sharedProfile = _profile;

        EnsurePostProcessingEnabledOnMainCamera();
    }

    // Post Processing이 카메라에서 꺼져 있으면 Vignette가 아예 렌더링되지 않으므로 방어적으로 켜준다.
    private void EnsurePostProcessingEnabledOnMainCamera()
    {
        var cam = Camera.main;
        if (cam == null) return; // 아직 카메라가 씬에 없을 수 있음(예: 초기 프레임) — 조용히 건너뜀

        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;
    }

    private IEnumerator WakeUpRoutine(Action onComplete)
    {
        int steps = Mathf.Max(1, blinkCount);
        float halfCycle = Mathf.Max(0.01f, blinkCycleDuration * 0.5f);

        for (int i = 1; i <= steps; i++)
        {
            // 매 블링크마다 다시 감기는 깊이(peak)가 점점 얕아져서, 마지막엔 0(완전히 뜸)에 수렴한다.
            float closedPeak = Mathf.Lerp(1f, 0f, (float)i / steps);

            yield return AnimateIntensity(0f, halfCycle); // 눈 뜸
            if (i < steps)
                yield return AnimateIntensity(closedPeak, halfCycle); // 다시 살짝 감음
        }

        yield return AnimateIntensity(0f, finalOpenDuration); // 완전히 눈을 뜬 상태로 종료

        _routine = null;
        onComplete?.Invoke();
    }

    private IEnumerator AnimateIntensity(float target, float duration)
    {
        float start = GetIntensity();
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetIntensity(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetIntensity(target);
    }

    private float GetIntensity() => _vignette != null ? _vignette.intensity.value : 0f;

    private void SetIntensity(float value)
    {
        if (_vignette == null) return;
        _vignette.intensity.overrideState = true;
        _vignette.intensity.value = Mathf.Clamp01(value);
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 모래바람 구역 연출 상태머신 (기능 명세 4.2, 4.3).
/// Idle -> Rising -> Active -> Calming -> Cleared 순서로 진행한다.
///
///  - Idle    : 구역 진입 전 대기 상태.
///  - Rising  : SandstormZoneTrigger.EnterZone() 호출 직후, 파티클/Fog/바람 SFX가 최대치로
///              차오르고 길잡이 등불이 서서히 꺼지는 구간.
///  - Active  : 폭풍이 최대치에 도달, BreathCircleUI와 지시문 UI를 노출하고 호흡을 기다리는 구간.
///  - Calming : BreathEventsSO.OnBreathLoopCompleted가 들어올 때마다 Particle Emission과
///              Fog Density를 단계적으로 낮추는 구간 (구슬 1개 = 1/N만큼 감소).
///  - Cleared : BreathEventsSO.OnMissionSuccess(3회 완료) 시 폭풍 완전 해제 + 등불 복구 +
///              SetMissionZone(false).
///
/// 이 컨트롤러는 SandstormZoneTrigger로부터만 시작 신호(EnterZone)를 받고, 이후로는 전부
/// BreathEventsSO 채널 구독만으로 동작한다 (새 아키텍처의 이벤트 채널 규칙을 따름).
/// </summary>
public class SandstormController : MonoBehaviour
{
    public enum State { Idle, Rising, Active, Calming, Cleared }

    [Header("이벤트 채널 (SO)")]
    [SerializeField] private BreathEventsSO breathEventsChannel;

    [Header("4.1 진입 연출 UI (World Space, FaceCamera)")]
    [Tooltip("챕터 UI 루트. 예: \"되살아난 모래 / 불안한 첫 발\"")]
    [SerializeField] private GameObject chapterUIRoot;
    [Tooltip("구역 이름 텍스트 UI 루트. 예: \"모래 폭풍\"")]
    [SerializeField] private GameObject zoneTextUIRoot;
    [Tooltip("진입 연출 UI를 자동으로 숨기기까지 대기하는 시간(초). 0 이하면 자동으로 숨기지 않는다.")]
    [SerializeField] private float introUIDuration = 3f;

    [Header("4.2 모래폭풍 파티클")]
    [SerializeField] private ParticleSystem sandstormParticles;
    [SerializeField] private float maxEmissionRate = 300f;
    [Tooltip("Idle -> Active까지 파티클/Fog가 차오르는 데 걸리는 시간(초).")]
    [SerializeField] private float riseDuration = 3f;

    [Header("4.2 Fog (시야 제한)")]
    [Tooltip("폭풍 최대치일 때의 Fog Density. 시작 시점의 RenderSettings.fogDensity를 기준값(Clear 상태)으로 사용한다.")]
    [SerializeField] private float maxFogDensity = 0.12f;

    [Header("4.2 바람 SFX (옵션)")]
    [Tooltip("루프 재생할 바람 SFX. 비워두면 재생하지 않는다 (NullReferenceException 방지).")]
    [SerializeField] private AudioClip windLoopSfx;
    [Range(0f, 1f)]
    [SerializeField] private float windSfxVolume = 0.6f;

    [Header("4.2 길잡이 등불")]
    [Tooltip("빛 강도를 서서히 낮췄다가 복구할 GuidingLightController. 비워두면 씬에서 자동으로 하나 찾는다.")]
    [SerializeField] private GuidingLightController guidingLight;
    [SerializeField] private float lightFadeDuration = 2f;

    [Header("4.3 호흡 시퀀스 (콘텐츠 C)")]
    [Tooltip("공용 호흡 UI. 비워두면 씬에서 자동으로 하나 찾는다.")]
    [SerializeField] private BreathCircleUI breathCircleUI;
    [Tooltip("지시문 UI 루트. 예: \"깊은 호흡으로 모래폭풍을 잠재우세요\"")]
    [SerializeField] private GameObject instructionUIRoot;
    [Tooltip("BreathManager.targetLoopCount와 동일하게 맞춰야 한다 (기본 3회).")]
    [SerializeField] private int totalBreathLoops = 3;
    [Tooltip("호흡 루프 완료 1회당 파티클/Fog가 감소하는 데 걸리는 시간(초).")]
    [SerializeField] private float calmStepDuration = 1.5f;
    [Tooltip("미션 성공(3회 완료) 시 폭풍이 완전히 사라지는 데 걸리는 시간(초).")]
    [SerializeField] private float clearDuration = 2f;

    public State CurrentState { get; private set; } = State.Idle;

    private float clearFogDensity;   // 구역 진입 전(맑은 상태) 기준 Fog Density
    private float currentEmissionRate;
    private ParticleSystem.EmissionModule emissionModule;
    private bool hasEmissionModule;

    private AudioSource windAudioSource;

    private Coroutine emissionRoutine;
    private Coroutine fogRoutine;
    private Coroutine introUIRoutine;
    private Coroutine activateAfterRiseRoutine;
    private Coroutine stopParticlesRoutine;

    private void Awake()
    {
        clearFogDensity = RenderSettings.fogDensity;

        if (sandstormParticles != null)
        {
            emissionModule = sandstormParticles.emission;
            hasEmissionModule = true;
        }

        if (guidingLight == null)
        {
#if UNITY_2023_1_OR_NEWER
            guidingLight = FindAnyObjectByType<GuidingLightController>();
#else
            guidingLight = FindObjectOfType<GuidingLightController>();
#endif
        }

        if (breathCircleUI == null)
        {
#if UNITY_2023_1_OR_NEWER
            breathCircleUI = FindAnyObjectByType<BreathCircleUI>();
#else
            breathCircleUI = FindObjectOfType<BreathCircleUI>();
#endif
        }

        SetActive(chapterUIRoot, false);
        SetActive(zoneTextUIRoot, false);
        SetActive(instructionUIRoot, false);
    }

    private void OnEnable()
    {
        if (breathEventsChannel != null)
        {
            breathEventsChannel.OnBreathLoopCompleted += HandleBreathLoopCompleted;
            breathEventsChannel.OnMissionSuccess += HandleMissionSuccess;
        }
    }

    private void OnDisable()
    {
        if (breathEventsChannel != null)
        {
            breathEventsChannel.OnBreathLoopCompleted -= HandleBreathLoopCompleted;
            breathEventsChannel.OnMissionSuccess -= HandleMissionSuccess;
        }
    }

    // =========================================================
    // 4.1: SandstormZoneTrigger에서 호출되는 진입점
    // =========================================================
    public void EnterZone()
    {
        if (CurrentState != State.Idle) return;

        SetActive(chapterUIRoot, true);
        SetActive(zoneTextUIRoot, true);

        if (introUIDuration > 0f)
        {
            if (introUIRoutine != null) StopCoroutine(introUIRoutine);
            introUIRoutine = StartCoroutine(HideIntroUIAfterDelay(introUIDuration));
        }

        BeginRising();
    }

    private IEnumerator HideIntroUIAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetActive(chapterUIRoot, false);
        SetActive(zoneTextUIRoot, false);
        introUIRoutine = null;
    }

    // =========================================================
    // 4.2: 모래폭풍 연출 시작 (Idle -> Rising -> Active)
    // =========================================================
    private void BeginRising()
    {
        CurrentState = State.Rising;

        if (sandstormParticles != null && !sandstormParticles.isPlaying)
        {
            sandstormParticles.Play();
        }

        LerpEmission(maxEmissionRate, riseDuration);
        LerpFog(maxFogDensity, riseDuration);
        PlayWindLoop();

        if (guidingLight != null)
        {
            guidingLight.FadeOutLight(lightFadeDuration);
        }

        if (activateAfterRiseRoutine != null) StopCoroutine(activateAfterRiseRoutine);
        activateAfterRiseRoutine = StartCoroutine(ActivateAfterRise());
    }

    private IEnumerator ActivateAfterRise()
    {
        yield return new WaitForSeconds(riseDuration);

        // 그 사이 다른 경로로 상태가 바뀌지 않았을 때만 Active로 전이
        if (CurrentState == State.Rising)
        {
            CurrentState = State.Active;
        }

        // 4.3: 호흡 시퀀스 UI 노출
        SetActive(instructionUIRoot, true);
        if (breathCircleUI != null) breathCircleUI.Show();

        activateAfterRiseRoutine = null;
    }

    // =========================================================
    // 4.3: 호흡 이벤트에 반응해 폭풍을 잦아들게 함
    // =========================================================
    private void HandleBreathLoopCompleted(int loopCount)
    {
        if (CurrentState == State.Idle || CurrentState == State.Cleared) return;

        CurrentState = State.Calming;

        int clampedCount = Mathf.Clamp(loopCount, 0, totalBreathLoops);
        float remainingRatio = totalBreathLoops > 0
            ? Mathf.Clamp01(1f - (float)clampedCount / totalBreathLoops)
            : 0f;

        LerpEmission(maxEmissionRate * remainingRatio, calmStepDuration);
        LerpFog(Mathf.Lerp(clearFogDensity, maxFogDensity, remainingRatio), calmStepDuration);
    }

    private void HandleMissionSuccess()
    {
        if (CurrentState == State.Cleared) return;

        CurrentState = State.Cleared;

        LerpEmission(0f, clearDuration);
        LerpFog(clearFogDensity, clearDuration);
        StopWindLoop();

        if (sandstormParticles != null)
        {
            if (stopParticlesRoutine != null) StopCoroutine(stopParticlesRoutine);
            stopParticlesRoutine = StartCoroutine(StopParticlesAfter(clearDuration));
        }

        if (guidingLight != null)
        {
            guidingLight.RestoreLightIntensity(lightFadeDuration);
        }

        SetActive(instructionUIRoot, false);
        if (breathCircleUI != null) breathCircleUI.Hide();

        // 미션 구역 상태 해제 (BreathManager가 이미 Idle로 되돌려도 안전하게 한 번 더 호출)
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.SetMissionZone(false);
        }
    }

    private IEnumerator StopParticlesAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sandstormParticles != null) sandstormParticles.Stop();
        stopParticlesRoutine = null;
    }

    // =========================================================
    // 파티클 Emission Lerp
    // =========================================================
    private void LerpEmission(float targetRate, float duration)
    {
        if (!hasEmissionModule) return;

        if (emissionRoutine != null) StopCoroutine(emissionRoutine);
        emissionRoutine = StartCoroutine(LerpEmissionRoutine(targetRate, duration));
    }

    private IEnumerator LerpEmissionRoutine(float targetRate, float duration)
    {
        float start = currentEmissionRate;

        if (duration <= 0f)
        {
            currentEmissionRate = targetRate;
            emissionModule.rateOverTime = targetRate;
            emissionRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            currentEmissionRate = Mathf.Lerp(start, targetRate, elapsed / duration);
            emissionModule.rateOverTime = currentEmissionRate;
            yield return null;
        }

        currentEmissionRate = targetRate;
        emissionModule.rateOverTime = targetRate;
        emissionRoutine = null;
    }

    // =========================================================
    // Fog Density Lerp
    // =========================================================
    private void LerpFog(float targetDensity, float duration)
    {
        if (fogRoutine != null) StopCoroutine(fogRoutine);
        fogRoutine = StartCoroutine(LerpFogRoutine(targetDensity, duration));
    }

    private IEnumerator LerpFogRoutine(float targetDensity, float duration)
    {
        RenderSettings.fog = true;
        float start = RenderSettings.fogDensity;

        if (duration <= 0f)
        {
            RenderSettings.fogDensity = targetDensity;
            fogRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            RenderSettings.fogDensity = Mathf.Lerp(start, targetDensity, elapsed / duration);
            yield return null;
        }

        RenderSettings.fogDensity = targetDensity;
        fogRoutine = null;
    }

    // =========================================================
    // 바람 SFX 루프 (옵션 AudioClip, SoomAudioManager 볼륨 반영)
    // =========================================================
    private void PlayWindLoop()
    {
        if (windLoopSfx == null) return;

        EnsureWindAudioSource();
        windAudioSource.clip = windLoopSfx;
        windAudioSource.volume = windSfxVolume * GetSfxVolumeScale();

        if (!windAudioSource.isPlaying) windAudioSource.Play();
    }

    private void StopWindLoop()
    {
        if (windAudioSource != null && windAudioSource.isPlaying)
        {
            windAudioSource.Stop();
        }
    }

    private void EnsureWindAudioSource()
    {
        if (windAudioSource != null) return;

        var go = new GameObject("SandstormWindAudio");
        go.transform.SetParent(transform, false);
        windAudioSource = go.AddComponent<AudioSource>();
        windAudioSource.loop = true;
        windAudioSource.playOnAwake = false;
        windAudioSource.spatialBlend = 0f; // 2D 앰비언트 취급
    }

    private float GetSfxVolumeScale()
    {
        // SoomAudioManager가 존재하면 SFX 채널 볼륨(0~100)을 반영, 없으면 1.0 취급
        if (SoomAudioManager.Instance != null)
        {
            return Mathf.Clamp01(SoomAudioManager.Instance.GetVolume(AudioChannel.SFX) / 100f);
        }
        return 1f;
    }

    private static void SetActive(GameObject root, bool active)
    {
        if (root != null) root.SetActive(active);
    }
}

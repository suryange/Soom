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
    [Tooltip("XR 카메라 자식의 DustVignetteOverlay Renderer.")]
    [SerializeField] private Renderer dustVignetteRenderer;
    [Tooltip("XR의 비대칭 양안 FOV까지 Quad가 덮도록 적용하는 크기 배수.")]
    [SerializeField, Min(4f)] private float vignetteOverscan = 10f;
    [SerializeField] private float maxEmissionRate = 300f;
    [Tooltip("Idle -> Active까지 파티클/Fog가 차오르는 데 걸리는 시간(초).")]
    [SerializeField] private float riseDuration = 3f;

    [Header("4.2 Fog (시야 제한)")]
    [Tooltip("폭풍 최대치일 때의 Fog Density. 시작 시점의 RenderSettings.fogDensity를 기준값(Clear 상태)으로 사용한다.")]
    [SerializeField] private float maxFogDensity = 0.12f;
    [SerializeField] private Color sandstormFogColor = new Color(0.55f, 0.45f, 0.31f, 1f);

    [Header("4.2 바람 SFX (옵션)")]
    [Tooltip("루프 재생할 바람 SFX. 비워두면 재생하지 않는다 (NullReferenceException 방지).")]
    [SerializeField] private AudioClip windLoopSfx;
    [Range(0f, 1f)]
    [SerializeField] private float windSfxVolume = 0.6f;

    [Header("4.2 길잡이 등불")]
    [Tooltip("첫 호흡 성공 시 런타임 Guiding Light를 생성하는 콘텐츠 소유자.")]
    [SerializeField] private HologramMessage guidingLightProvider;
    [Tooltip("빛 강도를 서서히 낮췄다가 복구할 GuidingLightController. 비워두면 씬에서 자동으로 하나 찾는다.")]
    [SerializeField] private GuidingLightController guidingLight;
    [SerializeField] private float lightFadeDuration = 2f;
    [Tooltip("폭풍 해제 후 Guiding Light가 이어서 이동할 Waypoint.")]
    [SerializeField] private Transform[] postClearWaypoints;

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
    [Tooltip("마지막 구슬 이동과 성공 Pulse를 보여준 뒤 호흡 UI를 숨기기까지의 시간.")]
    [SerializeField] private float successUIHideDelay = 0.9f;

    public State CurrentState { get; private set; } = State.Idle;
    public float CurrentObscurity => currentObscurity;

    private float clearFogDensity;
    private Color clearFogColor;
    private bool clearFogEnabled;
    private FogMode clearFogMode;
    private bool hasCapturedEnvironment;
    private float currentObscurity;
    private ParticleSystem.EmissionModule emissionModule;
    private bool hasEmissionModule;
    private MaterialPropertyBlock vignettePropertyBlock;
    private static readonly int ObscurityId = Shader.PropertyToID("_Obscurity");
    private static readonly int ClarityId = Shader.PropertyToID("_Clarity");

    private AudioSource windAudioSource;

    private Coroutine obscurityRoutine;
    private Coroutine introUIRoutine;
    private Coroutine activateAfterRiseRoutine;
    private Coroutine stopParticlesRoutine;
    private Coroutine hideBreathUIRoutine;
    private bool isMissionOwnerActive;
    private bool missionSuccessInProgress;
    private bool isStateSubscribed;
    private bool isGuidingLightProviderSubscribed;

    private void Awake()
    {
        if (sandstormParticles != null)
        {
            emissionModule = sandstormParticles.emission;
            hasEmissionModule = true;
            emissionModule.rateOverTime = 0f;
        }

        if (dustVignetteRenderer != null)
        {
            vignettePropertyBlock = new MaterialPropertyBlock();
            EnsureVignetteCoverage();
            dustVignetteRenderer.enabled = false;
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

#if UNITY_2023_1_OR_NEWER
        BreathManager breathManager = FindAnyObjectByType<BreathManager>();
#else
        BreathManager breathManager = FindObjectOfType<BreathManager>();
#endif
        if (breathManager != null)
            totalBreathLoops = Mathf.Max(1, breathManager.targetLoopCount);
    }

    private void OnEnable()
    {
        if (breathEventsChannel != null)
        {
            breathEventsChannel.OnBreathLoopCompleted += HandleBreathLoopCompleted;
            breathEventsChannel.OnMissionSuccess += HandleMissionSuccess;
        }

        TrySubscribeStateManager();
        TrySubscribeGuidingLightProvider();
    }

    private void Start()
    {
        TrySubscribeStateManager();
        TrySubscribeGuidingLightProvider();
    }

    private void OnDisable()
    {
        if (breathEventsChannel != null)
        {
            breathEventsChannel.OnBreathLoopCompleted -= HandleBreathLoopCompleted;
            breathEventsChannel.OnMissionSuccess -= HandleMissionSuccess;
        }

        UnsubscribeStateManager();
        UnsubscribeGuidingLightProvider();
        AbortAndRestore();
    }

    // =========================================================
    // 4.1: SandstormZoneTrigger에서 호출되는 진입점
    // =========================================================
    public void EnterZone()
    {
        if (CurrentState != State.Idle) return;

        CaptureEnvironmentSettings();
        ApplyObscurity(0f);
        ResolveActiveGuidingLight();

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

        LerpObscurity(1f, riseDuration);
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

        if (CurrentState == State.Active && PlayerStateManager.Instance != null &&
            PlayerStateManager.Instance.TryAcquireBreathMission(BreathMissionId.Sandstorm))
        {
            PlayerStateManager.Instance.SetMissionZone(true);
        }
        else if (CurrentState == State.Active)
        {
            Debug.LogWarning("[SandstormController] 호흡 미션 소유권을 얻지 못해 MissionReady 전환을 보류합니다.", this);
        }

        activateAfterRiseRoutine = null;
    }

    // =========================================================
    // 4.3: 호흡 이벤트에 반응해 폭풍을 잦아들게 함
    // =========================================================
    private void HandleBreathLoopCompleted(int loopCount)
    {
        if (!CanProcessBreathEvent()) return;

        CurrentState = State.Calming;

        int clampedCount = Mathf.Clamp(loopCount, 0, totalBreathLoops);
        float remainingRatio = totalBreathLoops > 0
            ? Mathf.Clamp01(1f - (float)clampedCount / totalBreathLoops)
            : 0f;

        LerpObscurity(remainingRatio, calmStepDuration);
    }

    private void HandleMissionSuccess()
    {
        if (!CanProcessBreathEvent() || missionSuccessInProgress) return;

        missionSuccessInProgress = true;
        isMissionOwnerActive = false;
        CurrentState = State.Cleared;

        LerpObscurity(0f, clearDuration);

        if (sandstormParticles != null)
        {
            if (stopParticlesRoutine != null) StopCoroutine(stopParticlesRoutine);
            stopParticlesRoutine = StartCoroutine(StopParticlesAfter(clearDuration));
        }

        if (guidingLight != null)
        {
            guidingLight.RestoreLightIntensity(lightFadeDuration);
            if (postClearWaypoints != null && postClearWaypoints.Length > 0)
                guidingLight.StartGuiding(postClearWaypoints);
        }

        SetActive(instructionUIRoot, false);
        if (hideBreathUIRoutine != null) StopCoroutine(hideBreathUIRoutine);
        hideBreathUIRoutine = StartCoroutine(HideBreathUIAfterDelay(successUIHideDelay));

        // 미션 구역 상태 해제 (BreathManager가 이미 Idle로 되돌려도 안전하게 한 번 더 호출)
        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.ReleaseBreathMission(BreathMissionId.Sandstorm);
            PlayerStateManager.Instance.SetMissionZone(false);
        }
    }

    private bool CanProcessBreathEvent()
    {
        return isMissionOwnerActive &&
               (CurrentState == State.Active || CurrentState == State.Calming) &&
               PlayerStateManager.Instance != null &&
               PlayerStateManager.Instance.CurrentState == PlayerState.BreathingActive &&
               PlayerStateManager.Instance.IsBreathMissionOwner(BreathMissionId.Sandstorm);
    }

    private void TrySubscribeGuidingLightProvider()
    {
        if (isGuidingLightProviderSubscribed) return;

        if (guidingLightProvider == null)
            guidingLightProvider = FindFirstObjectByType<HologramMessage>(FindObjectsInactive.Include);
        if (guidingLightProvider == null) return;

        guidingLightProvider.OnGuidingLightSpawned += HandleGuidingLightSpawned;
        isGuidingLightProviderSubscribed = true;
    }

    private void UnsubscribeGuidingLightProvider()
    {
        if (!isGuidingLightProviderSubscribed) return;

        if (guidingLightProvider != null)
            guidingLightProvider.OnGuidingLightSpawned -= HandleGuidingLightSpawned;
        isGuidingLightProviderSubscribed = false;
    }

    private void HandleGuidingLightSpawned(GuidingLightController spawnedLight)
    {
        if (spawnedLight != null && spawnedLight.isActiveAndEnabled)
            guidingLight = spawnedLight;
    }

    private void ResolveActiveGuidingLight()
    {
        if (guidingLight != null && guidingLight.isActiveAndEnabled &&
            guidingLight.gameObject.scene.IsValid() && !guidingLight.name.Contains("Template"))
            return;

        GuidingLightController[] candidates = FindObjectsByType<GuidingLightController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        guidingLight = null;
        foreach (GuidingLightController candidate in candidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.gameObject.scene.IsValid())
                continue;
            if (candidate.name.Contains("Template"))
                continue;

            guidingLight = candidate;
            break;
        }

        if (guidingLight == null)
            Debug.LogWarning("[SandstormController] 활성 Guiding Light 인스턴스를 찾지 못했습니다. 폭풍은 계속 진행합니다.", this);
    }

    private void TrySubscribeStateManager()
    {
        if (isStateSubscribed || PlayerStateManager.Instance == null) return;

        PlayerStateManager.Instance.OnStateEnter += HandlePlayerStateEnter;
        PlayerStateManager.Instance.OnStateExit += HandlePlayerStateExit;
        isStateSubscribed = true;
    }

    private void UnsubscribeStateManager()
    {
        if (!isStateSubscribed) return;

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter -= HandlePlayerStateEnter;
            PlayerStateManager.Instance.OnStateExit -= HandlePlayerStateExit;
        }
        isStateSubscribed = false;
    }

    private void HandlePlayerStateEnter(PlayerState state)
    {
        if (state == PlayerState.BreathingActive &&
            PlayerStateManager.Instance.IsBreathMissionOwner(BreathMissionId.Sandstorm) &&
            (CurrentState == State.Active || CurrentState == State.Calming))
        {
            isMissionOwnerActive = true;
            missionSuccessInProgress = false;
            SetActive(instructionUIRoot, true);
            if (breathCircleUI != null) breathCircleUI.Show(this);
            return;
        }

        if (state == PlayerState.Idle && isMissionOwnerActive && !missionSuccessInProgress)
        {
            isMissionOwnerActive = false;
            SetActive(instructionUIRoot, false);
            if (breathCircleUI != null) breathCircleUI.Hide(this);

            CurrentState = State.Active;
            LerpObscurity(1f, calmStepDuration);

            if (PlayerStateManager.Instance.IsBreathMissionOwner(BreathMissionId.Sandstorm))
                PlayerStateManager.Instance.ChangeState(PlayerState.MissionReady);
        }
    }

    private void HandlePlayerStateExit(PlayerState state)
    {
        if (state != PlayerState.BreathingActive || !isMissionOwnerActive) return;

        SetActive(instructionUIRoot, false);
        if (!missionSuccessInProgress && breathCircleUI != null) breathCircleUI.Hide(this);
    }

    private IEnumerator HideBreathUIAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);
        if (breathCircleUI != null) breathCircleUI.Hide(this);
        hideBreathUIRoutine = null;
    }

    private IEnumerator StopParticlesAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sandstormParticles != null)
            sandstormParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (hasCapturedEnvironment)
        {
            RenderSettings.fog = clearFogEnabled;
            RenderSettings.fogMode = clearFogMode;
            RenderSettings.fogColor = clearFogColor;
            RenderSettings.fogDensity = clearFogDensity;
        }
        StopWindLoop();
        stopParticlesRoutine = null;
    }

    private void CaptureEnvironmentSettings()
    {
        if (hasCapturedEnvironment) return;

        clearFogEnabled = RenderSettings.fog;
        clearFogMode = RenderSettings.fogMode;
        clearFogColor = RenderSettings.fogColor;
        clearFogDensity = RenderSettings.fogDensity;
        hasCapturedEnvironment = true;
    }

    public void AbortAndRestore()
    {
        if (!hasCapturedEnvironment && CurrentState == State.Idle) return;

        StopAllCoroutines();
        obscurityRoutine = null;
        introUIRoutine = null;
        activateAfterRiseRoutine = null;
        stopParticlesRoutine = null;
        hideBreathUIRoutine = null;

        ApplyObscurity(0f);
        if (sandstormParticles != null)
            sandstormParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (hasCapturedEnvironment)
        {
            RenderSettings.fog = clearFogEnabled;
            RenderSettings.fogMode = clearFogMode;
            RenderSettings.fogColor = clearFogColor;
            RenderSettings.fogDensity = clearFogDensity;
        }

        StopWindLoop();
        if (guidingLight != null)
            guidingLight.RestoreLightIntensity(0f);

        SetActive(chapterUIRoot, false);
        SetActive(zoneTextUIRoot, false);
        SetActive(instructionUIRoot, false);
        if (breathCircleUI != null) breathCircleUI.Hide(this);

        isMissionOwnerActive = false;
        missionSuccessInProgress = false;
        PlayerStateManager.Instance?.ReleaseBreathMission(BreathMissionId.Sandstorm);
    }

    // =========================================================
    // 비네트/파티클/Fog/SFX를 하나의 시각 강도로 구동
    // =========================================================
    private void LerpObscurity(float target, float duration)
    {
        if (obscurityRoutine != null) StopCoroutine(obscurityRoutine);
        obscurityRoutine = StartCoroutine(LerpObscurityRoutine(Mathf.Clamp01(target), duration));
    }

    private IEnumerator LerpObscurityRoutine(float target, float duration)
    {
        float start = currentObscurity;

        if (duration <= 0f)
        {
            ApplyObscurity(target);
            obscurityRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyObscurity(Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        ApplyObscurity(target);
        obscurityRoutine = null;
    }

    private void ApplyObscurity(float value)
    {
        currentObscurity = Mathf.Clamp01(value);

        if (hasEmissionModule)
            emissionModule.rateOverTime = maxEmissionRate * currentObscurity;

        if (dustVignetteRenderer != null)
        {
            if (vignettePropertyBlock == null) vignettePropertyBlock = new MaterialPropertyBlock();
            dustVignetteRenderer.GetPropertyBlock(vignettePropertyBlock);
            vignettePropertyBlock.SetFloat(ObscurityId, currentObscurity);
            vignettePropertyBlock.SetFloat(ClarityId, 1f - currentObscurity);
            dustVignetteRenderer.SetPropertyBlock(vignettePropertyBlock);
            dustVignetteRenderer.enabled = currentObscurity > 0.001f;
        }

        if (hasCapturedEnvironment)
        {
            RenderSettings.fog = currentObscurity > 0.001f || clearFogEnabled;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(clearFogColor, sandstormFogColor, currentObscurity);
            RenderSettings.fogDensity = maxFogDensity * currentObscurity;
        }

        if (windAudioSource != null)
            windAudioSource.volume = windSfxVolume * GetSfxVolumeScale() * currentObscurity;
    }

    private void EnsureVignetteCoverage()
    {
        Transform overlay = dustVignetteRenderer.transform;
        Camera camera = overlay.GetComponentInParent<Camera>();
        if (camera == null) return;

        // camera.fieldOfView/aspect는 XR 양안의 비대칭 projection을 충분히 나타내지 못한다.
        // 거리 비례 정사각 overscan으로 약 79도의 반시야각을 덮어 Quad 가장자리 노출을 막는다.
        float distance = Mathf.Max(camera.nearClipPlane + 0.03f, 0.08f);
        float coverage = distance * Mathf.Max(4f, vignetteOverscan);
        overlay.localPosition = new Vector3(0f, 0f, distance);
        overlay.localRotation = Quaternion.identity;
        overlay.localScale = new Vector3(coverage, coverage, 1f);
    }

    // =========================================================
    // 바람 SFX 루프 (옵션 AudioClip, SoomAudioManager 볼륨 반영)
    // =========================================================
    private void PlayWindLoop()
    {
        EnsureWindAudioSource();
        if (windAudioSource == null) return;

        windAudioSource.clip = windLoopSfx != null ? windLoopSfx : CreateProceduralWindClip();
        windAudioSource.volume = windSfxVolume * GetSfxVolumeScale() * currentObscurity;

        if (!windAudioSource.isPlaying) windAudioSource.Play();
    }

    private void StopWindLoop()
    {
        if (windAudioSource != null && windAudioSource.isPlaying)
        {
            windAudioSource.Stop();
        }
    }

    private static AudioClip CreateProceduralWindClip()
    {
        const int sampleRate = 22050;
        const int sampleCount = sampleRate * 2;
        float[] samples = new float[sampleCount];
        float smoothedNoise = 0f;
        uint state = 0x6D2B79F5u;

        for (int i = 0; i < sampleCount; i++)
        {
            state = state * 1664525u + 1013904223u;
            float whiteNoise = ((state >> 8) / 8388607.5f) - 1f;
            smoothedNoise = Mathf.Lerp(smoothedNoise, whiteNoise, 0.035f);
            float gust = 0.65f + 0.35f * Mathf.Sin(i * Mathf.PI * 2f / sampleCount * 3f);
            samples[i] = smoothedNoise * gust * 0.7f;
        }

        AudioClip clip = AudioClip.Create("ProceduralSandstormWind", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum AudioChannel { Master, BGM, SFX, Voice }

/// <summary>
/// 프로젝트의 단일 오디오 진입점. 채널 볼륨은 PlayerPrefs에 저장하고,
/// 이벤트별 클립과 상대 gain은 SoomAudioLibrarySO에서 읽는다.
/// </summary>
public class SoomAudioManager : MonoBehaviour
{
    private const string Scene03Name = "Scene_03_InGame_Outside";
    private const string PrefKeyPrefix = "Soom.Audio.Volume.";
    private const float DefaultVolume = 100f;
    private const float MinDb = -80f;

    public static SoomAudioManager Instance { get; private set; }
    public string CurrentSceneName => currentSceneName;
    public AudioClip CurrentBgm => currentBgm;
    public bool FootstepsPlaying => footstepsPlaying;

    [Header("Project audio data")]
    [SerializeField] private SoomAudioLibrarySO audioLibrary;
    [SerializeField] private BreathEventsSO breathEventsChannel;

    [Header("Optional mixer")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string bgmParam = "BGMVolume";
    [SerializeField] private string sfxParam = "SFXVolume";
    [SerializeField] private string voiceParam = "VoiceVolume";

    [Header("Channel sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource voiceSource;

    [Header("Runtime state (read only)")]
    [SerializeField] private string currentSceneName;
    [SerializeField] private AudioClip currentBgm;
    [SerializeField] private bool footstepsPlaying;

    private readonly Dictionary<AudioChannel, float> volumes = new Dictionary<AudioChannel, float>();
    private PlayerStateManager subscribedStateManager;
    private Coroutine sceneRefreshCoroutine;
    private Coroutine bgmFadeCoroutine;
    private Coroutine footstepCoroutine;
    private float bgmEventGain = 1f;
    private float bgmFadeFactor = 1f;
    private int lastFootstepIndex = -1;
    private int lastProcessedBreathLoopCount;
    private bool isPrimaryInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            // Scene 03에서는 같은 GameObject의 ScreenFader 등 다른 시스템을 유지해야 한다.
            Destroy(this);
            return;
        }

        Instance = this;
        isPrimaryInstance = true;
        DontDestroyOnLoad(gameObject);

        EnsureChannelSources();
        LoadAllVolumes();
    }

    private void OnEnable()
    {
        if (!isPrimaryInstance)
            return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SubscribeBreathEvents();
    }

    private void Start()
    {
        if (!isPrimaryInstance)
            return;

        RefreshSceneState();
        QueueSceneStateRefresh();
    }

    private void OnDisable()
    {
        if (!isPrimaryInstance)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        UnsubscribeBreathEvents();
        UnsubscribePlayerStateManager();
        StopFootsteps();

        if (sceneRefreshCoroutine != null)
        {
            StopCoroutine(sceneRefreshCoroutine);
            sceneRefreshCoroutine = null;
        }

        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureChannelSources()
    {
        bgmSource = EnsureSource(bgmSource, "BGMSource", true);
        sfxSource = EnsureSource(sfxSource, "SFXSource", false);
        voiceSource = EnsureSource(voiceSource, "VoiceSource", false);
    }

    private AudioSource EnsureSource(AudioSource source, string sourceName, bool loop)
    {
        if (source == null)
        {
            Transform existing = transform.Find(sourceName);
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            source = sourceObject.GetComponent<AudioSource>();
            if (source == null)
                source = sourceObject.AddComponent<AudioSource>();
        }

        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        if (mixer != null && source.outputAudioMixerGroup == null)
        {
            string groupName = sourceName.Replace("Source", string.Empty);
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(groupName);
            if (groups != null && groups.Length > 0)
                source.outputAudioMixerGroup = groups[0];
        }

        return source;
    }

    private void LoadAllVolumes()
    {
        foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
        {
            float value = Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeyPrefix + channel, DefaultVolume), 0f, 100f);
            volumes[channel] = value;
            ApplyVolume(channel, value);
        }

        RefreshSourceVolumes();
    }

    // ---------------------------------------------------------------- playback API

    public void PlayInteractionSfx()
    {
        if (audioLibrary != null)
            PlaySFX(audioLibrary.InteractionSfx, audioLibrary.InteractionVolume);
    }

    public void PlayFoxRevealSfx()
    {
        if (audioLibrary != null)
            PlaySFX(audioLibrary.FoxRevealSfx, audioLibrary.FoxRevealVolume);
    }

    public void PlayMembraneClearSfx()
    {
        if (audioLibrary != null)
            PlaySFX(audioLibrary.MembraneClearSfx, audioLibrary.MembraneClearVolume);
    }

    public void PlayBGM(AudioClip clip, bool restartIfSame = false)
    {
        if (clip == null || bgmSource == null)
            return;
        if (!restartIfSame && bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        StopBgmFade();
        bgmEventGain = 1f;
        bgmFadeFactor = 1f;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        currentBgm = clip;
        RefreshBgmVolume();
        bgmSource.Play();
    }

    public void StopBGM()
    {
        StopBgmFade();
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }

        currentBgm = null;
        bgmEventGain = 1f;
        bgmFadeFactor = 1f;
        RefreshBgmVolume();
    }

    /// <summary>겹쳐 재생할 수 있는 one-shot SFX.</summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void PlayVoice(AudioClip clip)
    {
        if (clip == null || voiceSource == null)
            return;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.loop = false;
        voiceSource.Play();
    }

    public void StopVoice()
    {
        if (voiceSource != null)
            voiceSource.Stop();
    }

    private void PlaySfxWithPitch(AudioClip clip, float volumeScale, float pitch)
    {
        if (clip == null || sfxSource == null)
            return;

        float originalPitch = sfxSource.pitch;
        sfxSource.pitch = Mathf.Max(0.01f, pitch);
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        sfxSource.pitch = originalPitch;
    }

    // ---------------------------------------------------------------- scene / BGM

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueSceneStateRefresh();
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        QueueSceneStateRefresh();
    }

    private void QueueSceneStateRefresh()
    {
        if (!isActiveAndEnabled)
            return;

        if (sceneRefreshCoroutine != null)
            StopCoroutine(sceneRefreshCoroutine);
        sceneRefreshCoroutine = StartCoroutine(RefreshSceneStateNextFrame());
    }

    private IEnumerator RefreshSceneStateNextFrame()
    {
        yield return null;
        sceneRefreshCoroutine = null;
        RefreshSceneState();
    }

    private void RefreshSceneState()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        currentSceneName = activeScene.IsValid() ? activeScene.name : string.Empty;

        SubscribePlayerStateManager(PlayerStateManager.Instance);

        if (currentSceneName == Scene03Name)
            StartScene03Bgm();
        else
            StopScene03Bgm();

        EvaluateFootsteps();
    }

    private void StartScene03Bgm()
    {
        if (audioLibrary == null || audioLibrary.Scene03Bgm == null || bgmSource == null)
            return;

        AudioClip sceneBgm = audioLibrary.Scene03Bgm;
        bool alreadyPlaying = bgmSource.clip == sceneBgm && bgmSource.isPlaying;
        bgmEventGain = audioLibrary.Scene03BgmVolume;
        currentBgm = sceneBgm;

        if (!alreadyPlaying)
        {
            StopBgmFade();
            bgmSource.clip = sceneBgm;
            bgmSource.loop = true;
            bgmFadeFactor = 0f;
            RefreshBgmVolume();
            bgmSource.Play();
        }

        StartBgmFade(1f, audioLibrary.BgmFadeDuration, false);
    }

    private void StopScene03Bgm()
    {
        if (audioLibrary == null || bgmSource == null || bgmSource.clip != audioLibrary.Scene03Bgm)
            return;

        StartBgmFade(0f, audioLibrary.BgmFadeDuration, true);
    }

    private void StartBgmFade(float target, float duration, bool stopAfterFade)
    {
        StopBgmFade();
        bgmFadeCoroutine = StartCoroutine(FadeBgm(Mathf.Clamp01(target), duration, stopAfterFade));
    }

    private IEnumerator FadeBgm(float target, float duration, bool stopAfterFade)
    {
        float start = bgmFadeFactor;
        float elapsed = 0f;

        while (elapsed < duration && !Mathf.Approximately(bgmFadeFactor, target))
        {
            elapsed += Time.unscaledDeltaTime;
            bgmFadeFactor = duration <= 0f ? target : Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            RefreshBgmVolume();
            yield return null;
        }

        bgmFadeFactor = target;
        RefreshBgmVolume();
        bgmFadeCoroutine = null;

        if (stopAfterFade && bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
            currentBgm = null;
            bgmEventGain = 1f;
            bgmFadeFactor = 1f;
            RefreshBgmVolume();
        }
    }

    private void StopBgmFade()
    {
        if (bgmFadeCoroutine == null)
            return;

        StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = null;
    }

    // ---------------------------------------------------------------- player state / footsteps

    private void SubscribePlayerStateManager(PlayerStateManager stateManager)
    {
        if (subscribedStateManager == stateManager)
            return;

        UnsubscribePlayerStateManager();
        if (stateManager == null)
            return;

        subscribedStateManager = stateManager;
        subscribedStateManager.OnStateEnter += HandlePlayerStateEnter;
        subscribedStateManager.OnStateExit += HandlePlayerStateExit;
    }

    private void UnsubscribePlayerStateManager()
    {
        if (subscribedStateManager != null)
        {
            subscribedStateManager.OnStateEnter -= HandlePlayerStateEnter;
            subscribedStateManager.OnStateExit -= HandlePlayerStateExit;
        }

        subscribedStateManager = null;
    }

    private void HandlePlayerStateEnter(PlayerState state)
    {
        EvaluateFootsteps();
    }

    private void HandlePlayerStateExit(PlayerState state)
    {
        if (state == PlayerState.Move)
            StopFootsteps();
    }

    private void EvaluateFootsteps()
    {
        bool shouldPlay = currentSceneName == Scene03Name &&
                          subscribedStateManager != null &&
                          subscribedStateManager.CurrentState == PlayerState.Move &&
                          HasFootstepClip();

        if (shouldPlay)
            StartFootsteps();
        else
            StopFootsteps();
    }

    private bool HasFootstepClip()
    {
        if (audioLibrary == null || audioLibrary.SandFootstepClips == null)
            return false;

        foreach (AudioClip clip in audioLibrary.SandFootstepClips)
        {
            if (clip != null)
                return true;
        }

        return false;
    }

    private void StartFootsteps()
    {
        if (footstepCoroutine != null)
            return;

        footstepCoroutine = StartCoroutine(FootstepLoop());
        footstepsPlaying = true;
    }

    private void StopFootsteps()
    {
        if (footstepCoroutine != null)
        {
            StopCoroutine(footstepCoroutine);
            footstepCoroutine = null;
        }

        footstepsPlaying = false;
        lastFootstepIndex = -1;
    }

    private IEnumerator FootstepLoop()
    {
        while (currentSceneName == Scene03Name &&
               subscribedStateManager != null &&
               subscribedStateManager.CurrentState == PlayerState.Move)
        {
            PlayNextFootstep();
            yield return new WaitForSeconds(audioLibrary != null ? audioLibrary.FootstepInterval : 0.5f);
        }

        footstepCoroutine = null;
        footstepsPlaying = false;
        lastFootstepIndex = -1;
    }

    private void PlayNextFootstep()
    {
        if (audioLibrary == null)
            return;

        AudioClip[] clips = audioLibrary.SandFootstepClips;
        if (clips == null || clips.Length == 0)
            return;

        List<int> validIndices = new List<int>();
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && (i != lastFootstepIndex || validIndices.Count == 0))
                validIndices.Add(i);
        }

        if (validIndices.Count > 1 && validIndices.Contains(lastFootstepIndex))
            validIndices.Remove(lastFootstepIndex);
        if (validIndices.Count == 0)
            return;

        int selectedIndex = validIndices[Random.Range(0, validIndices.Count)];
        lastFootstepIndex = selectedIndex;
        Vector2 pitchRange = audioLibrary.FootstepPitchRange;
        float pitch = Random.Range(pitchRange.x, pitchRange.y);
        PlaySfxWithPitch(clips[selectedIndex], audioLibrary.SandFootstepVolume, pitch);
    }

    // ---------------------------------------------------------------- breath events

    private void SubscribeBreathEvents()
    {
        if (breathEventsChannel == null)
            return;

        lastProcessedBreathLoopCount = breathEventsChannel.CurrentLoopCount;
        breathEventsChannel.OnBreathLoopCompleted += HandleBreathLoopCompleted;
        breathEventsChannel.OnBreathValueNormalized += HandleBreathValueNormalized;
    }

    private void UnsubscribeBreathEvents()
    {
        if (breathEventsChannel != null)
        {
            breathEventsChannel.OnBreathLoopCompleted -= HandleBreathLoopCompleted;
            breathEventsChannel.OnBreathValueNormalized -= HandleBreathValueNormalized;
        }

        lastProcessedBreathLoopCount = 0;
    }

    private void HandleBreathLoopCompleted(int loopCount)
    {
        if (loopCount <= lastProcessedBreathLoopCount)
            return;

        lastProcessedBreathLoopCount = loopCount;
        if (audioLibrary != null)
            PlaySFX(audioLibrary.BreathLoopSuccessSfx, audioLibrary.BreathLoopSuccessVolume);
    }

    private void HandleBreathValueNormalized(float value)
    {
        if (breathEventsChannel != null && breathEventsChannel.CurrentLoopCount == 0)
            lastProcessedBreathLoopCount = 0;
    }

    // ---------------------------------------------------------------- volume API

    public void SetVolume(AudioChannel channel, float volume0to100)
    {
        float clamped = Mathf.Clamp(volume0to100, 0f, 100f);
        volumes[channel] = clamped;
        PlayerPrefs.SetFloat(PrefKeyPrefix + channel, clamped);
        PlayerPrefs.Save();
        ApplyVolume(channel, clamped);
        RefreshSourceVolumes();
    }

    public float GetVolume(AudioChannel channel)
    {
        return volumes.TryGetValue(channel, out float value) ? value : DefaultVolume;
    }

    private void ApplyVolume(AudioChannel channel, float volume0to100)
    {
        float normalized = Mathf.Clamp01(volume0to100 / 100f);

        if (mixer != null)
        {
            string parameter = ParamFor(channel);
            if (!string.IsNullOrEmpty(parameter))
            {
                float db = normalized <= 0.0001f ? MinDb : Mathf.Log10(normalized) * 20f;
                mixer.SetFloat(parameter, Mathf.Clamp(db, MinDb, 0f));
            }
            return;
        }

        if (channel == AudioChannel.Master)
            AudioListener.volume = normalized;
    }

    private void RefreshSourceVolumes()
    {
        if (mixer != null)
        {
            if (sfxSource != null) sfxSource.volume = 1f;
            if (voiceSource != null) voiceSource.volume = 1f;
        }
        else
        {
            if (sfxSource != null) sfxSource.volume = ChannelNormalized(AudioChannel.SFX);
            if (voiceSource != null) voiceSource.volume = ChannelNormalized(AudioChannel.Voice);
        }

        RefreshBgmVolume();
    }

    private void RefreshBgmVolume()
    {
        if (bgmSource == null)
            return;

        float channelGain = mixer != null ? 1f : ChannelNormalized(AudioChannel.BGM);
        bgmSource.volume = channelGain * Mathf.Clamp01(bgmEventGain) * Mathf.Clamp01(bgmFadeFactor);
    }

    private float ChannelNormalized(AudioChannel channel)
    {
        return Mathf.Clamp01(GetVolume(channel) / 100f);
    }

    private string ParamFor(AudioChannel channel)
    {
        switch (channel)
        {
            case AudioChannel.Master: return masterParam;
            case AudioChannel.BGM: return bgmParam;
            case AudioChannel.SFX: return sfxParam;
            case AudioChannel.Voice: return voiceParam;
            default: return null;
        }
    }
}

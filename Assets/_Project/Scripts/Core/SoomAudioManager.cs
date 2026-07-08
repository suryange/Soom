// SoomAudioManager.cs
// Audio backbone: four channels (Master/BGM/SFX/Voice), volumes 0-100 saved to PlayerPrefs.
//
// The spec calls for "Audio Mixer volume control", but there is no public Unity API to create an
// AudioMixer asset from script (it's editor-serialized data with no ScriptableObject-style
// factory). So this manager takes an optional AudioMixer reference: if it's wired up (exposed
// parameters named "MasterVolume"/"BGMVolume"/"SFXVolume"/"VoiceVolume", see manual steps below)
// it drives those; if left empty it falls back to controlling each channel's AudioSource.volume
// directly. Either way SetVolume/GetVolume behave the same from the caller's point of view.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum AudioChannel { Master, BGM, SFX, Voice }

/// <summary>
/// Singleton audio manager. Persists across scene loads. Wire an AudioMixer in the inspector
/// once one exists (Assets/Create/Audio Mixer) for proper mixer-driven volume; otherwise it
/// runs fine with plain AudioSources.
/// </summary>
public class SoomAudioManager : MonoBehaviour
{
    public static SoomAudioManager Instance { get; private set; }

    [Header("Optional mixer (manual step — see class comment)")]
    [SerializeField] AudioMixer mixer;
    [SerializeField] string masterParam = "MasterVolume";
    [SerializeField] string bgmParam = "BGMVolume";
    [SerializeField] string sfxParam = "SFXVolume";
    [SerializeField] string voiceParam = "VoiceVolume";

    [Header("Fallback sources (used when mixer is not assigned)")]
    [SerializeField] AudioSource bgmSource;
    [SerializeField] AudioSource sfxSource;
    [SerializeField] AudioSource voiceSource;

    const string PrefKeyPrefix = "Soom.Audio.Volume.";
    const float DefaultVolume = 100f;
    const float MinDb = -80f; // effectively silent for mixer attenuation curves

    readonly Dictionary<AudioChannel, float> _volumes = new Dictionary<AudioChannel, float>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureFallbackSources();
        LoadAllVolumes();
    }

    void EnsureFallbackSources()
    {
        if (bgmSource == null) bgmSource = CreateSource("BGMSource", loop: true);
        if (sfxSource == null) sfxSource = CreateSource("SFXSource", loop: false);
        if (voiceSource == null) voiceSource = CreateSource("VoiceSource", loop: false);
    }

    AudioSource CreateSource(string name, bool loop)
    {
        var existing = transform.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.GetComponent<AudioSource>();
        if (src == null) src = go.AddComponent<AudioSource>();
        src.loop = loop;
        src.playOnAwake = false;
        if (mixer != null)
        {
            var groupName = name.Replace("Source", "");
            var groups = mixer.FindMatchingGroups(groupName);
            if (groups != null && groups.Length > 0) src.outputAudioMixerGroup = groups[0];
        }
        return src;
    }

    void LoadAllVolumes()
    {
        foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
        {
            float v = PlayerPrefs.GetFloat(PrefKeyPrefix + channel, DefaultVolume);
            _volumes[channel] = v;
            ApplyVolume(channel, v);
        }
    }

    // ---------------------------------------------------------------- playback API
    public void PlayBGM(AudioClip clip, bool restartIfSame = false)
    {
        if (clip == null || bgmSource == null) return;
        if (!restartIfSame && bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    /// <summary>One-shot SFX; multiple can overlap.</summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    /// <summary>Guidance/narration voice line. Replaces whatever voice line is currently playing.</summary>
    public void PlayVoice(AudioClip clip)
    {
        if (clip == null || voiceSource == null) return;
        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.loop = false;
        voiceSource.Play();
    }

    public void StopVoice()
    {
        if (voiceSource != null) voiceSource.Stop();
    }

    // ---------------------------------------------------------------- volume API
    /// <summary>Set a channel's volume, 0-100. Persisted to PlayerPrefs immediately.</summary>
    public void SetVolume(AudioChannel channel, float volume0to100)
    {
        float clamped = Mathf.Clamp(volume0to100, 0f, 100f);
        _volumes[channel] = clamped;
        PlayerPrefs.SetFloat(PrefKeyPrefix + channel, clamped);
        PlayerPrefs.Save();
        ApplyVolume(channel, clamped);
    }

    /// <summary>Current volume for a channel, 0-100.</summary>
    public float GetVolume(AudioChannel channel)
    {
        return _volumes.TryGetValue(channel, out var v) ? v : DefaultVolume;
    }

    void ApplyVolume(AudioChannel channel, float volume0to100)
    {
        float normalized = Mathf.Clamp01(volume0to100 / 100f);

        if (mixer != null)
        {
            string param = ParamFor(channel);
            if (!string.IsNullOrEmpty(param))
            {
                // Exposed mixer params are in dB; map 0-100 -> MinDb..0dB (log-ish curve via log10).
                float db = normalized <= 0.0001f ? MinDb : Mathf.Log10(normalized) * 20f;
                mixer.SetFloat(param, Mathf.Clamp(db, MinDb, 0f));
            }
            // Master also scales the fallback sources' base volume in case some sources
            // aren't routed through the mixer yet.
            if (channel != AudioChannel.Master) return;
        }

        switch (channel)
        {
            case AudioChannel.Master:
                AudioListener.volume = normalized;
                break;
            case AudioChannel.BGM:
                if (bgmSource != null) bgmSource.volume = normalized;
                break;
            case AudioChannel.SFX:
                if (sfxSource != null) sfxSource.volume = normalized;
                break;
            case AudioChannel.Voice:
                if (voiceSource != null) voiceSource.volume = normalized;
                break;
        }
    }

    string ParamFor(AudioChannel channel) => channel switch
    {
        AudioChannel.Master => masterParam,
        AudioChannel.BGM => bgmParam,
        AudioChannel.SFX => sfxParam,
        AudioChannel.Voice => voiceParam,
        _ => null,
    };
}

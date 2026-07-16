using UnityEngine;

[CreateAssetMenu(fileName = "SoomAudioLibrary", menuName = "SOOM/Audio/Audio Library")]
public sealed class SoomAudioLibrarySO : ScriptableObject
{
    [Header("Interaction")]
    [SerializeField] private AudioClip interactionSfx;
    [SerializeField, Range(0f, 1f)] private float interactionVolume = 1f;

    [Header("Scene 03 BGM")]
    [SerializeField] private AudioClip scene03Bgm;
    [SerializeField, Range(0f, 1f)] private float scene03BgmVolume = 1f;
    [SerializeField, Min(0f)] private float bgmFadeDuration = 1f;

    [Header("Sand Footstep")]
    [SerializeField] private AudioClip[] sandFootstepClips = System.Array.Empty<AudioClip>();
    [SerializeField, Range(0f, 1f)] private float sandFootstepVolume = 1f;
    [SerializeField, Min(0.05f)] private float footstepInterval = 0.5f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.95f, 1.05f);

    [Header("Fox")]
    [SerializeField] private AudioClip foxRevealSfx;
    [SerializeField, Range(0f, 1f)] private float foxRevealVolume = 1f;
    [SerializeField] private AudioClip membraneClearSfx;
    [SerializeField, Range(0f, 1f)] private float membraneClearVolume = 1f;

    [Header("Breath")]
    [SerializeField] private AudioClip breathLoopSuccessSfx;
    [SerializeField, Range(0f, 1f)] private float breathLoopSuccessVolume = 1f;

    public AudioClip InteractionSfx => interactionSfx;
    public float InteractionVolume => interactionVolume;
    public AudioClip Scene03Bgm => scene03Bgm;
    public float Scene03BgmVolume => scene03BgmVolume;
    public float BgmFadeDuration => bgmFadeDuration;
    public AudioClip[] SandFootstepClips => sandFootstepClips;
    public float SandFootstepVolume => sandFootstepVolume;
    public float FootstepInterval => Mathf.Max(0.05f, footstepInterval);
    public Vector2 FootstepPitchRange => footstepPitchRange;
    public AudioClip FoxRevealSfx => foxRevealSfx;
    public float FoxRevealVolume => foxRevealVolume;
    public AudioClip MembraneClearSfx => membraneClearSfx;
    public float MembraneClearVolume => membraneClearVolume;
    public AudioClip BreathLoopSuccessSfx => breathLoopSuccessSfx;
    public float BreathLoopSuccessVolume => breathLoopSuccessVolume;

    private void OnValidate()
    {
        footstepInterval = Mathf.Max(0.05f, footstepInterval);
        float minimumPitch = Mathf.Max(0.01f, Mathf.Min(footstepPitchRange.x, footstepPitchRange.y));
        float maximumPitch = Mathf.Max(minimumPitch, Mathf.Max(footstepPitchRange.x, footstepPitchRange.y));
        footstepPitchRange = new Vector2(minimumPitch, maximumPitch);
    }
}

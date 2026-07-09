using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스타팅 화면의 환경 설정 패널(기능 명세 1.2.2)을 담당합니다.
/// (a) 시점 초기화(Recenter), (b) Master/BGM/SFX/Voice 볼륨 슬라이더, (c) Continuous/Snap Turn 토글.
/// </summary>
public class EnvironmentSettingsPanel : MonoBehaviour
{
    [Header("(a) 시점 초기화 (Recenter)")]
    [Tooltip("리셋 대상이 되는 XR Origin 루트 Transform")]
    [SerializeField] private Transform xrOriginTransform;
    [Tooltip("리셋 기준이 되는 사전 지정 Origin Transform (Position/Rotation)")]
    [SerializeField] private Transform recenterTargetPose;
    [SerializeField] private Button recenterButton;

    [Header("(b) 볼륨 슬라이더 (0~100)")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider voiceVolumeSlider;

    [Header("(c) Turn 모드 토글 (on = Continuous, off = Snap)")]
    [SerializeField] private Toggle continuousTurnToggle;
    [SerializeField] private TurnModeSwitcher turnModeSwitcher;

    private void OnEnable()
    {
        if (recenterButton != null) recenterButton.onClick.AddListener(RecenterView);

        BindVolumeSlider(masterVolumeSlider, AudioChannel.Master);
        BindVolumeSlider(bgmVolumeSlider, AudioChannel.BGM);
        BindVolumeSlider(sfxVolumeSlider, AudioChannel.SFX);
        BindVolumeSlider(voiceVolumeSlider, AudioChannel.Voice);

        if (continuousTurnToggle != null)
        {
            if (turnModeSwitcher != null)
                continuousTurnToggle.SetIsOnWithoutNotify(turnModeSwitcher.IsContinuousActive);
            continuousTurnToggle.onValueChanged.AddListener(OnTurnToggleChanged);
        }
    }

    private void OnDisable()
    {
        if (recenterButton != null) recenterButton.onClick.RemoveListener(RecenterView);

        UnbindVolumeSlider(masterVolumeSlider);
        UnbindVolumeSlider(bgmVolumeSlider);
        UnbindVolumeSlider(sfxVolumeSlider);
        UnbindVolumeSlider(voiceVolumeSlider);

        if (continuousTurnToggle != null)
            continuousTurnToggle.onValueChanged.RemoveListener(OnTurnToggleChanged);
    }

    private void BindVolumeSlider(Slider slider, AudioChannel channel)
    {
        if (slider == null) return;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        if (SoomAudioManager.Instance != null)
            slider.SetValueWithoutNotify(SoomAudioManager.Instance.GetVolume(channel));
        slider.onValueChanged.AddListener(value => OnVolumeChanged(channel, value));
    }

    private void UnbindVolumeSlider(Slider slider)
    {
        if (slider == null) return;
        slider.onValueChanged.RemoveAllListeners();
    }

    private void OnVolumeChanged(AudioChannel channel, float value)
    {
        if (SoomAudioManager.Instance != null)
            SoomAudioManager.Instance.SetVolume(channel, value);
    }

    private void OnTurnToggleChanged(bool isContinuous)
    {
        if (turnModeSwitcher != null)
            turnModeSwitcher.SetContinuousActive(isContinuous);
    }

    /// <summary>XR Origin의 Position/Rotation을 사전 지정된 Origin Transform 기준으로 리셋합니다.</summary>
    public void RecenterView()
    {
        if (xrOriginTransform == null || recenterTargetPose == null)
        {
            Debug.LogWarning("[EnvironmentSettingsPanel] Recenter에 필요한 Transform이 설정되지 않았습니다.");
            return;
        }
        xrOriginTransform.SetPositionAndRotation(recenterTargetPose.position, recenterTargetPose.rotation);
    }
}

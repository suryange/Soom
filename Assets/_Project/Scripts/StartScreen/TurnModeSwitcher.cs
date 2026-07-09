using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// XRI Continuous Turn Provider ↔ Snap Turn Provider 컴포넌트를 서로 배타적으로 활성/비활성 전환합니다.
/// (기능 명세 1.2.2-c). XRI 3.3.2 기준 실제 클래스는
/// UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.ContinuousTurnProvider / SnapTurnProvider 이며,
/// 둘 다 LocomotionProvider(MonoBehaviour)를 상속하므로 enabled 플래그로 켜고 끌 수 있습니다.
/// 두 참조 모두 옵션이며, 씬에 배치되지 않았다면 아무 동작도 하지 않습니다(null 가드).
/// </summary>
public class TurnModeSwitcher : MonoBehaviour
{
    [Header("XRI Turn Provider 참조 (옵션)")]
    [SerializeField] private ContinuousTurnProvider continuousTurnProvider;
    [SerializeField] private SnapTurnProvider snapTurnProvider;

    [Tooltip("true = 시작 시 Continuous Turn 활성화, false = 시작 시 Snap Turn 활성화")]
    [SerializeField] private bool defaultToContinuous = true;

    /// <summary>현재 Continuous Turn이 활성화되어 있는지 여부.</summary>
    public bool IsContinuousActive => continuousTurnProvider != null && continuousTurnProvider.enabled;

    private void Start()
    {
        SetContinuousActive(defaultToContinuous);
    }

    /// <summary>true면 Continuous Turn을, false면 Snap Turn을 활성화합니다.</summary>
    public void SetContinuousActive(bool isContinuous)
    {
        if (continuousTurnProvider != null) continuousTurnProvider.enabled = isContinuous;
        if (snapTurnProvider != null) snapTurnProvider.enabled = !isContinuous;
    }
}

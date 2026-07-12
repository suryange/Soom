using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 여우와의 조우 (기능 명세서 5장) 전체 흐름을 관장하는 컨트롤러.
///
/// 단계: Detected -> Wary(경계, 5.2) -> FocusBreath(호흡 콘텐츠 D, 5.3) -> Revealed(상태 전환/VFX, 5.4)
///       -> MembraneBreath(호흡 콘텐츠 E, 5.5) -> Cleared -> Companion(동료 합류, 5.6)
///
/// 호흡 시퀀스는 공용 BreathEventsSO/BreathManager/BreathCircleUI를 그대로 재사용한다.
/// 이 컨트롤러는 breathEvents의 OnBreathLoopCompleted/OnMissionSuccess를 구독해 현재 단계에 맞게
/// 해석만 할 뿐, 호흡 계산 자체는 건드리지 않는다.
///
/// 실제 여우 모델(fox_sample_2.fbx)과 그로부터 생성한 Fox_Encounter 애니메이터 컨트롤러를 사용한다.
/// 컨트롤러의 상태 이름은 의미 기준("Wary"/"Joy")으로 두고, 각 상태가 실제 FBX 클립
/// (Action1_Alert / Action4_Standing_Happy)을 재생한다. 컨트롤러/상태가 아직 없어도
/// Animator 관련 호출은 전부 null/HasState 가드를 거치므로 흐름 자체는 그대로 동작한다.
/// </summary>
public class FoxEncounterController : MonoBehaviour
{
    public enum EncounterPhase
    {
        Detected,
        Wary,
        FocusBreath,
        Revealed,
        MembraneBreath,
        Cleared,
        Companion
    }

    [Header("데이터 / 이벤트 채널")]
    [SerializeField] private InteractableDataSO data;
    [SerializeField] private BreathEventsSO breathEvents;

    [Header("상태 UI — 여우 머리 위 (FaceCamera로 항상 카메라를 향함)")]
    [SerializeField] private GameObject statusPanelRoot;
    [SerializeField] private TMP_Text statusText;

    [Header("상호작용 안내 UI (지시문 + 액션 버튼)")]
    [SerializeField] private GameObject instructionPanelRoot;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private GameObject actionButtonRoot;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionButtonLabel;

    [Header("단계별 아이콘 (빌더가 phaseIcon Image 연결 — 스프라이트는 GlassUIKit에서 런타임 취득)")]
    [SerializeField] private Image phaseIcon;

    [Header("공용 호흡 UI (BreathCircleUI 재사용)")]
    [SerializeField] private BreathCircleUI breathCircleUI;

    [Header("불안의 막 VFX (명세 5.4/5.5)")]
    [SerializeField] private GameObject membraneObject;
    [SerializeField] private Renderer membraneRenderer;
    [Tooltip("BreathManager.targetLoopCount와 동일하게 맞춘다 (기본 3회 성공 = 3단계 감소)")]
    [SerializeField] private int membraneBreathSteps = 3;

    [Header("여우 애니메이터 (Fox_Encounter 컨트롤러 — 빌더가 자동 생성/연결)")]
    [SerializeField] private Animator foxAnimator;
    [Tooltip("경계 상태에서 재생할 상태 이름 (Action1_Alert 클립). 명세 5.2.2 'Sit_Growl'에 대응")]
    [SerializeField] private string waryStateName = "Wary";
    [Tooltip("동료 합류 시 재생할 상태 이름 (Action4_Standing_Happy 클립). 명세 5.6.1 'Stand_Joy'에 대응")]
    [SerializeField] private string joyStateName = "Joy";

    [Header("동료 합류 (명세 5.6)")]
    [SerializeField] private FoxCompanionFollower companionFollower;

    // 텍스트 자산이 아직 없을 때를 위한 기본값 (spec 원문 그대로)
    private const string DefaultFocusBreathGuide =
        "숨의 호흡 능력은 동료의 집중된 마음을 알아볼 수 있습니다. 깊게 호흡하세요";
    private const string MembraneBreathGuide =
        "안정된 호흡을 통해 불안의 막을 제거하고, 여우의 호흡을 안정시키세요 — 깊게 들이쉬고, 내쉬세요";
    private const string RevealedInstruction = "불안의 막을 제거하고 여우와 동료 되기";
    private const string WaryStatusText = "경계하는 여우";
    private const string RevealedStatusText = "불안함, 외로움";
    private const string CompanionStatusText = "언제나 함께";

    private Material _membraneMaterialInstance;

    public EncounterPhase CurrentPhase { get; private set; } = EncounterPhase.Detected;

    private void Awake()
    {
        if (actionButton != null) actionButton.onClick.AddListener(OnActionButtonClicked);
    }

    private void OnDestroy()
    {
        if (actionButton != null) actionButton.onClick.RemoveListener(OnActionButtonClicked);
    }

    private void OnEnable()
    {
        if (breathEvents != null)
        {
            breathEvents.OnBreathLoopCompleted += HandleLoopCompleted;
            breathEvents.OnMissionSuccess += HandleMissionSuccess;
        }
    }

    private void OnDisable()
    {
        if (breathEvents != null)
        {
            breathEvents.OnBreathLoopCompleted -= HandleLoopCompleted;
            breathEvents.OnMissionSuccess -= HandleMissionSuccess;
        }
    }

    // ========================================================
    // FoxInteractable에서 전달받는 통지
    // ========================================================

    /// <summary>여우가 처음 감지되었을 때(FoxInteractable.ShowUI) 호출 — 경계 상태로 전이.</summary>
    public void NotifyDetected()
    {
        if (CurrentPhase != EncounterPhase.Detected) return;
        EnterWary();
    }

    /// <summary>
    /// InteractionManager를 통한 상호작용 시작 통지. 현재 단계의 액션 버튼과 동일하게 동작해
    /// 버튼 클릭 경로와 향후 추가될 수 있는 다른 입력 경로가 항상 같은 결과를 내도록 한다.
    /// </summary>
    public void NotifyInteractBegin() => OnActionButtonClicked();

    public void NotifyInteractEnd() { /* 현재 특별히 처리할 것 없음 */ }

    // ========================================================
    // 단계 전이
    // ========================================================

    private void EnterWary()
    {
        CurrentPhase = EncounterPhase.Wary;

        if (statusPanelRoot != null) statusPanelRoot.SetActive(true);
        if (statusText != null) statusText.text = WaryStatusText;

        SafeAnimatorPlay(waryStateName);

        string guide = data != null && !string.IsNullOrEmpty(data.missionGuideText)
            ? data.missionGuideText
            : DefaultFocusBreathGuide;
        SetPhaseIcon(GlassUIKit.IconBreath);
        ShowInstructionAndAction(guide, "호흡 시작");
    }

    /// <summary>명세 5.3 — 호흡 콘텐츠 D 시작 (집중).</summary>
    private void BeginFocusBreath()
    {
        if (CurrentPhase != EncounterPhase.Wary) return;
        CurrentPhase = EncounterPhase.FocusBreath;

        HideActionButton();
        if (breathCircleUI != null) breathCircleUI.Show();

        if (PlayerStateManager.Instance != null)
            PlayerStateManager.Instance.ChangeState(PlayerState.BreathingActive);
    }

    /// <summary>명세 5.4 — 상태 전환 및 불안의 막 VFX 노출.</summary>
    private void RevealAnxiety()
    {
        CurrentPhase = EncounterPhase.Revealed;

        if (breathCircleUI != null) breathCircleUI.Hide();
        if (statusText != null) statusText.text = RevealedStatusText;

        if (membraneObject != null) membraneObject.SetActive(true);
        SetMembraneAlpha(1f);

        SetPhaseIcon(GlassUIKit.IconCheck);
        ShowInstructionAndAction(RevealedInstruction, "불안의 막 제거");
    }

    /// <summary>명세 5.5 — 호흡 콘텐츠 E 시작 (불안의 막 제거).</summary>
    private void BeginMembraneBreath()
    {
        if (CurrentPhase != EncounterPhase.Revealed) return;
        CurrentPhase = EncounterPhase.MembraneBreath;

        if (instructionText != null) instructionText.text = MembraneBreathGuide;
        HideActionButton();
        if (breathCircleUI != null) breathCircleUI.Show();

        if (PlayerStateManager.Instance != null)
            PlayerStateManager.Instance.ChangeState(PlayerState.BreathingActive);
    }

    /// <summary>불안의 막 제거 완료 — 동료 되기 버튼 노출.</summary>
    private void ClearMembrane()
    {
        CurrentPhase = EncounterPhase.Cleared;

        if (breathCircleUI != null) breathCircleUI.Hide();

        SetMembraneAlpha(0f);
        if (membraneObject != null) Destroy(membraneObject);

        // 5.6 전용 지시문 문단은 없으므로 액션 버튼("동료 되기")만 노출한다.
        SetPhaseIcon(GlassUIKit.IconHeart);
        ShowInstructionAndAction(string.Empty, "동료 되기");
    }

    /// <summary>명세 5.6 — '동료 되기' 버튼 클릭 시 최종 합류.</summary>
    private void BeginCompanion()
    {
        if (CurrentPhase != EncounterPhase.Cleared) return;
        CurrentPhase = EncounterPhase.Companion;

        HideActionButton();
        if (instructionPanelRoot != null) instructionPanelRoot.SetActive(false);

        if (statusText != null) statusText.text = CompanionStatusText;
        SafeAnimatorPlay(joyStateName);

        if (companionFollower != null) companionFollower.BeginFollowing();
    }

    // ========================================================
    // 호흡 이벤트 핸들러 (공용 BreathEventsSO)
    // ========================================================

    private void HandleLoopCompleted(int loopCount)
    {
        // 콘텐츠 E(불안의 막 제거) 중일 때만 루프마다 막을 단계적으로 옅게 만든다.
        if (CurrentPhase == EncounterPhase.MembraneBreath)
        {
            float alpha = membraneBreathSteps > 0
                ? Mathf.Clamp01(1f - (float)loopCount / membraneBreathSteps)
                : 0f;
            SetMembraneAlpha(alpha);
        }
    }

    private void HandleMissionSuccess()
    {
        switch (CurrentPhase)
        {
            case EncounterPhase.FocusBreath:
                RevealAnxiety();
                break;
            case EncounterPhase.MembraneBreath:
                ClearMembrane();
                break;
        }
    }

    // ========================================================
    // 액션 버튼 (단계별로 라벨/동작이 바뀌는 단일 버튼 재사용)
    // ========================================================

    private void OnActionButtonClicked()
    {
        switch (CurrentPhase)
        {
            case EncounterPhase.Wary:
                BeginFocusBreath();
                break;
            case EncounterPhase.Revealed:
                BeginMembraneBreath();
                break;
            case EncounterPhase.Cleared:
                BeginCompanion();
                break;
            // 그 외 단계에서는 버튼이 보이지 않아야 하므로 무시한다.
        }
    }

    private void ShowInstructionAndAction(string instruction, string buttonLabel)
    {
        if (instructionPanelRoot != null) instructionPanelRoot.SetActive(true);
        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(!string.IsNullOrEmpty(instruction));
            instructionText.text = instruction;
        }
        if (actionButtonRoot != null) actionButtonRoot.SetActive(true);
        if (actionButtonLabel != null) actionButtonLabel.text = buttonLabel;
    }

    private void HideActionButton()
    {
        if (actionButtonRoot != null) actionButtonRoot.SetActive(false);
    }

    /// <summary>현재 단계 아이콘 교체 (phaseIcon/스프라이트가 없으면 조용히 무시).</summary>
    private void SetPhaseIcon(Sprite sprite)
    {
        if (phaseIcon == null) return;
        if (sprite != null) phaseIcon.sprite = sprite;
        phaseIcon.gameObject.SetActive(phaseIcon.sprite != null);
    }

    // ========================================================
    // 불안의 막 VFX 헬퍼
    // ========================================================

    private void SetMembraneAlpha(float alpha)
    {
        if (membraneRenderer == null) return;
        if (_membraneMaterialInstance == null) _membraneMaterialInstance = membraneRenderer.material;
        if (_membraneMaterialInstance.HasProperty("_Alpha"))
            _membraneMaterialInstance.SetFloat("_Alpha", alpha);
    }

    // ========================================================
    // Animator 헬퍼 (모델/컨트롤러 없음 -> 항상 안전하게 무시)
    // ========================================================

    private void SafeAnimatorPlay(string stateName)
    {
        if (foxAnimator == null || foxAnimator.runtimeAnimatorController == null) return;
        if (string.IsNullOrEmpty(stateName)) return;
        if (foxAnimator.HasState(0, Animator.StringToHash(stateName)))
            foxAnimator.Play(stateName);
    }
}

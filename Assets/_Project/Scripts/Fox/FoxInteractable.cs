using TMPro;
using UnityEngine;

/// <summary>
/// 여우와의 조우 (명세 5장) — 여우 오브젝트의 IInteractable 구현체.
///
/// 감지/시야각 판정은 기존 InteractionDetector(OverlapSphere + 시야각)를 그대로 재사용하고,
/// 이 클래스는 그 결과(ShowUI/HideUI/OnInteractBegin/OnInteractEnd)를 받아 실제 조우 흐름을
/// 담당하는 FoxEncounterController에 위임하는 얇은 어댑터 역할만 한다.
///
/// InteractionDetector는 감지된 콜라이더의 GameObject에서 GetComponent<IInteractable>()로
/// 찾기 때문에, 이 컴포넌트는 반드시 Collider와 같은 GameObject(여우 루트)에 있어야 한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FoxInteractable : MonoBehaviour, IInteractable
{
    [Header("데이터 (명세 5.1 — ANIMAL / FOX 표시)")]
    [SerializeField] private InteractableDataSO data;

    [Header("감지 프롬프트 UI (ShowUI/HideUI로 토글)")]
    [Tooltip("플레이어 시야에 들어왔을 때 켜지는 월드 스페이스 프롬프트 패널")]
    [SerializeField] private GameObject promptPanelRoot;
    [SerializeField] private TMP_Text promptNameText;
    [SerializeField] private TMP_Text promptDescText;

    [Header("흐름 컨트롤러")]
    [SerializeField] private FoxEncounterController encounterController;

    public InteractableDataSO Data => data;

    private void Awake()
    {
        // 씬 시작 시에는 감지되기 전이므로 프롬프트는 꺼져 있어야 한다.
        if (promptPanelRoot != null) promptPanelRoot.SetActive(false);
    }

    private void ApplyPromptTexts()
    {
        if (data == null) return;
        if (promptNameText != null) promptNameText.text = data.objectName;
        if (promptDescText != null) promptDescText.text = data.descriptionText;
    }

    // ========================================================
    // IInteractable 구현부
    // ========================================================

    /// <summary>InteractionDetector가 시야/거리 조건을 만족했을 때 매 프레임 호출.</summary>
    public void ShowUI()
    {
        ApplyPromptTexts();
        if (promptPanelRoot != null) promptPanelRoot.SetActive(true);

        // 최초 감지 시점에 컨트롤러에게 통지 -> 경계 상태(Wary)로 전이 (명세 5.2)
        if (encounterController != null) encounterController.NotifyDetected();
    }

    /// <summary>플레이어가 시야/거리를 벗어났을 때 호출.</summary>
    public void HideUI()
    {
        if (promptPanelRoot != null) promptPanelRoot.SetActive(false);
    }

    /// <summary>
    /// InteractionManager.BeginInteraction(this)를 통해 상호작용이 시작되었을 때 호출.
    /// (현재 단계에 대응하는 액션 버튼과 동일한 동작을 트리거한다 — 향후 레이캐스트/그랩 등
    /// 다른 입력 경로가 추가되어도 동일하게 동작하도록 하기 위함.)
    /// </summary>
    public void OnInteractBegin()
    {
        if (encounterController != null) encounterController.NotifyInteractBegin();
    }

    public void OnInteractEnd()
    {
        if (encounterController != null) encounterController.NotifyInteractEnd();
    }

    /// <summary>호흡 미션과 연계된 오브젝트인지 여부.</summary>
    public bool IsMissionObject()
    {
        return data == null || data.requiresBreathing;
    }
}

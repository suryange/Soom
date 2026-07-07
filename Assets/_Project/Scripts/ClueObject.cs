using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class HologramMessage : MonoBehaviour, IInteractable
{
    [Header("Hierarchy Objects")]
    public GameObject hologramUI;     // 접근 시 뜨는 UI
    public GameObject messageClose;   // 접혀있는 쪽지 모델링
    public GameObject messageOpen;    // 열려있는 쪽지 모델링 

    [Header("Mission & Guiding Light")]
    public BreathEventsSO breathEvents;
    public GameObject guidingLightPrefab;
    public Transform spawnPoint;
    public Transform[] missionWaypoints;

    private XRGrabInteractable grabInteractable;
    private bool isMyMissionActive = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        // XR Grab Interactable의 잡기(Select) 이벤트에 코드 연결
        grabInteractable.selectEntered.AddListener(OnMessageGrabbed);
    }

    private void OnEnable()
    {
        if (breathEvents != null) breathEvents.OnMissionSuccess += OnBreathingMissionSuccess;
    }

    private void OnDisable()
    {
        if (breathEvents != null) breathEvents.OnMissionSuccess -= OnBreathingMissionSuccess;
        grabInteractable.selectEntered.RemoveListener(OnMessageGrabbed);
    }

    // ==========================================
    // IInteractable 인터페이스 구현부
    // ==========================================
    public void ShowUI()
    {
        if (hologramUI != null && messageClose.activeSelf)
            hologramUI.SetActive(true);
    }

    public void HideUI()
    {
        if (hologramUI != null) hologramUI.SetActive(false);
    }

    public void OnInteractBegin()
    {
        // Raycast 등으로 상호작용이 시작될 때의 로직 (필요시 구현)
    }

    public void OnInteractEnd() { }

    public bool IsMissionObject() { return true; }


    // ==========================================
    // Phase 1: 그랩(Grab) 했을 때 쪽지 펼치기
    // ==========================================
    private void OnMessageGrabbed(SelectEnterEventArgs args)
    {
        HideUI(); // 겉면 UI 끄기

        // 닫힌 모델링 끄고, 열린 모델링(내용) 켜기
        messageClose.SetActive(false);
        messageOpen.SetActive(true);
    }

    // ==========================================
    // Phase 2: 쪽지 내용 확인 및 호흡 미션 시작
    // (열려있는 쪽지 UI의 '확인' 버튼에서 이 함수를 OnClick으로 연결)
    // ==========================================
    public void ConfirmMessageAndStartMission()
    {
        isMyMissionActive = true;

        // 상태 전환: BreathingActive
        PlayerStateManager.Instance.ChangeState(PlayerState.BreathingActive);
        Debug.Log("호흡 미션 시작: 컨트롤러를 배 위에 올리고 호흡하세요.");

        // 2. 손에 들고 있던 쪽지 사라지게 하기 (자연스러운 UX)
        // 주의: 게임오브젝트 자체(gameObject)를 꺼버리면 빛무리를 스폰하는 이벤트도 수신할 수 없게 됩니다.
        // 따라서 외형(모델링)과 잡기(Grab) 기능만 비활성화합니다.
        messageOpen.SetActive(false);
        grabInteractable.enabled = false;

        // 💡 팁: 여기에 쪽지가 데이터로 부서지는 파티클을 Instantiate 해주면 완벽합니다!
    }

    // ==========================================
    // Phase 3: 호흡 미션 성공 시 빛무리 스폰
    // ==========================================
    private void OnBreathingMissionSuccess()
    {
        // 이 쪽지를 통해 시작된 미션이 아닐 경우 무시
        if (!isMyMissionActive) return;

        // 빛무리 생성 및 출발
        GameObject lightInstance = Instantiate(guidingLightPrefab, spawnPoint.position, Quaternion.identity);
        GuidingLightController lightController = lightInstance.GetComponent<GuidingLightController>();

        if (lightController != null)
        {
            Transform playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
            // lightController.StartGuiding(missionWaypoints, playerTransform);
        }

        // 미션 완료 플래그 초기화
        isMyMissionActive = false;
    }
}
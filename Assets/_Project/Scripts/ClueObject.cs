using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using TMPro;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(XRGrabInteractable))]
public class HologramMessage : MonoBehaviour, IInteractable
{
    private enum MessageProgress
    {
        Closed,
        Viewing,
        OpenedReady,
        MissionStarted
    }

    public event System.Action<GuidingLightController> OnGuidingLightSpawned;

    [Header("Hierarchy Objects")]
    public GameObject hologramUI;     // 접근 시 뜨는 UI
    public GameObject messageClose;   // 접혀있는 쪽지 모델링
    public GameObject messageOpen;    // 열려있는 쪽지 모델링 
    public GameObject grabConfirmUI;  // 열린 메시지를 들고 있을 때 표시할 확인 안내 UI
    public GameObject reopenPromptUI; // 열린 메시지를 Ray로 가리킬 때 표시할 재확인 안내 UI

    [Header("Right Controller Interaction Prompt")]
    public GameObject controllerPromptPrefab;
    public Transform rightController;
    [SerializeField] private Vector3 controllerPromptLocalPosition = new(0f, 0.12f, 0.08f);
    [SerializeField] private float controllerPromptScale = 0.00065f;

    [Header("Controller Grab Attach")]
    public Transform clueAttachPoint;
    [SerializeField] private Vector3 controllerAttachLocalPosition = new(0f, 0f, 0.4f);
    [SerializeField] private Vector3 controllerAttachLocalEulerAngles = new(0f, 180f, 0f);

    [Header("Mission & Guiding Light")]
    public BreathEventsSO breathEvents;
    public GameObject guidingLightPrefab;
    public Transform spawnPoint;
    public Transform postBreathPlayerSpawnPoint;
    public XROrigin xrOrigin;
    public Transform[] missionWaypoints;

    [Header("World UI / Data (3.2, 3.3 배선)")]
    public InteractableDataSO interactableData;
    public InteractableWorldUI worldUI;

    private XRGrabInteractable grabInteractable;
    private Rigidbody clueRigidbody;
    private MessageProgress progressState;
    private MessageProgress progressBeforeViewing;
    private bool isStateSubscribed;
    private BreathEventsSO subscribedBreathEvents;
    private bool isReturning;
    private bool ownsActiveBreathingMission;
    private bool detectionRequested;
    private bool grabConfirmRequested;
    private bool reopenPromptRequested;
    private bool breathMissionPromptRequested;
    private bool playerRelocatedAfterBreath;
    private GameObject controllerPromptInstance;
    private TMP_Text controllerButtonText;
    private TMP_Text controllerMessageText;
    private readonly HashSet<IXRHoverInteractor> rayHoverInteractors = new();
    private Vector3 initialWorldPosition;
    private Quaternion initialWorldRotation;
    private Transform initialParent;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        clueRigidbody = GetComponent<Rigidbody>();
        EnsureControllerAttachConfiguration();
        RegisterAllMessageColliders();
        initialWorldPosition = transform.position;
        initialWorldRotation = transform.rotation;
        initialParent = transform.parent;
        EnsureDetectionIndicatorTracking();
        EnsureControllerPrompt();

        progressState = messageOpen != null && messageOpen.activeSelf
            ? MessageProgress.OpenedReady
            : MessageProgress.Closed;
        progressBeforeViewing = progressState;
        detectionRequested = false;
        if (hologramUI != null)
            hologramUI.SetActive(false);
        if (worldUI != null)
            worldUI.gameObject.SetActive(false);
        SetGrabConfirmUI(false);
        SetReopenPromptUI(false);
        SetBreathMissionPromptVisible(false);
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        grabInteractable.selectEntered.AddListener(OnMessageGrabbed);
        grabInteractable.selectExited.AddListener(OnMessageReleased);
        grabInteractable.hoverEntered.AddListener(OnMessageHoverEntered);
        grabInteractable.hoverExited.AddListener(OnMessageHoverExited);

        TrySubscribeBreathEvents();
        TrySubscribeStateManager();
    }

    private void Start()
    {
        // PlayerStateManager의 Awake가 늦는 실행 순서에서도 취소 상태를 받을 수 있게 재시도한다.
        TrySubscribeBreathEvents();
        TrySubscribeStateManager();
    }

    private void OnDisable()
    {
        UnsubscribeBreathEvents();
        UnsubscribeStateManager();
        if (progressState == MessageProgress.MissionStarted)
            progressState = MessageProgress.OpenedReady;
        ownsActiveBreathingMission = false;
        PlayerStateManager.Instance?.ReleaseBreathMission(BreathMissionId.GuidingLight);

        if (grabInteractable != null)
        {
            if (isReturning)
                grabInteractable.enabled = true;

            grabInteractable.selectEntered.RemoveListener(OnMessageGrabbed);
            grabInteractable.selectExited.RemoveListener(OnMessageReleased);
            grabInteractable.hoverEntered.RemoveListener(OnMessageHoverEntered);
            grabInteractable.hoverExited.RemoveListener(OnMessageHoverExited);
        }

        isReturning = false;
        detectionRequested = false;
        rayHoverInteractors.Clear();
        SetDetectionIndicator(false);
        SetDetectionDescription(false);
        SetGrabConfirmUI(false);
        SetReopenPromptUI(false);
        SetBreathMissionPromptVisible(false);
    }

    // ==========================================
    // IInteractable 인터페이스 구현부
    // ==========================================
    public void ShowUI()
    {
        // InteractionDetector는 FOV/거리 감지만 소유한다. 실제 UI 조합은
        // 메시지 진행 상태와 Far Ray Hover를 알고 있는 이 컴포넌트에서 결정한다.
        detectionRequested = true;
        RefreshDetectionUI();
    }

    public void HideUI()
    {
        detectionRequested = false;
        RefreshDetectionUI();
    }

    public void OnInteractBegin()
    {
        // 트리거 상호작용 시작 시 단서 텍스트(3.3 지시문 UI) 갱신
        if (interactableData != null)
            MissionGuideTextUI.Instance?.ShowMessage(interactableData.missionGuideText);
    }

    public void OnInteractEnd() { }

    public bool IsMissionObject() { return true; }


    // ==========================================
    // Phase 1: 그랩(Grab) 했을 때 쪽지 펼치기
    // ==========================================
    private void OnMessageGrabbed(SelectEnterEventArgs args)
    {
        MonoBehaviour selectingInteractor = args.interactorObject as MonoBehaviour;
        string interactorName = selectingInteractor != null
            ? selectingInteractor.gameObject.name
            : args.interactorObject.ToString();
        bool selectedByFarRay = args.interactorObject is ICurveInteractionDataProvider curveProvider &&
                                curveProvider.isActive;
        Debug.Log(
            $"[HologramMessage] Select 진입: Interactor={interactorName}, " +
            $"FarRay={selectedByFarRay}, AttachMode={grabInteractable.farAttachMode}");

        HideUI(); // 겉면 UI 끄기
        rayHoverInteractors.Clear();
        SetReopenPromptUI(false);

        progressBeforeViewing = progressState;
        progressState = MessageProgress.Viewing;

        if (progressBeforeViewing == MessageProgress.Closed)
        {
            SoomAudioManager.Instance?.PlayInteractionSfx();

            // 첫 Grab에서만 상호작용 상태로 전환하고 메시지를 펼친다.
            if (PlayerStateManager.Instance != null)
                PlayerStateManager.Instance.ChangeState(PlayerState.Interact);

            if (messageClose != null) messageClose.SetActive(false);
            if (messageOpen != null) messageOpen.SetActive(true);

            // 첫 Grab은 메시지를 열고, 첫 Release를 확인 완료 입력으로 사용한다.
            // G 안내는 Select 전 Far Ray Hover 중에만 표시한다.
            return;
        }

        // 열린 메시지 재확인 동안에는 B 입력을 막고 MissionReady 안내를 잠시 숨긴다.
        if (progressBeforeViewing == MessageProgress.MissionStarted &&
            PlayerStateManager.Instance != null &&
            PlayerStateManager.Instance.CurrentState == PlayerState.MissionReady)
        {
            PlayerStateManager.Instance.ChangeState(PlayerState.Interact);
        }

        // 재확인 G 안내도 Select가 시작되면 즉시 숨긴다.
    }

    private void OnMessageReleased(SelectExitEventArgs args)
    {
        SetGrabConfirmUI(false);

        if (progressState != MessageProgress.Viewing || isReturning)
            return;

        bool isFirstConfirmation = progressBeforeViewing == MessageProgress.Closed;
        MessageProgress returnProgress = isFirstConfirmation
            ? MessageProgress.OpenedReady
            : progressBeforeViewing;
        progressState = returnProgress;

        // Release 즉시 열린 외형을 보장하고, XRI Select 종료 다음 프레임에 공통 복귀를 실행한다.
        if (messageClose != null) messageClose.SetActive(false);
        if (messageOpen != null) messageOpen.SetActive(true);

        StartCoroutine(ReturnToInitialPoseAfterRelease(isFirstConfirmation, returnProgress));
    }

    private void OnMessageHoverEntered(HoverEnterEventArgs args)
    {
        // Direct/Near Interactor는 최초 감지 위치 UI를 넘겨받지 않는다.
        // XRI의 활성 Curve Provider를 가진 Interactor만 실제 Far Ray Hover로 취급한다.
        if (!IsActiveFarRay(args.interactorObject))
            return;

        rayHoverInteractors.Add(args.interactorObject);
        RefreshDetectionUI();
        RefreshReopenPrompt();
    }

    private void OnMessageHoverExited(HoverExitEventArgs args)
    {
        if (!rayHoverInteractors.Remove(args.interactorObject))
            return;

        RefreshDetectionUI();
        RefreshReopenPrompt();
    }

    private static bool IsActiveFarRay(IXRHoverInteractor interactor)
    {
        return interactor is ICurveInteractionDataProvider curveProvider && curveProvider.isActive;
    }

    private void EnsureControllerAttachConfiguration()
    {
        if (grabInteractable == null)
            return;

        if (clueAttachPoint == null)
            clueAttachPoint = transform.Find("ClueAttachPoint");

        if (clueAttachPoint == null)
        {
            GameObject attachObject = new GameObject("ClueAttachPoint");
            clueAttachPoint = attachObject.transform;
            clueAttachPoint.SetParent(transform, false);
            clueAttachPoint.localPosition = controllerAttachLocalPosition;
            clueAttachPoint.localRotation = Quaternion.Euler(controllerAttachLocalEulerAngles);
            clueAttachPoint.localScale = Vector3.one;
        }

        grabInteractable.attachTransform = clueAttachPoint;
        grabInteractable.useDynamicAttach = false;
        grabInteractable.matchAttachPosition = true;
        grabInteractable.matchAttachRotation = true;
        grabInteractable.snapToColliderVolume = false;
        grabInteractable.farAttachMode = InteractableFarAttachMode.Near;
        grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        grabInteractable.trackPosition = true;
        grabInteractable.trackRotation = true;
        grabInteractable.retainTransformParent = true;
        grabInteractable.throwOnDetach = false;

        if (clueRigidbody != null)
        {
            clueRigidbody.useGravity = false;
            clueRigidbody.constraints = RigidbodyConstraints.None;
        }
    }

    private void EnsureDetectionIndicatorTracking()
    {
        if (hologramUI == null)
            return;

        RectTransform trackedCircle = hologramUI.transform.Find("Panel") as RectTransform;
        if (trackedCircle == null)
        {
            Debug.LogWarning("[HologramMessage] DetectionIndicatorUI의 Panel을 찾지 못해 위치 추적을 구성하지 못했습니다.");
            return;
        }

        DetectionIndicatorTracker tracker = hologramUI.GetComponent<DetectionIndicatorTracker>();
        if (tracker == null)
            tracker = hologramUI.AddComponent<DetectionIndicatorTracker>();

        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("[HologramMessage] Main Camera가 없어 감지 위치 UI를 추적할 수 없습니다.");
            return;
        }

        Vector3 targetWorldCenter = CalculateDetectionTargetCenter();
        tracker.Configure(
            camera,
            transform,
            trackedCircle,
            transform.InverseTransformPoint(targetWorldCenter));
    }

    private Vector3 CalculateDetectionTargetCenter()
    {
        Transform boundsRoot = messageClose != null ? messageClose.transform : transform;
        Renderer[] renderers = boundsRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        Collider[] colliders = boundsRoot.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);
            return bounds.center;
        }

        return transform.position;
    }

    private void RefreshDetectionUI()
    {
        bool isClosed = progressState == MessageProgress.Closed &&
                        messageClose != null && messageClose.activeSelf;
        bool showDetectedContent = detectionRequested && isClosed;

        // 위치 원은 Far Ray가 실제 Hover 가능한 시점에 조작 안내로 역할을 넘긴다.
        SetDetectionIndicator(showDetectedContent && rayHoverInteractors.Count == 0);

        // 설명 UI는 위치 이미지와 역할을 분리하여 FOV/거리 감지 중에는 유지한다.
        SetDetectionDescription(showDetectedContent);
    }

    private void SetDetectionIndicator(bool visible)
    {
        if (hologramUI != null && hologramUI.activeSelf != visible)
            hologramUI.SetActive(visible);
    }

    private void SetDetectionDescription(bool visible)
    {
        if (worldUI == null)
            return;

        if (visible)
        {
            if (interactableData != null && !worldUI.gameObject.activeSelf)
                worldUI.Show(interactableData);
        }
        else if (worldUI.gameObject.activeSelf)
        {
            worldUI.Hide();
        }
    }

    private bool CanShowRayGrabPrompt()
    {
        if (isReturning || progressState == MessageProgress.Viewing ||
            grabInteractable == null || grabInteractable.isSelected)
            return false;

        bool closedMessageAvailable = progressState == MessageProgress.Closed &&
                                      messageClose != null && messageClose.activeSelf;
        bool openMessageAvailable = progressState != MessageProgress.Closed &&
                                    messageOpen != null && messageOpen.activeSelf;
        return closedMessageAvailable || openMessageAvailable;
    }

    private void RefreshReopenPrompt()
    {
        SetReopenPromptUI(rayHoverInteractors.Count > 0 && CanShowRayGrabPrompt());
    }

    private IEnumerator ReturnToInitialPoseAfterRelease(
        bool isFirstConfirmation,
        MessageProgress returnProgress)
    {
        isReturning = true;

        // SelectExit 이벤트 처리 중 Transform/Rigidbody를 바꾸지 않는다.
        if (grabInteractable != null)
            grabInteractable.enabled = false;
        yield return null;

        if (transform.parent != initialParent)
            transform.SetParent(initialParent, true);

        if (clueRigidbody != null)
        {
            clueRigidbody.linearVelocity = Vector3.zero;
            clueRigidbody.angularVelocity = Vector3.zero;
            clueRigidbody.position = initialWorldPosition;
            clueRigidbody.rotation = initialWorldRotation;
        }

        transform.SetPositionAndRotation(initialWorldPosition, initialWorldRotation);

        if (messageClose != null) messageClose.SetActive(false);
        if (messageOpen != null) messageOpen.SetActive(true);

        isReturning = false;
        if (grabInteractable != null)
            grabInteractable.enabled = true;

        if (isFirstConfirmation)
        {
            CompleteFirstMessageReview();
        }
        else if (returnProgress == MessageProgress.MissionStarted &&
                 PlayerStateManager.Instance != null &&
                 PlayerStateManager.Instance.CurrentState == PlayerState.Interact)
        {
            PlayerStateManager.Instance.ChangeState(PlayerState.MissionReady);
        }
    }

    private void RegisterAllMessageColliders()
    {
        if (grabInteractable == null) return;

        Collider[] childColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider childCollider in childColliders)
        {
            if (childCollider != null && !childCollider.isTrigger &&
                !grabInteractable.colliders.Contains(childCollider))
            {
                grabInteractable.colliders.Add(childCollider);
            }
        }
    }

    private void TrySubscribeStateManager()
    {
        if (isStateSubscribed || PlayerStateManager.Instance == null) return;

        PlayerStateManager.Instance.OnStateEnter += HandlePlayerStateEnter;
        isStateSubscribed = true;
    }

    private void TrySubscribeBreathEvents()
    {
        if (subscribedBreathEvents == breathEvents && subscribedBreathEvents != null)
            return;

        UnsubscribeBreathEvents();
        if (breathEvents == null)
        {
            Debug.LogError("[HologramMessage] BreathEventsChannel 참조가 없어 성공 보상을 받을 수 없습니다.", this);
            return;
        }

        breathEvents.OnMissionSuccess += OnBreathingMissionSuccess;
        subscribedBreathEvents = breathEvents;
    }

    private void UnsubscribeBreathEvents()
    {
        if (subscribedBreathEvents == null) return;

        subscribedBreathEvents.OnMissionSuccess -= OnBreathingMissionSuccess;
        subscribedBreathEvents = null;
    }

    private void UnsubscribeStateManager()
    {
        if (!isStateSubscribed) return;

        if (PlayerStateManager.Instance != null)
            PlayerStateManager.Instance.OnStateEnter -= HandlePlayerStateEnter;
        isStateSubscribed = false;
    }

    private void HandlePlayerStateEnter(PlayerState state)
    {
        if (state == PlayerState.MissionReady)
        {
            RefreshReopenPrompt();
        }
        else
        {
            SetReopenPromptUI(false);
        }

        if (state == PlayerState.BreathingActive && progressState == MessageProgress.MissionStarted)
        {
            // 이 단서가 준비한 MissionReady에서 실제 호흡 상태로 진입한 뒤에만 성공 이벤트를 소유한다.
            ownsActiveBreathingMission = true;
        }
        else if (state == PlayerState.Idle)
        {
            // 성공 이벤트는 Idle 복귀보다 먼저 발생한다. 여전히 MissionStarted라면 취소 경로다.
            ownsActiveBreathingMission = false;
            if (progressState == MessageProgress.MissionStarted)
                progressState = MessageProgress.OpenedReady;
            PlayerStateManager.Instance?.ReleaseBreathMission(BreathMissionId.GuidingLight);
        }
    }

    private void SetGrabConfirmUI(bool visible)
    {
        grabConfirmRequested = visible;
        if (grabConfirmUI != null && grabConfirmUI.activeSelf)
            grabConfirmUI.SetActive(false);
        RefreshControllerPrompt();
    }

    private void SetReopenPromptUI(bool visible)
    {
        reopenPromptRequested = visible;
        if (reopenPromptUI != null && reopenPromptUI.activeSelf)
            reopenPromptUI.SetActive(false);
        RefreshControllerPrompt();
    }

    public void SetBreathMissionPromptVisible(bool visible)
    {
        breathMissionPromptRequested = visible;
        RefreshControllerPrompt();
    }

    private void EnsureControllerPrompt()
    {
        if (controllerPromptInstance != null)
            return;

        if (rightController == null)
            rightController = FindSceneTransform("Right Controller");

        if (rightController == null)
        {
            Debug.LogWarning("[HologramMessage] Right Controller를 찾지 못해 조작 안내 UI를 표시할 수 없습니다.", this);
            return;
        }

        Transform existing = rightController.Find("Scene03InteractionPrompt");
        if (existing != null)
        {
            controllerPromptInstance = existing.gameObject;
        }
        else
        {
            if (controllerPromptPrefab == null)
            {
                Debug.LogWarning("[HologramMessage] 08 interact_button 프리팹 참조가 비어 있습니다.", this);
                return;
            }

            controllerPromptInstance = Instantiate(controllerPromptPrefab, rightController, false);
            controllerPromptInstance.name = "Scene03InteractionPrompt";
        }

        RectTransform promptRect = controllerPromptInstance.GetComponent<RectTransform>();
        if (promptRect != null)
        {
            promptRect.localPosition = controllerPromptLocalPosition;
            promptRect.localRotation = Quaternion.identity;
            promptRect.localScale = Vector3.one * controllerPromptScale;
        }

        Canvas canvas = controllerPromptInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 50;
        }

        if (controllerPromptInstance.GetComponent<FaceCamera>() == null)
            controllerPromptInstance.AddComponent<FaceCamera>();

        TMP_Text[] promptTexts = controllerPromptInstance.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text promptText in promptTexts)
        {
            if (promptText.name == "Button")
                controllerButtonText = promptText;
            else if (promptText.name == "Message")
                controllerMessageText = promptText;
        }
        if (controllerButtonText == null || controllerMessageText == null)
        {
            Debug.LogError("[HologramMessage] 08 interact_button에서 Button/Message 텍스트를 찾지 못했습니다.", this);
            controllerPromptInstance.SetActive(false);
            return;
        }

        controllerPromptInstance.SetActive(false);
    }

    private void RefreshControllerPrompt()
    {
        bool showMessagePrompt = grabConfirmRequested || reopenPromptRequested;
        bool shouldShow = showMessagePrompt || breathMissionPromptRequested;

        // 비활성화/씬 종료 중에는 Right Controller가 먼저 파괴될 수 있다.
        // 숨김 요청에서 프롬프트를 다시 탐색하거나 생성하지 않는다.
        if (!shouldShow)
        {
            if (controllerPromptInstance != null && controllerPromptInstance.activeSelf)
                controllerPromptInstance.SetActive(false);
            return;
        }

        EnsureControllerPrompt();
        if (controllerPromptInstance == null || controllerButtonText == null || controllerMessageText == null)
            return;

        if (showMessagePrompt)
        {
            controllerButtonText.text = "G";
            controllerMessageText.text = "메세지 잡기";
            controllerPromptInstance.SetActive(true);
        }
        else if (breathMissionPromptRequested)
        {
            controllerButtonText.text = "B";
            controllerMessageText.text = "호흡 미션";
            controllerPromptInstance.SetActive(true);
        }
        else
        {
            controllerPromptInstance.SetActive(false);
        }
    }

    private static Transform FindSceneTransform(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                return candidate;
        }

        return null;
    }

    // ==========================================
    // Phase 2: 쪽지 내용 확인 및 미션 준비
    // ==========================================
    private void CompleteFirstMessageReview()
    {
        if (progressState != MessageProgress.OpenedReady) return;

        if (PlayerStateManager.Instance != null)
        {
            SetGrabConfirmUI(false);
            if (!PlayerStateManager.Instance.TryAcquireBreathMission(BreathMissionId.GuidingLight))
            {
                Debug.LogWarning("[HologramMessage] 다른 호흡 콘텐츠가 진행 중이어서 미션을 준비할 수 없습니다.", this);
                return;
            }

            PlayerStateManager.Instance.SetMissionZone(true);

            if (PlayerStateManager.Instance.CurrentState != PlayerState.MissionReady)
            {
                PlayerStateManager.Instance.ReleaseBreathMission(BreathMissionId.GuidingLight);
                Debug.LogWarning("[HologramMessage] 첫 Release를 확인했지만 MissionReady 전환이 거부되었습니다.");
                return;
            }

            progressState = MessageProgress.MissionStarted;
            RefreshReopenPrompt();
            Debug.Log("[HologramMessage] 첫 Release 확인 완료: 호흡 미션 준비 상태로 전환합니다.");
        }
        else
        {
            Debug.LogWarning("[HologramMessage] PlayerStateManager가 없어 MissionReady로 전환할 수 없습니다.");
        }
    }

    // ==========================================
    // Phase 3: 호흡 미션 성공 시 빛무리 스폰
    // ==========================================
    private void OnBreathingMissionSuccess()
    {
        bool breathingIsActive = PlayerStateManager.Instance != null &&
                                 PlayerStateManager.Instance.CurrentState == PlayerState.BreathingActive &&
                                 PlayerStateManager.Instance.IsBreathMissionOwner(BreathMissionId.GuidingLight);
        bool canConsumeReward = progressState == MessageProgress.MissionStarted &&
                                (ownsActiveBreathingMission || breathingIsActive);

        Debug.Log(
            $"[HologramMessage] 호흡 성공 보상 수신. Progress={progressState}, " +
            $"OwnsMission={ownsActiveBreathingMission}, State={PlayerStateManager.Instance?.CurrentState}, " +
            $"Prefab={(guidingLightPrefab != null ? guidingLightPrefab.name : "NULL")}", this);

        // 상태 진입 이벤트 구독 시점을 놓쳤더라도 이 단서가 MissionStarted이고
        // 성공 방송 시점이 BreathingActive라면 이 단서의 정상 완료로 처리한다.
        if (!canConsumeReward)
        {
            Debug.LogWarning("[HologramMessage] 현재 단서가 시작한 호흡 미션이 아니므로 보상 스폰을 건너뜁니다.", this);
            return;
        }

        // 공유 이벤트 채널에서 같은 성공이 중복 방송돼도 보상은 한 번만 처리한다.
        ownsActiveBreathingMission = false;
        progressState = MessageProgress.OpenedReady;
        PlayerStateManager.Instance?.ReleaseBreathMission(BreathMissionId.GuidingLight);

        if (guidingLightPrefab == null)
        {
            Debug.LogWarning("[HologramMessage] guidingLightPrefab이 비어 있어 길잡이 등불을 생성할 수 없습니다.");
            return;
        }

        // Guiding Light는 첫 번째 Waypoint에서 시작한다.
        // 경로가 비어 있을 때만 기존 spawnPoint 또는 단서 위치로 폴백한다.
        Transform firstWaypoint = missionWaypoints != null && missionWaypoints.Length > 0
            ? missionWaypoints[0]
            : null;
        Vector3 spawnPosition = firstWaypoint != null
            ? firstWaypoint.position
            : spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject lightInstance = Instantiate(guidingLightPrefab, spawnPosition, Quaternion.identity);
        lightInstance.SetActive(true); // guidingLightPrefab이 씬 내 비활성 템플릿이어도 인스턴스는 항상 켜서 스폰

        RelocatePlayerAfterBreath();

        // PowerUp 파티클이 에셋의 이전 재생 상태와 무관하게 즉시 보이도록 강제 재생한다.
        foreach (ParticleSystem particles in lightInstance.GetComponentsInChildren<ParticleSystem>(true))
            particles.Play(true);

        GuidingLightController lightController = lightInstance.GetComponent<GuidingLightController>();

        // PowerUp 아트 프리팹처럼 이동 컴포넌트가 없는 에셋도 길잡이 등불로 사용할 수 있게 한다.
        if (lightController == null)
            lightController = lightInstance.AddComponent<GuidingLightController>();

        if (lightController != null)
        {
            // GuidingLightController.StartGuiding은 Transform[] 하나만 받도록 되어 있어 시그니처를 맞춰 호출
            lightController.StartGuiding(missionWaypoints);
            Debug.Log(
                $"[HologramMessage] 빛무리 스폰 완료: {lightInstance.name}, " +
                $"Position={spawnPosition}, Waypoints={(missionWaypoints != null ? missionWaypoints.Length : 0)}", this);
            OnGuidingLightSpawned?.Invoke(lightController);
        }
        else
        {
            Debug.LogWarning("[HologramMessage] 생성된 길잡이 등불에 GuidingLightController가 없습니다.");
        }

        // 미션 소유 상태는 성공 처리 시작 시 이미 소비되어 중복 스폰되지 않는다.
    }

    private void RelocatePlayerAfterBreath()
    {
        if (playerRelocatedAfterBreath)
            return;

        if (postBreathPlayerSpawnPoint == null)
        {
            Debug.LogWarning("[HologramMessage] PostBreathPlayerSpawnPoint 참조가 없어 플레이어를 이동하지 않습니다.", this);
            return;
        }

        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (xrOrigin == null || xrOrigin.Camera == null)
        {
            Debug.LogWarning("[HologramMessage] XROrigin 또는 XR Camera를 찾지 못해 플레이어를 이동하지 않습니다.", this);
            return;
        }

        CharacterController characterController = xrOrigin.GetComponent<CharacterController>();
        bool controllerWasEnabled = characterController != null && characterController.enabled;
        if (controllerWasEnabled)
            characterController.enabled = false;

        Vector3 destinationUp = postBreathPlayerSpawnPoint.up;
        Vector3 destinationForward = Vector3.ProjectOnPlane(
            postBreathPlayerSpawnPoint.forward, destinationUp).normalized;
        if (destinationForward.sqrMagnitude < 0.001f)
            destinationForward = Vector3.ProjectOnPlane(Vector3.forward, destinationUp).normalized;

        xrOrigin.MatchOriginUpCameraForward(destinationUp, destinationForward);

        float currentHeadHeight = Mathf.Max(
            0f,
            Vector3.Dot(xrOrigin.Camera.transform.position - xrOrigin.transform.position, destinationUp));
        Vector3 desiredCameraPosition = postBreathPlayerSpawnPoint.position +
                                        destinationUp * currentHeadHeight;
        bool moved = xrOrigin.MoveCameraToWorldLocation(desiredCameraPosition);

        if (controllerWasEnabled)
            characterController.enabled = true;

        if (moved)
        {
            playerRelocatedAfterBreath = true;
            Debug.Log(
                $"[HologramMessage] 호흡 완료 플레이어 이동: {postBreathPlayerSpawnPoint.position}", this);
        }
        else
        {
            Debug.LogWarning("[HologramMessage] XROrigin 카메라 위치 이동에 실패했습니다.", this);
        }
    }
}

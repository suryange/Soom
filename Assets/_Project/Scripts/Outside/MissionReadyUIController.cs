using UnityEngine;

/// <summary>
/// MissionReady 상태의 튜토리얼과 오른손 컨트롤러 조작 안내를 제어한다.
/// 조작 안내는 HologramMessage가 생성한 08 interact_button 인스턴스를 공유한다.
/// </summary>
public class MissionReadyUIController : MonoBehaviour
{
    [SerializeField] private GameObject tutorialRoot;
    [SerializeField] private GameObject controllerPromptRoot; // 이전 B 전용 UI. 항상 비활성화한다.
    [SerializeField] private HologramMessage promptOwner;

    private bool isSubscribed;

    private void Awake()
    {
        ResolveReferences();
        SetVisible(false);

        Camera camera = Camera.main;
        if (camera != null)
            ConfigureCanvasCameras(tutorialRoot, camera);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        SyncCurrentState();
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetVisible(false);
    }

    private void TrySubscribe()
    {
        if (isSubscribed || PlayerStateManager.Instance == null)
            return;

        PlayerStateManager.Instance.OnStateEnter += HandleStateEnter;
        PlayerStateManager.Instance.OnStateExit += HandleStateExit;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
            return;

        if (PlayerStateManager.Instance != null)
        {
            PlayerStateManager.Instance.OnStateEnter -= HandleStateEnter;
            PlayerStateManager.Instance.OnStateExit -= HandleStateExit;
        }

        isSubscribed = false;
    }

    private void SyncCurrentState()
    {
        SetVisible(PlayerStateManager.Instance != null &&
                   PlayerStateManager.Instance.CurrentState == PlayerState.MissionReady);
    }

    private void HandleStateEnter(PlayerState state)
    {
        SetVisible(state == PlayerState.MissionReady);
    }

    private void HandleStateExit(PlayerState state)
    {
        if (state == PlayerState.MissionReady)
            SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (tutorialRoot != null && tutorialRoot.activeSelf != visible)
            tutorialRoot.SetActive(visible);

        // 이전 별도 B 텍스트는 사용하지 않는다.
        if (controllerPromptRoot != null && controllerPromptRoot.activeSelf)
            controllerPromptRoot.SetActive(false);

        ResolvePromptOwner();
        promptOwner?.SetBreathMissionPromptVisible(visible);
    }

    private void ResolveReferences()
    {
        if (tutorialRoot == null)
            tutorialRoot = transform.Find("TutorialContent")?.gameObject;
        if (controllerPromptRoot == null)
            controllerPromptRoot = transform.Find("MissionReadyPromptAnchor")?.gameObject;

        ResolvePromptOwner();
    }

    private void ResolvePromptOwner()
    {
        if (promptOwner == null)
            promptOwner = FindFirstObjectByType<HologramMessage>(FindObjectsInactive.Include);
    }

    private static void ConfigureCanvasCameras(GameObject root, Camera camera)
    {
        if (root == null)
            return;

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
            canvas.worldCamera = camera;
    }
}

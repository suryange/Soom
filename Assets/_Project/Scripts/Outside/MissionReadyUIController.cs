using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// MissionReady 상태에서만 Scene 03 튜토리얼 UI를 표시한다.
/// 이 컴포넌트가 붙은 루트는 항상 활성으로 유지하고 contentRoot만 토글한다.
/// </summary>
public class MissionReadyUIController : MonoBehaviour
{
    [FormerlySerializedAs("contentRoot")]
    [SerializeField] private GameObject tutorialRoot;
    [SerializeField] private GameObject controllerPromptRoot;
    [SerializeField] private Transform rightController;
    [SerializeField] private Vector3 controllerPromptLocalPosition = new(0f, 0.12f, 0.08f);
    [SerializeField] private float controllerPromptScale = 0.00065f;

    private bool isSubscribed;

    private void Awake()
    {
        ResolveTutorialRoot();
        EnsureControllerPromptAnchor();
        SetVisible(false);

        Camera camera = Camera.main;
        if (camera != null)
        {
            ConfigureCanvasCameras(tutorialRoot, camera);
            ConfigureCanvasCameras(controllerPromptRoot, camera);
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // PlayerStateManager의 Awake가 늦은 실행 순서인 경우 Start에서 다시 연결한다.
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
        if (isSubscribed || PlayerStateManager.Instance == null) return;

        PlayerStateManager.Instance.OnStateEnter += HandleStateEnter;
        PlayerStateManager.Instance.OnStateExit += HandleStateExit;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

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

        if (controllerPromptRoot != null && controllerPromptRoot.activeSelf != visible)
            controllerPromptRoot.SetActive(visible);
    }

    private void ResolveTutorialRoot()
    {
        if (tutorialRoot == null)
            tutorialRoot = transform.Find("TutorialContent")?.gameObject;
    }

    private void EnsureControllerPromptAnchor()
    {
        if (controllerPromptRoot == null)
            controllerPromptRoot = transform.Find("MissionReadyPromptAnchor")?.gameObject;

        if (controllerPromptRoot == null)
        {
            Transform legacyPrompt = tutorialRoot != null
                ? tutorialRoot.transform.Find("BreathingStartPrompt")
                : null;

            if (legacyPrompt == null)
            {
                Debug.LogWarning("[MissionReadyUIController] B 안내 UI를 찾지 못해 컨트롤러 안내를 숨깁니다.");
                return;
            }

            controllerPromptRoot = new GameObject(
                "MissionReadyPromptAnchor",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(FaceCamera));
            controllerPromptRoot.transform.SetParent(transform, false);

            RectTransform anchorRect = controllerPromptRoot.GetComponent<RectTransform>();
            anchorRect.sizeDelta = new Vector2(760f, 120f);

            Canvas canvas = controllerPromptRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;

            legacyPrompt.SetParent(controllerPromptRoot.transform, false);
            legacyPrompt.name = "BreathingStartPromptText";
            RectTransform promptRect = legacyPrompt as RectTransform;
            if (promptRect != null)
            {
                promptRect.anchorMin = Vector2.zero;
                promptRect.anchorMax = Vector2.one;
                promptRect.offsetMin = Vector2.zero;
                promptRect.offsetMax = Vector2.zero;
                promptRect.localScale = Vector3.one;
            }
        }

        if (controllerPromptRoot.GetComponent<FaceCamera>() == null)
            controllerPromptRoot.AddComponent<FaceCamera>();

        if (rightController == null)
            rightController = FindSceneTransform("Right Controller");

        Transform promptTransform = controllerPromptRoot.transform;
        if (rightController != null)
        {
            promptTransform.SetParent(rightController, false);
            promptTransform.localPosition = controllerPromptLocalPosition;
            promptTransform.localRotation = Quaternion.identity;
            promptTransform.localScale = Vector3.one * controllerPromptScale;
        }
        else
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                controllerPromptRoot.SetActive(false);
                Debug.LogWarning("[MissionReadyUIController] 오른손 컨트롤러와 Main Camera가 없어 B 안내를 숨깁니다.");
                return;
            }

            promptTransform.SetParent(camera.transform, false);
            promptTransform.localPosition = new Vector3(0.22f, -0.22f, 0.6f);
            promptTransform.localRotation = Quaternion.identity;
            promptTransform.localScale = Vector3.one * 0.001f;
            Debug.LogWarning("[MissionReadyUIController] 오른손 컨트롤러를 찾지 못해 B 안내를 카메라 폴백 위치에 표시합니다.");
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

    private static void ConfigureCanvasCameras(GameObject root, Camera camera)
    {
        if (root == null)
            return;

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        foreach (Canvas canvas in canvases)
            canvas.worldCamera = camera;
    }
}

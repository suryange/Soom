using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Scene_01_Start 시작 흐름 부트스트랩(기능 명세 1.1~1.3).
/// 로고(아무 입력) → 메뉴(START 버튼) → 추락 컷신 스텁 → 페이드 → Scene_02 순서로 진행합니다.
///
/// 민서님 UI 프리팹(01 start_logo / 02 start_menu)을 런타임에 인스턴스화해서 배선하므로
/// 씬에는 이 컨트롤러 하나만 배치하면 됩니다. Systems 프리팹의 XR Origin(카메라)과
/// SOOMSceneManager가 씬에 존재한다고 전제합니다.
///
/// 씬에 EventSystem이 없으면(현재 프로젝트 상태) 월드 스페이스 버튼 입력을 위해 하나를 생성합니다.
/// 컷신은 CrashCutsceneController가 같은 오브젝트에 붙어 있으면 그것을 사용하고,
/// 없으면 cutsceneFallbackDuration 만큼 대기하는 스텁으로 동작합니다(Timeline 미제작 상태 대응).
/// </summary>
public class StartSceneFlow : MonoBehaviour
{
    private enum Step { Loading, Logo, Menu, Cutscene, Done }

    [Header("민서님 UI 프리팹")]
    [Tooltip("01 start_logo — 로고/안내 화면 (Screen Space Overlay)")]
    [SerializeField] private GameObject logoPrefab;
    [Tooltip("02 start_menu — START 버튼이 있는 메뉴 (World Space)")]
    [SerializeField] private GameObject menuPrefab;

    [Header("전환 대상 씬")]
    [SerializeField] private SceneType nextScene = SceneType.Scene_02_InGame_Inside;

    [Header("연출 타이밍")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [Tooltip("로고 표시 후 이 시간 동안은 입력을 무시합니다(즉시 스킵 방지).")]
    [SerializeField] private float logoMinDisplay = 1.0f;
    [Tooltip("월드 스페이스 메뉴를 카메라 정면 이 거리(m)에 배치합니다.")]
    [SerializeField] private float menuDistance = 2.0f;
    [SerializeField] private float menuHeightOffset = 0f;

    [Header("컷신 스텁")]
    [Tooltip("CrashCutsceneController가 없을 때 대기하는 시간(초). Timeline 미제작 상태의 폴백입니다.")]
    [SerializeField] private float cutsceneFallbackDuration = 4f;

    private Step _step = Step.Loading;
    private GameObject _logo;
    private GameObject _menu;
    private float _logoShownAt;
    private IDisposable _anyButtonSub;
    private bool _isStarting;

    private void Start()
    {
        EnsureEventSystem();
        EnsureScreenFader();

        // 씬 진입 페이드 인(기능 명세 1.1). 시작 씬이라 대개 클리어 상태지만,
        // ScreenFader를 확보해 두어야 Scene_02 전환 시 SOOMSceneManager의 페이드 아웃/인이 동작합니다.
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.EnsureFadeQuad();
            ScreenFader.Instance.FadeIn(fadeInDuration);
        }

        ShowLogo();
    }

    private void OnDestroy()
    {
        _anyButtonSub?.Dispose();
        _anyButtonSub = null;
    }

    // ------------------------------------------------------------------ 1) 로고
    private void ShowLogo()
    {
        if (logoPrefab != null)
        {
            _logo = Instantiate(logoPrefab);
        }
        else
        {
            Debug.LogWarning("[StartSceneFlow] logoPrefab이 비어 있어 로고 단계를 건너뜁니다.");
        }

        _step = Step.Logo;
        _logoShownAt = Time.time;

        // 키보드/마우스/게임패드/XR 컨트롤러 어느 버튼이든 누르면 메뉴로 넘어갑니다.
        _anyButtonSub = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
    }

    private void OnAnyButtonPressed(InputControl control)
    {
        if (_step != Step.Logo) return;
        if (Time.time - _logoShownAt < logoMinDisplay) return;
        GoToMenu();
    }

    private void GoToMenu()
    {
        _anyButtonSub?.Dispose();
        _anyButtonSub = null;
        if (_logo != null) Destroy(_logo);
        ShowMenu();
    }

    // ------------------------------------------------------------------ 2) 메뉴
    private void ShowMenu()
    {
        _step = Step.Menu;

        if (menuPrefab == null)
        {
            Debug.LogWarning("[StartSceneFlow] menuPrefab이 비어 있어 메뉴를 건너뛰고 바로 시작합니다.");
            BeginStart();
            return;
        }

        _menu = Instantiate(menuPrefab);
        PlaceInFrontOfCamera(_menu);

        Button startButton = FindStartButton(_menu);
        if (startButton != null)
        {
            startButton.onClick.AddListener(BeginStart);
        }
        else
        {
            Debug.LogWarning("[StartSceneFlow] 메뉴 프리팹에서 START 버튼을 찾지 못했습니다. 키보드/게임패드 확인 입력으로 시작할 수 있습니다.");
        }
    }

    private void Update()
    {
        // 에디터/데스크톱 편의: 메뉴 단계에서 Enter/Space/게임패드 A로도 시작할 수 있게 합니다.
        if (_step != Step.Menu) return;

        bool submit = false;
        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) submit = true;
        Gamepad pad = Gamepad.current;
        if (pad != null && pad.buttonSouth.wasPressedThisFrame) submit = true;

        if (submit) BeginStart();
    }

    // ------------------------------------------------------------------ 3) 컷신 → 씬 전환
    private void BeginStart()
    {
        if (_isStarting) return;
        _isStarting = true;
        _step = Step.Cutscene;

        if (_menu != null) _menu.SetActive(false);

        CrashCutsceneController cutscene = GetComponent<CrashCutsceneController>();
        if (cutscene != null)
        {
            cutscene.Play(LoadNextScene);
        }
        else
        {
            StartCoroutine(FallbackCutsceneThenLoad());
        }
    }

    private IEnumerator FallbackCutsceneThenLoad()
    {
        if (cutsceneFallbackDuration > 0f)
            yield return new WaitForSeconds(cutsceneFallbackDuration);
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        _step = Step.Done;

        if (SOOMSceneManager.Instance != null)
        {
            SOOMSceneManager.Instance.LoadScene(nextScene);
        }
        else
        {
            Debug.LogError("[StartSceneFlow] SOOMSceneManager 인스턴스가 없어 SceneManager로 직접 로드합니다.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene.ToString());
        }
    }

    // ------------------------------------------------------------------ 헬퍼
    /// <summary>월드 스페이스 캔버스를 카메라 정면(수평면 기준)에 배치합니다. Screen Space면 그대로 둡니다.</summary>
    private void PlaceInFrontOfCamera(GameObject go)
    {
        Canvas canvas = go.GetComponentInChildren<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace) return;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[StartSceneFlow] Camera.main을 찾지 못해 메뉴를 기본 위치에 둡니다.");
            return;
        }

        Transform root = canvas != null ? canvas.transform : go.transform;

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        root.position = cam.transform.position + fwd * menuDistance + Vector3.up * menuHeightOffset;
        root.rotation = Quaternion.LookRotation(root.position - cam.transform.position, Vector3.up);
    }

    /// <summary>메뉴 프리팹에서 START 버튼을 찾습니다. "START" 라벨 우선, 없으면 첫 버튼.</summary>
    private static Button FindStartButton(GameObject root)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0) return null;

        foreach (Button b in buttons)
        {
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null && !string.IsNullOrEmpty(label.text) &&
                label.text.IndexOf("START", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return b;
            }
        }
        return buttons[0];
    }

    /// <summary>씬에 ScreenFader가 없으면(현재 프로젝트 상태) 하나 생성합니다. Awake에서 싱글톤 등록 + DontDestroyOnLoad 되어 다음 씬까지 유지됩니다.</summary>
    private static void EnsureScreenFader()
    {
        if (ScreenFader.Instance != null) return;
        new GameObject("ScreenFader (StartSceneFlow)").AddComponent<ScreenFader>();
    }

    /// <summary>씬에 EventSystem이 없으면(현재 프로젝트 상태) 새 Input System용 EventSystem을 생성합니다.</summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem (StartSceneFlow)");
        go.AddComponent<EventSystem>();
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();

        // 런타임 생성 시 액션 에셋이 비어 있으므로 기본 UI 액션을 할당합니다.
        if (module.actionsAsset == null)
            module.AssignDefaultActions();
    }
}

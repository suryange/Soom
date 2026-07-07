using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 이름을 Enum으로 관리합니다.
/// </summary>
public enum SceneType
{
    Scene_01_Start,
    Scene_02_InGame_Inside,
    Scene_03_InGame_Outside
}

/// <summary>
/// VR 환경에서 멀미를 방지하기 위해 비동기(Async)로 씬을 전환하는 총괄 매니저입니다.
/// </summary>
public class SOOMSceneManager : MonoBehaviour
{
    // 싱글톤
    public static SOOMSceneManager Instance { get; private set; }

    private void Awake()
    {
        // 씬이 넘어가도 매니저가 파괴되지 않도록 유지
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    /// <summary>
    /// 외부(UI 버튼이나 스크립트)에서 씬 전환을 요청할 때 호출하는 함수입니다.
    /// 예: SOOMSceneManager.Instance.LoadScene(SceneType.Scene_02_InGame_Inside);
    /// </summary>
    public void LoadScene(SceneType sceneType)
    {
        StartCoroutine(LoadSceneAsyncRoutine(sceneType));
    }

    /// <summary>
    /// 실제 비동기 로딩을 처리하는 코루틴
    /// </summary>
    private IEnumerator LoadSceneAsyncRoutine(SceneType sceneType)
    {
        Debug.Log($"[SOOMSceneManager] '{sceneType}' 씬 로딩 시작...");

        // TODO: 기획된 '카메라 페이드 아웃(Fade-Out)' 효과나 '로딩 터미널 UI'를 여기서 켭니다.

        // 비동기 로드 시작
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneType.ToString());

        // 로딩이 완료될 때까지 대기
        while (!asyncLoad.isDone)
        {
            // asyncLoad.progress는 0 ~ 0.9까지 증가합니다 (0.9가 로딩 완료)
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // TODO: 로딩 진행도(progress)를 UI 바에 연결할 수 있습니다.

            yield return null; // 다음 프레임까지 대기
        }

        Debug.Log($"[SOOMSceneManager] '{sceneType}' 씬 로딩 완료!");

        // TODO: 기획된 '카메라 페이드 인(Fade-In)' 효과를 여기서 켭니다.
    }
}

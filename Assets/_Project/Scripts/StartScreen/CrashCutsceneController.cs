using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 우주선 불시착 컷신(기능 명세 1.3) 전체를 총괄합니다.
/// PlayableDirector(Timeline)가 옵션으로 연결되어 있으면 그 길이에 맞춰 재생하고,
/// 연결되어 있지 않거나 PlayableAsset이 비어 있으면 fallbackDuration만큼의 코루틴 기반 폴백 시퀀스로 동작합니다(1.3.1).
/// 재생과 동시에 카메라 셰이크(1.3.1)와 CLI 로딩 UI(1.3.2)를 진행시키고,
/// 종료 시 착륙 연출(1.3.3)을 재생한 뒤 onComplete 콜백을 호출합니다.
/// </summary>
public class CrashCutsceneController : MonoBehaviour
{
    [Header("Timeline (옵션)")]
    [SerializeField] private PlayableDirector director;

    [Header("1.3.1 카메라 셰이크")]
    [SerializeField] private CameraShakeNoise cameraShake;

    [Header("1.3.2 CLI 로딩 UI")]
    [SerializeField] private CliLoadingUI loadingUI;

    [Header("1.3.3 착륙 연출 (탑뷰 + 잔해 파티클)")]
    [SerializeField] private CutsceneLandingSequence landingSequence;

    [Header("폴백 시퀀스 길이(초) — Timeline 미할당 시 사용")]
    [SerializeField] private float fallbackDuration = 6f;

    [Header("착륙 연출을 유지하는 시간(초)")]
    [SerializeField] private float landingHoldDuration = 2f;

    private Coroutine _playRoutine;
    private Action _onComplete;

    /// <summary>컷신이 현재 재생 중인지 여부.</summary>
    public bool IsPlaying => _playRoutine != null;

    /// <summary>컷신 재생을 시작합니다. 완료되면 onComplete가 호출됩니다.</summary>
    public void Play(Action onComplete)
    {
        if (_playRoutine != null)
        {
            Debug.LogWarning("[CrashCutsceneController] 이미 컷신이 재생 중입니다.");
            return;
        }
        _onComplete = onComplete;
        _playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        bool useTimeline = director != null && director.playableAsset != null;
        float duration = fallbackDuration;

        if (useTimeline)
        {
            director.time = 0;
            director.Play();
            duration = (float)director.duration;
            if (duration <= 0f) duration = fallbackDuration; // 길이를 알 수 없는 Timeline이면 폴백 길이를 사용
        }

        if (cameraShake != null) cameraShake.BeginShake(duration);
        if (loadingUI != null) loadingUI.PlayBootSequence(duration);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (useTimeline && director.state == PlayState.Playing) director.Stop();
        if (cameraShake != null) cameraShake.StopShake();
        if (loadingUI != null) loadingUI.Hide();

        if (landingSequence != null) landingSequence.Play();

        if (landingHoldDuration > 0f)
            yield return new WaitForSeconds(landingHoldDuration);

        _playRoutine = null;
        var callback = _onComplete;
        _onComplete = null;
        callback?.Invoke();
    }
}

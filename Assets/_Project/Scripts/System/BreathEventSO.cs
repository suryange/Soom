using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "BreathEventsChannel", menuName = "SOOM/Breath Events Channel")]
public class BreathEventsSO : ScriptableObject
{
    // 실시간 호흡값 (0.0 ~ 1.0) 방송 채널
    public UnityAction<float> OnBreathValueNormalized;

    // 루프(들숨/날숨 완료) 카운트 방송 채널 (현재 몇 회 성공했는지)
    public UnityAction<int> OnBreathLoopCompleted;

    // 미션 최종 클리어 방송 채널
    public UnityAction OnMissionSuccess;

    // 비활성 UI가 이벤트 구독 시점을 놓쳐도 최신 상태를 복구할 수 있는 런타임 스냅샷.
    // NonSerialized이므로 에셋 파일에는 Play Mode 값이 저장되지 않는다.
    [System.NonSerialized] private float currentBreathValue;
    [System.NonSerialized] private int currentLoopCount;
    [System.NonSerialized] private int breathValueVersion;
    [System.NonSerialized] private int loopVersion;

    public float CurrentBreathValue => currentBreathValue;
    public int CurrentLoopCount => currentLoopCount;
    public int BreathValueVersion => breathValueVersion;
    public int LoopVersion => loopVersion;

    // 외부에서 데이터를 Publish하는 함수
    public void RaiseBreathValue(float value)
    {
        currentBreathValue = Mathf.Clamp01(value);
        breathValueVersion++;
        OnBreathValueNormalized?.Invoke(currentBreathValue);
    }

    public void RaiseLoopCompleted(int count)
    {
        currentLoopCount = Mathf.Max(0, count);
        loopVersion++;
        OnBreathLoopCompleted?.Invoke(currentLoopCount);
    }

    public void RaiseMissionSuccess() => OnMissionSuccess?.Invoke();
}

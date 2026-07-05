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

    // 외부에서 데이터를 Publish하는 함수
    public void RaiseBreathValue(float value) => OnBreathValueNormalized?.Invoke(value);
    public void RaiseLoopCompleted(int count) => OnBreathLoopCompleted?.Invoke(count);
    public void RaiseMissionSuccess() => OnMissionSuccess?.Invoke();
}
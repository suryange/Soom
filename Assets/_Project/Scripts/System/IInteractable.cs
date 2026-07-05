using UnityEngine;
public interface IInteractable
{
    void ShowUI();             // 플레이어 시야/거리에 들어왔을 때 UI 표시
    void HideUI();             // 플레이어가 멀어졌을 때 UI 숨김
    void OnInteractBegin();    // 상호작용 시작 
    void OnInteractEnd();      // 상호작용 종료
    bool IsMissionObject();    // 호흡 미션 연계 여부 반환
}

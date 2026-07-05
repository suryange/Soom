using UnityEngine;

[CreateAssetMenu(fileName = "NewInteractableData", menuName = "SOOM/Interactable Data")]
public class InteractableDataSO : ScriptableObject
{
    [Header("UI Settings")]
    public string objectName;           // 예: "UNKNOWN DEVICE", "FOX"
    public string descriptionText;      // 예: "Origin: Unknown"
    public string missionGuideText;     // 미션 진입 시 가이드 텍스트

    [Header("Mission Settings")]
    public bool requiresBreathing;      // 호흡 미션 필요 여부
    public int targetBreathCount = 3;   // 목표 호흡 횟수
}
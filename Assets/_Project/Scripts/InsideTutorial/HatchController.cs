using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 명세 2.3 해치 개방: 해치 오브젝트를 회전시키고(코루틴 Lerp), 해치 너머의
/// Directional/Spot Light 강도를 0 → 최대로 올려 빛이 쏟아지는 연출을 재생한다.
/// hatch/hatchLight가 인스펙터에 배선되지 않았다면(우주선 내부 모델이 아직 없는 경우)
/// 플레이스홀더 큐브 + Spot Light를 스스로 생성해 NullReferenceException 없이 동작한다.
/// </summary>
public class HatchController : MonoBehaviour
{
    [Header("해치 오브젝트 (옵션 — 비워두면 큐브 플레이스홀더 자동 생성)")]
    [SerializeField] private Transform hatch;
    [SerializeField] private Vector3 closedLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 openLocalEuler = new Vector3(-100f, 0f, 0f);
    [SerializeField] private float openDuration = 2.5f;

    [Header("해치 너머 빛 (옵션 — 비워두면 Spot Light 플레이스홀더 자동 생성)")]
    [SerializeField] private Light hatchLight;
    [SerializeField] private float lightMaxIntensity = 8f;

    private Coroutine _routine;

    // Awake가 아닌 Start에서 플레이스홀더를 만드는 이유: 에디터 빌더 스크립트가
    // AddComponent 직후 SerializedObject로 hatch/hatchLight를 배선하는데, Awake는
    // AddComponent 시점에 즉시(에디터 모드에서도) 실행되어 배선보다 먼저 플레이스홀더를
    // 만들어버린다. Start는 플레이 모드 진입 시에만 실행되므로 배선이 끝난 뒤 안전하게
    // "정말 비어 있는지"를 판단할 수 있다.
    private void Start()
    {
        EnsurePlaceholders();
    }

    // hatch/hatchLight가 비어 있으면 최소한의 플레이스홀더를 만들어 채운다.
    // 실제 모델/조명이 씬에 배선되면(에디터 빌더 또는 인스펙터) 이 블록은 아무 것도 하지 않는다.
    private void EnsurePlaceholders()
    {
        if (hatch == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Hatch_Placeholder";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);
            hatch = go.transform;
        }
        hatch.localRotation = Quaternion.Euler(closedLocalEuler); // 항상 닫힌 상태에서 시작

        if (hatchLight == null)
        {
            var go = new GameObject("HatchLight_Placeholder");
            // 해치가 회전해도 빛(해치 너머 광원)은 같이 돌아가면 안 되므로 hatch가 아닌
            // 컨트롤러 자신의 하위에 배치한다.
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 1.5f); // 대략 해치 안쪽(플레이어 반대편) 임시 좌표
            go.transform.localRotation = Quaternion.Euler(40f, 180f, 0f); // 대략 플레이어 쪽을 비추도록

            hatchLight = go.AddComponent<Light>();
            hatchLight.type = LightType.Spot;
            hatchLight.spotAngle = 90f;
            hatchLight.range = 15f;
            hatchLight.color = Color.white;
        }
        hatchLight.intensity = 0f; // 개방 전에는 빛이 없어야 한다
    }

    /// <summary>해치 회전 + 조명 강도 상승 애니메이션을 재생하고, 완료 시 콜백을 호출한다.</summary>
    public Coroutine OpenHatch(Action onComplete = null)
    {
        EnsurePlaceholders();
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(OpenRoutine(onComplete));
        return _routine;
    }

    private IEnumerator OpenRoutine(Action onComplete)
    {
        Quaternion startRot = hatch.localRotation;
        Quaternion endRot = Quaternion.Euler(openLocalEuler);
        float startIntensity = hatchLight != null ? hatchLight.intensity : 0f;

        for (float t = 0f; t < openDuration; t += Time.deltaTime)
        {
            float k = t / openDuration;
            hatch.localRotation = Quaternion.Slerp(startRot, endRot, k);
            if (hatchLight != null)
                hatchLight.intensity = Mathf.Lerp(startIntensity, lightMaxIntensity, k);
            yield return null;
        }

        hatch.localRotation = endRot;
        if (hatchLight != null) hatchLight.intensity = lightMaxIntensity;

        _routine = null;
        onComplete?.Invoke();
    }
}

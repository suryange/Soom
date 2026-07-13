using UnityEngine;

/// <summary>
/// 추락 완료 연출(기능 명세 1.3.3): 카메라를 탑뷰로 전환하고, 우주선 잔해 파티클(연기/스파크)을 재생합니다.
/// 파티클 참조는 옵션이며 비어 있으면 건너뜁니다(null 가드).
/// </summary>
public class CutsceneLandingSequence : MonoBehaviour
{
    [Header("탑뷰 전용 카메라 (옵션 — 있으면 이 카메라로 전환하고 Main Camera는 비활성화)")]
    [SerializeField] private Camera topViewCamera;

    [Header("전용 탑뷰 카메라가 없을 때, Main Camera를 직접 옮길 목표 Pose (옵션)")]
    [SerializeField] private Transform topViewPose;

    [Header("잔해 파티클 (연기/스파크, 옵션)")]
    [SerializeField] private ParticleSystem[] debrisParticles;

    /// <summary>탑뷰 전환과 잔해 파티클 재생을 동시에 수행합니다.</summary>
    public void Play()
    {
        ActivateTopView();
        ActivateDebrisParticles();
    }

    private void ActivateTopView()
    {
        if (topViewCamera != null)
        {
            topViewCamera.gameObject.SetActive(true);
            var main = Camera.main;
            if (main != null && main != topViewCamera) main.gameObject.SetActive(false);
            return;
        }

        // 별도 탑뷰 카메라가 없으면 메인 카메라를 지정된 탑뷰 Pose로 직접 이동시킵니다.
        if (topViewPose != null)
        {
            var main = Camera.main;
            if (main != null)
                main.transform.SetPositionAndRotation(topViewPose.position, topViewPose.rotation);
        }
    }

    private void ActivateDebrisParticles()
    {
        if (debrisParticles == null) return;
        foreach (var ps in debrisParticles)
        {
            if (ps == null) continue;
            ps.gameObject.SetActive(true);
            ps.Play(true);
        }
    }
}

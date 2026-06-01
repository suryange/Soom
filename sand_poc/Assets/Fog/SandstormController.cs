using UnityEngine;

public class SandstormController : MonoBehaviour
{
    [Header("모래바람 시각화 설정")]
    [Tooltip("0: 투명(모래바람 없음), 100: 화면 가득 참")]
    [Range(0f, 100f)]
    public float sandstormGauge = 0f;
    public ParticleSystem sandstormParticle;

    private void Update()
    {
        if (sandstormParticle != null)
            UpdateSandstormDensity();
    }

    private void UpdateSandstormDensity()
    {
        var colorModule = sandstormParticle.colorOverLifetime;
        Gradient currentGradient = colorModule.color.gradient;

        if (currentGradient == null) return;

        GradientAlphaKey[] alphaKeys = currentGradient.alphaKeys;

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            if (Mathf.Abs(alphaKeys[i].time - 0.5f) < 0.1f)
            {
                // 인스펙터의 게이지(0~100)를 알파값(0.0~1.0)으로 변환하여 적용
                alphaKeys[i].alpha = sandstormGauge / 100f;
                break;
            }
        }

        currentGradient.SetKeys(currentGradient.colorKeys, alphaKeys);
        colorModule.color = new ParticleSystem.MinMaxGradient(currentGradient);
    }
}

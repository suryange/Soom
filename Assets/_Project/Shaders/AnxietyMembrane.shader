// AnxietyMembrane.shader
// 여우와의 조우 (명세 5.4) — 여우를 감싸는 '불안의 막' VFX.
//
// 프레넬(Fresnel) 림 라이트로 얇은 막의 가장자리를 밝히고, 절차적 3D 노이즈로 표면이 천천히
// 일렁이는 안개처럼 보이게 한다. 별도의 노이즈 텍스처 에셋이 필요 없도록 노이즈는 해시 기반으로
// 셰이더 안에서 직접 생성한다(레퍼런스: GroundDust.shader의 HLSL 구조).
//
// _Alpha는 FoxEncounterController가 호흡 진행도에 따라 1(막이 온전함) -> 0(막이 걷힘)으로
// 단계적으로 낮추는 마스터 투명도 프로퍼티다.
Shader "SOOM/AnxietyMembrane"
{
    Properties
    {
        _BaseColor  ("Base Color", Color) = (0.42, 0.2, 0.6, 1)
        _RimColor   ("Rim Color", Color) = (0.75, 0.45, 1, 1)
        _RimPower   ("Fresnel Rim Power", Range(0.1, 8)) = 2.5
        _NoiseScale ("Noise Scale", Float) = 3
        _NoiseSpeed ("Noise Scroll Speed", Float) = 0.25
        _MaxAlpha   ("Max Alpha", Range(0,1)) = 0.65
        _Alpha      ("Membrane Alpha (0..1, 1=온전함 0=제거됨)", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                float _RimPower;
                float _NoiseScale;
                float _NoiseSpeed;
                float _MaxAlpha;
                float _Alpha;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            // 해시 기반 3D 값 노이즈 (별도 텍스처 없이 절차적으로 생성)
            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);
                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);
                return lerp(nxy0, nxy1, f.z);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);

                // 프레넬 림 라이트 — 시야에 대해 가장자리(수직에 가까운 면)일수록 밝아진다.
                float fresnel = pow(saturate(1.0 - dot(N, V)), _RimPower);

                // 천천히 위로 흐르는 두 겹의 3D 노이즈를 섞어 불안한 아지랑이처럼 보이게 한다.
                float3 noiseCoordA = IN.positionWS * _NoiseScale * 0.2 + float3(0, _Time.y * _NoiseSpeed, 0);
                float3 noiseCoordB = IN.positionWS * _NoiseScale * 0.35 - float3(0, _Time.y * _NoiseSpeed * 0.6, 0);
                float n1 = Noise3(noiseCoordA);
                float n2 = Noise3(noiseCoordB);
                float noiseMask = saturate(n1 * 0.6 + n2 * 0.4);

                half3 color = lerp(_BaseColor.rgb, _RimColor.rgb, fresnel);
                float alpha = saturate((noiseMask * 0.6 + fresnel * 0.7) * _MaxAlpha * _Alpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

Shader "SOOM/DustVignette"
{
    Properties
    {
        _Color ("Dust Color", Color) = (0.58, 0.45, 0.28, 0.78)
        _Obscurity ("Obscurity", Range(0, 1)) = 0
        _Clarity ("Clarity", Range(0, 1)) = 1
        _NoiseScale ("Noise Scale", Float) = 3.2
        _WarpStrength ("Warp Strength", Range(0, 1)) = 0.28
        _Speed ("Wind Speed", Vector) = (0.055, 0.018, -0.035, 0.026)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "DustVignette"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Obscurity;
                float _Clarity;
                float _NoiseScale;
                float _WarpStrength;
                float4 _Speed;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + 1), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                [unroll(3)]
                for (int octave = 0; octave < 3; octave++)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.17;
                    amplitude *= 0.5;
                }
                return sum;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uv * 2.0 - 1.0;
                centered.x *= _ScreenParams.x / max(1.0, _ScreenParams.y);

                float2 p = input.uv * _NoiseScale;
                float2 warp = float2(
                    Fbm(p + _Time.y * _Speed.xy),
                    Fbm(p + 9.37 + _Time.y * _Speed.zw)) - 0.5;
                float clouds = Fbm(p * 1.45 + warp * _WarpStrength * 4.0 - _Time.y * _Speed.zw);

                // 최대 폭풍에서도 조준/멀미 완화를 위한 작은 투명 중심을 남긴다.
                float clearRadius = lerp(0.12, 0.72, _Clarity);
                float feather = lerp(0.48, 0.20, _Clarity);
                float radial = smoothstep(clearRadius, clearRadius + feather, length(centered));
                float density = saturate((clouds - 0.18) * 1.35);
                float alpha = radial * density * _Obscurity * _Color.a;

                return half4(_Color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}

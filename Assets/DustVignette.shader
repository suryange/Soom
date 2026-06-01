// DustVignette.shader
// URP unlit transparent overlay. Put it on a quad parented to the camera.
// Clear in the center, dust thickens toward the edges; _Clarity expands the clear zone.
// Stereo-instancing safe (Quest single-pass) — uses the standard URP stereo macros.
Shader "SOOM/DustVignette"
{
    Properties
    {
        _DustTex   ("Dust Noise (R)", 2D) = "white" {}
        _Color     ("Dust Color", Color) = (0.87, 0.81, 0.69, 1)
        _Clarity   ("Clarity (0..1)", Range(0,1)) = 0
        _DustAmount("Dust Amount (0..1)", Range(0,1)) = 1
        _InnerRadius ("Inner Radius (clear)", Range(0,1.5)) = 0.20
        _OuterRadius ("Outer Radius (full dust)", Range(0,1.5)) = 0.85
        _ScrollSpeed ("Scroll Speed (xy)", Vector) = (0.03, 0.012, 0, 0)
        _MaxAlpha  ("Max Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always   // draw over the scene like an overlay
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_DustTex);
            SAMPLER(sampler_DustTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DustTex_ST;
                half4  _Color;
                float  _Clarity;
                float  _DustAmount;
                float  _InnerRadius;
                float  _OuterRadius;
                float4 _ScrollSpeed;
                float  _MaxAlpha;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // distance from quad center (0.5,0.5); ~0 center, ~1 edge
                float dist = length(IN.uv - 0.5) * 2.0;

                // higher clarity pushes the clear zone outward
                float inner = lerp(_InnerRadius, 1.5, _Clarity);
                float outer = lerp(_OuterRadius, 1.6, _Clarity);
                float ring  = smoothstep(inner, outer, dist);

                // Two scrolling noise layers + domain warp -> the haze billows & churns
                // like wind-borne dust instead of a static ring (same idea as the ground dust).
                float2 baseUV = TRANSFORM_TEX(IN.uv, _DustTex);
                float2 flow   = _Time.y * _ScrollSpeed.xy;
                half n1 = SAMPLE_TEXTURE2D(_DustTex, sampler_DustTex, baseUV + flow).r;
                // warp the 2nd lookup by the 1st so layers swirl rather than slide in lockstep
                half n2 = SAMPLE_TEXTURE2D(_DustTex, sampler_DustTex,
                                           baseUV * 1.9 - flow * 1.4 + n1 * 0.12).r;
                half noise = saturate(n1 * 0.65 + n2 * 0.65);

                half alpha = ring * (0.35 + 0.65 * noise) * _DustAmount * _MaxAlpha * _Color.a;
                return half4(_Color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}

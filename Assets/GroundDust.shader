// GroundDust.shader
// Wind-blown sand drifting across the desert floor — HLSL port of the Unity blog's
// "MovingDust Mask" Shader Graph (nature-shaders-with-shader-graph).
//
// Key idea from the blog: project the dust noise by WORLD-SPACE XZ position (not mesh UVs)
// so it never stretches on dune slopes (their "Method in the scene", vs the smeared
// "Default" UV result). Then scroll several noise layers at different speeds/directions and
// multiply them -> streaky, flowing sand. _Strength is driven from Clarity by the controller
// (calm drift when clear, heavy ground-blow during the storm).
//
// Transparent, unlit, cheap. Put it on a large flat plane just above the ground.
Shader "SOOM/GroundDust"
{
    Properties
    {
        _DustTex     ("Dust Noise (R)", 2D) = "white" {}
        _Color       ("Sand Color", Color) = (0.87, 0.81, 0.69, 1)
        _Strength    ("Strength (0..1)", Range(0,1)) = 0.5
        _WorldScale  ("World Scale (tiling)", Float) = 0.08
        _Wind        ("Wind dir+speed (xy)", Vector) = (0.05, 0.015, 0, 0)
        _Layer2Scale ("Layer2 relative scale", Float) = 1.7
        _FadeStart   ("Fade start (m from cam)", Float) = 6
        _FadeEnd     ("Fade end (m from cam)", Float) = 45
        _MaxAlpha    ("Max Alpha", Range(0,1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_DustTex);
            SAMPLER(sampler_DustTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DustTex_ST;
                half4  _Color;
                float  _Strength;
                float  _WorldScale;
                float4 _Wind;
                float  _Layer2Scale;
                float  _FadeStart;
                float  _FadeEnd;
                float  _MaxAlpha;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // World-XZ projection (the blog's Position(World)->Swizzle(xz)) -> no UV stretch.
                float2 p = IN.positionWS.xz * _WorldScale;
                float2 flow = _Time.y * _Wind.xy;

                // Two scrolling noise layers at different scales/directions, multiplied -> streaks.
                half n1 = SAMPLE_TEXTURE2D(_DustTex, sampler_DustTex, p + flow).r;
                half n2 = SAMPLE_TEXTURE2D(_DustTex, sampler_DustTex, p * _Layer2Scale - flow * 1.3).r;
                half mask = saturate(n1 * n2 * 2.2);

                // Fade out with distance so the plane edge is never visible; concentrate near viewer.
                float camDist = distance(IN.positionWS, _WorldSpaceCameraPos);
                float distFade = 1.0 - saturate((camDist - _FadeStart) / max(0.001, _FadeEnd - _FadeStart));

                half alpha = mask * distFade * _Strength * _MaxAlpha * _Color.a;
                return half4(_Color.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}

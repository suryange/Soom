// UIFrostedGlass.shader
// visionOS 스타일 "뿌연 유리(frosted glass)" 월드 스페이스 UGUI 패널용 셰이더.
//
// URP의 _CameraOpaqueTexture(불투명 씬 컬러)를 화면 UV로 다중 샘플(디스크 블러)하여
// 패널 뒤 배경을 흐리게 비추고, 그 위에 라이트 틴트를 얹어 유리질감을 만든다.
// 라운드 모서리는 Image에 물린 스프라이트(_MainTex, 예: Unity 내장 UISprite 9-slice)의
// 알파를 마스크로 사용한다.
//
// ⚠️ 요구사항: 사용 중인 URP 파이프라인 에셋의 "Opaque Texture"가 켜져 있어야 한다.
//    (PC_RPAsset는 기본 ON, Quest용 Mobile_RPAsset는 수동으로 ON 필요 — 성능 비용 있음)
//    Opaque Texture가 꺼져 있으면 배경 샘플이 비어 패널이 어둡게 보인다.
Shader "SOOM/UIFrostedGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (rounded mask)", 2D) = "white" {}
        _TintColor  ("Glass Tint (rgb=색, a=틴트 세기)", Color) = (0.92, 0.94, 0.98, 0.42)
        _BlurRadius ("Blur Radius (화면 비율)", Range(0, 0.03)) = 0.014
        _Brightness ("Brightness", Range(0.5, 1.6)) = 1.08
        _Saturation ("Saturation", Range(0, 2)) = 0.9

        // UGUI 마스크/스텐실 호환용 표준 프로퍼티
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
                float  _BlurRadius;
                float  _Brightness;
                float  _Saturation;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            // 2링 디스크 커널(안쪽 6 + 바깥 6) — 한 패스에서 부드러운 프로스트를 만든다.
            static const float2 kDisk[12] =
            {
                float2( 0.500,  0.000), float2( 0.250,  0.433), float2(-0.250,  0.433),
                float2(-0.500,  0.000), float2(-0.250, -0.433), float2( 0.250, -0.433),
                float2( 1.000,  0.000), float2( 0.500,  0.866), float2(-0.500,  0.866),
                float2(-1.000,  0.000), float2(-0.500, -0.866), float2( 0.500, -0.866)
            };

            half3 SampleSceneBlur(float2 uv)
            {
                half3 sum = SampleSceneColor(uv);
                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    sum += SampleSceneColor(uv + kDisk[i] * _BlurRadius);
                }
                return sum / 13.0;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // 프래그먼트의 픽셀 좌표 → 0..1 화면 UV (스테레오/플랫폼 처리 포함)
                float2 screenUV = GetNormalizedScreenSpaceUV(IN.positionCS.xy);
                half3 scene = SampleSceneBlur(screenUV);

                // 채도/밝기 살짝 조정해 유리질감
                half lum = dot(scene, half3(0.2126, 0.7152, 0.0722));
                scene = lerp(half3(lum, lum, lum), scene, _Saturation) * _Brightness;

                // 흐린 배경 위에 라이트 틴트를 얹어 프로스티드 글래스
                half3 glass = lerp(scene, _TintColor.rgb, _TintColor.a);

                // 라운드 모서리 마스크 = 스프라이트 알파 × 버텍스 컬러 알파
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * IN.color.a;
                return half4(glass, mask);
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}

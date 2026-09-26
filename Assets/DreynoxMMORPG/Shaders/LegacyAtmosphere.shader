Shader "Dreynox/Enhanced/LegacyAtmosphere"
{
    Properties
    {
        _SkyTex ("Authored sky gradient (sRGB)", 2D) = "gray" {}
        _Cloud1 ("Authored lower clouds", 2D) = "black" {}
        _Cloud2 ("Authored upper clouds", 2D) = "black" {}
        _Tint ("Atmosphere tint", Color) = (1,1,1,1)
        _CloudOpacity1 ("Lower cloud opacity", Range(0,1)) = 0.45
        _CloudOpacity2 ("Upper cloud opacity", Range(0,1)) = 0.65
        _CloudTiling ("Cloud projection scale", Range(0.1,4)) = 0.7
        _CloudOffset1 ("Lower offset", Vector) = (0,0,0,0)
        _CloudOffset2 ("Upper offset", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_SkyTex); SAMPLER(sampler_SkyTex);
            TEXTURE2D(_Cloud1); SAMPLER(sampler_Cloud1);
            TEXTURE2D(_Cloud2); SAMPLER(sampler_Cloud2);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float4 _CloudOffset1, _CloudOffset2;
                float _CloudOpacity1, _CloudOpacity2, _CloudTiling;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionCS.z = UNITY_RAW_FAR_CLIP_VALUE * output.positionCS.w;
                output.direction = input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d = normalize(input.direction);
                // The small original BMP is a vertical color gradient, not a cubemap.
                float skyV = saturate(0.28 + 0.72 * d.y);
                half3 color = SAMPLE_TEXTURE2D(_SkyTex, sampler_SkyTex, float2(0.5, skyV)).rgb;
                float2 planeUV = d.xz * (_CloudTiling / max(0.12, d.y));
                float horizonFade = smoothstep(0.02, 0.18, d.y);
                half4 lower = SAMPLE_TEXTURE2D(_Cloud1, sampler_Cloud1, planeUV + _CloudOffset1.xy);
                half4 upper = SAMPLE_TEXTURE2D(_Cloud2, sampler_Cloud2, planeUV * 0.77 + _CloudOffset2.xy);
                color = lerp(color, lower.rgb, lower.a * _CloudOpacity1 * horizonFade);
                color = lerp(color, upper.rgb, upper.a * _CloudOpacity2 * horizonFade);
                return half4(color * _Tint.rgb, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

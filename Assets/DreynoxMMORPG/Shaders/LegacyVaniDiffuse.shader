Shader "Dreynox/Enhanced/LegacyVaniDiffuse"
{
    Properties
    {
        [MainTexture] _BaseMap ("Original albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Material tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.45
        _AlphaClip ("Alpha clipping", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff, _AlphaClip;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 diffuse : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 diffuse : COLOR;
            half fog : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.diffuse = input.diffuse;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        half4 OriginalSurface(Varyings input)
        {
            half4 value = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.diffuse;
            if (_AlphaClip > 0.5h) clip(value.a - _Cutoff);
            return value;
        }
        ENDHLSL
        Pass
        {
            Name "VaniForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 surface = OriginalSurface(input);
                half3 normal = normalize(input.normalWS);
                Light sun = GetMainLight();
                // Existing VANI draws have receiveShadows=false. Keep that policy.
                // Main diffuse + ambient probes is modern local presentation,
                // not a claim of byte-identical D3D9 fixed-function lighting.
                half3 illumination = SampleSH(normal) + sun.color * saturate(dot(normal, sun.direction)) * sun.distanceAttenuation;
                return half4(MixFog(surface.rgb * illumination, input.fog), surface.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "VaniDepth"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            half4 Depth(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                OriginalSurface(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

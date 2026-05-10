Shader "Split Body/Text/Unlit Instanced Text Outline"
{
    Properties
    {
        [MainColor] _BaseColor ("Text Color", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width (Object Units)", Range(0, 0.25)) = 0.035
        _OutlineDepthOffset ("Outline Depth Offset", Range(-0.01, 0.01)) = 0
        _RiseDistance ("Rise Distance", Float) = 0.35
        _RiseDuration ("Rise Duration", Float) = 0.35
        _SettleStrength ("Settle Strength", Range(0, 0.5)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #pragma target 3.5
        #pragma multi_compile_instancing

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _RiseDistance;
            float _RiseDuration;
            float _SettleStrength;
        CBUFFER_END

        UNITY_INSTANCING_BUFFER_START(TextOutlineProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _OutlineColor)
            UNITY_DEFINE_INSTANCED_PROP(float, _OutlineWidth)
            UNITY_DEFINE_INSTANCED_PROP(float, _OutlineDepthOffset)
            UNITY_DEFINE_INSTANCED_PROP(float, _IntroAge)
        UNITY_INSTANCING_BUFFER_END(TextOutlineProps)

        struct Attributes
        {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings OffsetTextVertex(Attributes input, float2 direction)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);

            float outlineWidth = UNITY_ACCESS_INSTANCED_PROP(TextOutlineProps, _OutlineWidth);
            float3 positionOS = input.positionOS.xyz + float3(direction * outlineWidth, 0.0);
            float introAge = UNITY_ACCESS_INSTANCED_PROP(TextOutlineProps, _IntroAge);
            float introDuration = max(_RiseDuration, 0.0001);
            float introT = saturate(introAge / introDuration);
            float smoothIntro = introT * introT * (3.0 - 2.0 * introT);
            float settle = sin(introT * 9.42477796) * exp2(-8.0 * introT) * _RiseDistance * _SettleStrength;
            positionOS.y += (-_RiseDistance * (1.0 - smoothIntro)) + settle;

            float4 positionCS = TransformObjectToHClip(positionOS);

            float depthOffset = UNITY_ACCESS_INSTANCED_PROP(TextOutlineProps, _OutlineDepthOffset);
            positionCS.z += depthOffset * positionCS.w;

            output.positionCS = positionCS;
            return output;
        }

        Varyings TextVertex(Attributes input)
        {
            return OffsetTextVertex(input, float2(0.0, 0.0));
        }

        Varyings OutlineVertexRight(Attributes input) { return OffsetTextVertex(input, float2(1.0, 0.0)); }
        Varyings OutlineVertexLeft(Attributes input) { return OffsetTextVertex(input, float2(-1.0, 0.0)); }
        Varyings OutlineVertexUp(Attributes input) { return OffsetTextVertex(input, float2(0.0, 1.0)); }
        Varyings OutlineVertexDown(Attributes input) { return OffsetTextVertex(input, float2(0.0, -1.0)); }
        Varyings OutlineVertexUpRight(Attributes input) { return OffsetTextVertex(input, float2(0.70710678, 0.70710678)); }
        Varyings OutlineVertexUpLeft(Attributes input) { return OffsetTextVertex(input, float2(-0.70710678, 0.70710678)); }
        Varyings OutlineVertexDownRight(Attributes input) { return OffsetTextVertex(input, float2(0.70710678, -0.70710678)); }
        Varyings OutlineVertexDownLeft(Attributes input) { return OffsetTextVertex(input, float2(-0.70710678, -0.70710678)); }

        half4 OutlineFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return UNITY_ACCESS_INSTANCED_PROP(TextOutlineProps, _OutlineColor);
        }

        half4 TextFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            return UNITY_ACCESS_INSTANCED_PROP(TextOutlineProps, _BaseColor);
        }
        ENDHLSL

        Pass
        {
            Name "Outline Right"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexRight
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Outline Left"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexLeft
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Outline Up"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexUp
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Outline Down"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexDown
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Outline Up Right"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexUpRight
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Outline Up Left"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexUpLeft
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Outline Down Right"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexDownRight
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Outline Down Left"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertexDownLeft
            #pragma fragment OutlineFragment
            ENDHLSL
        }

        Pass
        {
            Name "Text"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex TextVertex
            #pragma fragment TextFragment
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

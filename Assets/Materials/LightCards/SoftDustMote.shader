Shader "SplitBody/Particles/Soft Dust Mote"
{
    Properties
    {
        [HDR]_Color("Color", Color) = (1, 0.86, 0.55, 0.22)
        _Core("Core", Range(0, 1)) = 0.08
        _Softness("Softness", Range(0.01, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SoftDustMote"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Core;
                half _Softness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half2 centeredUv = input.uv - 0.5;
                half distanceFromCenter = length(centeredUv) * 2.0;
                half alpha = 1.0 - smoothstep(_Core, saturate(_Core + _Softness), distanceFromCenter);
                half4 color = _Color * input.color;
                color.a *= alpha;
                return color;
            }
            ENDHLSL
        }
    }
}

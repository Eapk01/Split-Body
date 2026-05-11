Shader "SplitBody/Light Cards/Gradient God Ray"
{
    Properties
    {
        [HDR]_Color("Color", Color) = (1, 0.88, 0.55, 1)
        _Intensity("Intensity", Range(0, 10)) = 2
        _Alpha("Alpha", Range(0, 1)) = 0.35

        [Header(Gradient)]
        [Enum(UV,0,Object XY,1,Object XZ,2,Object YZ,3)] _MappingMode("Mapping Mode", Float) = 0
        _Angle("Direction Angle", Range(0, 360)) = 90
        _Start("Start", Range(-0.5, 1.5)) = 0
        _End("End", Range(-0.5, 1.5)) = 1
        _Falloff("Falloff", Range(0.1, 8)) = 1.75
        [Toggle]_Invert("Invert Direction", Float) = 0

        [Header(Card Feather)]
        _ObjectProjectionSize("Object Projection Size", Vector) = (1, 1, 0, 0)
        _ObjectProjectionOffset("Object Projection Offset", Vector) = (0, 0, 0, 0)
        _EdgeFeather("Edge Feather", Range(0, 0.5)) = 0.12
        _WidthFeather("Width Feather", Range(0, 0.5)) = 0.18

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Z Test", Float) = 4
        [Toggle]_ZWrite("Z Write", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
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
            Name "LightCardGradient"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Alpha;
                half _MappingMode;
                half _Angle;
                half _Start;
                half _End;
                half _Falloff;
                half _Invert;
                half4 _ObjectProjectionSize;
                half4 _ObjectProjectionOffset;
                half _EdgeFeather;
                half _WidthFeather;
                half _SrcBlend;
                half _DstBlend;
                half _ZTest;
                half _ZWrite;
                half _Cull;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half SafeSmoothStep(half edge0, half edge1, half x)
            {
                half range = max(abs(edge1 - edge0), 0.0001);
                half t = saturate((x - edge0) / range);
                return t * t * (3.0 - 2.0 * t);
            }

            half FeatherPair(half value, half feather)
            {
                half safeFeather = max(feather, 0.0001);
                return smoothstep(0.0, safeFeather, value) *
                       smoothstep(0.0, safeFeather, 1.0 - value);
            }

            half2 GetProjectionUv(Varyings input)
            {
                half mode = round(_MappingMode);
                half2 projected = input.uv;

                if (mode == 1.0)
                {
                    projected = input.positionOS.xy;
                }
                else if (mode == 2.0)
                {
                    projected = input.positionOS.xz;
                }
                else if (mode == 3.0)
                {
                    projected = input.positionOS.yz;
                }

                if (mode > 0.5)
                {
                    half2 size = max(abs(_ObjectProjectionSize.xy), half2(0.0001, 0.0001));
                    projected = (projected - _ObjectProjectionOffset.xy) / size + 0.5;
                }

                return projected;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half2 beamUv = GetProjectionUv(input);
                float radians = _Angle * 0.01745329252;
                half2 direction = half2(cos(radians), sin(radians));
                half2 centeredUv = beamUv - 0.5;

                half along = dot(centeredUv, direction) + 0.5;
                half across = dot(centeredUv, half2(-direction.y, direction.x)) + 0.5;

                half gradient = SafeSmoothStep(_Start, _End, along);
                gradient = lerp(gradient, 1.0 - gradient, saturate(_Invert));
                gradient = pow(saturate(gradient), _Falloff);

                half edgeFade = FeatherPair(beamUv.x, _EdgeFeather) *
                                FeatherPair(beamUv.y, _EdgeFeather);
                half widthFade = FeatherPair(across, _WidthFeather);
                half alpha = gradient * edgeFade * widthFade * _Alpha * _Color.a;

                half3 color = _Color.rgb * _Intensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

// Aditivo com vertex color pra rastro (TrailRenderer) e partículas.
// _Shape 0 = faixa (some nas bordas em uv.y), 1 = radial (círculo macio), 2 = losango, 3 = anel.
Shader "Rail/Aurora/Additive"
{
    Properties
    {
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        [Enum(Band,0,Radial,1,Diamond,2,Ring,3)] _Shape ("Shape", Float) = 0
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.5
        _Core ("Hot Core", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "AuroraAdditive"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                half _Shape;
                half _Softness;
                half _Core;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d;
                if (_Shape < 0.5)       d = abs(i.uv.y * 2.0 - 1.0);
                else if (_Shape < 1.5)  d = length(i.uv * 2.0 - 1.0);
                else if (_Shape < 2.5)  d = abs(i.uv.x * 2.0 - 1.0) + abs(i.uv.y * 2.0 - 1.0);
                else                    d = abs(length(i.uv * 2.0 - 1.0) - 0.8) * 5.0;   // anel fino

                half mask = saturate((1.0 - d) / _Softness);
                half core = pow(mask, 4.0) * _Core;
                half a = i.color.a;
                half3 col = (i.color.rgb * _TintColor.rgb) * (mask + core) * a;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

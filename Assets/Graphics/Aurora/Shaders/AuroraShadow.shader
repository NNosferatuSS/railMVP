// Sombra de contato (blob) deitada na pista sob coin, gem e hazards: ancora o objeto no
// deck e dá leitura de altura. Multiplica o que está atrás (escurece), sem luz nem textura.
// _Shape 0 = círculo, 1 = losango (coin), 2 = retângulo arredondado (hazards).
Shader "Rail/Aurora/Shadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.0, 0.02, 0.06, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.6
        [Enum(Circle,0,Diamond,1,RoundRect,2)] _Shape ("Shape", Float) = 0
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.6
        _Aspect ("RoundRect Aspect (x/y)", Range(0.2, 5)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "AuroraShadow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _Opacity;
                half _Shape;
                half _Softness;
                half _Aspect;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float d;
                if (_Shape < 0.5)       d = length(p);
                else if (_Shape < 1.5)  d = abs(p.x) + abs(p.y);
                else
                {
                    float2 a = abs(p) - float2(min(1.0, _Aspect), min(1.0, 1.0 / _Aspect)) * 0.55;
                    d = length(max(a, 0.0)) / 0.45;
                }
                half mask = saturate((1.0 - d) / _Softness);
                return half4(_ShadowColor.rgb, mask * mask * _Opacity);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

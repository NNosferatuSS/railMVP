// Bolha/aura aditiva só de borda (fresnel) — escudo, ghost, aura de power-up.
// _Pulse anima a borda; centro quase transparente pra não esconder a pérola.
Shader "Rail/Aurora/Fresnel"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.4, 1.6, 2.0, 1)
        _Power ("Fresnel Power", Range(0.5, 8)) = 2.5
        _Core ("Center Fill", Range(0, 1)) = 0.06
        _Pulse ("Pulse Speed", Range(0, 12)) = 4
        _Bands ("Scan Bands", Range(0, 40)) = 14
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "AuroraFresnel"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Power;
                half _Core;
                half _Pulse;
                half _Bands;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 posWS : TEXCOORD1; float3 posOS : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.posWS = p.positionWS;
                o.posOS = v.positionOS.xyz;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 vd = normalize(GetWorldSpaceViewDir(i.posWS));
                half rim = pow(1.0 - saturate(dot(n, vd)), _Power);
                half bands = 0.75 + 0.25 * sin(i.posOS.y * _Bands * 6.2832 - _Time.y * _Pulse * 2.0);
                half pulse = 0.85 + 0.15 * sin(_Time.y * _Pulse);
                return half4(_Color.rgb * (rim * bands + _Core) * pulse, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

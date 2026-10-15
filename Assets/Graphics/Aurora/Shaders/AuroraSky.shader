// Skybox gradiente do vazio Aurora. A câmera olha ~50° pra baixo, então quase
// todo o fundo vem da metade inferior: _Deep embaixo, _Mid perto do horizonte.
Shader "Rail/Aurora/Sky"
{
    Properties
    {
        _Deep ("Deep (down)", Color) = (0.004, 0.012, 0.045, 1)
        _Mid ("Mid", Color) = (0.012, 0.045, 0.12, 1)
        _Top ("Top (up)", Color) = (0.03, 0.02, 0.10, 1)
        _MidHeight ("Mid Height", Range(-1, 1)) = -0.25
        _Spread ("Spread", Range(0.05, 2)) = 0.7
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Mid;
                half4 _Top;
                half _MidHeight;
                half _Spread;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float y = normalize(i.dir).y;
                float below = saturate((_MidHeight - y) / _Spread);
                float above = saturate((y - _MidHeight) / _Spread);
                half3 col = lerp(_Mid.rgb, _Deep.rgb, smoothstep(0, 1, below));
                col = lerp(col, _Top.rgb, smoothstep(0, 1, above));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

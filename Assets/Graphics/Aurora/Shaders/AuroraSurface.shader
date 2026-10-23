// Superfície flat da direção Aurora: dois tons (lit/shade) pela main light,
// fresnel opcional, emissão HDR (alimenta o bloom) e fog próprio pro vazio
// azul-marinho (distância + altura). Sem texturas, sem sombras — barato no mobile.
// Globais (_Aurora*) vêm do AuroraEnvironment.
Shader "Rail/Aurora/Surface"
{
    Properties
    {
        [MainColor] _BaseColor ("Lit Color", Color) = (0.12, 0.36, 0.42, 1)
        _ShadeColor ("Shade Color", Color) = (0.03, 0.10, 0.18, 1)
        _ShadeSoftness ("Shade Softness", Range(0.01, 1)) = 0.35
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [HDR] _RimColor ("Fresnel Rim", Color) = (0, 0, 0, 1)
        _RimPower ("Fresnel Power", Range(0.5, 8)) = 3
        _FogAmount ("Fog Amount", Range(0, 1)) = 1
        [Header(Juice)]
        _PlayerGlow ("Player Glow (poça de luz + onda)", Range(0, 4)) = 0
        _Flash ("Flash (MPB)", Range(0, 4)) = 0
        [Header(Volume)]
        [Toggle] _VertexAO ("AO por vértice (canal R da cor do mesh)", Float) = 0
        _AOStrength ("AO Strength", Range(0, 1.5)) = 1
        _AOEmission ("AO escurece emissão", Range(0, 1)) = 1
        _LightRamp ("Rampa de luz (facetas)", Range(0, 1)) = 0
        [HDR] _GlintColor ("Brilho especular (cor)", Color) = (0, 0, 0, 1)
        _GlintSize ("Brilho especular (tamanho)", Range(0.5, 0.999)) = 0.94
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half _ShadeSoftness;
            half4 _EmissionColor;
            half4 _RimColor;
            half _RimPower;
            half _FogAmount;
            half _PlayerGlow;
            half _Flash;
            half _VertexAO;
            half _AOStrength;
            half _AOEmission;
            half _LightRamp;
            half4 _GlintColor;
            half _GlintSize;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "AuroraForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 _AuroraFogColor;
            float4 _AuroraFogParams;   // x = início (dist), y = fim (dist), z = topo do fog de altura (y), w = base (y)
            float4 _AuroraLightDir;    // xyz = direção PARA a luz (mundo), w = 1 quando publicado pelo AuroraEnvironment
            // Juice (AuroraJuice)
            float4 _AuroraPlayerPos;   // xyz = player, w = raio da poça de luz
            half4 _AuroraGlowColor;    // cor da poça (a = intensidade)
            float4 _AuroraWave;        // x,z = origem, y = tempo de início, w = força
            float4 _AuroraWaveParams;  // x = velocidade, y = largura, z = duração
            half4 _AuroraWaveColor;
            float _AuroraTime;
            float _AuroraDim;          // 0..1: escurece a parte NÃO emissiva (zona Eclipse)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half ao : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                // AO assado no Blender (Art/Source/AuroraV2). Sem o toggle = 1 (meshes sem cor).
                o.ao = _VertexAO > 0.5 ? saturate(1.0 - (1.0 - v.color.r) * _AOStrength) : 1.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);

                // Direção de arte fixa (AuroraEnvironment) > main light da cena > default.
                float3 l = _AuroraLightDir.xyz;
                if (_AuroraLightDir.w < 0.5)
                {
                    l = GetMainLight().direction;
                    if (dot(l, l) < 1e-4) l = float3(0.35, 0.85, -0.4);
                }
                l = normalize(l);

                half ndl = saturate(dot(n, l));
                half lit = smoothstep(0.5 - _ShadeSoftness * 0.5, 0.5 + _ShadeSoftness * 0.5, ndl);
                half3 col = lerp(_ShadeColor.rgb, _BaseColor.rgb, lit) * (1.0 - _AuroraDim * 0.85);

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                col += _RimColor.rgb * fres;
                // Volume: rampa contínua por cima dos 2 tons (cada faceta ganha um valor) e
                // brilho especular duro (lapidação de coin/gem, chanfros).
                col *= lerp(1.0, 0.5 + 0.5 * ndl, _LightRamp);
                half glint = smoothstep(_GlintSize, lerp(_GlintSize, 1.0, 0.35), saturate(dot(n, normalize(l + viewDir))));
                col += _GlintColor.rgb * glint;
                col *= i.ao;
                col += _EmissionColor.rgb * (1.0 + _Flash) * lerp(1.0, i.ao, _AOEmission);

                // Juice: poça de luz sob o player + onda de choque que corre pelos trilhos.
                if (_PlayerGlow > 0.001)
                {
                    float2 dp = i.positionWS.xz - _AuroraPlayerPos.xz;
                    float r = max(0.01, _AuroraPlayerPos.w);
                    float pool = exp(-dot(dp, dp) / (r * r));
                    half up = saturate(n.y * 0.7 + 0.3);
                    col += _AuroraGlowColor.rgb * (_AuroraGlowColor.a * pool * up * _PlayerGlow);

                    float age = _AuroraTime - _AuroraWave.y;
                    if (age >= 0 && age < _AuroraWaveParams.z)
                    {
                        float d = length(i.positionWS.xz - _AuroraWave.xz);
                        float radius = age * _AuroraWaveParams.x;
                        float ring = saturate(1.0 - abs(d - radius) / max(0.01, _AuroraWaveParams.y));
                        float fade = 1.0 - age / _AuroraWaveParams.z;
                        col += _AuroraWaveColor.rgb * (ring * ring * fade * fade * _AuroraWave.w * _PlayerGlow);
                    }
                }

                // Fog: distância da câmera + afundar no vazio abaixo das pistas.
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                float fogD = saturate((dist - _AuroraFogParams.x) / max(0.01, _AuroraFogParams.y - _AuroraFogParams.x));
                float fogH = saturate((_AuroraFogParams.z - i.positionWS.y) / max(0.01, _AuroraFogParams.z - _AuroraFogParams.w));
                float fog = saturate(max(fogD * fogD, fogH)) * _FogAmount * _AuroraFogColor.a;
                col = lerp(col, _AuroraFogColor.rgb, fog);

                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

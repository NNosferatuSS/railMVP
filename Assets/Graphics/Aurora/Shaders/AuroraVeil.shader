// Véu de aurora: cortina aditiva com raios verticais que escorrem devagar.
// UV do mesh (Blender): u ao longo da cortina, v na altura (0 = base).
// Base brilhante, some pro topo e nas pontas. Sem textura.
Shader "Rail/Aurora/Veil"
{
    Properties
    {
        [HDR] _ColorBottom ("Color Bottom", Color) = (0.15, 1.2, 0.65, 1)
        [HDR] _ColorTop ("Color Top", Color) = (0.25, 0.2, 0.9, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1
        _RayDensity ("Ray Density", Range(4, 120)) = 38
        _ScrollSpeed ("Scroll Speed", Range(0, 2)) = 0.25
        _Falloff ("Vertical Falloff", Range(0.3, 6)) = 1.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "AuroraVeil"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBottom;
                half4 _ColorTop;
                half _Intensity;
                half _RayDensity;
                half _ScrollSpeed;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float u = i.uv.x;
                float v = i.uv.y;
                float t = _Time.y * _ScrollSpeed;

                // Raios: soma de senos com fase deslocada pela altura (dobra suave).
                float warp = sin(u * 6.0 - t * 1.7) * 0.6 + sin(u * 2.3 + t) * 0.4;
                float r1 = 0.5 + 0.5 * sin(u * _RayDensity + warp * 3.0 + t * 2.0);
                float r2 = 0.5 + 0.5 * sin(u * _RayDensity * 0.47 - t * 1.3 + v * 1.5);
                float rays = saturate(0.35 + 0.65 * r1 * r1 * (0.6 + 0.4 * r2));

                float vert = smoothstep(0.0, 0.06, v) * pow(saturate(1.0 - v), _Falloff);
                float ends = smoothstep(0.0, 0.18, u) * smoothstep(1.0, 0.82, u);
                float breath = 0.85 + 0.15 * sin(t * 2.1 + u * 3.0);

                half3 col = lerp(_ColorBottom.rgb, _ColorTop.rgb, saturate(v * 1.3));
                return half4(col * rays * vert * ends * breath * _Intensity, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

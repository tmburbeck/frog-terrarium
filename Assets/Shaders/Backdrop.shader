// The room around the tank, used as the sky (Universal Render Pipeline): a soft gradient from a
// lit ceiling through the walls to a dark floor, in the colours of the tank's style set.
// A tiny bit of noise stops the gradient from showing bands.
Shader "FrogTerrarium/Backdrop"
{
    Properties
    {
        _Top ("Top", Color) = (0.2, 0.24, 0.22, 1)
        _Horizon ("Middle", Color) = (0.12, 0.16, 0.14, 1)
        _Bottom ("Bottom", Color) = (0.04, 0.05, 0.045, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top;
                half4 _Horizon;
                half4 _Bottom;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float y = normalize(i.direction).y;
                half3 c = y > 0 ? lerp(_Horizon.rgb, _Top.rgb, smoothstep(0.0, 0.8, y))
                                : lerp(_Horizon.rgb, _Bottom.rgb, smoothstep(0.0, 0.5, -y));
                float dither = frac(sin(dot(i.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                return half4(c + dither / 255.0, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

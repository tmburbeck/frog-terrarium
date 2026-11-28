// Water for the tank (Universal Render Pipeline). Looks like real shallow water:
// - you see the ground through it, bent a little by the ripples (refraction),
// - it gets darker and greener the deeper it is (light is absorbed),
// - it reflects the room more at low angles (Fresnel), with a sharp glint of the lamp,
// - and it fades out softly where it meets the shore.
// The ripples are small moving waves made from a few sine waves, so it needs no textures.
Shader "FrogTerrarium/Water"
{
    Properties
    {
        _Color ("Shallow colour", Color) = (0.16, 0.3, 0.27, 1)
        _DeepColor ("Deep colour", Color) = (0.04, 0.1, 0.09, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
        _Clarity ("Murkiness", Range(0.1, 10)) = 2.2
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _DeepColor;
                half _Glossiness;
                half _Clarity;
                half _Refraction;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // The slope of the ripples at a point: four small waves crossing at different angles.
            float2 RippleSlope(float2 p, float t)
            {
                float2 s = 0;
                const float2 d0 = float2(0.8, 0.6), d1 = float2(-0.45, 0.89), d2 = float2(0.2, -0.98), d3 = float2(-0.95, -0.3);
                s += d0 * cos(dot(p, d0) * 5.3 + t * 1.6) * 0.5;
                s += d1 * cos(dot(p, d1) * 7.9 - t * 1.9) * 0.35;
                s += d2 * cos(dot(p, d2) * 13.7 + t * 2.7) * 0.2;
                s += d3 * cos(dot(p, d3) * 21.0 - t * 3.3) * 0.12;
                return s;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float surfaceDepth = i.screenPos.w;
                float groundDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float thickness = max(0, groundDepth - surfaceDepth);

                float3 n = normalize(float3(0, 1, 0) + float3(RippleSlope(i.positionWS.xz, _Time.y), 0).xzy * float3(0.09, 0, 0.09));
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // Refraction: look up the ground a little to one side, but never pick up things above the water.
                float2 bent = uv + n.xz * _Refraction * saturate(thickness * 3.0);
                float bentDepth = LinearEyeDepth(SampleSceneDepth(bent), _ZBufferParams);
                if (bentDepth < surfaceDepth) bent = uv; else thickness = max(0, bentDepth - surfaceDepth);
                half3 ground = SampleSceneColor(bent);

                // Absorption: shallow water is clear and faintly tinted; deep water fades to the deep colour.
                float absorb = 1.0 - exp(-thickness * _Clarity);
                half3 tint = _Color.rgb / max(max(_Color.r, _Color.g), max(_Color.b, 1e-3));
                half3 body = lerp(ground * lerp(1.0, tint, 0.4 * saturate(thickness * 5.0)), _DeepColor.rgb, saturate(absorb));

                // Reflection of the surroundings, strongest when looking across the water.
                float ndv = saturate(dot(n, view));
                float fresnel = 0.02 + 0.98 * pow(1.0 - ndv, 5.0);
                half3 reflected = GlossyEnvironmentReflection(reflect(-view, n), 1.0 - _Glossiness, 1.0);

                // The lamp's glint, cut off where the water is in shadow.
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 halfway = normalize(sun.direction + view);
                float glint = pow(saturate(dot(n, halfway)), 700.0 * _Glossiness) * 6.0;
                half3 specular = sun.color * glint * sun.shadowAttenuation;
                body *= lerp(0.65, 1.0, sun.shadowAttenuation);

                half3 colour = lerp(body, reflected, fresnel) + specular;

                // A faint light line where the water laps the shore, and a soft edge so there is no hard seam.
                float shore = 1.0 - saturate(thickness / 0.05);
                colour = lerp(colour, colour + half3(0.08, 0.09, 0.09), shore * 0.6);
                float alpha = saturate(thickness / 0.015);

                colour = MixFog(colour, i.fogFactor);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

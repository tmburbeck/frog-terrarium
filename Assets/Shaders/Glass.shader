// The tank's glass walls (Universal Render Pipeline). Clear when you look straight through, and
// more and more mirror-like at a slant (Fresnel), with the room and the lamp reflected in it and
// the faint green of real glass. Both sides are drawn, so the far wall shows through the near one.
Shader "FrogTerrarium/Glass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.86, 0.96, 0.93, 1)
        _Base ("Straight-on opacity", Range(0, 0.5)) = 0.05
        _Reflect ("Reflection strength", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Base;
                half _Reflect;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS) * (IS_FRONT_VFACE(face, 1.0, -1.0));
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ndv = saturate(dot(n, view));
                float fresnel = 0.04 + 0.96 * pow(1.0 - ndv, 5.0);

                half3 reflected = GlossyEnvironmentReflection(reflect(-view, n), 0.02, 1.0) * _Reflect;
                Light sun = GetMainLight();
                float3 halfway = normalize(sun.direction + view);
                half glint = pow(saturate(dot(n, halfway)), 900.0) * 4.0;

                // Premultiplied: the light the glass adds, and how much of what is behind it it hides.
                half3 tint = _Tint.rgb;
                half3 colour = reflected * fresnel + sun.color * glint + (1.0 - tint) * 0.02;
                half alpha = saturate(_Base + fresnel * 0.75 + glint);
                colour = MixFog(colour, i.fogFactor);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

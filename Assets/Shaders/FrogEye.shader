// A frog's eye (Universal Render Pipeline), drawn on a ball whose +Z points where the eye looks.
// The iris has fine radiating fibres, a darker rim and a net of dark veins (like a White's tree
// frog's golden eye); the pupil is a wide horizontal slot, or an upright slit for Agalychnis tree
// frogs; and the whole eye is glassy wet, with a sharp reflection of the lamp.
Shader "FrogTerrarium/FrogEye"
{
    Properties
    {
        _BaseColor ("Colour when nothing is set", Color) = (0.75, 0.55, 0.15, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
    CBUFFER_END

    UNITY_INSTANCING_BUFFER_START(EyeProps)
        UNITY_DEFINE_INSTANCED_PROP(float4, _EyeIris)  // rgb iris colour (linear), a = pupil shape (0 slot, 1 slit)
        UNITY_DEFINE_INSTANCED_PROP(float4, _EyeSkin)  // rgb skin colour round the eye, a = pattern seed
    UNITY_INSTANCING_BUFFER_END(EyeProps)

    float EyeHash(float3 p)
    {
        p = frac(p * 0.3183099 + 0.1);
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    float EyeNoise(float3 x)
    {
        float3 i = floor(x);
        float3 f = frac(x);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(lerp(EyeHash(i), EyeHash(i + float3(1, 0, 0)), f.x),
                         lerp(EyeHash(i + float3(0, 1, 0)), EyeHash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(EyeHash(i + float3(0, 0, 1)), EyeHash(i + float3(1, 0, 1)), f.x),
                         lerp(EyeHash(i + float3(0, 1, 1)), EyeHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 irisIn = UNITY_ACCESS_INSTANCED_PROP(EyeProps, _EyeIris);
                float4 skinIn = UNITY_ACCESS_INSTANCED_PROP(EyeProps, _EyeSkin);
                if (dot(irisIn.rgb, irisIn.rgb) < 1e-6) irisIn.rgb = _BaseColor.rgb;

                float3 d = normalize(i.positionOS);
                float2 e = d.xy;                  // across the front of the eye
                float r = length(e);
                float angle = atan2(e.y, e.x);

                // The iris: radiating fibres, a net of dark veins, darker towards its rim and round the pupil.
                float fibres = EyeNoise(float3(angle * 7.0, r * 26.0, skinIn.a));
                float veins = abs(EyeNoise(float3(e * 13.0, skinIn.a + 3.7)) - 0.5);
                half3 iris = irisIn.rgb * (0.72 + 0.45 * fibres);
                iris *= lerp(0.55, 1.0, smoothstep(0.0, 0.05, veins));
                iris *= lerp(1.0, 0.55, smoothstep(0.55, 0.85, r));

                // The pupil: an ellipse, wide and flat or tall and thin.
                float2 size = lerp(float2(0.4, 0.2), float2(0.13, 0.38), irisIn.a);
                float pupil = length(e / size);
                float dark = (1.0 - smoothstep(0.9, 1.0, pupil)) * step(0.0, d.z);
                iris *= lerp(1.0, 0.7, (1.0 - smoothstep(1.0, 1.35, pupil)) * step(0.0, d.z));
                half3 colour = lerp(iris, half3(0.01, 0.01, 0.012), dark);
                // The back of the ball sits inside the head: give it the skin colour.
                colour = lerp(colour, skinIn.rgb, smoothstep(-0.05, -0.35, d.z));

                float3 n = normalize(i.normalWS);
                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.normalWS = n;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(n);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = colour;
                s.smoothness = 0.96;
                s.occlusion = 1;
                s.alpha = 1;
                half4 c = UniversalFragmentPBR(input, s);

                // The bright glint of the lamp on the wet surface of the eye.
                Light sun = GetMainLight(input.shadowCoord);
                float3 halfway = normalize(sun.direction + input.viewDirectionWS);
                c.rgb += sun.color * pow(saturate(dot(n, halfway)), 900.0) * 3.0 * sun.shadowAttenuation;
                c.rgb = MixFog(c.rgb, i.fogFactor);
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(p, n, _LightDirection));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 DepthVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings NormalsVert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 NormalsFrag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

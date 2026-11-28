// Frog skin (Universal Render Pipeline). Paints every frog from a handful of numbers per frog
// (see SkinLook in Simulation/Looks/FrogSkin.cs), with no textures:
// - a darker back fading to lighter sides and a belly colour underneath,
// - the species' markings: speckles, blotches, coloured flanks with pale bars, a lip stripe,
//   and crossbands on the legs, placed differently on every frog (its "seed"),
// - real frog details: the round eardrum behind the eye, nostrils, the mouth line and the ridge
//   of skin along each side of the back,
// - warts and fine grainy skin as small bumps that catch the light,
// - a wet film on top (a second, sharp highlight) and a little light shining through thin skin.
// Each frog's numbers come in per instance, so hundreds of different frogs still draw in a few batches.
Shader "FrogTerrarium/FrogSkin"
{
    Properties
    {
        _BaseColor ("Colour when nothing is set", Color) = (0.35, 0.66, 0.38, 1)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
    CBUFFER_END

    UNITY_INSTANCING_BUFFER_START(SkinProps)
        UNITY_DEFINE_INSTANCED_PROP(float4, _SkinBack)     // rgb back colour (linear), a = pattern seed
        UNITY_DEFINE_INSTANCED_PROP(float4, _SkinMark)     // rgb marking colour, a = part (0 body, 1 leg, 2 foot, 3 tadpole)
        UNITY_DEFINE_INSTANCED_PROP(float4, _SkinBelly)    // rgb belly colour, a = warts
        UNITY_DEFINE_INSTANCED_PROP(float4, _SkinPattern)  // spots, blotches, bars, lip
        UNITY_DEFINE_INSTANCED_PROP(float4, _SkinToe)      // rgb toe colour, a = how much of it shows
        UNITY_DEFINE_INSTANCED_PROP(float4, _SkinExtra)    // x gloss, y contrast
    UNITY_INSTANCING_BUFFER_END(SkinProps)

    float SkinHash(float3 p)
    {
        p = frac(p * 0.3183099 + 0.1);
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    float3 SkinHash3(float3 p)
    {
        p = float3(dot(p, float3(127.1, 311.7, 74.7)), dot(p, float3(269.5, 183.3, 246.1)), dot(p, float3(113.5, 271.9, 124.6)));
        return frac(sin(p) * 43758.5453);
    }

    float SkinNoise(float3 x)
    {
        float3 i = floor(x);
        float3 f = frac(x);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(lerp(SkinHash(i), SkinHash(i + float3(1, 0, 0)), f.x),
                         lerp(SkinHash(i + float3(0, 1, 0)), SkinHash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(SkinHash(i + float3(0, 0, 1)), SkinHash(i + float3(1, 0, 1)), f.x),
                         lerp(SkinHash(i + float3(0, 1, 1)), SkinHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
    }

    float SkinFbm(float3 p)
    {
        float sum = 0.0, amp = 0.5;
        [unroll] for (int k = 0; k < 4; k++)
        {
            sum += amp * SkinNoise(p);
            p = p * 2.03 + 1.7;
            amp *= 0.5;
        }
        return sum / 0.9375;
    }

    // Distance to the nearest of a scatter of points (x) and a random number for that point (y):
    // the basis of round spots and warts.
    float2 SkinCells(float3 p)
    {
        float3 i = floor(p);
        float3 f = frac(p);
        float best = 8.0, id = 0.0;
        [unroll] for (int z = -1; z <= 1; z++)
        [unroll] for (int y = -1; y <= 1; y++)
        [unroll] for (int x = -1; x <= 1; x++)
        {
            float3 g = float3(x, y, z);
            float3 r = g + SkinHash3(i + g) - f;
            float d = dot(r, r);
            if (d < best) { best = d; id = SkinHash(i + g + 5.3); }
        }
        return float2(sqrt(best), id);
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
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
                float scale : TEXCOORD4;
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
                o.scale = length(mul((float3x3)UNITY_MATRIX_M, float3(0, 1, 0)));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 backIn = UNITY_ACCESS_INSTANCED_PROP(SkinProps, _SkinBack);
                float4 markIn = UNITY_ACCESS_INSTANCED_PROP(SkinProps, _SkinMark);
                float4 bellyIn = UNITY_ACCESS_INSTANCED_PROP(SkinProps, _SkinBelly);
                float4 pattern = UNITY_ACCESS_INSTANCED_PROP(SkinProps, _SkinPattern);
                float4 toeIn = UNITY_ACCESS_INSTANCED_PROP(SkinProps, _SkinToe);
                float4 extra = UNITY_ACCESS_INSTANCED_PROP(SkinProps, _SkinExtra);
                // A frog drawn without its numbers (should not happen) still gets a sensible colour.
                if (dot(backIn.rgb, backIn.rgb) < 1e-6) backIn.rgb = _BaseColor.rgb;

                float3 p = i.positionOS;
                float seed = backIn.a;
                int part = (int)round(markIn.a);
                float warts = bellyIn.a;
                float gloss = extra.x;
                float contrast = extra.y;
                float3 q = p + float3(1.31, 0.73, 1.97) * seed;

                float3 colour = backIn.rgb;
                float height = 0.0;
                float mottle = SkinFbm(q * 6.0);
                float belly = 0.0;

                if (part == 0)
                {
                    // ---- body ----
                    float sideways = saturate(abs(p.x) * 2.2);
                    colour *= lerp(0.84, 1.06, sideways) * lerp(0.9, 1.1, mottle);

                    // Coloured flanks with pale upright bars (red-eyed tree frogs).
                    float flank = smoothstep(0.3, 0.4, abs(p.x)) * smoothstep(0.22, 0.02, p.y) * smoothstep(0.32, 0.12, p.z);
                    float bar = smoothstep(0.55, 0.8, sin(p.z * 32.0 + SkinNoise(q * 4.0) * 3.0));
                    colour = lerp(colour, lerp(markIn.rgb, bellyIn.rgb, bar), flank * pattern.z);

                    // The belly, fading in along the lower sides.
                    belly = smoothstep(-0.03, -0.17, p.y);
                    colour = lerp(colour, bellyIn.rgb * lerp(0.94, 1.04, mottle), belly);

                    // A pale stripe along the lower lip, under the dark mouth line.
                    float front = smoothstep(0.08, 0.22, p.z);
                    float lip = exp(-pow((p.y + 0.095) / 0.022, 2.0)) * front;
                    colour = lerp(colour, markIn.rgb, saturate(lip * pattern.w * 1.3));
                    float mouth = exp(-pow((p.y + 0.06) / 0.008, 2.0)) * front;
                    colour *= 1.0 - 0.45 * mouth;

                    // The eardrum: a smooth round disc behind each eye, with a little rim.
                    float ear = length(float3(abs(p.x) - 0.35, p.y - 0.11, p.z - 0.15));
                    float drum = 1.0 - smoothstep(0.045, 0.058, ear);
                    colour *= lerp(1.0, 0.82, drum);
                    height += exp(-pow((ear - 0.058) / 0.01, 2.0)) * 0.5;

                    // Nostrils near the tip of the snout.
                    float nose = length(float3(abs(p.x) - 0.065, p.y - 0.14, p.z - 0.46));
                    colour *= lerp(0.3, 1.0, smoothstep(0.012, 0.02, nose));

                    // The ridge of skin along each side of the back.
                    height += 0.35 * exp(-pow((abs(p.x) - 0.27) / 0.03, 2.0)) * smoothstep(0.05, 0.2, p.y) * (1.0 - smoothstep(0.25, 0.45, abs(p.z + 0.05)));
                }
                else if (part == 1)
                {
                    // ---- leg bone: dark crossbands, paler and toe-coloured underneath ----
                    float band = smoothstep(0.35, 0.75, sin(p.z * 17.0 + seed + SkinNoise(q * 5.0) * 2.5));
                    float banding = saturate(0.2 + pattern.y * 0.6 + pattern.x * 0.3);
                    colour *= lerp(0.92, 1.06, mottle) * (1.0 - 0.38 * band * banding * contrast);
                    float under = smoothstep(0.05, -0.3, p.y);
                    colour = lerp(colour, lerp(bellyIn.rgb, toeIn.rgb, toeIn.a), under * 0.5);
                    belly = under * 0.5;
                }
                else if (part == 2)
                {
                    // ---- foot: toe colour, lighter sticky pads at the tips ----
                    colour = lerp(colour, toeIn.rgb, toeIn.a);
                    colour *= 1.0 + 0.12 * smoothstep(0.7, 0.95, length(p.xz));
                }
                else
                {
                    // ---- tadpole: dark with gold flecks, paler underneath ----
                    colour *= lerp(0.85, 1.1, mottle);
                    belly = smoothstep(0.0, -0.3, p.y);
                    colour = lerp(colour, bellyIn.rgb, belly * 0.6);
                }

                // Blotches (also on the belly, like the black blotches on a fire-belly's red belly).
                float blotch = smoothstep(0.5, 0.57, SkinFbm(q * 2.6 + 3.1)) * pattern.y;
                // Spots: only some cells get one, and more of them for spottier species.
                float2 cells = SkinCells(q * (part == 0 ? 7.0 : 9.0));
                float radius = lerp(0.14, 0.32, frac(cells.y * 7.13));
                float spot = (1.0 - smoothstep(radius * 0.7, radius, cells.x)) * step(cells.y, pattern.x * 1.2);
                colour = lerp(colour, markIn.rgb, saturate(blotch * contrast));
                colour = lerp(colour, markIn.rgb, saturate(spot * contrast * (1.0 - belly * 0.6)));

                // Warts on top and fine grain everywhere.
                float2 bumps = SkinCells(q * 11.0);
                float wart = saturate(1.0 - bumps.x / 0.45);
                wart = wart * wart * warts * (1.0 - belly * 0.7);
                colour *= 1.0 + 0.12 * wart;
                height += wart * 0.9 + SkinNoise(q * 42.0) * 0.18;

                // Turn the bump height into a tilted normal (bump mapping from screen-space slopes).
                float3 nGeo = normalize(i.normalWS);
                float h = height * 0.02 * i.scale;
                float3 dpdx = ddx(i.positionWS), dpdy = ddy(i.positionWS);
                float dhx = ddx(h), dhy = ddy(h);
                float3 r1 = cross(dpdy, nGeo), r2 = cross(nGeo, dpdx);
                float det = dot(dpdx, r1);
                float3 surfGrad = sign(det) * (dhx * r1 + dhy * r2);
                float3 n = normalize(abs(det) * nGeo - surfGrad);
                if (any(isnan(n))) n = nGeo;

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
                s.albedo = saturate(colour);
                s.metallic = 0;
                s.smoothness = lerp(0.35, 0.82, gloss) * (1.0 - wart * 0.35);
                s.occlusion = 1.0 - saturate(wart * 0.25);
                s.alpha = 1;
                half4 c = UniversalFragmentPBR(input, s);

                // The wet film: a second, sharper highlight that follows the smooth shape, not the bumps.
                Light sun = GetMainLight(input.shadowCoord);
                float3 halfway = normalize(sun.direction + input.viewDirectionWS);
                float film = pow(saturate(dot(nGeo, halfway)), 320.0) * gloss * 1.6;
                c.rgb += sun.color * film * sun.shadowAttenuation;

                // Light shining through thin skin (toes, the edges of the body) when lit from behind.
                float through = pow(saturate(dot(input.viewDirectionWS, -sun.direction)), 4.0) * (part == 2 ? 0.5 : 0.18);
                c.rgb += s.albedo * sun.color * through * (0.5 + 0.5 * sun.shadowAttenuation);

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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 l = normalize(_LightPosition - p);
                #else
                float3 l = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(p, n, l));
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

            half4 NormalsFrag(Varyings i) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

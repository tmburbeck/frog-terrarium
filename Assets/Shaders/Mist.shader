// The mister's spray cloud (Universal Render Pipeline): a soft, see-through puff that is thickest
// in the middle, fades to nothing at its edges and where it meets the ground, and drifts a little.
Shader "FrogTerrarium/Mist"
{
    Properties
    {
        _Color ("Colour", Color) = (0.9, 0.94, 0.97, 0.35)
        _Softness ("Ground softness", Range(0.01, 2)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.screenPos = ComputeScreenPos(p.positionCS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float core = pow(saturate(dot(normalize(i.normalWS), view)), 1.6);
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float soft = saturate((sceneDepth - i.screenPos.w) / _Softness);
                float drift = 0.85 + 0.15 * sin(i.positionWS.x * 6.0 + _Time.y * 1.7) * sin(i.positionWS.z * 5.0 - _Time.y * 1.3);
                Light sun = GetMainLight();
                half3 colour = _Color.rgb * (SampleSH(float3(0, 1, 0)) + sun.color * 0.6);
                return half4(colour, _Color.a * core * soft * drift);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

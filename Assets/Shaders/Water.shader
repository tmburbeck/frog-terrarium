// See-through water surface with a slow ripple. Also used for the mister's spray cloud.
Shader "FrogTerrarium/Water"
{
    Properties
    {
        _Color ("Colour", Color) = (0.22, 0.42, 0.46, 0.55)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        fixed4 _Color;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float ripple = sin(IN.worldPos.x * 3.1 + _Time.y * 1.3) * sin(IN.worldPos.z * 2.7 - _Time.y * 1.1);
            o.Albedo = _Color.rgb * (0.94 + 0.06 * ripple);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = _Color.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}

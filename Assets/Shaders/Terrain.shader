// Ground for the tank. Colour comes from the mesh's vertex colours (painted layer, wetness,
// or a temperature/humidity map), with a little procedural grain so flat colour reads as soil.
// Vertex colour alpha is wetness: wet ground near water is shinier.
Shader "FrogTerrarium/Terrain"
{
    Properties
    {
        _Glossiness ("Wet smoothness", Range(0, 1)) = 0.6
        _Grain ("Grain", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        half _Glossiness;
        half _Grain;

        struct Input
        {
            float4 color : COLOR;
            float3 worldPos;
        };

        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float fine = Hash(floor(IN.worldPos.xz * 24.0));
            float coarse = Hash(floor(IN.worldPos.xz * 5.0) + 17.0);
            float grain = (fine * 0.6 + coarse * 0.4) - 0.5;
            o.Albedo = saturate(IN.color.rgb * (1.0 + _Grain * grain));
            o.Metallic = 0;
            o.Smoothness = _Glossiness * IN.color.a;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

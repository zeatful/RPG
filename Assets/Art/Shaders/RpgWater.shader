// Simple transparent water with a gentle colour ripple.
Shader "RPG/Water"
{
    Properties
    {
        _Color ("Shallow", Color) = (0.25,0.55,0.65,0.7)
        _Deep ("Deep", Color) = (0.08,0.25,0.38,0.85)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        ZWrite Off
        CGPROGRAM
        #pragma surface surf BlinnPhong alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        fixed4 _Deep;
        struct Input { float3 worldPos; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            float w = sin(IN.worldPos.x * 0.35 + _Time.y * 1.1) * 0.5 + sin(IN.worldPos.z * 0.29 - _Time.y * 0.8) * 0.5;
            fixed4 c = lerp(_Deep, _Color, saturate(0.5 + w * 0.35));
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Specular = 0.6;
            o.Gloss = 0.8;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}

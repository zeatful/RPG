// See-through version of a wall: matte lit colour with alpha. Swapped in for interior walls that sit
// between the camera and the player.
Shader "RPG/Fade"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.25)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        ZWrite Off
        CGPROGRAM
        #pragma surface surf Lambert alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float3 worldPos; };
        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Alpha = _Color.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}

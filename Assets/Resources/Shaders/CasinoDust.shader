// Specks of dust drifting in the light beams (Ultra): tiny camera-facing glows like Casino/Glow, one static mesh,
// each speck wandering slowly round where it was placed and catching the light now and then as it turns.
// POSITION = speck, TEXCOORD0 = corner (0..1), TEXCOORD1: x = size (m), y = seed, z = drift speed. Vertex colour = light.
Shader "Casino/Dust"
{
    Properties
    {
        _MainTex ("Glow", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CasinoLook.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 data : TEXCOORD1;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float seed = v.data.y;
                float t = _Time.y * v.data.z;
                float3 drift = float3(sin(t + seed), sin(t * 0.7 + seed * 2.3) * 0.6, cos(t * 0.8 + seed * 1.7)) * 0.12;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz + drift;
                float3 view = mul(UNITY_MATRIX_V, float4(world, 1.0)).xyz;
                view.xy += (v.uv - 0.5) * v.data.x;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.uv = v.uv;
                half glint = 0.5 + 0.5 * sin(_Time.y * (0.6 + frac(seed) * 0.8) + seed * 5.1);
                half nearFade = saturate((-view.z - 0.3) * 1.5);
                o.color = v.color * (0.2 + 0.8 * glint * glint * glint) * nearFade * (1.0 - CasinoHaze(world));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return half4(i.color.rgb * tex2D(_MainTex, i.uv).a, 0);
            }
            ENDCG
        }
    }
}

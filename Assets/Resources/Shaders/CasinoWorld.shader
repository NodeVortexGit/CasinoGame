// Opaque world geometry. Lighting is baked into the vertex colours on the CPU (no real-time lights), so this is about
// as cheap as a shader gets. Vertex colours hold half the light so over-bright spots (up to 2x) survive 8-bit colours. TEXCOORD1 animates brightness for neon, marquee bulbs and screens:
// x = phase, y = depth (0 = static), z = speed.
Shader "Casino/World"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 anim : TEXCOORD1;
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
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                half wave = sin(_Time.y * v.anim.z + v.anim.x);
                half pulse = saturate(wave * 2.5 + 0.5);
                o.color = v.color * (2.0 * lerp(1.0, 0.3 + 1.0 * pulse, v.anim.y));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * i.color;
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}

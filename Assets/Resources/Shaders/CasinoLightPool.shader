// Pools of light on the floor under the ceiling's downlights (high graphics settings): flat soft discs that brighten
// what is already drawn, multiplying it so the carpet's own colours glow rather than washing out (Blend DstColor One:
// result = floor * (1 + light)). Vertex colour = the light; the disc's falloff comes from the UV.
Shader "Casino/LightPool"
{
    Properties { }
    SubShader
    {
        Tags { "Queue" = "Geometry+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend DstColor One
        ZWrite Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CasinoLook.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
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
                o.uv = v.uv * 2.0 - 1.0;
                o.color = v.color * (1.0 - CasinoHaze(mul(unity_ObjectToWorld, v.vertex).xyz));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // A beam's pool: even in the middle, fading softly at the rim.
                half d = saturate(1.0 - dot(i.uv, i.uv));
                return half4(i.color.rgb * (d * d), 1);
            }
            ENDCG
        }
    }
}

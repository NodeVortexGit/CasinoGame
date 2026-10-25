// Additive light glows (bulbs, chandeliers, neon halos), all camera-facing, expanded in the vertex shader so a whole
// room of glows is one static mesh and one draw call. POSITION = glow centre, TEXCOORD0 = corner (0..1),
// TEXCOORD1: x = size (m), y = phase, z = speed, w = pulse depth. Vertex colour = glow colour (alpha unused).
// Glows fade into the haze like everything else (they add light, so fading them means dimming them). From High up the
// camera's real bloom spreads the brightest light itself, so the painted halos are turned down by _GlowDim.
Shader "Casino/Glow"
{
    Properties
    {
        _MainTex ("Glow", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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
                float3 view = UnityObjectToViewPos(v.vertex.xyz);
                view.xy += (v.uv - 0.5) * v.data.x;
                o.pos = mul(UNITY_MATRIX_P, float4(view, 1.0));
                o.uv = v.uv;
                half pulse = 0.5 + 0.5 * sin(_Time.y * v.data.z + v.data.y);
                // Fade glows that get very close to the camera instead of letting them fill the screen.
                half nearFade = saturate((-view.z - 0.4) * 0.8);
                half haze = CasinoHaze(mul(unity_ObjectToWorld, v.vertex).xyz);
                o.color = v.color * lerp(1.0, pulse, v.data.w) * nearFade * (1.0 - haze) * (1.0 - _GlowDim);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half a = tex2D(_MainTex, i.uv).a;
                return half4(i.color.rgb * (a * 1.8), 0);
            }
            ENDCG
        }
    }
}

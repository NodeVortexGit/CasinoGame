// Opaque world geometry. Lighting is baked into the vertex colours on the CPU (no real-time lights), so this is about
// as cheap as a shader gets. Vertex colours hold half the light so over-bright spots (up to 2x) survive 8-bit colours. TEXCOORD1 animates brightness for neon, marquee bulbs and screens:
// x = phase, y = depth (0 = static), z = speed; w = 1 for metal. Vertex alpha = how polished the surface is.
// The higher graphics settings (CASINO_REFLECT) make polished surfaces reflect the room: a cubemap rendered once from
// the middle of the casino and box-projected onto the room's walls, blurrier the less polished, with Fresnel (stronger
// at grazing angles, like a real floor) for marble, lacquer and wood, tinted by the surface for metal. _HazeDensity > 0
// adds a faint haze that softens the far side of the room.
// The second pass only draws depth, for the depth of field at Ultra.
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
            #pragma target 3.0
            #pragma multi_compile __ CASINO_REFLECT
            #include "UnityCG.cginc"
            #include "CasinoLook.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 anim : TEXCOORD1;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half haze : TEXCOORD1;
            #if CASINO_REFLECT
                float3 world : TEXCOORD2;
                float3 normal : TEXCOORD3;
                half2 finish : TEXCOORD4;   // gloss, metal
            #endif
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(world);
                o.uv = v.uv;
                half wave = sin(_Time.y * v.anim.z + v.anim.x);
                half pulse = saturate(wave * 2.5 + 0.5);
                o.color = half4(v.color.rgb * (2.0 * lerp(1.0, 0.3 + 1.0 * pulse, v.anim.y)), 1);
                o.haze = CasinoHaze(world);
            #if CASINO_REFLECT
                o.world = world;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.finish = half2(v.color.a, v.anim.w);
            #endif
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                half4 c = tex * i.color;
            #if CASINO_REFLECT
                half gloss = i.finish.x;
                if (gloss > 0.01)
                {
                    float3 n = normalize(i.normal);
                    float3 view = normalize(i.world - _WorldSpaceCameraPos);
                    half3 env = CasinoReflection(reflect(view, n), i.world, gloss);
                    half fresnel = pow(1.0 - saturate(dot(n, -view)), 5.0);
                    half metal = i.finish.y;
                    // Dielectrics (marble, lacquer, varnish) reflect 4-8% head-on and most of the light at grazing
                    // angles; metal reflects strongly at any angle, in its own colour (the lit colour, normalised).
                    half3 tint = c.rgb / max(max(c.r, c.g), max(c.b, 0.001));
                    half3 f = lerp((0.08 + 0.92 * fresnel).xxx, tint * (0.85 + 0.15 * fresnel), metal) * gloss;
                    c.rgb = c.rgb * (1.0 - f * lerp(0.5, 0.8, metal)) + env * f;
                }
            #endif
                c.rgb = CasinoApplyHaze(c.rgb, i.haze);
                c.a = 1;
                return c;
            }
            ENDCG
        }

        // Depth only: draws the camera's depth texture for the depth of field at Ultra (the built-in pipeline draws it
        // with the shadow caster pass; the casino has no real-time lights, so nothing else uses it).
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}

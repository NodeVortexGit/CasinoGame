// Realistic staff (MakeHuman models): texture x colour, per-pixel light (warm key from the camera side, warm ambient,
// neon rim) times the room light where they stand. Per pixel so faces shade smoothly; the highlight tightens with
// _Gloss (soft on skin, a crisp catch-light in the eyes). _Cutoff > 0 makes it alpha-tested (hair, brows, lashes).
// Hair also sways: vertices below the head move by _Sway (world space), more the lower they hang.
// The high graphics settings (CASINO_REAL) add the room's own lamp above or beside them (_RoomLight, the strongest
// light where they stand, with its colour), light that scatters under the skin (a softer, warmer edge to shadows on
// _Skin materials) and reflections of the casino in glossy things: eyes, shoes, satin.
Shader "Casino/Person"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Tint ("Room light", Color) = (1, 1, 1, 1)
        _Rim ("Rim colour", Color) = (1, 0.35, 0.75, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0
        _Gloss ("Sheen", Range(0, 1)) = 0
        _Skin ("Skin", Range(0, 1)) = 0
        _RoomLightDir ("Room lamp direction (world)", Vector) = (0, 1, 0, 0)
        _RoomLightColor ("Room lamp colour", Color) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _Sway ("Hair sway (world)", Vector) = (0, 0, 0, 0)
        _HeadY ("Head height (world)", Float) = 1.6
        _SwayAmount ("Sway amount", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull [_Cull]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile __ CASINO_REAL
            #include "UnityCG.cginc"
            #include "CasinoLook.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color, _Tint, _Rim, _RoomLightColor;
            float4 _RoomLightDir;
            half _Cutoff, _Gloss, _SwayAmount, _Skin;
            float4 _Sway;
            float _HeadY;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2;
                half haze : TEXCOORD3;
            #if CASINO_REAL
                float3 world : TEXCOORD4;
            #endif
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float hang = saturate((_HeadY - worldPos.y) / 0.45);
                worldPos += _Sway.xyz * (hang * hang * _SwayAmount);
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - worldPos;
                o.haze = CasinoHaze(worldPos);
            #if CASINO_REAL
                o.world = worldPos;
            #endif
                return o;
            }

            half4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * _Color;
                clip(c.a - _Cutoff);
                float3 n = normalize(i.normal) * (facing < 0 ? -1.0 : 1.0);   // two-sided hair lights its back faces too
                float3 view = normalize(i.view);
                float3 key = normalize(float3(0.3, 0.9, 0.0) + view * 0.7);
                half ndl = dot(n, key);
                half hemi = n.y * 0.5 + 0.5;
                half rim = pow(1.0 - saturate(dot(n, view)), 3.0);
                half3 ambient = lerp(half3(0.24, 0.17, 0.2), half3(0.42, 0.36, 0.38), hemi);
                half3 h = normalize(key + view);
                half3 spec = pow(saturate(dot(n, h)), lerp(16.0, 96.0, _Gloss)) * _Gloss * half3(1.0, 0.92, 0.85);
            #if CASINO_REAL
                // Under the skin, red light travels furthest: shadows on skin get a soft, warm edge instead of a grey one.
                half3 wrap = lerp(half3(0.25, 0.25, 0.25), half3(0.55, 0.32, 0.28), _Skin);
                half3 diffuse = saturate((ndl + wrap) / (1.0 + wrap));
                half3 lamp = _RoomLightColor.rgb;
                half ndl2 = dot(n, _RoomLightDir.xyz);
                half3 lampDiffuse = saturate((ndl2 + wrap) / (1.0 + wrap));
                // Less of the flat light from the camera's side, more from the real lamp: faces get their shape from above.
                half3 light = (ambient * 0.85 + diffuse * half3(0.78, 0.7, 0.64) * 0.5 + lampDiffuse * lamp * 1.25) * _Tint.rgb + rim * _Rim.rgb * 0.55;
                half3 h2 = normalize(_RoomLightDir.xyz + view);
                spec += pow(saturate(dot(n, h2)), lerp(16.0, 96.0, _Gloss)) * _Gloss * lamp;
                // Glossy things mirror the casino: lights glint in the eyes, on shoes and satin.
                half fresnel = pow(1.0 - saturate(dot(n, view)), 5.0);
                half3 env = CasinoReflection(reflect(-view, n), i.world, _Gloss);
                spec += env * (_Gloss * _Gloss) * lerp(0.12, 0.9, fresnel);
            #else
                half3 diffuse = saturate(ndl * 0.75 + 0.25);
                half3 light = (ambient + diffuse * half3(0.78, 0.7, 0.64)) * _Tint.rgb + rim * _Rim.rgb * 0.55;
            #endif
                c.rgb = c.rgb * light + spec;
                c.rgb = CasinoApplyHaze(c.rgb, i.haze);
                c.a = 1;
                return c;
            }
            ENDCG
        }

        // Depth only, for the depth of field at Ultra (drawn with the shadow caster pass), with the same hair sway and
        // cut-outs so a face's outline is sharp against the blurred room behind it.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff, _SwayAmount;
            float4 _Sway;
            float _HeadY;

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float hang = saturate((_HeadY - worldPos.y) / 0.45);
                worldPos += _Sway.xyz * (hang * hang * _SwayAmount);
                v.vertex = mul(unity_WorldToObject, float4(worldPos, 1.0));
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                clip(tex2D(_MainTex, i.uv).a * _Color.a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}

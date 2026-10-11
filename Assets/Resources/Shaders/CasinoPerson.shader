// Realistic staff (MakeHuman models): texture x colour, cheap per-vertex light (warm key from the camera side, warm
// ambient, neon rim) times the room light where they stand. _Cutoff > 0 makes it alpha-tested (hair, brows, lashes).
// Hair also sways: vertices below the head move by _Sway (world space), more the lower they hang.
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color, _Tint, _Rim;
            half _Cutoff, _Gloss, _SwayAmount;
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
                half3 light : TEXCOORD1;
                half3 spec : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float hang = saturate((_HeadY - worldPos.y) / 0.45);
                worldPos += _Sway.xyz * (hang * hang * _SwayAmount);
                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 view = normalize(_WorldSpaceCameraPos - worldPos);
                float3 key = normalize(float3(0.3, 0.9, 0.0) + view * 0.7);
                half ndl = dot(n, key);
                half diffuse = saturate(ndl * 0.75 + 0.25);
                half hemi = n.y * 0.5 + 0.5;
                half rim = pow(1.0 - saturate(dot(n, view)), 3.0);
                half3 ambient = lerp(half3(0.24, 0.17, 0.2), half3(0.42, 0.36, 0.38), hemi);
                o.light = (ambient + diffuse * half3(0.78, 0.7, 0.64)) * _Tint.rgb + rim * _Rim.rgb * 0.55;
                half3 h = normalize(key + view);
                o.spec = pow(saturate(dot(n, h)), 24.0) * _Gloss * half3(1.0, 0.92, 0.85);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * _Color;
                clip(c.a - _Cutoff);
                c.rgb = c.rgb * i.light + i.spec;
                c.a = 1;
                return c;
            }
            ENDCG
        }
    }
}

// A soft contact shadow on the floor under someone walking around (the standing staff's shadows are baked into the
// floor): darkens what is already drawn by the texture's alpha (a radial falloff) times _Strength.
Shader "Casino/Shadow"
{
    Properties
    {
        _MainTex ("Falloff", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend DstColor Zero
        ZWrite Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CasinoLook.cginc"

            sampler2D _MainTex;
            half _Strength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half haze : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.haze = CasinoHaze(mul(unity_ObjectToWorld, v.vertex).xyz);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half dark = tex2D(_MainTex, i.uv).a * _Strength * (1.0 - i.haze);
                return half4((1.0 - dark).xxx, 1);
            }
            ENDCG
        }
    }
}

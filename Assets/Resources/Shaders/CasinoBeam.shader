// Beams of light from the downlights to the floor, lit up by the haze (high graphics settings). Each is a cone, widest
// at the floor, drawn additively from both sides: densest through its middle (where you look through the most lit
// air), fading softly at its outline, near the ceiling and into the floor, and when the camera walks into one.
// Vertex colour = the light; TEXCOORD0.y = 0 at the lamp, 1 at the floor.
Shader "Casino/Beam"
{
    Properties { }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                float3 normal : TEXCOORD0;
                float3 view : TEXCOORD1;
                half along : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(world);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - world;
                o.along = v.uv.y;
                o.color = v.color * (1.0 - CasinoHaze(world));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float dist = length(i.view);
                half facing = abs(dot(normalize(i.normal), i.view / dist));
                half along = i.along;
                // Brightest just under the lamp, thinning out as it spreads; soft where it leaves the ceiling and
                // where it reaches the floor.
                half fade = (1.0 - along * 0.6) * saturate(along * 10.0) * saturate((1.0 - along) * 3.0);
                half near = saturate((dist - 0.5) * 0.6);
                return half4(i.color.rgb * (facing * facing * fade * near), 0);
            }
            ENDCG
        }
    }
}

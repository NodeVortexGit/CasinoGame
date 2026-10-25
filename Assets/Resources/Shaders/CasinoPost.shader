// The world camera's finishing pass at the high graphics settings (WorldPost.cs): the casino as a film camera would
// shoot it. It renders in HDR, so neon, bulbs and glows go above 1:
// 0 prefilter: downsample, keeping only what is brighter than _Threshold (with a soft knee);
// 1 down / 2 up: a dual-filter blur pyramid, the up pass adding each level onto the next larger one;
// 3 final: depth of field and lens fringes (Ultra), bloom and streaks added in linear light, exposure, filmic (ACES)
//   tone mapping so bright colours roll off smoothly instead of clipping, a film grade (cool shadows, warm highlights,
//   a gentle S-curve), a vignette, film grain, and a little dither against banding in the dark gradients;
// 4 streak down / 5 streak up: the same pyramid, horizontal only, for the long flat streaks of an anamorphic lens;
// 6 focus: the autofocus, a 1x1 target easing towards a face the game picks (the person you're about to talk to or
//   play with) when it can be seen, else towards whatever is in the middle of the view;
// 7 depth prefilter / 8 bokeh / 9 tent: depth of field at half resolution, each pixel spread into a disc as wide as
//   its circle of confusion (alpha); out-of-focus lights become soft round discs, as through a real lens.
Shader "Hidden/Casino/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex, _Bloom, _Streak, _DofTex, _FocusTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float4 _MainTex_TexelSize;
    half _Threshold, _Knee, _BloomStrength, _Exposure, _Vignette;
    half _StreakStrength, _Grade, _Grain, _Aberration, _FocusBlend;
    float4 _FocusTarget;    // a face to focus on: xy = where it is on screen, z = how far away, w = 1 if there is one
    half4 _StreakTint;
    float _GrainSeed;
    float4 _DofParams;   // x = aperture (how fast things blur away from the focus), y = widest blur (fraction of the
                         // screen height), z = how much things nearer than the focus blur, compared with further ones

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float2 uvDepth : TEXCOORD1;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        o.uvDepth = v.texcoord;
    #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0) o.uvDepth.y = 1.0 - o.uvDepth.y;   // the frame is upside down, the depth isn't
    #endif
        return o;
    }

    half3 Sample(float2 uv) { return tex2D(_MainTex, uv).rgb; }

    float SceneDepth(float2 uv) { return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv)); }

    // How blurred the scene is at uv: 0 in focus, up to 1 (the widest blur) behind the focus, down to -1 in front.
    // Like a real lens it goes with 1/focus - 1/distance: focus close and the room behind melts away; focus far and
    // the background stays nearly sharp.
    half CircleOfConfusion(float2 uvDepth)
    {
        float focus = tex2D(_FocusTex, float2(0.5, 0.5)).r;
        float coc = (1.0 / focus - 1.0 / SceneDepth(uvDepth)) * _DofParams.x;
        coc = coc < 0.0 ? coc * _DofParams.z : coc;
        return clamp(coc, -1.0, 1.0);
    }

    half4 FragPrefilter(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        half3 c = (Sample(i.uv + float2(-d.x, -d.y)) + Sample(i.uv + float2(d.x, -d.y)) +
                   Sample(i.uv + float2(-d.x, d.y)) + Sample(i.uv + float2(d.x, d.y))) * 0.25;
        c = min(c, 8.0);   // a stray very bright pixel would otherwise flicker as a big square of bloom, and a
                           // chandelier's stacked glows would swell into a fog
        half bright = max(c.r, max(c.g, c.b));
        half soft = clamp(bright - _Threshold + _Knee, 0.0, 2.0 * _Knee);
        soft = soft * soft / (4.0 * _Knee + 1e-4);
        half keep = max(soft, bright - _Threshold) / max(bright, 1e-4);
        return half4(c * keep, 1);
    }

    half4 FragDown(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        half3 c = Sample(i.uv) * 4.0;
        c += Sample(i.uv + float2(-d.x, -d.y)) + Sample(i.uv + float2(d.x, -d.y));
        c += Sample(i.uv + float2(-d.x, d.y)) + Sample(i.uv + float2(d.x, d.y));
        return half4(c * 0.125, 1);
    }

    half4 FragUp(v2f i) : SV_Target
    {
        float2 d = _MainTex_TexelSize.xy;
        half3 c = Sample(i.uv + float2(-d.x * 2.0, 0)) + Sample(i.uv + float2(d.x * 2.0, 0));
        c += Sample(i.uv + float2(0, -d.y * 2.0)) + Sample(i.uv + float2(0, d.y * 2.0));
        c += (Sample(i.uv + float2(-d.x, d.y)) + Sample(i.uv + float2(d.x, d.y))) * 2.0;
        c += (Sample(i.uv + float2(-d.x, -d.y)) + Sample(i.uv + float2(d.x, -d.y))) * 2.0;
        return half4(c / 12.0, 1);
    }

    // Halves the width only: each level of the streak pyramid is twice as long and just as thin.
    half4 FragStreakDown(v2f i) : SV_Target
    {
        float dx = _MainTex_TexelSize.x;
        half3 c = (Sample(i.uv + float2(-dx * 1.5, 0)) + Sample(i.uv + float2(dx * 1.5, 0))) * 0.125;
        c += (Sample(i.uv + float2(-dx * 0.5, 0)) + Sample(i.uv + float2(dx * 0.5, 0))) * 0.375;
        return half4(c, 1);
    }

    half4 FragStreakUp(v2f i) : SV_Target
    {
        float dx = _MainTex_TexelSize.x;
        half3 c = Sample(i.uv) * 0.5 + (Sample(i.uv + float2(-dx, 0)) + Sample(i.uv + float2(dx, 0))) * 0.25;
        return half4(c, 1);
    }

    // The autofocus, like a camera's face detection: a face the game picks (the dealer behind a table, the bartender
    // behind the bar) when the scene really is that far away where the face is (so not hidden behind a pillar);
    // otherwise whatever is in the middle of the view, averaged in dioptres (1/distance) so a near thing in the middle
    // wins over the far wall beside it. Eased from the last frame's, so the focus pulls smoothly from one thing to the
    // next like a camera operator's.
    float4 FragFocus(v2f i) : SV_Target
    {
        float inv = 2.0 / SceneDepth(float2(0.5, 0.5));
        inv += 1.0 / SceneDepth(float2(0.46, 0.5)) + 1.0 / SceneDepth(float2(0.54, 0.5));
        inv += 1.0 / SceneDepth(float2(0.5, 0.45)) + 1.0 / SceneDepth(float2(0.5, 0.55));
        float focus = clamp(6.0 / max(inv, 1e-3), 0.6, 30.0);
        if (_FocusTarget.w > 0.5)
        {
            float2 at = _FocusTarget.xy;
            float miss = abs(SceneDepth(at) - _FocusTarget.z);
            miss = min(miss, abs(SceneDepth(saturate(at + float2(0.0, -0.025))) - _FocusTarget.z));   // chin and neck,
            miss = min(miss, abs(SceneDepth(saturate(at + float2(0.012, -0.012))) - _FocusTarget.z)); // in case of hair
            if (miss < 0.3 + 0.05 * _FocusTarget.z) focus = clamp(_FocusTarget.z, 0.6, 30.0);
        }
        float last = tex2D(_MainTex, float2(0.5, 0.5)).r;
        return lerp(last, focus, _FocusBlend).xxxx;
    }

    half4 FragDofPrefilter(v2f i) : SV_Target
    {
        half3 c = min(Sample(i.uv), 16.0);   // one bilinear tap halfway between four pixels averages them
        return half4(c, CircleOfConfusion(i.uvDepth));
    }

    #define BOKEH_TAPS 25
    static const float2 Bokeh[BOKEH_TAPS] =
    {
        float2(0.0000, 0.0000), float2(0.5000, 0.0000), float2(0.3536, 0.3536), float2(0.0000, 0.5000),
        float2(-0.3536, 0.3536), float2(-0.5000, 0.0000), float2(-0.3536, -0.3536), float2(-0.0000, -0.5000),
        float2(0.3536, -0.3536), float2(0.9808, 0.1951), float2(0.8315, 0.5556), float2(0.5556, 0.8315),
        float2(0.1951, 0.9808), float2(-0.1951, 0.9808), float2(-0.5556, 0.8315), float2(-0.8315, 0.5556),
        float2(-0.9808, 0.1951), float2(-0.9808, -0.1951), float2(-0.8315, -0.5556), float2(-0.5556, -0.8315),
        float2(-0.1951, -0.9808), float2(0.1951, -0.9808), float2(0.5556, -0.8315), float2(0.8315, -0.5556),
        float2(0.9808, -0.1951)
    };

    // Gathers each neighbour that is blurred widely enough to reach this pixel. Something behind this pixel can't
    // spread further over it than this pixel's own blur (a sharp face keeps a sharp outline against the blurred room),
    // while a blurred thing in front spreads over whatever is behind it. Alpha = how far blurred things in front
    // spread over this pixel, so the final pass blurs there even though this pixel itself is in focus.
    half4 FragBokeh(v2f i) : SV_Target
    {
        half4 center = tex2D(_MainTex, i.uv);
        float pixels = _DofParams.y * _MainTex_TexelSize.w;
        float2 radius = float2(_DofParams.y * _MainTex_TexelSize.w * _MainTex_TexelSize.x, _DofParams.y);
        half3 sum = 0;
        half total = 0, nearSpread = max(-center.a, 0.0);
        for (int k = 0; k < BOKEH_TAPS; k++)
        {
            half4 s = tex2D(_MainTex, i.uv + Bokeh[k] * radius);
            half coc = abs(s.a);
            coc = s.a > center.a ? min(coc, abs(center.a)) : coc;
            half w = saturate(coc * pixels - length(Bokeh[k]) * pixels + 1.0);
            sum += s.rgb * w;
            total += w;
            nearSpread = max(nearSpread, w * max(-s.a, 0.0));
        }
        return half4(sum / max(total, 1e-4), nearSpread);
    }

    half4 FragTent(v2f i) : SV_Target
    {
        float4 d = _MainTex_TexelSize.xyxy * float4(-0.5, -0.5, 0.5, 0.5);
        half4 s = tex2D(_MainTex, i.uv + d.xy) + tex2D(_MainTex, i.uv + d.zy);
        s += tex2D(_MainTex, i.uv + d.xw) + tex2D(_MainTex, i.uv + d.zw);
        return s * 0.25;
    }

    // Narkowicz's fit of the ACES filmic curve.
    half3 Aces(half3 x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    // The colour of a film print: shadows a little cool, highlights warm, a gentle S-curve for deeper blacks and
    // brighter highlights, and slightly richer colour. _Grade blends it in.
    half3 Grade(half3 g)
    {
        const half3 luma = half3(0.2126, 0.7152, 0.0722);
        half l = dot(g, luma);
        half3 graded = g * lerp(half3(0.92, 0.98, 1.08), half3(1.06, 1.0, 0.9), smoothstep(0.05, 0.7, l));
        graded = saturate(graded);
        graded = lerp(graded, graded * graded * (3.0 - 2.0 * graded), 0.22);
        graded = lerp(dot(graded, luma).xxx, graded, 1.1);
        return lerp(g, saturate(graded), _Grade);
    }

    // A hash without sin(), so the grain doesn't turn into patterns on phone GPUs.
    float Hash(float2 p)
    {
        float3 p3 = frac(p.xyx * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return frac((p3.x + p3.y) * p3.z);
    }

    // The frame at uv: the sharp one, or (by blend) the depth of field's blurred one.
    half3 Frame(float2 uv, half blend)
    {
        half3 c = tex2D(_MainTex, uv).rgb;
    #if DEPTH_OF_FIELD
        c = lerp(c, tex2D(_DofTex, uv).rgb, blend);
    #endif
        return c;
    }

    half4 FragFinal(v2f i) : SV_Target
    {
        float2 v = i.uv - 0.5;
        half blend = 0;
    #if DEPTH_OF_FIELD
        // Blur is measured in pixels of this frame: under a pixel's width it's sharp, from three it's fully blurred.
        half spread = max(abs(CircleOfConfusion(i.uvDepth)), tex2D(_DofTex, i.uv).a) * _DofParams.y * abs(_MainTex_TexelSize.w);
        blend = saturate((spread - 1.0) * 0.5);
    #endif
    #if LENS
        // Red and blue focus very slightly apart towards the edges of the frame, as through real glass.
        float2 shift = v * dot(v, v) * _Aberration;
        half3 c = half3(Frame(i.uv + shift, blend).r, Frame(i.uv, blend).g, Frame(i.uv - shift, blend).b);
    #else
        half3 c = Frame(i.uv, blend);
    #endif
        c = max(c, 0.0);
        half3 b = max(tex2D(_Bloom, i.uv).rgb, 0.0);
        // The project works in gamma space: tone map in linear light, then go back.
        half3 lin = pow(c, 2.2) + pow(b, 2.2) * _BloomStrength;
    #if LENS
        lin += pow(max(tex2D(_Streak, i.uv).rgb, 0.0), 2.2) * _StreakTint.rgb * _StreakStrength;
    #endif
        half3 g = pow(Aces(lin * _Exposure), 1.0 / 2.2);
        g = Grade(g);
        g *= 1.0 - _Vignette * smoothstep(0.1, 0.5, dot(v, v));

        float2 p = i.uv * abs(_MainTex_TexelSize.zw);
        // Film grain: new every frame, strongest in the mid-tones where film shows it most.
        half grain = Hash(p + _GrainSeed * float2(113.7, 271.3)) + Hash(p.yx * 1.37 + _GrainSeed * float2(57.1, 19.9)) - 1.0;
        half l = dot(g, half3(0.2126, 0.7152, 0.0722));
        g += grain * _Grain * (0.25 + 3.0 * l * (1.0 - l));
        float noise = frac(52.9829189 * frac(dot(p, float2(0.06711056, 0.00583715))));
        g += (noise - 0.5) / 255.0;
        return half4(g, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragFinal
            #pragma target 3.0
            #pragma multi_compile_local __ DEPTH_OF_FIELD
            #pragma multi_compile_local __ LENS
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragStreakDown
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragStreakUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragFocus
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragDofPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragBokeh
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragTent
            ENDCG }
    }
}

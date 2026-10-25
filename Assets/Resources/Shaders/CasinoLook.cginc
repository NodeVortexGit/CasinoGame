// Shared by the casino's shaders: the room's reflection cubemap and the haze, both set globally by the world for the
// chosen graphics quality (CasinoWorld.Look.cs).
#ifndef CASINO_LOOK_INCLUDED
#define CASINO_LOOK_INCLUDED

samplerCUBE _ReflCube;
float4 _ReflBoxMin, _ReflBoxMax, _ReflPos;
half _ReflBlur;            // blur (mip level) for a completely matte surface
half4 _HazeColor;
half _HazeDensity;         // 0 = no haze
half _GlowDim;             // how much the painted glows are turned down (where real bloom takes over)

half CasinoHaze(float3 world)
{
    return 1.0 - exp(-distance(world, _WorldSpaceCameraPos) * _HazeDensity);
}

half3 CasinoApplyHaze(half3 c, half haze)
{
    return lerp(c, _HazeColor.rgb, haze);
}

// What a surface at `world` sees in direction `r`: the cubemap is box-projected onto the room so reflections line up
// with the walls instead of floating at infinity, and blurred for less polished surfaces.
half3 CasinoReflection(float3 r, float3 world, half gloss)
{
    float3 d = normalize(r);
    float3 t = (lerp(_ReflBoxMin.xyz, _ReflBoxMax.xyz, step(0.0, d)) - world) / d;
    float hit = max(0.0, min(min(t.x, t.y), t.z));
    return texCUBElod(_ReflCube, float4(world + d * hit - _ReflPos.xyz, (1.0 - gloss) * _ReflBlur)).rgb;
}

#endif

#ifndef COMMON_INPUT
#define COMMON_INPUT


struct RenderPatch
{
    float2 position;
    float2 minmax;
    uint lod;
};
struct Bounds
{
    float3 minPosition;
    float3 maxPosition;
};


#endif
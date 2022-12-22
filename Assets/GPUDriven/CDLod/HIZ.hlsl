#ifndef HIZ
#define HIZ
#include "CommonInput.hlsl"
uniform float4x4 _HizCameraMatrixVP;
Texture2D<float4> _HizMapTexture;
uniform float4 _HizMapParam;

float SampleLevel(float2 uv, float mip, float texSize)
{
    uint2 coord = floor(uv * texSize / pow(2, mip));
    return _HizMapTexture.mips[mip][coord].r;
}

float SampleLevelNeighbour(float2 minuv, float2 maxuv, float mip, float texSize)
{
    float mipTexSize = round(texSize / pow(2,mip));
    
    uint2 minCoord = clamp(floor(minuv * mipTexSize), 0, mipTexSize - 1);
    uint2 maxCoord = clamp(floor(maxuv * mipTexSize), 0, mipTexSize - 1);
    float d1 = _HizMapTexture.mips[mip][uint2(minCoord.x, minCoord.y)].r;
    float d2 = _HizMapTexture.mips[mip][uint2(minCoord.x, maxCoord.y)].r;
    float d3 = _HizMapTexture.mips[mip][uint2(maxCoord.x, minCoord.y)].r;
    float d4 = _HizMapTexture.mips[mip][uint2(maxCoord.x, maxCoord.y)].r;
    #if _REVERSED_Z
    float d = min(min(d1, d2), min(d3, d4));
    #else
    float d = max(max(d1, d2), max(d3, d4));
    #endif
    return d;
}

float3 GetClipUVD(float4x4 localToClipMat, float4 localPosition)
{
    float4 p = mul(localToClipMat, localPosition);
    p = p / p.w;
    p.xy = (p.xy + 1) / 2.0;
    if(p.z < 0){
        #if _REVERSED_Z
        p.z = 1;
        #else
        p.z = 0;
        #endif
    }
    return p;
}

Bounds GetClipBounds(Bounds worldBounds)
{
    float3 minp = worldBounds.minPosition;
    float3 maxp = worldBounds.maxPosition;
    float4x4 localToClipMat = _HizCameraMatrixVP;
    float3 p1 = GetClipUVD(localToClipMat, float4(minp.x, minp.y, minp.z, 1));
    float3 p2 = GetClipUVD(localToClipMat, float4(minp.x, maxp.y, minp.z, 1));
    float3 p3 = GetClipUVD(localToClipMat, float4(minp.x, maxp.y, maxp.z, 1));
    float3 p4 = GetClipUVD(localToClipMat, float4(minp.x, minp.y, maxp.z, 1));
    float3 p5 = GetClipUVD(localToClipMat, float4(maxp.x, minp.y, minp.z, 1));
    float3 p6 = GetClipUVD(localToClipMat, float4(maxp.x, maxp.y, minp.z, 1));
    float3 p7 = GetClipUVD(localToClipMat, float4(maxp.x, maxp.y, maxp.z, 1));
    float3 p8 = GetClipUVD(localToClipMat, float4(maxp.x, minp.y, maxp.z, 1));
    float3 m1 = min(min(p1, p2), min(p3, p4));
    float3 m2 = min(min(p5, p6), min(p7, p8));
    minp = min(m1, m2);
    m1 = max(max(p1, p2), max(p3, p4));
    m2 = max(max(p5, p6), max(p7, p8));
    maxp = max(m1, m2);
    Bounds nBounds;
    nBounds.minPosition = minp;
    nBounds.maxPosition = maxp;
    return nBounds;
}

bool OcclusionCulling(Bounds worldBounds)
{
    //根据bounds计算lod
    Bounds clipBounds = GetClipBounds(worldBounds);
    float3 minp = clipBounds.minPosition;
    float3 maxp = clipBounds.maxPosition;
    float2 uvoffset = maxp.xy - minp.xy;
    float distance = max(uvoffset.x * _HizMapParam.x, uvoffset.y * _HizMapParam.y);
    uint mip = clamp(ceil(log2(distance)), 0, _HizMapParam.z - 1);
    float d = SampleLevelNeighbour(minp, maxp, mip, _HizMapParam.x);
    
    #if _REVERSED_Z
    float z = maxp.z;
    if (d > z)
        return false;
    #else
    float z = minp.z;
    if (d < z)
        return false;
    #endif
    return true;
}

#endif
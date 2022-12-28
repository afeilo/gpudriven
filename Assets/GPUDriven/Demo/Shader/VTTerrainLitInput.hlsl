#ifndef UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED
#define UNIVERSAL_TERRAIN_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _BaseColor;
    half _Cutoff;
CBUFFER_END

#define _Surface 0.0 // Terrain is always opaque

CBUFFER_START(_Terrain)
    half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
    half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
    half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
    half4 _DiffuseRemapScale0, _DiffuseRemapScale1, _DiffuseRemapScale2, _DiffuseRemapScale3;
    half4 _MaskMapRemapOffset0, _MaskMapRemapOffset1, _MaskMapRemapOffset2, _MaskMapRemapOffset3;
    half4 _MaskMapRemapScale0, _MaskMapRemapScale1, _MaskMapRemapScale2, _MaskMapRemapScale3;

    float4 _Control_ST;
    float4 _Control_TexelSize;
    half _DiffuseHasAlpha0, _DiffuseHasAlpha1, _DiffuseHasAlpha2, _DiffuseHasAlpha3;
    half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
    half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
    half _HeightTransition;
    half _NumLayersCount;

    #ifdef UNITY_INSTANCING_ENABLED
    float4 _TerrainHeightmapRecipSize;   // float4(1.0f/width, 1.0f/height, 1.0f/(width-1), 1.0f/(height-1))
    float4 _TerrainHeightmapScale;       // float4(hmScale.x, hmScale.y / (float)(kMaxHeight), hmScale.z, 0.0f)
    #endif
    #ifdef SCENESELECTIONPASS
    int _ObjectId;
    int _PassValue;
    #endif
CBUFFER_END


TEXTURE2D(_Control);    SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);     SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);
TEXTURE2D(_Splat2);
TEXTURE2D(_Splat3);

#ifdef _NORMALMAP
TEXTURE2D(_Normal0);     SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);
TEXTURE2D(_Normal2);
TEXTURE2D(_Normal3);
#endif

#ifdef _MASKMAP
TEXTURE2D(_Mask0);      SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1);
TEXTURE2D(_Mask2);
TEXTURE2D(_Mask3);
#endif

TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
TEXTURE2D(_SpecGlossMap);  SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_MetallicTex);   SAMPLER(sampler_MetallicTex);

#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
#define ENABLE_TERRAIN_PERPIXEL_NORMAL
#endif

// #ifdef UNITY_INSTANCING_ENABLED
// TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
SAMPLER(sampler_TerrainNormalmapTexture);
// #endif

UNITY_INSTANCING_BUFFER_START(Terrain)
UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)  // float4(xBase, yBase, skipScale, ~)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _ALPHATEST_ON
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);

void ClipHoles(float2 uv)
{
    float hole = SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
    clip(hole == 0.0f ? -1 : 1);
}
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    specGloss = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, uv);
    specGloss.a = albedoAlpha;
    return specGloss;
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedoSmoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    outSurfaceData.alpha = 1;

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoSmoothness.a);
    outSurfaceData.albedo = albedoSmoothness.rgb;

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1;
    outSurfaceData.emission = 0;
}



Texture2D _VTHeightTiledTex;
Texture2D _VTNormaltTiledTex;
Texture2D _LookupTex;
float4 _VTSplatTiledTex_TexelSize;
float4 _LookupParam;
float _HeightTileSize;
float _SplatTileSize;

TEXTURE2D(_VTSplatTiledTex); 
SAMPLER( sampler_VTSplatTiledTex);
TEXTURE2D(_VTBakeNormalTex); 
SAMPLER( sampler_VTBakeNormalTex);
TEXTURE2D(_VTBakeDiffuseTex); 
SAMPLER( sampler_VTBakeDiffuseTex);

StructuredBuffer<RenderPatch> PatchList;

float3 _CameraPosition;
float _MeshResolution;
float _UVResolution;
float _LerpValue;
int _LerpRange;

float _Metallic;
float _Smoothness;

// morphs vertex xy from from high to low detailed mesh position
float4 morphVertex(float4 inPos, float4 vertex, float morphLerpK, float2 g_quadScale)
{
    float2 fracPart = (frac(inPos.xz * 0.5) * 2) * g_quadScale.xy;
    vertex.xz -= fracPart * morphLerpK;
    return vertex;
}

half revertLerp(half min, half max, half value)
{
    if (value <= min)
        return 0;
    if (value >= max)
        return 1;
    return (value - min) / (max - min);
    // return 0.5f;
}

float2 TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 texcoord, uint instanceID : SV_InstanceID)
{
    RenderPatch p = PatchList[instanceID];
    float4 vertex = positionOS;
    float scale = (1 << p.lod);
    vertex *= scale;
    vertex = vertex + float4(p.position.x, 0, p.position.y, 0);
    //过渡部分
    float3 cameraWS = _CameraPosition; //GetCameraPositionWS();
    half d = length(half3(vertex.x - cameraWS.x, 0, vertex.z - cameraWS.z));
    half maxDistance = _MeshResolution * scale * _LerpRange;
    half minDistance = maxDistance / 2;
    half morphLerpK = revertLerp((maxDistance - minDistance) * _LerpValue + minDistance, maxDistance, d);
    vertex = morphVertex(positionOS, vertex, morphLerpK, float2(scale, scale));

    float4 node_os = _LookupTex.Load(int3(floor(p.position.xy / _LookupParam.w), 0));
    int pageX = floor(node_os.w / _LookupParam.x);
    int pageZ = floor(node_os.w % _LookupParam.x);
    int tileSize = _HeightTileSize - 1;
    int scaleTileSize = (tileSize * node_os.z);
    // vertex.xz -= fracPart * morphLerpK;
    float2 uv = texcoord - frac(texcoord * _UVResolution * 0.5) * morphLerpK / _UVResolution * 2;
    float heightPageOffsetX = pageX * (_HeightTileSize + _LookupParam.z) + tileSize * node_os.x + scaleTileSize * uv.x;
    float heightPageOffsetZ = pageZ * (_HeightTileSize + _LookupParam.z) + tileSize * node_os.y + scaleTileSize * uv.y;
    float height = UnpackHeightmap(_VTHeightTiledTex.Load(int3(heightPageOffsetX, heightPageOffsetZ, 0)));
    // float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));
    // positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
    // positionOS.y = height * _TerrainHeightmapScale.y;
    vertex.y = height * 1000;
    
    positionOS.xyz = vertex.xyz;
    texcoord.xy = vertex.xz / 2048;
    normal = _VTNormaltTiledTex.Load(int3(heightPageOffsetX, heightPageOffsetZ, 0)).rgb * 2 - 1;

    float2 splatUV = (tileSize * node_os + scaleTileSize * uv) / _SplatTileSize;
    splatUV = splatUV * (_SplatTileSize - 1.0) + 0.5f;
    float splatUVOffsetX = pageX * (_SplatTileSize + _LookupParam.z);;
    float splatUVOffsetZ = pageZ * (_SplatTileSize + _LookupParam.z);
    splatUV += float2(splatUVOffsetX, splatUVOffsetZ);
    splatUV *= _VTSplatTiledTex_TexelSize.xy;
    return splatUV;
}


float2 CaclSplatUV(float2 uv, uint instanceID)
{
    RenderPatch p = PatchList[instanceID];
    
    float4 node_os = _LookupTex.Load(int3(floor(p.position.xy / _LookupParam.w), 0));
    int pageX = floor(node_os.w / _LookupParam.x);
    int pageZ = floor(node_os.w % _LookupParam.x);
    int tileSize = _HeightTileSize - 1;
    int scaleTileSize = (tileSize * node_os.z);
    float2 splatUV = (tileSize * node_os + scaleTileSize * uv) / _SplatTileSize;
    splatUV = splatUV * (_SplatTileSize - 1.0) + 0.5f;
    float splatUVOffsetX = pageX * (_SplatTileSize + _LookupParam.z);;
    float splatUVOffsetZ = pageZ * (_SplatTileSize + _LookupParam.z);
    splatUV += float2(splatUVOffsetX, splatUVOffsetZ);
    splatUV *= _VTSplatTiledTex_TexelSize.xy;
    return splatUV;
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 texcoord)
{
#ifdef UNITY_INSTANCING_ENABLED
    float2 patchVertex = positionOS.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z; // (xy + float2(xBase,yBase)) * skipScale
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
    positionOS.y = height * _TerrainHeightmapScale.y;

#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
    normal = float3(0, 1, 0);
#else
    normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
#endif
    uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
#endif
}
void TerrainInstancing(inout float4 positionOS, inout float3 normal)
{
    float2 uv = { 0, 0 };
    // TerrainInstancing(positionOS, normal, uv);
}
#endif

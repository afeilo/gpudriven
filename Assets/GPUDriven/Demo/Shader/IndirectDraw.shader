// Upgrade NOTE: commented out 'float3 _WorldSpaceCameraPos', a built-in variable

Shader "Unlit/IndirectDraw"
{
    Properties
    {
        _TerrainHeightmapTexture ("Texture", 2D) = "white" {}
        _NormalMap ("Texture", 2D) = "white" {}
        _Metallic ("metallic", range(0,1)) = 1
        _Smoothness ("smoothness", range(0,1)) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #include "../../CDLod/CommonInput.hlsl"
            // make fog work
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 shadowCoord : TEXCOORD1;
                float4 vertex : SV_POSITION;

                float3 normal : TEXCOORD3;
                float3 viewDir : TEXCOORD4;
                half3 vertexSH : TEXCOORD5; // SH
                half4 fogFactorAndVertexLight : TEXCOORD6; // x: fogFactor, yzw: vertex light
                float3 positionWS : TEXCOORD7;
                float4 color : TEXCOORD8;
            };

            Texture2D _VTTiledTex;
            SAMPLER(sampler_VTTiledTex);
            Texture2D _LookupTex;
            Texture2D _NormalMap;
            SAMPLER(sampler_NormalMap);
            float4 _NormalMap_TexelSize;
            float4 _LookupParam;


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

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                RenderPatch p = PatchList[instanceID];

                float4 vertex = v.vertex;
                float scale = (1 << p.lod);
                vertex *= scale;
                vertex = vertex + float4(p.position.x, 0, p.position.y, 0);

                //过渡部分
                float3 cameraWS = _CameraPosition; //GetCameraPositionWS();
                half d = length(half3(vertex.x - cameraWS.x, 0, vertex.z - cameraWS.z));
                half maxDistance = _MeshResolution * scale * _LerpRange;
                half minDistance = maxDistance / 2;
                half morphLerpK = revertLerp((maxDistance - minDistance) * _LerpValue + minDistance, maxDistance, d);
                vertex = morphVertex((v.vertex), vertex, morphLerpK, float2(scale, scale));

                #if _MAIN_LIGHT_SHADOWS
                o.shadowCoord = TransformWorldToShadowCoord(vertex.xyz);
                #endif


                // float4 node_os = _LookupTex.Load(int3(0,0, 0));
                float4 node_os = _LookupTex.Load(int3(floor(p.position.xy / _LookupParam.w), 0));
                int pageX = floor(node_os.w / _LookupParam.x);
                int pageZ = floor(node_os.w % _LookupParam.x);
                int tileSize = _LookupParam.y - 1;
                int scaleTileSize = (tileSize * node_os.z);

                // vertex.xz -= fracPart * morphLerpK;
                
                float2 uv = v.uv - frac(v.uv * _UVResolution * 0.5) * morphLerpK / _UVResolution * 2;
                float pageOffsetX = pageX * _LookupParam.z + tileSize * node_os.x + scaleTileSize * uv.x;
                float pageOffsetZ = pageZ * _LookupParam.z + tileSize * node_os.y + scaleTileSize * uv.y;
                
                float height = _VTTiledTex.Load(int3(pageOffsetX, pageOffsetZ, 0));

                // float height = SAMPLE_TEXTURE2D_LOD(_VTTiledTex, sampler_VTTiledTex, float2(pageOffsetX, pageOffsetZ) / 1566.0 , 0);
                vertex.y = height * 500;
                // if (d <= minDistance || d >= maxDistance)
                // {
                //     o.color = float4(1,0,0,1);
                // }else
                // {
                //     o.color = float4(0,0,0,1);
                // }

                float2 fracPart = (frac(v.vertex.xz * 0.5) * 2) * float2(scale, scale);
                // vertex.xz -= fracPart * morphLerpK;
                // frac(v.uv * _UVResolution * 0.5)

                 o.color = float4(height ,0,0,1);
                
                o.vertex = TransformObjectToHClip(vertex.xyz);

                ///shading
                half3 viewDirWS = cameraWS - vertex;
                o.normal = TransformObjectToWorldNormal(v.normalOS);
                o.viewDir = viewDirWS;
                o.vertexSH = SampleSH(o.normal);
                o.positionWS = vertex;

                o.fogFactorAndVertexLight.x = ComputeFogFactor(o.vertex.z);
                o.fogFactorAndVertexLight.yzw = VertexLighting(o.positionWS, o.normal.xyz);
                return o;
            }


            void InitializeInputData(v2f IN, out InputData input)
            {
                input = (InputData)0;

                half3 SH = half3(0, 0, 0);


                input.positionWS = IN.positionWS;
                float2 uv = float2(IN.positionWS.x / _NormalMap_TexelSize.z, IN.positionWS.z / _NormalMap_TexelSize.w);
                input.normalWS = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
                SH = IN.vertexSH;


                input.normalWS = SafeNormalize(input.normalWS);
                input.viewDirectionWS = SafeNormalize(IN.viewDir);

                #if defined(_MAIN_LIGHT_SHADOWS)
				input.shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
				input.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                input.shadowCoord = float4(0, 0, 0, 0);
                #endif

                input.fogCoord = IN.fogFactorAndVertexLight.x;
                input.vertexLighting = IN.fogFactorAndVertexLight.yzw;

                //NOTE SAMPLE_GI 会调用SampleSHPixel函数，这个函数有问题，手机上使用的是EVALUATE_SH_MIXED宏
                //pc使用的是EVALUATE_SH_VERTEX
                #if defined(LIGHTMAP_ON)
				input.bakedGI = SAMPLE_GI(IN.uvMainAndLM.zw, SH, input.normalWS);
                #else
                input.bakedGI = SH;
                #endif
            }

            half4 frag(v2f i) : SV_Target
            {
                return i.color;
                float c = 0.5f; //i.uv.x / 6;
                #if _MAIN_LIGHT_SHADOWS
                // Light mainLight = GetMainLight(i.shadowCoord);
                // c *= mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                #endif

                InputData inputData;
                InitializeInputData(i, inputData);
                float metallic = _Metallic;
                float smoothness = _Smoothness;
                half4 color = UniversalFragmentPBR(inputData, c, metallic, /* specular */ half3(0.0h, 0.0h, 0.0h),
                                                   smoothness, 0, /* emission */ half3(0, 0, 0), 1);

                return color;
            }
            ENDHLSL
        }


        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma target 2.0


            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            Texture2D _TerrainHeightmapTexture;
            float4 _MainTex_ST;

            struct RenderPatch
            {
                float2 position;
                uint lod;
            };

            StructuredBuffer<RenderPatch> PatchList;

            float3 _CameraPosition;
            float _MeshResolution;
            float _LerpValue;

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


            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                RenderPatch p = PatchList[instanceID];

                float4 vertex = v.vertex;
                float scale = (1 << p.lod);
                vertex *= scale;
                vertex = vertex + float4(p.position.x, 0, p.position.y, 0);

                //过渡部分
                float3 cameraWS = _CameraPosition; //GetCameraPositionWS();
                half d = length(half3(vertex.x - cameraWS.x, vertex.y - cameraWS.y, vertex.z - cameraWS.z));
                half resolution = _MeshResolution * scale;
                half morphLerpK = revertLerp(resolution * _LerpValue, resolution, d - resolution);
                vertex = morphVertex((v.vertex), vertex, morphLerpK, float2(scale, scale));


                float2 sampleCoords = vertex.xz;
                float height = _TerrainHeightmapTexture.Load(int3(sampleCoords, 0));
                vertex.y = height * 50;
                o.vertex = UnityObjectToClipPos(vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
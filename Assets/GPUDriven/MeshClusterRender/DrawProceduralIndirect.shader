Shader "DrawProceduralIndirect/Basic"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex v2f
            #pragma fragment frag

            StructuredBuffer<float3> VertexDataArray;
            StructuredBuffer<int> instanceBuffer;

            // Structs
            struct V2F_Input
            {
                uint vertexID : SV_VertexID;
            };

            struct V2F_Output
            {
                float4 positionCS : SV_POSITION;
                float4 color : TEXCOORD0;
            };

            uniform float4x4 _MatrixM;
            // Vertex Shader
            V2F_Output v2f(uint vertexID : SV_VertexID, uint instance_id: SV_InstanceID)
            {
                V2F_Output OUT;
                float3 pos = VertexDataArray[instanceBuffer[instance_id] * 64 + vertexID];
                OUT.positionCS = mul(mul(UNITY_MATRIX_VP, _MatrixM), float4(pos, 1));
                instance_id = instanceBuffer[instance_id] + 1;
                OUT.color = float4((instance_id % 10) / 10.0f, (instance_id % 5) / 5.0f, (instance_id % 3) / 3.0f,
                                   1);
                return OUT;
            }

            // Fragment Shader
            half4 frag(V2F_Output IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshClusterRenderer : MonoBehaviour
{

    public MeshClusterData Data;

    public ComputeShader ComputeShader;

    public Shader shader;

    private Material _material;
    
    uint[] args = new uint[5] {0, 0, 0, 0, 0};
    ComputeBuffer argsBuffer;
    private int kernelId;
    ComputeBuffer cullResult;

    private ComputeBuffer clusterBoundsBuffer;
    
    private GraphicsBuffer mIndexBuffer;
    // Start is called before the first frame update
    void Start()
    {
        _material = new Material(shader);
        
        var mVertexBuffer = new ComputeBuffer(Data.vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        mVertexBuffer.SetData(Data.vertices);
        mIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, Data.indices.Length, sizeof(int));
        mIndexBuffer.SetData(Data.indices);
        _material.SetBuffer("VertexDataArray", mVertexBuffer);
        clusterBoundsBuffer = new ComputeBuffer(Data.clusterBounds.Length, sizeof(float) * 6, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        clusterBoundsBuffer.SetData(Data.clusterBounds);
        
        cullResult = new ComputeBuffer(Data.clusterBounds.Length, sizeof(uint), ComputeBufferType.Append);
        // index-0 : 每个实例有多少个顶点
        // index-1 : 有多少个实例
        // index-2 : start vertex location
        // index-3 : start instance location
        args[0] = (uint) 96;
        args[1] = 0;
        args[2] = 0;
        args[3] = 0;
        argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
        kernelId = 0;
        if(SystemInfo.usesReversedZBuffer){
            ComputeShader.EnableKeyword("_REVERSED_Z");
        }else{
            ComputeShader.DisableKeyword("_REVERSED_Z");
        }
    }

    private Plane[] _Planes = new Plane[6];
    private Vector4[] _PlaneVector = new Vector4[6];
    private CommandBuffer cmd;
    // Update is called once per frame
    void Update()
    {
        
        var camera = Camera.main;
        
        GeometryUtility.CalculateFrustumPlanes(Camera.main.cullingMatrix, _Planes);
        for (int i = 0; i < _Planes.Length; i++)
        {
            _PlaneVector[i] = new Vector4(_Planes[i].normal.x, _Planes[i].normal.y, _Planes[i].normal.z, _Planes[i].distance);
        }

        
        Shader.SetGlobalMatrix("_MatrixM", transform.localToWorldMatrix);
        cmd = CommandBufferPool.Get("HIZRenderPass");

        argsBuffer.SetData(args);
        cmd.SetComputeVectorArrayParam(ComputeShader, "_Planes", _PlaneVector);
        cmd.SetComputeIntParam(ComputeShader, "instanceCount", Data.clusterBounds.Length);
        cmd.SetComputeBufferParam(ComputeShader, kernelId, "clusterBounds", clusterBoundsBuffer);
        cullResult.SetCounterValue(0);
        cmd.SetComputeBufferParam(ComputeShader, kernelId, "instanceBuffer", cullResult);
        cmd.SetComputeBufferParam(ComputeShader, kernelId, "argsBuffer", argsBuffer);
        cmd.DispatchCompute(ComputeShader, kernelId, Data.clusterBounds.Length / 640 + 1, 1, 1);
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
        // LogPatchArgs(argsBuffer, 5, 1);
        // mat.SetBuffer("positionBuffer", cullResult);
        _material.SetBuffer("instanceBuffer",cullResult);
        var mBounds = new Bounds(transform.position, new Vector3(100, 100, 100)); 
        // Graphics.DrawProceduralIndirect(_material, mBounds, MeshTopology.Quads, argsBuffer);
        Graphics.DrawProceduralIndirect(_material, mBounds, MeshTopology.Triangles, mIndexBuffer, argsBuffer);
        // Graphics.DrawProcedural(_material, mBounds, MeshTopology.Triangles, mIndexBuffer, 96, Data.clusterBounds.Length);
        // Graphics.DrawProcedural(_material, mBounds, MeshTopology.Quads, 64, Data.clusterBounds.Length);
    }
    
    
    private void LogPatchArgs(ComputeBuffer buffer, int count, int index, string args = ""){
        var data = new uint[count];
        buffer.GetData(data);
        Debug.Log(args + data[index]);
    }
}

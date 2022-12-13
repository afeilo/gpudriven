

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 四叉树
/// </summary>
public class QuadTreeBuilder
{

    public ComputeBuffer IndirectArgsBuffer1
    {
        get
        {
            return _IndirectArgsBuffer1;
        }
    }
    
    public ComputeBuffer IndirectArgsBuffer2
    {
        get
        {
            return _IndirectArgsBuffer2;
        }
    }
    
    public ComputeBuffer FinalNodeList1
    {
        get
        {
            return _FinalNodeList1;
        }
    }
    
    public ComputeBuffer FinalNodeList2
    {
        get
        {
            return _FinalNodeList2;
        }
    }
    
    public RenderPatch[] FinalPatch1
    {
        get
        {
            return _FinalPatch1;
        }
    }
    
    public RenderPatch[] FinalPatch2
    {
        get
        {
            return _FinalPatch2;
        }
    }

    private int _MaxLodLevel;

    private int _kernelIndex = 0;
    
    private ComputeShader _ComputeShader;

    private CommandBuffer _commandBuffer = new CommandBuffer();

    private ComputeBuffer _TopNodeBuffer;
    private ComputeBuffer _TempNodeList1;
    private ComputeBuffer _TempNodeList2;
    private ComputeBuffer _FinalNodeList1;
    private ComputeBuffer _FinalNodeList2;
    private ComputeBuffer _IndirectArgsBuffer;
    private ComputeBuffer _IndirectArgsBuffer1;
    private ComputeBuffer _IndirectArgsBuffer2;
    private RenderPatch[] _FinalPatch1;
    private RenderPatch[] _FinalPatch2;
    public QuadTreeBuilder(ComputeShader computeShader, QuadTreeData config)
    {
        _ComputeShader = computeShader;
        Create(config);
        var topNodeCount = _TopNodeBuffer.count;
        
        var maxNodeCount = topNodeCount * (int)Mathf.Pow(4, _MaxLodLevel);
        _commandBuffer.name = "QuadTreeSelect";
        _TempNodeList1 = new ComputeBuffer(topNodeCount * maxNodeCount, 8, ComputeBufferType.Append);
        _TempNodeList2 = new ComputeBuffer(topNodeCount * maxNodeCount, 8, ComputeBufferType.Append);
        _FinalNodeList1 = new ComputeBuffer(topNodeCount * maxNodeCount, 12, ComputeBufferType.Append);
        _FinalNodeList2 = new ComputeBuffer(topNodeCount * maxNodeCount, 12, ComputeBufferType.Append);
        _IndirectArgsBuffer = new ComputeBuffer(3, 4, ComputeBufferType.IndirectArguments);
        _IndirectArgsBuffer.SetData(new uint[]{1,1,1});
        _IndirectArgsBuffer1 = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        _IndirectArgsBuffer2 = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
        _ComputeShader.SetBuffer(_kernelIndex, ShaderConstants.FinalNodeList1, _FinalNodeList1);
        _ComputeShader.SetBuffer(_kernelIndex, ShaderConstants.FinalNodeList2, _FinalNodeList2);
        
    }
    
    void Create(QuadTreeData config)
    {
        int lengthX = 1 << (config.mapLevel.x - config.startLevel);
        int lengthZ = 1 << (config.mapLevel.y - config.startLevel);
        int topSize = 1 << config.startLevel;
        uint2[] datas = new uint2[lengthX * lengthZ];
        for (uint i = 0; i < lengthX; i++)
        {
            for (uint j = 0; j < lengthZ; j++)
            {
                datas[i * lengthZ + j] = new uint2(i, j);
            }
        }

        _MaxLodLevel = config.startLevel - config.endLevel;
        _ComputeShader.SetInt(ShaderConstants.MaxLodLevel, _MaxLodLevel);
        _ComputeShader.SetInt(ShaderConstants.TopGridSize, topSize);
        _ComputeShader.SetInt( ShaderConstants.LodRange, config.lodRange);
        _TopNodeBuffer = new ComputeBuffer(datas.Length, 8, ComputeBufferType.Append);
        _TopNodeBuffer.SetData(datas);
        Debug.Log(datas.Length);
    }

    private Plane[] _Planes = new Plane[6];
    private Vector4[] _PlaneVector = new Vector4[6];
    public void Select(Camera camera)
    {
        GeometryUtility.CalculateFrustumPlanes(Camera.main.cullingMatrix, _Planes);
        for (int i = 0; i < _Planes.Length; i++)
        {
            _PlaneVector[i] = new Vector4(_Planes[i].normal.x, _Planes[i].normal.y, _Planes[i].normal.z, _Planes[i].distance);
        }
        
        Vector3 cameraPosition = camera.transform.position;
        _commandBuffer.Clear();
        _commandBuffer.SetComputeVectorArrayParam(_ComputeShader, ShaderConstants.Planes, _PlaneVector);
        _commandBuffer.SetComputeVectorParam(_ComputeShader, ShaderConstants.CameraPositionWS, cameraPosition);
        _commandBuffer.SetBufferCounterValue(_TempNodeList1, 0);
        _commandBuffer.SetBufferCounterValue(_TempNodeList2, 0);
        _commandBuffer.SetBufferCounterValue(_FinalNodeList1, 0);
        _commandBuffer.SetBufferCounterValue(_FinalNodeList2, 0);
        _commandBuffer.SetBufferCounterValue(_TopNodeBuffer,(uint)_TopNodeBuffer.count);
        ComputeBuffer _ConsumeNodeList = _TopNodeBuffer;
        ComputeBuffer _TempNodeList = _TempNodeList2;
        _commandBuffer.CopyCounterValue(_TopNodeBuffer, _IndirectArgsBuffer, 0);
        for (int i = _MaxLodLevel; i >= 0; i--)
        {
            _commandBuffer.SetComputeIntParam(_ComputeShader, ShaderConstants.CurLod, i);
            _commandBuffer.SetComputeBufferParam(_ComputeShader, _kernelIndex, ShaderConstants.ConsumeNodeList, _ConsumeNodeList);
            _commandBuffer.SetComputeBufferParam(_ComputeShader, _kernelIndex, ShaderConstants.TempNodeList, _TempNodeList);
            _commandBuffer.DispatchCompute(_ComputeShader, _kernelIndex, _IndirectArgsBuffer, 0);
            _commandBuffer.CopyCounterValue(_TempNodeList, _IndirectArgsBuffer, 0);
            var temp = _TempNodeList == _TempNodeList1 ? _TempNodeList2 : _TempNodeList1;
            _ConsumeNodeList = _TempNodeList;
            _TempNodeList = temp;
        }
        _commandBuffer.CopyCounterValue(_FinalNodeList1, _IndirectArgsBuffer1, 4);
        _commandBuffer.CopyCounterValue(_FinalNodeList2, _IndirectArgsBuffer2, 4);
        Graphics.ExecuteCommandBuffer(_commandBuffer);
        
        var data = new uint[5];
        _IndirectArgsBuffer1.GetData(data);
        var finalCount1 = data[1];
        _IndirectArgsBuffer2.GetData(data);
        var finalCount2 = data[1];
        _FinalPatch1 = new RenderPatch[finalCount1];
        _FinalPatch2 = new RenderPatch[finalCount2];
        _FinalNodeList1.GetData(_FinalPatch1);
        _FinalNodeList2.GetData(_FinalPatch2);
        
        // LogPatchArgs(_IndirectArgsBuffer1, 5, 1);
        // LogPatchArgs(_IndirectArgsBuffer2, 5, 1);
        // LogPatchArgs(_IndirectArgsBuffer2, 5, 1);
        // _ComputeShader.SetBuffer();
        // LogPatchArgs(_IndirectArgsBuffer, 3, 0);
        
        
        // var data0 = new uint[5];
        // _IndirectArgsBuffer1.GetData(data0);
        // var count = data0[1];
        //
        // var data = new RenderPatch[count];
        // _FinalNodeList1.GetData(data);
        // // Debug.Log(data[index]);
        // Debug.Log(data[0].position.x + "," + data[0].position.y + "," + data[0].lod);
    }

    
    
    
    public void Dispose()
    {
        _commandBuffer.Dispose();
        _TempNodeList1.Dispose();
        _TempNodeList2.Dispose();
        _FinalNodeList1.Dispose();
        _FinalNodeList2.Dispose();
        _IndirectArgsBuffer.Dispose();
        _IndirectArgsBuffer1.Dispose();
        _IndirectArgsBuffer2.Dispose();
    }
    
    private void LogPatchArgs(ComputeBuffer buffer, int count, int index){
        var data = new uint[count];
        buffer.GetData(data);
        Debug.Log(data[index]);
    }
    private class ShaderConstants{
        public static readonly int ConsumeNodeList = Shader.PropertyToID("_ConsumeNodeList");
        public static readonly int TempNodeList = Shader.PropertyToID("_TempNodeList");
        public static readonly int FinalNodeList1 = Shader.PropertyToID("_FinalNodeList1");
        public static readonly int FinalNodeList2 = Shader.PropertyToID("_FinalNodeList2");
        public static readonly int LodRange = Shader.PropertyToID("_LodRange");
        public static readonly int CurLod = Shader.PropertyToID("_CurLod");
        public static readonly int CameraPositionWS = Shader.PropertyToID("_CameraPositionWS");
        public static readonly int TopGridSize = Shader.PropertyToID("_TopGridSize");
        public static readonly int MaxLodLevel = Shader.PropertyToID("_MaxLodLevel");
        public static readonly int Planes = Shader.PropertyToID("_Planes");
    }
    
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HIZDepthFeature : ScriptableRendererFeature
{
    public ComputeShader computeShader;
    private HizMapPass hizPass;
    public override void Create()
    {
        hizPass = new HizMapPass(computeShader);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var cameraData = renderingData.cameraData;
        if(cameraData.isSceneViewCamera || cameraData.isPreviewCamera){
            return;
        }
        if(cameraData.camera.name == "Preview Camera"){
            return;
        }
        renderer.EnqueuePass(hizPass);
    }
    
    public class HizMapPass : ScriptableRenderPass
    {
        private RenderTexture HizMap;
        private ComputeShader computeShader;


        private RenderTextureFormat format = RenderTextureFormat.RHalf;
        
        const string m_ProfilerTag = "Render HizMap";
        ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
        
        public HizMapPass(ComputeShader computeShader)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            this.computeShader = computeShader;
            if(SystemInfo.usesReversedZBuffer){
                computeShader.EnableKeyword("_REVERSED_Z");
            }else{
                computeShader.DisableKeyword("_REVERSED_Z");
            }
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (null == computeShader)
                return;
            
            //Step1:gen mipmap texture: depth map
            //Step2:blit urp depth texture to mipmap0 depth map
            //Step3:loop to render mipmap to depth map
            
            //Step1
            var size = GetPreferredSize(renderingData.cameraData.camera);
            if (HizMap == default || HizMap.width != size)
            {
                if(HizMap != default){
                    RenderTexture.ReleaseTemporary(HizMap);
                }
                var mipCount = Mathf.CeilToInt(Mathf.Log(size , 2));
                var desc = new RenderTextureDescriptor(size,size,format,0,mipCount);
                desc.autoGenerateMips = false;
                desc.useMipMap = mipCount > 1;
                HizMap = RenderTexture.GetTemporary(desc);
                HizMap.filterMode = FilterMode.Point;
                HizMap.enableRandomWrite = true;
                HizMap.Create();
            }
            
            CommandBuffer cmd = CommandBufferPool.Get("HIZMap");

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                var camera = renderingData.cameraData.camera;
                var width = HizMap.width;
                var height = HizMap.height;
                //Step2 
                GetTemporaryTexture(cmd, ShaderConstants.TempTexure1, HizMap.width, HizMap.height);
                cmd.SetComputeVectorParam(computeShader, ShaderConstants.SrcSize, new Vector4(camera.pixelWidth, camera.pixelHeight, 0, 0));
                cmd.SetComputeVectorParam(computeShader, ShaderConstants.DstSize, new Vector4(HizMap.width, HizMap.height, 0, 0));
                cmd.SetComputeTextureParam(computeShader, 0, ShaderConstants.InTexture, ShaderConstants.CameraDepthTexture);
                cmd.SetComputeTextureParam(computeShader, 0, ShaderConstants.OutTexture, ShaderConstants.TempTexure1);
                cmd.SetComputeTextureParam(computeShader, 0, ShaderConstants.HizMapTexture, HizMap, 0);
                cmd.DispatchCompute(computeShader, 0, Mathf.CeilToInt(HizMap.width / 8.0f), Mathf.CeilToInt(HizMap.height / 8.0f), 1);

                int InTexture = ShaderConstants.TempTexure1;
                int OutTexture = ShaderConstants.TempTexure2;
                int level = 1;
                
                //Step3
                while (level < HizMap.mipmapCount)
                {

                    cmd.SetComputeVectorParam(computeShader, ShaderConstants.SrcSize, new Vector4(width, height, 0, 0));
                    width = HizMap.width >> level;
                    height = HizMap.height >> level;
                    cmd.SetComputeVectorParam(computeShader, ShaderConstants.DstSize, new Vector4(width, height, 0, 0));
                    GetTemporaryTexture(cmd, OutTexture, width, height);
                    
                    cmd.SetComputeTextureParam(computeShader, 1, ShaderConstants.InTexture, InTexture);
                    cmd.SetComputeTextureParam(computeShader, 1, ShaderConstants.OutTexture, OutTexture);
                    cmd.SetComputeTextureParam(computeShader, 1, ShaderConstants.HizMapTexture, HizMap, level);
                    cmd.DispatchCompute(computeShader, 1, Mathf.CeilToInt(width / 8.0f), Mathf.CeilToInt(height / 8.0f), 1);
                    
                    cmd.ReleaseTemporaryRT(InTexture);
                    (InTexture, OutTexture) = (OutTexture, InTexture);
                    
                    level++;
                }
                Matrix4x4 projectionMatrix = renderingData.cameraData.GetGPUProjectionMatrix();
                Matrix4x4 viewMatrix = renderingData.cameraData.GetViewMatrix();
                cmd.SetGlobalVector(ShaderConstants.HizCameraPosition, camera.transform.position);
                cmd.SetGlobalMatrix(ShaderConstants.HizCameraMatrixVP, GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) *
                                                                       camera.worldToCameraMatrix);
                cmd.SetGlobalTexture(ShaderConstants.HizMapTexture, HizMap);
                cmd.SetGlobalVector(ShaderConstants.HizMapParam, new Vector4(HizMap.width, HizMap.height, HizMap.mipmapCount));
               
                cmd.ReleaseTemporaryRT(InTexture);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

        }

        void GetTemporaryTexture(CommandBuffer cmd, int nameId, int width, int height)
        {
            var desc = new RenderTextureDescriptor(width,width,format,0,0);
            desc.autoGenerateMips = false;
            desc.useMipMap = false;
            desc.enableRandomWrite = true;
            cmd.GetTemporaryRT(nameId, desc, FilterMode.Point);
        }        
        
        public static int GetPreferredSize(Camera camera){
            var screenSize = Mathf.Max(camera.pixelWidth,camera.pixelHeight);
            var textureSize = Mathf.NextPowerOfTwo(screenSize);
            return textureSize;
        }
        
        private class ShaderConstants{
            public static readonly int HizCameraMatrixVP = Shader.PropertyToID("_HizCameraMatrixVP");
            public static readonly int HizCameraPosition = Shader.PropertyToID("_HizCameraPositionWS");
            public static readonly RenderTargetIdentifier CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
            public static readonly int InTexture = Shader.PropertyToID("_InTexture");
            public static readonly int OutTexture = Shader.PropertyToID("_OutTexture");
            public static readonly int SrcSize = Shader.PropertyToID("_SrcSize");
            public static readonly int DstSize = Shader.PropertyToID("_DstSize");
            public static readonly int HizMapTexture = Shader.PropertyToID("_HizMapTexture");
            public static readonly int HizMapParam = Shader.PropertyToID("_HizMapParam");
            public static readonly int TempTexure1 = Shader.PropertyToID("_TempTexure1");
            public static readonly int TempTexure2 = Shader.PropertyToID("_TempTexure2");
        }
    }
}

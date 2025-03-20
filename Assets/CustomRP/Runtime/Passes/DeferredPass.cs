using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering;
using System;
using UnityEngine.Experimental.GlobalIllumination;

public class DeferredPass
{
    static readonly ProfilingSampler samplerDeferred = new("Deferred Pass");

    static readonly ShaderTagId[] shaderTagIds =
    {
        new("SRPDefaultUnlit"),
        new("CustomLit")
    };



    CameraRenderer renderer;

    bool useDynamicBatching, useGPUInstancing;

    int renderingLayerMask;
    TextureHandle[] gBufferTexs;
    RenderTexture[] gBuffersTarget;
    TextureHandle depthTex;

    TextureHandle colorHandle;
    Camera camera;

    Material deferredMat;

    ComputeBufferHandle tilesBuffer;

    ComputeBufferHandle directionalLightDataBuffer;
    ComputeBufferHandle otherLightDataBuffer;
    TextureHandle directionalAtlas;
    TextureHandle otherAtlas;
    ComputeBufferHandle directionalShadowCascadesBuffer;
    ComputeBufferHandle directionalShadowMatricesBuffer;
    ComputeBufferHandle otherShadowDataBuffer;

    readonly int _vpMatrixInv = Shader.PropertyToID("_vpMatrixInv");
    readonly int _lightsPerObj = Shader.PropertyToID("_LIGHTS_PER_OBJECT");

    readonly int _GbufferTex0 = Shader.PropertyToID("_GBuffer0");
    readonly int _GbufferTex1 = Shader.PropertyToID("_GBuffer1");
    readonly int _GbufferTex2 = Shader.PropertyToID("_GBuffer2");

    void Render(RenderGraphContext context)
    {
        var cmd = context.cmd;
        cmd.SetGlobalTexture(_GbufferTex0, gBuffersTarget[0]);
        cmd.SetGlobalTexture(_GbufferTex1, gBuffersTarget[1]);
        cmd.SetGlobalTexture(_GbufferTex2, gBuffersTarget[2]);

        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        Matrix4x4 vpMatrix = projMatrix * viewMatrix;
        Matrix4x4 vpMatrixInv = vpMatrix.inverse;
        //Shader.SetGlobalMatrix(_vpMatrixInv, vpMatrixInv);
        cmd.SetGlobalMatrix(_vpMatrixInv, vpMatrixInv);
        LocalKeyword enableShadows = new LocalKeyword(deferredMat.shader, "_RECEIVE_SHADOWS");
        deferredMat.SetKeyword(enableShadows, true);
        /*LocalKeyword enableReflCub = new LocalKeyword(deferredMat.shader, "_REFLECTION_CUBEMAP");
        deferredMat.SetKeyword(enableReflCub, true);*/
        /*LocalKeyword enableShadowFilter = new LocalKeyword(deferredMat.shader, "DIRECTIONAL_FILTER_SETUP");
        deferredMat.SetKeyword(enableShadowFilter, true);
        LocalKeyword enableOthShadowFilter = new LocalKeyword(deferredMat.shader, "OTHER_FILTER_SETUP");
        deferredMat.SetKeyword(enableShadowFilter, true);*/

        //cmd.SetGlobalBuffer(LightingPass.dirLightDataId, directionalLightDataBuffer);

        //cmd.SetGlobalBuffer

        /*deferredMat.SetTexture(_GbufferTex0, gBuffersTarget[0]);
        deferredMat.SetTexture(_GbufferTex1, gBuffersTarget[1]);
        deferredMat.SetTexture(_GbufferTex2, gBuffersTarget[2]);*/

        cmd.SetRenderTarget
        (
            colorHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, 
            depthTex, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
        );
        //GraphicsBuffer defferedBuffer = directionalLightDataBuffer;
        cmd.DrawProcedural(Matrix4x4.identity, deferredMat, 0, MeshTopology.Triangles, 3);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }

    public static void Record
    (
        RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
        in CameraRendererTextures textures, ref RenderTexture[] renderTargets, Material deferredMat,
        in LightResources lightData, int renderingLayerMask
    )
    {
        ProfilingSampler sampler = samplerDeferred;

        using RenderGraphBuilder builder = renderGraph.AddRenderPass
            (sampler.name, out DeferredPass pass, sampler);
        pass.deferredMat = deferredMat;
        

        // Readwrite not change renderTarget. Also allow RenderBufferLoadAction.DontCare
        pass.colorHandle = builder.ReadWriteTexture(textures.colorAttachment);
        //builder.ReadWriteTexture(textures.depthAttachment);
        //pass.colorTex = textures.colorAttachment;
        pass.depthTex = builder.ReadWriteTexture(textures.depthAttachment);
        pass.gBuffersTarget = renderTargets;
        pass.camera = camera;


        bool tilesBufferValid = lightData.tilesBuffer.IsValid();
        bool useLightsPerObject = !tilesBufferValid;
        if (tilesBufferValid)
        {
            pass.tilesBuffer = builder.ReadComputeBuffer(lightData.tilesBuffer);
        }

        var list = builder.UseRendererList
        (
            renderGraph.CreateRendererList
            (
                new RendererListDesc(shaderTagIds, cullingResults, camera)
                {
                    sortingCriteria = SortingCriteria.CommonOpaque,
                    rendererConfiguration =
                        PerObjectData.ReflectionProbes |
                        PerObjectData.Lightmaps |
                        PerObjectData.ShadowMask |
                        PerObjectData.LightProbe |
                        PerObjectData.OcclusionProbe |
                        PerObjectData.LightProbeProxyVolume |
                        PerObjectData.OcclusionProbeProxyVolume |
                        (useLightsPerObject ?
                            PerObjectData.LightData | PerObjectData.LightIndices :
                            PerObjectData.None),
                    renderQueueRange = RenderQueueRange.opaque,
                    renderingLayerMask = (uint)renderingLayerMask
                }
            )
        );


        pass.directionalLightDataBuffer = builder.ReadComputeBuffer(lightData.directionalLightDataBuffer);
        pass.otherLightDataBuffer = builder.ReadComputeBuffer(lightData.otherLightDataBuffer);
        pass.directionalAtlas = builder.ReadTexture(lightData.shadowResources.directionalAtlas);
        pass.otherAtlas = builder.ReadTexture(lightData.shadowResources.otherAtlas);
        pass.directionalShadowCascadesBuffer = builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowCascadesBuffer);
        pass.directionalShadowMatricesBuffer = builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowMatricesBuffer);
        pass.otherShadowDataBuffer = builder.ReadComputeBuffer(lightData.shadowResources.otherShadowDataBuffer);

        builder.ReadComputeBuffer(lightData.directionalLightDataBuffer);
        builder.ReadComputeBuffer(lightData.otherLightDataBuffer);
        builder.ReadTexture(lightData.shadowResources.directionalAtlas);
        builder.ReadTexture(lightData.shadowResources.otherAtlas);
        builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowCascadesBuffer);
        builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowMatricesBuffer);
        builder.ReadComputeBuffer(lightData.shadowResources.otherShadowDataBuffer);

        builder.AllowRendererListCulling(false);

        builder.AllowPassCulling(false);
        builder.SetRenderFunc<DeferredPass>(static (pass, context) => pass.Render(context));
    }
}

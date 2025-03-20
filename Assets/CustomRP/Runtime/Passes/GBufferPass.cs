using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering;
using static Unity.Burst.Intrinsics.X86.Avx;

public class GBufferPass
{
    static readonly ProfilingSampler samplerGbuffer = new("GBuffer Pass");

    static readonly ShaderTagId[] shaderTagIds =
    {
        new("CustomGBuffer")
    };

    RendererListHandle list;
    CameraRenderer renderer;

    bool useDynamicBatching, useGPUInstancing;

    int renderingLayerMask;
    TextureHandle[] gBufferTexs;
    RenderTargetIdentifier[] gBuffersTarget;
    TextureHandle depthTex;

    void Render(RenderGraphContext context)
    {
        context.cmd.SetRenderTarget(gBuffersTarget, depthTex);
        context.cmd.ClearRenderTarget
        (
            clearDepth: true,
            clearColor: true,
            backgroundColor: Color.clear
        );
        context.cmd.DrawRendererList(list);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }

    public static void Record
    (
        RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
        int renderingLayerMask,
        in CameraRendererTextures textures, in RenderTargetIdentifier[] renderTargets, bool useLightsPerObject
    )
    {
        ProfilingSampler sampler = samplerGbuffer;

        using RenderGraphBuilder builder = renderGraph.AddRenderPass
            (sampler.name, out GBufferPass pass, sampler);
        pass.renderingLayerMask = renderingLayerMask;

        pass.list = builder.UseRendererList
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


        /*if (lightData.tilesBuffer.IsValid())
        {
            builder.ReadComputeBuffer(lightData.tilesBuffer);
        }*/


        // Readwrite not change renderTarget. Also allow RenderBufferLoadAction.DontCare
        builder.ReadWriteTexture(textures.colorAttachment);
        builder.ReadWriteTexture(textures.depthAttachment);
        //pass.colorTex = textures.colorAttachment;
        pass.depthTex = textures.depthAttachment;
        pass.gBuffersTarget = renderTargets;
        //pass.gdepth = gdepth;

        
        // Indicate that this resources is needed
        /*builder.ReadComputeBuffer(lightData.directionalLightDataBuffer);
        builder.ReadComputeBuffer(lightData.otherLightDataBuffer);
        builder.ReadTexture(lightData.shadowResources.directionalAtlas);
        builder.ReadTexture(lightData.shadowResources.otherAtlas);
        builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowCascadesBuffer);
        builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowMatricesBuffer);
        builder.ReadComputeBuffer(lightData.shadowResources.otherShadowDataBuffer);*/


        // GBuffers
        /*var gBuffers = textures.gBuffers;
        pass.gBuffersTarget = new RenderTargetIdentifier[gBuffers.Length];
        for (int i = 0; i < gBuffers.Length; i++)
        {
            var gBufferTex = gBuffers[i];
            builder.WriteTexture(gBufferTex);
            pass.gBuffersTarget[i] = gBufferTex;
        }*/


        builder.AllowPassCulling(false);
        builder.SetRenderFunc<GBufferPass>(static (pass, context) => pass.Render(context));
    }
}

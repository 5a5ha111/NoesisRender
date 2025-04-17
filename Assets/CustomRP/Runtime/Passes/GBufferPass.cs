using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering;


namespace NoesisRender.Passes
{
    using NoesisRender.ResourcesHolders;

    public class GBufferPass
    {
        static readonly ProfilingSampler samplerGbuffer = new("GBuffer Pass");

        static readonly ShaderTagId[] shaderTagIds =
        {
        new("CustomGBuffer")
    };

        RendererListHandle list;

        bool useDynamicBatching, useGPUInstancing;

        int renderingLayerMask;
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
                        renderQueueRange = RenderQueueRange.opaque,
                        renderingLayerMask = (uint)renderingLayerMask
                    }
                )
            );


            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);
            pass.depthTex = textures.depthAttachment;
            pass.gBuffersTarget = renderTargets;

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<GBufferPass>(static (pass, context) => pass.Render(context));
        }
    }

}

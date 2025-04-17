using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;


namespace NoesisRender.Passes
{
    using ResourcesHolders;

    public class VisibleGeometryPass
    {
        static readonly ProfilingSampler
            samplerOpaque = new("Opaque Geometry"),
            samplerTransparent = new("Transparent Geometry");

        static readonly ShaderTagId[] shaderTagIds =
        {
        new("SRPDefaultUnlit"),
        new("CustomLit")
    };

        RendererListHandle list;
        CameraRenderer renderer;

        bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
        bool setTarget = false;

        int renderingLayerMask;
        TextureHandle colorTex, depthTex;

        void Render(RenderGraphContext context)
        {
            if (setTarget)
            {
                context.cmd.SetRenderTarget
                (
                    colorTex, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                    depthTex, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                );
            }

            context.cmd.DrawRendererList(list);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record
        (
            RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
            bool useLightsPerObject, int renderingLayerMask, bool opaque, bool setTarget,
            in CameraRendererTextures textures,
            in LightResources lightData
        )
        {
            ProfilingSampler sampler = opaque ? samplerOpaque : samplerTransparent;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass
                (sampler.name, out VisibleGeometryPass pass, sampler);
            pass.useLightsPerObject = useLightsPerObject;
            pass.renderingLayerMask = renderingLayerMask;

            pass.list = builder.UseRendererList
            (
                renderGraph.CreateRendererList
                (
                    new RendererListDesc(shaderTagIds, cullingResults, camera)
                    {
                        sortingCriteria = opaque ? SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
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
                        renderQueueRange = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent,
                        renderingLayerMask = (uint)renderingLayerMask
                    }
                )
            );


            if (lightData.tilesBuffer.IsValid())
            {
                builder.ReadComputeBuffer(lightData.tilesBuffer);
            }


            // Readwrite not change renderTarget. Also allow RenderBufferLoadAction.DontCare
            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);
            pass.colorTex = textures.colorAttachment;
            pass.depthTex = textures.depthAttachment;
            pass.setTarget = opaque;

            if (!opaque)
            {
                if (textures.colorCopy.IsValid())
                {
                    builder.ReadTexture(textures.colorCopy);
                }
                if (textures.depthCopy.IsValid())
                {
                    builder.ReadTexture(textures.depthCopy);
                }
            }

            // Indicate that this resources is needed
            builder.ReadComputeBuffer(lightData.directionalLightDataBuffer);
            builder.ReadComputeBuffer(lightData.otherLightDataBuffer);
            builder.ReadTexture(lightData.shadowResources.directionalAtlas);
            builder.ReadTexture(lightData.shadowResources.otherAtlas);
            builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowCascadesBuffer);
            builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowMatricesBuffer);
            builder.ReadComputeBuffer(lightData.shadowResources.otherShadowDataBuffer);

            builder.SetRenderFunc<VisibleGeometryPass>(static (pass, context) => pass.Render(context));
        }
    }
}

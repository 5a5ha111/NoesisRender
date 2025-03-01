using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

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

    int renderingLayerMask;

    void Render(RenderGraphContext context)
    {
        context.cmd.DrawRendererList(list);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }

    public static void Record
    (
        RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
        bool useLightsPerObject, int renderingLayerMask, bool opaque,
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
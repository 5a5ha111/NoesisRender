using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class VisibleGeometryPass
{
    static readonly ProfilingSampler sampler = new("VisibleGeometry");

    CameraRenderer renderer;

    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    int renderingLayerMask;

    void Render(RenderGraphContext context) => renderer.DrawVisibleGeometry
    (
        useDynamicBatching, useGPUInstancing, useLightsPerObject, renderingLayerMask
    );

    public static void Record
    (
        RenderGraph renderGraph, CameraRenderer renderer,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        int renderingLayerMask
    )
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass
            (sampler.name, out VisibleGeometryPass pass, sampler);
        pass.renderer = renderer;
        pass.useDynamicBatching = useDynamicBatching;
        pass.useGPUInstancing = useGPUInstancing;
        pass.useLightsPerObject = useLightsPerObject;
        pass.renderingLayerMask = renderingLayerMask;
        builder.SetRenderFunc<VisibleGeometryPass>((pass, context) => pass.Render(context));
    }
}
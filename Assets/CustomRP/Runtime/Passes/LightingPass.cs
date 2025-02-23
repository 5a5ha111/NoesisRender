using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


/// <summary>
/// It invokes Setup on lighting object, which also renders shadows if needed.
/// </summary>
public class LightingPass
{
    static readonly ProfilingSampler sampler = new("Lighting");

    Lighting lighting;

    CullingResults cullingResults;

    ShadowSettings shadowSettings;

    bool useLightsPerObject;

    int renderingLayerMask;

    void Render(RenderGraphContext context) => lighting.Setup
    (
        context, cullingResults, shadowSettings,
        useLightsPerObject, renderingLayerMask
    );

    public static void Record
    (
        RenderGraph renderGraph, Lighting lighting,
        CullingResults cullingResults, ShadowSettings shadowSettings,
        bool useLightsPerObject, int renderingLayerMask
    )
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass
        (
            sampler.name, out LightingPass pass, sampler
        );
        pass.lighting = lighting;
        pass.cullingResults = cullingResults;
        pass.shadowSettings = shadowSettings;
        pass.useLightsPerObject = useLightsPerObject;
        pass.renderingLayerMask = renderingLayerMask;
        builder.SetRenderFunc<LightingPass>((pass, context) => pass.Render(context));
    }
}
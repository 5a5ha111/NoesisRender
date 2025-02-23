using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

/// <summary>
/// Setup pass invokes Setup, which creates intermediate buffers if needed and clears the render target.
/// </summary>
public class SetupPass
{
    // If we don't provide an explicit sampler as a third argument to AddRenderPass then the render graph will create one based on the pass name, pooling it in a dictionary. Although this works it is inefficient, because it requires calculating the hash code of the name every time we add the pass. So we give each pass their own explicit sampler.
    static readonly ProfilingSampler sampler = new("Setup");

    CameraRenderer renderer;

    void Render(RenderGraphContext context) => renderer.Setup();

    public static void Record(RenderGraph renderGraph, CameraRenderer renderer)
    {
        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass(sampler.name, out SetupPass pass, sampler);
        pass.renderer = renderer;
        builder.SetRenderFunc<SetupPass>((pass, context) => pass.Render(context));
    }
}
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


/// <summary>
/// FinalPass is used when we are rendering to intermediate buffers while not using post FX. It needs the camera renderer and the final blend mode to do its work.
/// </summary>
public class FinalPass
{
    static readonly ProfilingSampler sampler = new("Final");

    CameraRenderer renderer;

    CameraSettings.FinalBlendMode finalBlendMode;

    void Render(RenderGraphContext context)
    {
        renderer.DrawFinal(finalBlendMode);
        renderer.ExecuteBuffer();
    }

    public static void Record(
        RenderGraph renderGraph,
        CameraRenderer renderer,
        CameraSettings.FinalBlendMode finalBlendMode)
    {
        using RenderGraphBuilder builder =
            renderGraph.AddRenderPass(sampler.name, out FinalPass pass, sampler);
        pass.renderer = renderer;
        pass.finalBlendMode = finalBlendMode;
        builder.SetRenderFunc<FinalPass>((pass, context) => pass.Render(context));
    }
}
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class PostFXPass
{
    static readonly ProfilingSampler sampler = new("PostFX");

    PostFXStack postFXStack;

    void Render(RenderGraphContext context) => postFXStack.Render(context, CameraRenderer.colorAttachmentId);

    public static void Record(RenderGraph renderGraph, PostFXStack postFXStack)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass
        (
            sampler.name, out PostFXPass pass, sampler
        );
        pass.postFXStack = postFXStack;
        builder.SetRenderFunc<PostFXPass>((pass, context) => pass.Render(context));
    }
}
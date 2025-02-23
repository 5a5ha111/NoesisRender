using System.Diagnostics;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class UnsupportedShadersPass
{
    #if UNITY_EDITOR
        static readonly ProfilingSampler sampler = new("UnsupportedShaders");

        CameraRenderer renderer;

        void Render(RenderGraphContext context) => renderer.DrawUnsupportedShaders();
    #endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(RenderGraph renderGraph, CameraRenderer renderer)
    {
        #if UNITY_EDITOR
            using RenderGraphBuilder builder = renderGraph.AddRenderPass
            (sampler.name, out UnsupportedShadersPass pass, sampler);
            pass.renderer = renderer;
            builder.SetRenderFunc<UnsupportedShadersPass>((pass, context) => pass.Render(context));
        #endif
    }
}
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace NoesisRender.Passes
{
    public class UnsupportedShadersPass
    {
#if UNITY_EDITOR

        static readonly ProfilingSampler sampler = new("UnsupportedShaders");

        CameraRenderer renderer;

        void Render(RenderGraphContext context)
        {
            //renderer.DrawUnsupportedShaders();

            // To finally draw the geometry invoke DrawRendererList on the context's command buffer with the handle as an argument. The argument's type is RendererList, but RendererListHandle implicitly converts to it.

            context.cmd.DrawRendererList(list);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

#endif

        static readonly ShaderTagId[] shaderTagIds =
        {
        new("Always"),
        new("ForwardBase"),
        new("PrepassBase"),
        new("Vertex"),
        new("VertexLMRGBM"),
        new("VertexLM")
    };

        static Material errorMaterial;
        RendererListHandle list;

        [Conditional("UNITY_EDITOR")]
        public static void Record(RenderGraph renderGraph, Camera camera, CullingResults cullingResults)
        {
#if UNITY_EDITOR


            using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out UnsupportedShadersPass pass, sampler);

            if (errorMaterial == null)
            {
                errorMaterial = new(Shader.Find("Hidden/InternalErrorShader"));
            }

            // The renderer list description replaces the drawing, filtering, and sorting settings. Now we only have to create a single description.
            pass.list = builder.UseRendererList(renderGraph.CreateRendererList
            (
                new RendererListDesc(shaderTagIds, cullingResults, camera)
                {
                    overrideMaterial = errorMaterial,
                    renderQueueRange = RenderQueueRange.all
                }
            ));

            builder.SetRenderFunc<UnsupportedShadersPass>(static (pass, context) => pass.Render(context));

#endif
        }
    }
}

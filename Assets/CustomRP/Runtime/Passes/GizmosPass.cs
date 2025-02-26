using System.Diagnostics;
using UnityEditor;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class GizmosPass
{
    #if UNITY_EDITOR
        static readonly ProfilingSampler sampler = new("Gizmos");

        //CameraRenderer renderer;
        bool requiresDepthCopy;
        CameraRendererCopier copier;
        TextureHandle depthAttachment;

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;
            ScriptableRenderContext renderContext = context.renderContext;
            if (requiresDepthCopy)
            {
                //renderer.Draw(
                //  CameraRenderer.depthAttachmentId, BuiltinRenderTextureType.CameraTarget,
                //  true);
                //renderer.ExecuteBuffer();
                copier.CopyByDrawing(
                    buffer, depthAttachment, BuiltinRenderTextureType.CameraTarget, true);
                renderContext.ExecuteCommandBuffer(buffer);
                buffer.Clear();
            }
            renderContext.DrawGizmos(copier.Camera, GizmoSubset.PreImageEffects);
            renderContext.DrawGizmos(copier.Camera, GizmoSubset.PostImageEffects);
        }
    #endif

    [Conditional("UNITY_EDITOR")]
    public static void Record
    (
        RenderGraph renderGraph, bool useIntermediateBuffer,
        CameraRendererCopier copier,
        in CameraRendererTextures textures
    )
    {
        #if UNITY_EDITOR
            if (Handles.ShouldRenderGizmos()) 
            {
                using RenderGraphBuilder builder = renderGraph.AddRenderPass
                (
                    sampler.name, out GizmosPass pass, sampler
                );
                //pass.renderer = renderer;
                pass.requiresDepthCopy = useIntermediateBuffer;
                pass.copier = copier;
                if (useIntermediateBuffer)
                {
                    pass.depthAttachment = builder.ReadTexture(textures.depthAttachment);
                }
                builder.SetRenderFunc<GizmosPass>((pass, context) => pass.Render(context));
            }
        #endif
    }
}
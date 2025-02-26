using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class CopyAttachmentsPass
{
    static readonly ProfilingSampler sampler = new("Copy Attachments");

    static readonly int
        colorCopyID = Shader.PropertyToID("_CameraColorTexture"),
        depthCopyID = Shader.PropertyToID("_CameraDepthTexture");


    //CameraRenderer renderer;
    bool copyColor, copyDepth;
    CameraRendererCopier copier;
    TextureHandle colorAttachment, depthAttachment, colorCopy, depthCopy;

    void Render(RenderGraphContext context) //=> renderer.CopyAttachments();
    {
        CommandBuffer buffer = context.cmd;
        if (copyColor)
        {
            copier.Copy(buffer, colorAttachment, colorCopy, false);
            buffer.SetGlobalTexture(colorCopyID, colorCopy);
        }
        if (copyDepth)
        {
            copier.Copy(buffer, depthAttachment, depthCopy, true);
            buffer.SetGlobalTexture(depthCopyID, depthCopy);
        }
        if (CameraRendererCopier.RequiresRenderTargetResetAfterCopy)
        {
            buffer.SetRenderTarget(
                colorAttachment,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                depthAttachment,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static void Record
    (
        RenderGraph renderGraph,
        bool copyColor,
        bool copyDepth,
        CameraRendererCopier copier,
        in CameraRendererTextures textures
    )
    {
        if (copyColor || copyDepth)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass
            (
                sampler.name, out CopyAttachmentsPass pass, sampler
            );
            //pass.renderer = renderer;
            pass.copyColor = copyColor;
            pass.copyDepth = copyDepth;
            pass.copier = copier;

            pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);
            pass.depthAttachment = builder.ReadTexture(textures.depthAttachment);
            if (copyColor)
            {
                pass.colorCopy = builder.WriteTexture(textures.colorCopy);
            }
            if (copyDepth)
            {
                pass.depthCopy = builder.WriteTexture(textures.depthCopy);
            }

            builder.SetRenderFunc<CopyAttachmentsPass>
            (
                (pass, context) => pass.Render(context)
            );
        }
    }
}
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using static UnityEditor.ObjectChangeEventStream;

/// <summary>
/// Setup pass invokes Setup, which creates intermediate buffers if needed and clears the render target.
/// </summary>
public class SetupPass
{
    // If we don't provide an explicit sampler as a third argument to AddRenderPass then the render graph will create one based on the pass name, pooling it in a dictionary. Although this works it is inefficient, because it requires calculating the hash code of the name every time we add the pass. So we give each pass their own explicit sampler.
    static readonly ProfilingSampler sampler = new("Setup");

    CameraRenderer renderer;

    bool useIntermediateAttachments;
    bool useDeferred;
    /// <summary>
    /// By some reason, in Unity we must cast TextureHandle to renderTarget for using several renderTargets
    /// </summary>
    RenderTargetIdentifier[] gBuffersTarget;
    TextureHandle[] gBufferTexs;

    static readonly int attachmentSizeID = Shader.PropertyToID("_CameraBufferSize");
    // Texture Handle structs act as identifiers for textures
    TextureHandle colorAttachment, depthAttachment, motionTexture, motionDepthTexture;
    Vector2Int attachmentSize;
    Camera camera;
    CameraClearFlags clearFlags;

    void Render(RenderGraphContext context)
    {
        context.renderContext.SetupCameraProperties(camera);
        CommandBuffer cmd = context.cmd;

        cmd.SetRenderTarget(motionTexture, motionDepthTexture); //Set CameraTarget to the motion vector texture
        cmd.ClearRenderTarget(true, true, Color.black);

        if (useIntermediateAttachments)
        {
            cmd.SetRenderTarget
            (
                colorAttachment,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }
        /*if (useDeferred)
        {
            gBuffersTarget = new RenderTargetIdentifier[2];
            for (int i = 0; i < 2; i++)
            {
                var gBufferTex = gBufferTexs[i];
                gBuffersTarget[i] = gBufferTex;
            }
            cmd.SetRenderTarget
            (
                gBuffersTarget, depthAttachment
            );
            cmd.ClearRenderTarget
            (
                clearFlags <= CameraClearFlags.Depth,
                clearFlags <= CameraClearFlags.Color,
                clearFlags == CameraClearFlags.Color ?
                    camera.backgroundColor.linear : Color.clear
            );
        }*/
        cmd.ClearRenderTarget
        (
            clearFlags <= CameraClearFlags.Depth,
            clearFlags <= CameraClearFlags.Color,
            clearFlags == CameraClearFlags.Color ?
                camera.backgroundColor.linear : Color.clear
        );
        cmd.SetGlobalVector
        (
            attachmentSizeID, new Vector4
            (
                attachmentSize.x, attachmentSize.y, 1f / attachmentSize.x, 1f / attachmentSize.y
            )
        );
        context.renderContext.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }


    public static CameraRendererTextures Record
    (
        RenderGraph renderGraph,
        bool useIntermediateAttachments,
        bool copyColor,
        bool copyDepth,
        bool useHDR,
        bool useDeferred,
        Vector2Int attachmentSize,
        Camera camera
    )
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out SetupPass pass, sampler);

        pass.useIntermediateAttachments = useIntermediateAttachments;
        pass.attachmentSize = attachmentSize;
        pass.camera = camera;
        pass.clearFlags = camera.clearFlags;

        TextureHandle colorAttachment, depthAttachment;
        TextureHandle colorCopy = default, depthCopy = default;
        var defColorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR);
        if (useIntermediateAttachments)
        {
            if (pass.clearFlags > CameraClearFlags.Color)
            {
                pass.clearFlags = CameraClearFlags.Color;
            }

            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = defColorFormat,
                name = "Color Attachment"
            };
            colorAttachment = pass.colorAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
            if (copyColor)
            {
                desc.name = "Color Copy";
                colorCopy = renderGraph.CreateTexture(desc);
            }
            
            
            desc.depthBufferBits = DepthBits.Depth32;
            desc.name = "Depth Attachment";
            depthAttachment = pass.depthAttachment = builder.WriteTexture(renderGraph.CreateTexture(desc));
            if (copyDepth)
            {
                desc.name = "Depth Copy";
                depthCopy = renderGraph.CreateTexture(desc);
            }
        }
        else
        {
            colorAttachment = depthAttachment = pass.colorAttachment = pass.depthAttachment = builder.WriteTexture
            (
                renderGraph.ImportBackbuffer
                (
                    BuiltinRenderTextureType.CameraTarget
                )
            );
        }

        TextureDesc motionvectorRTDesc = new TextureDesc(attachmentSize.x, attachmentSize.y);
        TextureDesc motionvectorDepthRTDesc = new TextureDesc(attachmentSize.x, attachmentSize.y);
        motionvectorRTDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat;
        motionvectorDepthRTDesc.depthBufferBits = DepthBits.Depth32;
        //motionvectorRTDesc.depthBufferBits = DepthBits.Depth32;

        //motionvectorRTDesc.msaaSamples = MSAASamples.None;
        motionvectorRTDesc.enableRandomWrite = false;
        motionvectorRTDesc.name = "motionVectorsTexture";
        motionvectorRTDesc.filterMode = FilterMode.Bilinear;
        motionvectorRTDesc.isShadowMap = false;

        motionvectorDepthRTDesc.enableRandomWrite = false;
        motionvectorDepthRTDesc.name = "motionVectorsDepthTexture";
        motionvectorDepthRTDesc.filterMode = FilterMode.Bilinear;
        motionvectorDepthRTDesc.isShadowMap = false;


        TextureHandle motionVectorTexture, motionVectorDepthTexture;
        motionVectorTexture = builder.WriteTexture(renderGraph.CreateTexture(motionvectorRTDesc));
        motionVectorDepthTexture = builder.WriteTexture(renderGraph.CreateTexture(motionvectorDepthRTDesc));
        pass.motionTexture = motionVectorTexture;
        pass.motionDepthTexture = motionVectorDepthTexture;

        // Prevent from culling
        builder.AllowPassCulling(false);
        // We're going to explicitly mark all anonymous methods of our render passes as static. This isn't required but prevents mistakes that could cause the enclosing scope to be captured, leading to unwanted memory allocations.
        builder.SetRenderFunc<SetupPass>(static (pass, context) => pass.Render(context));


        // GBuffer
        /*TextureHandle[] gBuffers = new TextureHandle[0];
        if (useDeferred)
        {
            TextureDesc gBufferDesc = new TextureDesc(attachmentSize.x, attachmentSize.y);
            gBufferDesc.colorFormat = defColorFormat; // RGB color, A metallic
            gBufferDesc.depthBufferBits = DepthBits.None;
            TextureDesc gBufferDesc2 = new TextureDesc(attachmentSize.x, attachmentSize.y);
            gBufferDesc2.colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR); // R smoothness, GB normals, A occlusion
            gBufferDesc2.depthBufferBits = DepthBits.None;

            gBuffers = new TextureHandle[2];
            TextureHandle gBuffer1 = builder.WriteTexture(renderGraph.CreateTexture(gBufferDesc));
            TextureHandle gBuffer2 = builder.WriteTexture(renderGraph.CreateTexture(gBufferDesc2));
            gBuffers[0] = gBuffer1;
            gBuffers[1] = gBuffer2;
            pass.useDeferred = useDeferred;
            *//*pass.gBuffersTarget = new RenderTargetIdentifier[2];
            for (int i = 0; i < 2; i++)
            {
                var gBufferTex = gBuffers[i];
                //builder.WriteTexture(gBufferTex);
                pass.gBuffersTarget[i] = gBufferTex;
            }*//*
            pass.gBufferTexs = gBuffers;
        }
        else
        {

        }*/
        


        return new CameraRendererTextures(colorAttachment, depthAttachment, colorCopy, depthCopy, motionVectorTexture, motionVectorDepthTexture);
    }
}
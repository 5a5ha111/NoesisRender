using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct CameraRendererTextures
{
    public readonly TextureHandle
        colorAttachment, depthAttachment,
        colorCopy, depthCopy;

    public CameraRendererTextures
    (
        TextureHandle colorAttachment,
        TextureHandle depthAttachment,
        TextureHandle colorCopy,
        TextureHandle depthCopy
    )
    {
        this.colorAttachment = colorAttachment;
        this.depthAttachment = depthAttachment;
        this.colorCopy = colorCopy;
        this.depthCopy = depthCopy;
    }
}
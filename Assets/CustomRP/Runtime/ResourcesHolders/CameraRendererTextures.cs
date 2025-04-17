using UnityEngine.Experimental.Rendering.RenderGraphModule;


namespace NoesisRender.ResourcesHolders
{
    public readonly ref struct CameraRendererTextures
    {
        public readonly TextureHandle
            colorAttachment, depthAttachment,
            colorCopy, depthCopy,
            motionVectorsTexture, motionVectorDepth;


        public CameraRendererTextures
        (
            TextureHandle colorAttachment,
            TextureHandle depthAttachment,
            TextureHandle colorCopy,
            TextureHandle depthCopy,
            TextureHandle motionVectorsTexture,
            TextureHandle motionVectorDepth
        )
        {
            this.colorAttachment = colorAttachment;
            this.depthAttachment = depthAttachment;
            this.colorCopy = colorCopy;
            this.depthCopy = depthCopy;
            this.motionVectorsTexture = motionVectorsTexture;
            this.motionVectorDepth = motionVectorDepth;

            //this.gBuffers = gBuffers;
        }
    }
}

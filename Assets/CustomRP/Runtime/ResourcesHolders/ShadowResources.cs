using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct ShadowResources
{
    public readonly TextureHandle directionalAtlas, otherAtlas;

    public readonly ComputeBufferHandle
        directionalShadowCascadesBuffer,
        directionalShadowMatricesBuffer,
        otherShadowDataBuffer;

    public ShadowResources(TextureHandle directionalAtlas, TextureHandle otherAtlas,
        ComputeBufferHandle directionalShadowCascadesBuffer,
        ComputeBufferHandle directionalShadowMatricesBuffer,
        ComputeBufferHandle otherShadowDataBuffer)
    {
        this.directionalAtlas = directionalAtlas;
        this.otherAtlas = otherAtlas;
        this.directionalShadowCascadesBuffer = directionalShadowCascadesBuffer;
        this.directionalShadowMatricesBuffer = directionalShadowMatricesBuffer;
        this.otherShadowDataBuffer = otherShadowDataBuffer;
    }
}
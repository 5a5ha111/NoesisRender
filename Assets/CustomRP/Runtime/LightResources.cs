using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct LightResources
{
    public readonly ComputeBufferHandle
        directionalLightDataBuffer, otherLightDataBuffer;

    public readonly ShadowResources shadowResources;

    public LightResources
    (
        ComputeBufferHandle directionalLightDataBuffer,
        ComputeBufferHandle otherLightDataBuffer,
        ShadowResources shadowResources
    )
    {
        this.directionalLightDataBuffer = directionalLightDataBuffer;
        this.otherLightDataBuffer = otherLightDataBuffer;
        this.shadowResources = shadowResources;
    }
}
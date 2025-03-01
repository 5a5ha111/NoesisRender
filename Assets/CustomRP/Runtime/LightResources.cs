using UnityEngine.Experimental.Rendering.RenderGraphModule;

public readonly ref struct LightResources
{
    public readonly ComputeBufferHandle directionalLightDataBuffer;
    public readonly ComputeBufferHandle otherLightDataBuffer;
    /// <summary>
    /// Forward+ light data screen space buffer.
    /// We use a simple buffer layout, storing a list of all tiles. Each tile consists of a header that contains the amount of lights that affect it, followed by a list containing all the indices of those lights. We simply reserve space for the maximum amount of lights that we allow per tile, so the data for all tiles is the same size. Supporting variable lists per tile is more memory efficient but also more complex, requiring an additional lookup table, so we won't do that at this time.
    /// </summary>
    public readonly ComputeBufferHandle tilesBuffer;

    public readonly ShadowResources shadowResources;

    public LightResources
    (
        ComputeBufferHandle directionalLightDataBuffer,
        ComputeBufferHandle otherLightDataBuffer,
        ComputeBufferHandle tilesBuffer,
        ShadowResources shadowResources
    )
    {
        this.directionalLightDataBuffer = directionalLightDataBuffer;
        this.otherLightDataBuffer = otherLightDataBuffer;
        this.tilesBuffer = tilesBuffer;
        this.shadowResources = shadowResources;
    }
}
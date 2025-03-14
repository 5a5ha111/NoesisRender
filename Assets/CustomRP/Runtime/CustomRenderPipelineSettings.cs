using UnityEngine;

[System.Serializable]
public class CustomRenderPipelineSettings
{
    public CameraBufferSettings cameraBuffer = new()
    {
        allowHDR = true,
        renderScale = 1f,
        fxaa = new()
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f
        }
    };

    [SerializeField, Tooltip("GPU instancing is always enabled in RenderGraph.")]
    public bool
        useSRPBatcher = true,
        useLightsPerObject = true;

    [Space]
    [Space]
    [Space]
    public ShadowSettings shadows;


    [Space]
    [Space]
    public PostFXSettings postFXSettings;

    public enum ColorLUTResolution
    { _16 = 16, _32 = 32, _64 = 64 }
    public ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    [Space]
    [Space]
    [Space]
    public ForwardPlusSettings forwardPlus;

    [Space]
    [Space]
    [Space]
    public DeferredSettings deferredSettings;


    [Space]
    [Space]
    [Space]
    public Shader cameraRendererShader;
    public Shader cameraDebuggerShader, cameraMotionShader, depthOnlyShader, motionVectorDebug;
}


[System.Serializable]
public struct ForwardPlusSettings
{
    public enum TileSize
    {
        Default, _16 = 16, _32 = 32, _64 = 64, _128 = 128, _256 = 256
    }

    [Tooltip("Tile size in pixels per dimension, default is 64.")]
    public TileSize tileSize;

    [Range(0, 256)]
    [Tooltip("Maximum allowed lights per tile, 0 means default, which is 31.")]
    public int maxLightsPerTile;
}

[System.Serializable]
public struct DeferredSettings
{
    [Tooltip("If enabled, override ForwardPlus and (deprecated)Lights Per Object")]
    public bool enabled; // true
}
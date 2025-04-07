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
    public DecalsSettings decals = new DecalsSettings { forwardNormalReconstructQuality = DecalsSettings.DecalForwardNormalQuality._ACCURATE };

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

    [Tooltip("Assign Hidden/Custom RP/Deferred Calculate shader")]
    public Shader deferredShader;

    public Cubemap reflectionCubemap; // In deferred shader, wu currently does not support procedural skybox reflections () 
}

[System.Serializable]
public struct DecalsSettings
{
    public enum DecalForwardNormalQuality
    {
        DDX, Tap3, Tap4, _IMPROVED, _ACCURATE
    }
    [Tooltip("How quality will be normal reconstruction method in forward render. In deferred path it not used, since we have normal buffer. \n\nDDX - fastest, but worsest quality. \n\nTap3 - naive normal reconstruction. Accurate mid triangle normals, slightly diagonally offset on edges. Artifacts on depth disparities. \n41 math, 3 tex \n\nTap4 - no diagonal offset on edges, but sharp details are softened. Worse artifacts on depth disparities than 3 tap. Probably little reason to use this over the 3 tap approach. \n50 math, 4 tex \n\n_IMPROVED - sharpness of 3 tap with better handling of depth disparities. Worse artifacts on convex edges than either 3 tap or 4 tap. \n62 math, 5 tex \n\n_ACCURATE - basically as accurate as you can get! No artifacts on depth disparities. No artifacts on edges. Artifacts only on triangles that are <3 pixels across. \n66 math, 9 tex")]
    public DecalForwardNormalQuality forwardNormalReconstructQuality;
}
using System;
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
    public DecalsSettings decalsSettings = new DecalsSettings { forwardNormalReconstructQuality = DecalsSettings.DecalForwardNormalQuality._ACCURATE };

    [Space]
    [Space]
    [Space]
    public Shader cameraRendererShader;
    public Shader cameraDebuggerShader, cameraMotionShader, depthOnlyShader, motionVectorDebug;

    [System.Serializable]
    public class XeGTAOSettings
    {
        public bool enabled = true;
        // some constants reduce performance if provided as dynamic values; if these constants are not required to be dynamic and they match default values, 
        // set XE_GTAO_USE_DEFAULT_CONSTANTS and the code will compile into a more efficient shader
        private const float XE_GTAO_DEFAULT_RADIUS_MULTIPLIER =             1.457f; // allows us to use different value as compared to ground truth radius to counter inherent screen space biases
        private const float XE_GTAO_DEFAULT_FALLOFF_RANGE =                 0.615f; // distant samples contribute less
        private const float XE_GTAO_DEFAULT_SAMPLE_DISTRIBUTION_POWER =     2.0f;   // small crevices more important than big surfaces
        //private const float XE_GTAO_DEFAULT_THIN_OCCLUDER_COMPENSATION =    0.0f;   // the new 'thickness heuristic' approach
        private const float XE_GTAO_DEFAULT_THIN_OCCLUDER_COMPENSATION =    3.3f;   // the new 'thickness heuristic' approach
        private const float XE_GTAO_DEFAULT_FINAL_VALUE_POWER =             2.2f;   // modifies the final ambient occlusion value using power function - this allows some of the above heuristics to do different things
        private const float XE_GTAO_DEFAULT_DEPTH_MIP_SAMPLING_OFFSET=      3.30f;  // main trade-off between performance (memory bandwidth) and quality (temporal stability is the first affected, thin objects next)

        private const float XE_GTAO_OCCLUSION_TERM_SCALE =                  1.5f;   // for packing in UNORM (because raw, pre-denoised occlusion term can overshoot 1 but will later average out to 1)

        [Header("Shaders")]
        public ComputeShader computeShader;
        public Shader XeGTAOApply;

        public enum GTAOQuality 
        {
            low = 0, medium = 1,hight = 2, ultra = 3 
        }

        [Header("Settings")]
        [Tooltip("Render effect at half resolution or not. Recommend enable if res > 1080, effect pretty smooth anyway.")] public bool HalfRes = true;
        [Tooltip("Better leave on hight, it pretty fast")][SerializeField]public GTAOQuality QualityLevel = GTAOQuality.hight;
        [Tooltip("0: disabled; 1: sharp; 2: medium; 3: soft")][Range(0, 5)][SerializeField] public int DenoisePasses = 1;
        [Tooltip("[0.0,  ~ ]   World (view) space size of the occlusion sphere.")][Range(0, 2)][SerializeField] public float Radius = 0.5f;




        // auto-tune-d settings
        [NonSerialized] public readonly float RadiusMultiplier = XE_GTAO_DEFAULT_RADIUS_MULTIPLIER;
        [NonSerialized] public readonly float FalloffRange = XE_GTAO_DEFAULT_FALLOFF_RANGE;
        [NonSerialized] public readonly float SampleDistributionPower = XE_GTAO_DEFAULT_SAMPLE_DISTRIBUTION_POWER;
        [NonSerialized] public readonly float ThinOccluderCompensation = XE_GTAO_DEFAULT_THIN_OCCLUDER_COMPENSATION;
        [Tooltip(" modifies the final ambient occlusion value using power function. Default value = 2.2f")][SerializeField]public float FinalValuePower = XE_GTAO_DEFAULT_FINAL_VALUE_POWER;
        [NonSerialized] public readonly float DepthMIPSamplingOffset = XE_GTAO_DEFAULT_DEPTH_MIP_SAMPLING_OFFSET;
    }

    [Space] public XeGTAOSettings xeGTAOsettings;
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
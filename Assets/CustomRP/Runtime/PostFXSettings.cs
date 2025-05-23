using System;
using UnityEditor;
using UnityEngine;

namespace NoesisRender
{
    [CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
    public class PostFXSettings : ScriptableObject
    {
        [SerializeField] Shader shader = default;

        [NonSerialized] Material material;

        [SerializeField] bool enabled = true;
        public bool Enabled { get { return enabled; } }

        [Serializable] public struct BloomSettings
        {
            /// <summary>
            /// Smaller render scale can make bloom larger, when larger render scale will make bloom smaller. It is possible to basing on camera size instead of buffer size, so it will keep bloom consistent.
            /// </summary>
            public bool ignoreRenderScale;

            [Range(0f, 16f)] public int maxIterations;
            [Min(1f)] public int downscaleLimit;

            public bool bicubicUpsampling;

            [Min(0f)] public float threshold;
            [Range(0f, 1f)] public float thresholdKnee;

            [Min(0f)] public float intensity;
        
            public bool fadeFireflies;

            public enum Mode { Additive, Scattering };
            [Space][Space] public Mode mode;
            [Range(0.05f, 0.95f)] public float scatter;
        }
        [SerializeField] BloomSettings bloom = new BloomSettings
        {
            scatter = 0.7f
        };


        [Serializable] public struct ColorAdjustmentsSettings 
        {
            public float postExposure;

            [Range(-100f, 100f)] public float contrast;

            [ColorUsage(false, true)] public Color colorFilter; //HDR color without alpha

            [Range(-180f, 180f)] public float hueShift;

            [Range(-100f, 100f)] public float saturation;
        }
        [SerializeField] ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings
        {
            colorFilter = Color.white
        };


        [Serializable] public struct WhiteBalanceSettings
        {
            [Range(-100f, 100f)] public float temperature, tint;
        }
        [SerializeField] WhiteBalanceSettings whiteBalance = default;


        [Serializable] public struct SplitToningSettings
        {

            [ColorUsage(false)] public Color shadows, highlights;

            [Range(-100f, 100f)] public float balance;
        }
        [SerializeField] SplitToningSettings splitToning = new SplitToningSettings
        {
            shadows = Color.gray,
            highlights = Color.gray
        };


        [Serializable] public struct ChannelMixerSettings
        {

            [Tooltip("It allows you to combine input RGB values to create a new RGB value. For example, you could swap R and G, subtract B from G, or add G to R to push green toward yellow.")] public Vector3 red, green, blue;
        }
        [SerializeField] ChannelMixerSettings channelMixer = new ChannelMixerSettings
        {
            red = Vector3.right,
            green = Vector3.up,
            blue = Vector3.forward
        };


        [Serializable] public struct ShadowsMidtonesHighlightsSettings
        {
            [ColorUsage(false, true)] public Color shadows, midtones, highlights;

            [Range(0f, 2f)] public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
        }
        [SerializeField] ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = 
        new ShadowsMidtonesHighlightsSettings
        {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightsStart = 0.55f,
            highLightsEnd = 1f
        };


        [Serializable] public struct ToneMappingSettings
        {

            public enum Mode { None = 0, Neutral, Reinhard, ACES, GranTurismo, Uncharted2 }

            public Mode mode;
        }
        [SerializeField] ToneMappingSettings toneMapping = default;


        [Serializable] public struct Dither
        {
            public enum Mode { Disabled = 0, On = 1, HighQuality = 2 }
            public Mode mode;
            #if UNITY_EDITOR
                    [Tooltip("Because scene camera render one frame and freeze, dithering become obvious")]public bool useInScene;
            #endif
        }
        [SerializeField] public Dither dither = new Dither 
        { 
            mode = Dither.Mode.HighQuality
            #if UNITY_EDITOR
                ,
                useInScene = false
            #endif
        };




        public BloomSettings Bloom => bloom;
        public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;
        public WhiteBalanceSettings WhiteBalance => whiteBalance;
        public SplitToningSettings SplitToning => splitToning;
        public ChannelMixerSettings ChannelMixer => channelMixer;
        public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights => shadowsMidtonesHighlights;
        public ToneMappingSettings ToneMapping => toneMapping;


        public Material Material
        {
            get
            {
                if (material == null && shader != null)
                {
                    material = new Material(shader);
                    material.hideFlags = HideFlags.HideAndDontSave;
                }
                return material;
            }
        }

        public static bool AreApplicableTo(Camera camera)
        {
            #if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView &&
                    !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
                {
                    return false;
                }
            #endif
            return camera.cameraType <= CameraType.SceneView;
        }
    }
}
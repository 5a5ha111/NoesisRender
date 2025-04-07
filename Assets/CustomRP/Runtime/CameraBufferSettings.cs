using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.NVIDIA;


#if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
    using NVIDIA = UnityEngine.NVIDIA;
#endif

[Serializable] public struct CameraBufferSettings
{

    public bool allowHDR;

    [Space]
    public bool copyColor;
    public bool copyColorReflection, copyDepth, copyDepthReflection;

    [Space]
    [Tooltip("Renderscale 4/3 with high FXAA can be considered as high quality")][Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)] public float renderScale;

    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }
    [Tooltip("Less blocky but more soapy. Downscaling image (renderScale > 1) using bicubic is undesireble, better use FXAA.")] public BicubicRescalingMode bicubicRescaling;


    [Serializable] public struct FXAA
    {
        public bool enabled;


        [Tooltip("Trims the algorithm from processing darks.\n0.0833 - upper limit (default, the start of visible unfiltered edges)\n0.0625 - high quality (faster)\n0.0312 - visible limit (slower)")][Range(0.0312f, 0.0833f)] public float fixedThreshold;


        [Tooltip("The minimum amount of local contrast required to apply algorithm. \n0.333 - too little (faster) \n0.250 - low quality \n0.166 - default \n0.125 - high quality \n0.063 - overkill (slower)")][Range(0.063f, 0.333f)] public float relativeThreshold;

        [Tooltip("Choose the amount of sub-pixel aliasing removal.\nThis can effect sharpness.\n1.00 - upper limit (softer)\n0.75 - default amount of filtering\n0.50 - lower limit (sharper, less sub-pixel aliasing removal)\n0.25 - almost off\n0.00 - completely off")][Range(0f, 1f)] public float subpixelBlending;

        public enum Quality { Low, Medium, High }

        [Tooltip("Mostly affect how long lines are treated")]
        public Quality quality;
    }

    [Space]
    public FXAA fxaa;

    #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
    [Serializable] public struct DLSS_Settings
    {
        [Tooltip("When DLSS enabled, it disable FXAA")]
        public bool enabled;

        [Header("DLSS Settings")]

        [Tooltip("Biases mip maps to allow sharper texture detail in upscaling.\n" +
                        "This is pretty important for textures to look good with DLSS. Completely custom shaders in CRP need to use the _GlobalMipBias property for this to work.")]
        public bool useMipBias; // = true;

        [Tooltip("Whether to use motion vectors or not. DLSS doesn't work well without at least *some* motion vectors.\n" +
                    "If certain things don't have them it can be alright, like grass moving in the wind. But especially for walls and stuff it's pretty necessary.\n" +
                    "I recommend only to disable these for debugging.")]
        public bool useMotionVectors; // = true;

        [Tooltip("If true, will use the base resolution and sharpness settings recommended by DLSS. Disable this to manually set a scaling ratio and sharpness.")]
        public bool useOptimalSettings; // = true;

        [Tooltip("Quality setting for automatic optimal DLSS settings.")]
        public NVIDIA.DLSSQuality dlssQuality; // = NVIDIA.DLSSQuality.MaximumQuality;

        [Tooltip("DLAA is the same DLSS, but with renderScale = 1. More expensive and have more quality. Overwrite previous settings")] public bool useDLAA; // = false;

        //[Tooltip("Manual viewport scaling ratio.")]
        //public float viewportMult; // = 0.5f;

        [Tooltip("DLSS sharpening amount.")]
        public float sharpness; // = 0.5f;


        [Header("Jitter")]
        [Tooltip("Wether to enable jitter. This should always be left on as DLSS is uselss without it pretty much. Useful for debugging/experimenting.")]
        public bool useJitter; // = true;
        [Tooltip("Scale of jitter to wiggle the camera for the temporal aspect of DLSS. This is best left at 1. Fun to experiment with.")]
        public float jitterScale; // = 1;
        [Tooltip("Additional slight randomness to jitter. At 0.1 this adds a slight imperceptable shimmering in the pixels but eliminates a lot of artifacts when the camera is still and looking at a complex pattern.\n" +
            "Also improves readability. Disable only if you feel like it's adding too much shimmering.")]
        public float jitterRand; // = 0.1f;


    }

    [Space]
    public DLSS_Settings dlss;
    #endif
}
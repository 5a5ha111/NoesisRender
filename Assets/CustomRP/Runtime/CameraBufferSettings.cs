using System;
using UnityEngine;


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

        public Quality quality;
    }

    [Space]
    public FXAA fxaa;
}
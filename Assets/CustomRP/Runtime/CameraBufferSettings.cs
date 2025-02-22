using UnityEngine;


[System.Serializable] public struct CameraBufferSettings
{

    public bool allowHDR;

    [Space]
    [Space]
    public bool copyColor, copyColorReflection, copyDepth, copyDepthReflection;

    [Space]
    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)] public float renderScale;

    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }
    [Tooltip("Less blocky but more soapy. Downscaling image (renderScale > 1) using bicubic is undesireble, better use FXAA.")] public BicubicRescalingMode bicubicRescaling;
}
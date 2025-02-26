using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset 
{

    [SerializeField] bool useSRPBatcher = true,
        useLightsPerObject = true;

    [Header("Deprecated Settings")][SerializeField, Tooltip("Dynamic batching is no longer used.")] bool useDynamicBatching;

    [SerializeField, Tooltip("GPU instancing is always enabled in RenderGraph.")] bool useGPUInstancing;


    [Space]
    [Space]
    //bool allowHDR = true;
    [SerializeField] CameraBufferSettings cameraBufferSettings = new CameraBufferSettings
    {
        allowHDR = true,
        renderScale = 1f,
        fxaa = new CameraBufferSettings.FXAA
        {
            fixedThreshold = 0.0833f,
            relativeThreshold = 0.166f,
            subpixelBlending = 0.75f,
            quality = CameraBufferSettings.FXAA.Quality.Medium,
        }
    };

    [Space]
    [Space]
    [SerializeField] ShadowSettings shadows = default;


    [Space]
    [Space]
    [Space]
    [Space]
    [SerializeField] PostFXSettings postFXSettings = default;

    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }
    [SerializeField] ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    [Space]
    [SerializeField] Shader cameraRendererShader = default;

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            useDynamicBatching, useGPUInstancing, useLightsPerObject, useSRPBatcher, cameraBufferSettings, shadows, postFXSettings, (int)colorLUTResolution,
            cameraRendererShader
        );
    }
}
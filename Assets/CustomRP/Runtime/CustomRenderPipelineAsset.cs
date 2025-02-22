using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset 
{

    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true,
        useLightsPerObject = true;

    //bool allowHDR = true;
    [SerializeField] CameraBufferSettings cameraBufferSettings = new CameraBufferSettings
    {
        allowHDR = true,
        renderScale = 1f
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
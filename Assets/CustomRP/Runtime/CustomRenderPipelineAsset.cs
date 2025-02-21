using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset 
{

    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true,
        useLightsPerObject = true;

    [SerializeField] bool allowHDR = true;

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

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            useDynamicBatching, useGPUInstancing, useLightsPerObject, allowHDR, useSRPBatcher, shadows, postFXSettings, (int)colorLUTResolution
        );
    }
}
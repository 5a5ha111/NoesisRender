using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset 
{

    [SerializeField]
    bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true;


    [Space][SerializeField] ShadowSettings shadows = default;


    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(
            useDynamicBatching, useGPUInstancing, useSRPBatcher, shadows
        );
    }
}
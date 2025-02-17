using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline 
{

    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;

    public CustomRenderPipeline (
		bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject, 
        ShadowSettings shadowSettings,
        PostFXSettings postFXSettings
    ) {
		this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        this.shadowSettings = shadowSettings;
        this.postFXSettings = postFXSettings;

        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }

    CameraRenderer renderer = new CameraRenderer();

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    { 
        
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.Render(context, cameras[i], useDynamicBatching, useGPUInstancing, useLightsPerObject,
                shadowSettings, postFXSettings
            );
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline 
{

    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    //bool allowHDR;
    CameraBufferSettings cameraBufferSettings;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    int colorLUTResolution;
    CameraRenderer renderer;

    public CustomRenderPipeline (
		bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, bool useLightsPerObject,
        CameraBufferSettings cameraBufferSettings,
        ShadowSettings shadowSettings,
        PostFXSettings postFXSettings,
        int colorLUTResolution, 
        Shader cameraRendererShader
    ) {
		this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        this.cameraBufferSettings = cameraBufferSettings;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        this.shadowSettings = shadowSettings;
        this.postFXSettings = postFXSettings;
        this.colorLUTResolution = colorLUTResolution;
        renderer = new CameraRenderer(cameraRendererShader);

        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }


    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    { 
        
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            renderer.Render(context, cameras[i], useDynamicBatching, useGPUInstancing, useLightsPerObject, cameraBufferSettings,
                shadowSettings, postFXSettings, 
                colorLUTResolution
            );
        }
    }
}
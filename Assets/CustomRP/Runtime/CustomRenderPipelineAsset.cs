using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset 
{

    [SerializeField] CustomRenderPipelineSettings settings;

    [NonSerialized, HideInInspector] bool useSRPBatcher = true;



    [Space]
    [Space]
    //bool allowHDR = true;
    [SerializeField, HideInInspector] CameraBufferSettings cameraBufferSettings = new CameraBufferSettings
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
    [SerializeField, HideInInspector] ShadowSettings shadows = default;


    [Space]
    [Space]
    [Space]
    [Space]
    [SerializeField, HideInInspector] PostFXSettings postFXSettings = default;

    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }
    [SerializeField, HideInInspector] ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;

    [Space]
    [SerializeField, HideInInspector] Shader cameraRendererShader = default;


    [Header("Deprecated Settings")]
    [Tooltip("Deprecated, lights-per-object drawing mode will be removed.")]
    public bool useLightsPerObject;

    protected override RenderPipeline CreatePipeline()
    {
        /*return new CustomRenderPipeline(
            useDynamicBatching, useGPUInstancing, useLightsPerObject, useSRPBatcher, cameraBufferSettings, shadows, postFXSettings, (int)colorLUTResolution,
            cameraRendererShader
        );*/

        if ((settings == null || settings.cameraRendererShader == null) &&
            cameraRendererShader != null)
        {
            settings = new CustomRenderPipelineSettings
            {
                cameraBuffer = cameraBufferSettings,
                useSRPBatcher = useSRPBatcher,
                useLightsPerObject = useLightsPerObject,
                shadows = shadows,
                postFXSettings = postFXSettings,
                colorLUTResolution =
                    (CustomRenderPipelineSettings.ColorLUTResolution)
                    colorLUTResolution,
                cameraRendererShader = cameraRendererShader
            };

            if (postFXSettings != null)
            {
                postFXSettings = null;
            }
            if (cameraRendererShader != null)
            {
                cameraRendererShader = null;
            }
        }

        return new CustomRenderPipeline(settings);
    }
}
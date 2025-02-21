using System;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    ScriptableRenderContext context;
    Camera camera;
    CullingResults cullingResults;


    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };
    Lighting lighting = new Lighting();

    PostFXStack postFXStack = new PostFXStack();
    bool useHDR;


    const string bufferName = "Render Camera";
    static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");
    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    static CameraSettings defaultCameraSettings = new CameraSettings();


    public void Render(ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        bool allowHDR,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings,
        int colorLUTResolution
    )
    {
        this.context = context;
        this.camera = camera;
        useHDR = allowHDR && camera.allowHDR;

        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;
        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        PrepareBuffer();
        PrepareForSceneWindow(); // Handle UI in Scene camera

        if (!Cull(shadowSettings.maxDistance)) // Avoiding errors due to incorrect camera settings
        {
            return;
        }

        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1);
        postFXStack.Setup(context, camera, postFXSettings, useHDR, colorLUTResolution,
            cameraSettings.finalBlendMode);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject,
            cameraSettings.renderingLayerMask);
        DrawUnsupportedShaders();

        DrawGizmosBeforeFX();
        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();

        Cleanup();
        Submit();
    }


    void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;

        if (postFXStack.IsActive)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(
                frameBufferId, camera.pixelWidth, camera.pixelHeight,
                32, FilterMode.Bilinear, 
                useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }

        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth, 
            flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                camera.backgroundColor.linear : Color.clear
        );
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if (postFXStack.IsActive)
        {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        int renderingLayerMask)
    {
        PerObjectData lightsPerObjectFlags = useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices :
            PerObjectData.None;

        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask);

        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(
            unlitShaderTagId, sortingSettings
        )
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData =
                PerObjectData.ReflectionProbes |
                PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                PerObjectData.LightProbeProxyVolume |
                PerObjectData.OcclusionProbeProxyVolume |
                lightsPerObjectFlags
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);

        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );

        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(
            cullingResults, ref drawingSettings, ref filteringSettings
        );
    }

    

    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    bool Cull(float maxShadowDistance)
    {
        ScriptableCullingParameters p;

        if (camera.TryGetCullingParameters(out p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
}
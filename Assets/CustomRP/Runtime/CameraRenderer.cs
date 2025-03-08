using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.NVIDIA;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    // Static & const variavles
    public static int
        bufferSizeId = Shader.PropertyToID("_CameraBufferSize"),
        colorAttachmentId = Shader.PropertyToID("_CameraColorAttachment"),
        colorTextureId = Shader.PropertyToID("_CameraColorTexture"), // Camera color copy for distirtion based effects
        depthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment"),
        depthTextureId = Shader.PropertyToID("_CameraDepthTexture"), // Depth copy
        sourceTextureId = Shader.PropertyToID("_SourceTexture"),
        srcBlendId = Shader.PropertyToID("_CameraSrcBlend"),
        dstBlendId = Shader.PropertyToID("_CameraDstBlend");
    static CameraSettings defaultCameraSettings = new CameraSettings();

    public const float renderScaleMin = 0.1f;
    /// <summary>
    /// I using single downsample step in SSAA, so render scale > 2 doesnt make sense.
    /// </summary>
    public const float renderScaleMax = 2f;


    // Temp variables which changed every Render
    ScriptableRenderContext context;
    Camera camera;
    CullingResults cullingResults;
    CommandBuffer buffer;
    bool useHDR, useScaledRendering;
    CameraBufferSettings cameraBufferSettings;
    bool useColorTexture, useDepthTexture, useIntermediateBuffer;
    Vector2Int bufferSize;


    // Temp variables which created and destroyed with CameraRenderer class
    Material material;
    Material materialMotion, depthOnlyMaterial, motionVectorDebugMaterial;
    Texture2D missingTexture;
    PostFXStack postFXStack = new PostFXStack();


    // Cached NVIDIA variabled
    #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
        bool? cachedDeviceAviable;
        int cachedDLSSQuality = -1;
        Vector2Int cachedDLSSResolution;
        float cachedDLSSSharpness;
        float cachedDLSSJitterScale;
    #endif


    public CameraRenderer(Shader shader, Shader cameraDebuggerShader, Shader cameraMotionShader, Shader depthOnlyShader, Shader motionDebug)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        materialMotion = CoreUtils.CreateEngineMaterial(cameraMotionShader);
        depthOnlyMaterial = CoreUtils.CreateEngineMaterial(depthOnlyShader);
        motionVectorDebugMaterial = CoreUtils.CreateEngineMaterial(motionDebug);
        missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "MissingDepthOrColorTexture"
        };
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
        #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
            cachedDLSSQuality = -1;
        #endif

        CameraDebugger.Initialize(cameraDebuggerShader);
    }


    public void Render
    (
        RenderGraph renderGraph,
        ScriptableRenderContext context,
        Camera camera,
        CustomRenderPipelineSettings settings
    )
    {
        CameraBufferSettings bufferSettings = settings.cameraBuffer;
        PostFXSettings postFXSettings = settings.postFXSettings;
        ShadowSettings shadowSettings = settings.shadows;
        bool useLightsPerObject = settings.useLightsPerObject;
        cameraBufferSettings = settings.cameraBuffer;
        useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;


        ProfilingSampler cameraSampler;
        CameraSettings cameraSettings;
        if (camera.TryGetComponent(out CustomRenderPipelineCamera crpCamera))
        {
            cameraSampler = crpCamera.Sampler;
            cameraSettings = crpCamera.Settings;
        }
        else
        {
            cameraSampler = ProfilingSampler.Get(camera.cameraType);
            cameraSettings = defaultCameraSettings;
        }
        if (cameraSettings.overridePostFX)
        {
            postFXSettings = cameraSettings.postFXSettings;
        }

        if (camera.cameraType == CameraType.Reflection)
        {
            useColorTexture = cameraBufferSettings.copyColorReflection;
            useDepthTexture = cameraBufferSettings.copyDepthReflection;
        }
        else
        {
            useColorTexture = cameraBufferSettings.copyColor && cameraSettings.copyColor;
            useDepthTexture = cameraBufferSettings.copyDepth && cameraSettings.copyDepth;
        }


        PrepareForSceneWindow(); // Handle Scene camera
        float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
        // Very slight deviations from 1 will have neither visual nor performance differences that matter. So let's only use scaled rendering if there is at least a 1% difference.
        useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;

        if (!camera.TryGetCullingParameters
        (
            out ScriptableCullingParameters scriptableCullingParameters
        ))
        {
            return;
        }
        scriptableCullingParameters.shadowDistance = Mathf.Min(shadowSettings.maxDistance, camera.farClipPlane);
        CullingResults cullingResults = context.Cull(ref scriptableCullingParameters);

        if (useScaledRendering)
        {
            // prevent too small or large render scale
            renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax); 
            bufferSize.x = (int)(camera.pixelWidth * renderScale);
            bufferSize.y = (int)(camera.pixelHeight * renderScale);

            if (bufferSize.x < 1)
            {
                bufferSize.x = 1;
            }
            if (bufferSize.y < 1)
            {
                bufferSize.y = 1;
            }
        }
        else
        {
            bufferSize.x = camera.pixelWidth;
            bufferSize.y = camera.pixelHeight;
        }
        #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
            if (!cachedDeviceAviable.HasValue)
            {
                cachedDeviceAviable = UnityDLSS.UnityDlssCommon.device != null;
            }
            cameraBufferSettings.dlss.enabled &= cameraSettings.allowDLSS && cachedDeviceAviable.Value;
            if (cameraBufferSettings.dlss.enabled)
            {
                // Dlss is upscaler, not downscaler
                renderScale = Mathf.Clamp01(renderScale);

                // We cache results, because its unnecessary spam request if they results are same. Not catch screen size change in current state
                if ((cameraBufferSettings.dlss.useOptimalSettings && !cameraBufferSettings.dlss.useDLAA) && (int)cameraBufferSettings.dlss.dlssQuality != cachedDLSSQuality)
                    {
                    cachedDLSSQuality = (int)cameraBufferSettings.dlss.dlssQuality;
                    bufferSize = GetScaledBufferSize(renderScale);
                    UnityEngine.NVIDIA.OptimalDLSSSettingsData optimalDLSSSettingsData;
                    bool fit = UnityDLSS.UnityDlssCommon.device.GetOptimalSettings((uint)bufferSize.x, (uint)bufferSize.y, cameraBufferSettings.dlss.dlssQuality, out optimalDLSSSettingsData);
                    bufferSize.x = (int)optimalDLSSSettingsData.outRenderWidth;
                    bufferSize.y = (int)optimalDLSSSettingsData.outRenderHeight;
                    float multJitter = 1.5f; // 2.254698f
                    float relation = Mathf.Clamp01((float)bufferSize.y * multJitter / GetScreenSize().y);
                    cameraBufferSettings.dlss.jitterScale *= relation;
                    cameraBufferSettings.dlss.sharpness = optimalDLSSSettingsData.sharpness;
                    Vector2 minRec = new Vector2(optimalDLSSSettingsData.minWidth, optimalDLSSSettingsData.minHeight);

                    cachedDLSSResolution = bufferSize;
                    cachedDLSSJitterScale = relation;
                    cachedDLSSSharpness = optimalDLSSSettingsData.sharpness;
                    //Debug.Log("bufferSize " + bufferSize + " " + relation + " minSize " + minRec);
                }
                else if (cameraBufferSettings.dlss.useOptimalSettings && !cameraBufferSettings.dlss.useDLAA)
                {
                    bufferSize = cachedDLSSResolution;
                    cameraBufferSettings.dlss.jitterScale *= cachedDLSSJitterScale;
                    cameraBufferSettings.dlss.sharpness = cachedDLSSSharpness;
                }
                else if (cameraBufferSettings.dlss.useDLAA)
                {
                    renderScale = 1;
                    bufferSize = GetScaledBufferSize(renderScale);
                }
                bufferSize = Vector2Int.Max(bufferSize, Vector2Int.one);
            }
        #endif

        if (camera.cameraType == CameraType.SceneView)
        {
            useDepthTexture = cameraBufferSettings.copyDepth;
            #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
                cameraBufferSettings.dlss.useJitter = false;
            #endif
        }
        else
        {
            // We can directly modify the buffer settings struct field because it contains a copy of the RP settings struct, not a reference to the original.
            cameraBufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
            #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
                if (cameraBufferSettings.dlss.enabled)
                {
                    // FXAA will be overwritten anyway, so disable it
                    cameraBufferSettings.fxaa.enabled = false;
                }
            #endif
        }
        cameraBufferSettings.allowHDR &= camera.allowHDR;
        bool hasActivePostFX = postFXSettings != null && PostFXSettings.AreApplicableTo(camera);

        /// DLSS upscale image before PostFXStack
        Vector2Int postFXBufferSize = bufferSize;
        #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
            if (cameraBufferSettings.dlss.enabled)
            {
                postFXBufferSize = GetScreenSize();
            }
        #endif

        postFXStack.Setup
        (
            /*context,*/ camera,
            postFXBufferSize, 
        cameraBufferSettings.bicubicRescaling,
            cameraBufferSettings.fxaa,
            postFXSettings, cameraSettings.keepAlpha, useHDR, (int)settings.colorLUTResolution,
            cameraSettings.finalBlendMode
        );

        useIntermediateBuffer = useScaledRendering ||
            useColorTexture || useDepthTexture || postFXStack.IsActive ||
            !useLightsPerObject;


        //DrawMotionVectors(context, camera, materialMotion, cullingResults);


        var renderGraphParameters = new RenderGraphParameters 
        {
            commandBuffer = CommandBufferPool.Get(),
            currentFrameIndex = Time.frameCount,
            executionName = cameraSampler.name,
            // To cull pasees that not nedded we need explicitly enable culling
            rendererListCulling = true,
            scriptableRenderContext = context
        };
        // This is technically incorrect because the render graph could internally use multiple buffers, but that's only the case when asynchronous passes are used, which we don't.
        buffer = renderGraphParameters.commandBuffer;

        using (renderGraph.RecordAndExecute(renderGraphParameters))
        {
            // Minimum pass builder
            /*using RenderGraphBuilder builder = renderGraph.AddRenderPass("Test Pass", out CameraSettings data);
            builder.SetRenderFunc((CameraSettings data, RenderGraphContext context) => { });*/


            using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);

            LightResources lightResources = LightingPass.Record
            (
                renderGraph, bufferSize, settings.forwardPlus,
                cullingResults, shadowSettings, useLightsPerObject,
                cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1
            );
            CameraRendererTextures textures = SetupPass.Record
            (
                renderGraph, useIntermediateBuffer, 
                useColorTexture, useDepthTexture, useHDR, bufferSize, camera
            );


            MotionVectorPass.Record(renderGraph, camera, textures, cameraBufferSettings, materialMotion, cameraSettings.renderingLayerMask, cullingResults);

            VisibleGeometryPass.Record
            (
                renderGraph, camera,
                cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask, opaque: true, 
                textures, lightResources
            );
            UnsupportedShadersPass.Record(renderGraph, camera, cullingResults);
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                SkyboxPass.Record(renderGraph, camera, textures);
            }
            var copier = new CameraRendererCopier(material, camera, cameraSettings.finalBlendMode);
            CopyAttachmentsPass.Record
            (
                renderGraph, useColorTexture, useDepthTexture, copier, textures
            );



            VisibleGeometryPass.Record
            (
                renderGraph, camera,
                cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask, opaque: false, 
                textures, lightResources
            );

            #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
                DLSSPass.Record(renderGraph, camera, textures, cameraBufferSettings, bufferSize, useHDR);
            #endif

            if (hasActivePostFX)
            {
                postFXStack.BufferSettings = cameraBufferSettings;
                postFXStack.bufferSize = postFXBufferSize;
                postFXStack.camera = camera;
                postFXStack.finalBlendMode = cameraSettings.finalBlendMode;
                postFXStack.settings = postFXSettings;
                PostFXPass.Record
                (
                    renderGraph, postFXStack, (int)settings.colorLUTResolution,
                    cameraSettings.keepAlpha, textures, motionVectorDebugMaterial
                );
            }
            else if (useIntermediateBuffer)
            {
                FinalPass.Record(renderGraph, copier, textures);
            }

            DebugPass.Record(renderGraph, settings, camera, lightResources);
            GizmosPass.Record(renderGraph, useIntermediateBuffer, copier, textures);
        }


        context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
        context.Submit();
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);
    }


    private static Vector2Int GetScaledBufferSize(float renderScale)
    {
        Vector2Int screenSize = GetScreenSize();
        Vector2Int bufferSize = new Vector2Int();
        bufferSize.x = Mathf.Max((int)(screenSize.x * renderScale), 1);
        bufferSize.y = Mathf.Max((int)(screenSize.y * renderScale), 1);
        return bufferSize;
    }

    public static Vector2Int GetScreenSize()
    {
        return Vector2Int.Max(new Vector2Int(Screen.width, Screen.height), new Vector2Int(1,1));
    }


    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(motionVectorDebugMaterial);
        CoreUtils.Destroy(materialMotion);
        CoreUtils.Destroy(depthOnlyMaterial);
        CameraDebugger.Cleanup();
    }
    
    /// <summary>
    /// Draw fullscreen buffer and change renderTarget to RenderTargetIdentifier to
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="isDepth"></param>
    public void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, bool isDepth = false)
    {
        buffer.SetGlobalTexture(sourceTextureId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, material, isDepth ? 1 : 0, MeshTopology.Triangles, 3);
    }

    public void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    
}
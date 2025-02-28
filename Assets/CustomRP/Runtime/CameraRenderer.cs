using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    ScriptableRenderContext context;
    Camera camera;
    CullingResults cullingResults;



    // Now RenderGraph init buffer. Also it handle all buffer.BeginSample() and .EndSample()
    /*CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };*/
    CommandBuffer buffer;


    Lighting lighting = new Lighting();
    Material material;
    Texture2D missingTexture;


    PostFXStack postFXStack = new PostFXStack();
    bool useHDR, useScaledRendering;
    CameraBufferSettings cameraBufferSettings;
    bool useColorTexture, useDepthTexture, useIntermediateBuffer;
    Vector2Int bufferSize;



    public ScriptableRenderContext _context { get { return context; } }
    public Camera _camera { get { return camera; } }
    public CullingResults _cullingResults { get { return cullingResults; } }

    public bool _useHDR { get { return useHDR; } }
    public bool _useScaledRendering { get { return useScaledRendering; } }
    public CameraBufferSettings _cameraBufferSettings { get { return cameraBufferSettings; } }

    public bool _useColorTexture { get { return useColorTexture; } }
    public bool _useDepthTexture { get { return useDepthTexture; } }
    public bool _useIntermediateBuffer { get { return useIntermediateBuffer; } }
    public Vector2Int _bufferSize { get { return bufferSize; } }


    // In RenderGraph we dont need init buffer themselves, so we dont need buffer name
    const string bufferName = "Render Camera";
    
    
    static ShaderTagId
        unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
        litShaderTagId = new ShaderTagId("CustomLit");

    // Camera frame buffer are always stored in separeta buffers, known as frame buffer attachments.
    //static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
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

    static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;
    static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);


    public const float renderScaleMin = 0.1f;
    /// <summary>
    /// I using single downsample step in SSAA, so render scale > 2 doesnt make sense. 
    /// </summary>
    public const float renderScaleMax = 2f;


    public CameraRenderer(Shader shader)
    {
        material = CoreUtils.CreateEngineMaterial(shader);
        missingTexture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "MissingDepthOrColorTexture"
        };
        missingTexture.SetPixel(0, 0, Color.white * 0.5f);
        missingTexture.Apply(true, true);
    }


    public void Render
    (
        RenderGraph renderGraph,
        ScriptableRenderContext context, Camera camera,
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        CameraBufferSettings cameraBufferSettings,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings,
        int colorLUTResolution
    )
    {
        this.context = context;
        this.camera = camera;
        this.cameraBufferSettings = cameraBufferSettings;
        useHDR = cameraBufferSettings.allowHDR && camera.allowHDR;
        //useDepthTexture = true;


        // --- Moved to RenderGraph ---
        var crpCamera = camera.GetComponent<CustomRenderPipelineCamera>();
        CameraSettings cameraSettings = crpCamera ? crpCamera.Settings : defaultCameraSettings;
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

        // --- Moved to renderGraph ---
        //PrepareBuffer();


        PrepareForSceneWindow(); // Handle Scene camera
        float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
        // Very slight deviations from 1 will have neither visual nor performance differences that matter. So let's only use scaled rendering if there is at least a 1% difference.
        useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;

        /*if (!Cull(shadowSettings.maxDistance)) // Avoiding errors due to incorrect camera settings
        {
            return;
        }*/
        if (!camera.TryGetCullingParameters
        (
            out ScriptableCullingParameters scriptableCullingParameters
        ))
        {
            return;
        }
        scriptableCullingParameters.shadowDistance =
            Mathf.Min(shadowSettings.maxDistance, camera.farClipPlane);
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

        if (camera.cameraType == CameraType.SceneView)
        {
            useDepthTexture = cameraBufferSettings.copyDepth;
        }
        else
        {
            // We can directly modify the buffer settings struct field because it contains a copy of the RP settings struct, not a reference to the original.
            cameraBufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
        }
        cameraBufferSettings.allowHDR &= camera.allowHDR;
        cameraBufferSettings.allowHDR = true;
        bool hasActivePostFX = postFXSettings != null && PostFXSettings.AreApplicableTo(camera);

        // --- Moved to renderGraph ---
        //buffer.BeginSample(SampleName);


        // --- Moved to renderGraph ---
        //ExecuteBuffer();

        // --- Moved to renderGraph ---
        /*lighting.Setup
        (
            context, cullingResults, shadowSettings, useLightsPerObject,
            cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1
        );*/

        postFXStack.Setup
        (
            /*context,*/ camera, bufferSize, cameraBufferSettings.bicubicRescaling,
            cameraBufferSettings.fxaa,
            postFXSettings, cameraSettings.keepAlpha, useHDR, colorLUTResolution,
            cameraSettings.finalBlendMode
        );

        // --- Moved to renderGraph ---
        //buffer.EndSample(SampleName);

        // --- Moved to renderGraph ---
        /*Setup();

        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject,
            cameraSettings.renderingLayerMask);
        DrawUnsupportedShaders();

        DrawGizmosBeforeFX();
        if (stack.IsActive)
        {
            stack.Render(colorAttachmentId);
        }
        else if (useIntermediateBuffer)
        {
            DrawFinal(cameraSettings.finalBlendMode);
            ExecuteBuffer();
        }
        DrawGizmosAfterFX();*/

        useIntermediateBuffer = useScaledRendering ||
            useColorTexture || useDepthTexture || postFXStack.IsActive;


        ProfilingSampler cameraSampler;
        //CameraSettings cameraSettings;
        if (crpCamera != null)
        {
            cameraSampler = crpCamera.Sampler;
            //cameraSettings = crpCamera.Settings;
        }
        else
        {
            cameraSampler = ProfilingSampler.Get(camera.cameraType);
            //cameraSettings = defaultCameraSettings;
        }

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

            ShadowTextures shadowTextures = LightingPass.Record
            (
                renderGraph, /*lighting,*/
                cullingResults, shadowSettings, useLightsPerObject,
                cameraSettings.maskLights ? cameraSettings.renderingLayerMask : -1
            );
            CameraRendererTextures textures = SetupPass.Record
            (
                renderGraph, useIntermediateBuffer, 
                useColorTexture, useDepthTexture, useHDR, bufferSize, camera
            );
            VisibleGeometryPass.Record
            (
                renderGraph, camera,
                cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask, opaque: true, 
                textures, shadowTextures
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
                textures, shadowTextures
            );
            if (hasActivePostFX)
            {
                //PostFXPass.Record(renderGraph, postFXStack, textures);
                postFXStack.BufferSettings = cameraBufferSettings;
                postFXStack.bufferSize = bufferSize;
                postFXStack.camera = camera;
                postFXStack.finalBlendMode = cameraSettings.finalBlendMode;
                postFXStack.settings = postFXSettings;
                PostFXPass.Record
                (
                    renderGraph, postFXStack, colorLUTResolution,
                    cameraSettings.keepAlpha, textures
                );
            }
            else if (useIntermediateBuffer)
            {
                FinalPass.Record(renderGraph, copier, textures);
            }
            GizmosPass.Record(renderGraph, useIntermediateBuffer, copier, textures);
        }


        //Cleanup();
        //Submit();
        //lighting.Cleanup();
        context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
        context.Submit();
        CommandBufferPool.Release(renderGraphParameters.commandBuffer);
    }


    public void Setup()
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;

        // Move to area before renderGraph
        /*useIntermediateBuffer = useScaledRendering || useColorTexture || useDepthTexture || stack.IsActive;*/

        if (useIntermediateBuffer)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }

            // Get color buffer
            buffer.GetTemporaryRT
            (
                colorAttachmentId, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear, 
                useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            //Get depth buffer 
            buffer.GetTemporaryRT
            (
                depthAttachmentId, bufferSize.x, bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth
            );

            buffer.SetRenderTarget
            (
                colorAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachmentId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }

        buffer.ClearRenderTarget
        (
            flags <= CameraClearFlags.Depth, 
            flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ?
                camera.backgroundColor.linear : Color.clear
        );

        // --- Moved to renderGraph ---
        //buffer.BeginSample(SampleName);
        buffer.SetGlobalTexture(depthTextureId, missingTexture);
        buffer.SetGlobalTexture(colorTextureId, missingTexture);
        // Set scaled color buffer size for shaders, same as URP screenSize
        buffer.SetGlobalVector
        (
            bufferSizeId, new Vector4
            (
                bufferSize.x, bufferSize.y,
                1f / bufferSize.x, 1f / bufferSize.y
            )
        );

        // --- Moved to renderGraph ---
        //ExecuteBuffer();
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if (useIntermediateBuffer)
        {
            // --- Moved to renderGraph
            /*buffer.ReleaseTemporaryRT(colorAttachmentId);
            buffer.ReleaseTemporaryRT(depthAttachmentId);*/

            /*if (useColorTexture)
            {
                buffer.ReleaseTemporaryRT(colorTextureId);
            }
            if (useDepthTexture)
            {
                buffer.ReleaseTemporaryRT(depthTextureId);
            }*/
        }

        
    }
    public void Dispose()
    {
        CoreUtils.Destroy(material);
        CoreUtils.Destroy(missingTexture);
    }

    public void DrawVisibleGeometry
    (
        bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject,
        int renderingLayerMask
    )
    {
        ExecuteBuffer();

        PerObjectData lightsPerObjectFlags = 
            useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices :
            PerObjectData.None;

        var filteringSettings = new FilteringSettings
        (
            RenderQueueRange.opaque, renderingLayerMask: (uint)renderingLayerMask
        );

        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings
        (
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

        context.DrawRenderers
        (
            cullingResults, ref drawingSettings, ref filteringSettings
        );

        context.DrawSkybox(camera);
        if (useColorTexture || useDepthTexture)
        {
            CopyAttachments();
        }

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers
        (
            cullingResults, ref drawingSettings, ref filteringSettings
        );
    }

    public void CopyAttachments()
    {
        ExecuteBuffer();
        if (useColorTexture)
        {
            buffer.GetTemporaryRT
            (
                colorTextureId, bufferSize.x, bufferSize.y,
                0, FilterMode.Bilinear, useHDR ?
                    RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default
            );
            if (copyTextureSupported)
            {
                buffer.CopyTexture(colorAttachmentId, colorTextureId);
            }
            else
            {
                Draw(colorAttachmentId, colorTextureId);
            }
        }
        if (useDepthTexture)
        {
            buffer.GetTemporaryRT
            (
                depthTextureId, bufferSize.x, bufferSize.y,
                32, FilterMode.Point, RenderTextureFormat.Depth
            );
            if (copyTextureSupported)
            {
                buffer.CopyTexture(depthAttachmentId, depthTextureId);
            }
            else
            { 
                Draw(depthAttachmentId, depthTextureId, true);
            }
        }

        if (!copyTextureSupported)
        {
            // Draw() changes render target, so we need to change it back
            buffer.SetRenderTarget
            (
                colorAttachmentId,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                depthAttachmentId,
                RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
        }
        // --- Moved to renderGraph ---
        //ExecuteBuffer();
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
    /// <summary>
    /// For support multi-camera (i.g. splitscreem) color and depth buffers without post fx stack we need this method
    /// </summary>
    /// <param name="finalBlendMode"></param>
    public void DrawFinal(CameraSettings.FinalBlendMode finalBlendMode)
    {
        buffer.SetGlobalFloat(srcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendId, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureId, colorAttachmentId);
        buffer.SetRenderTarget
        (
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ?
                RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store
        );
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural
        (
            Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3
        );
        buffer.SetGlobalFloat(srcBlendId, 1f);
        buffer.SetGlobalFloat(dstBlendId, 0f);
    }



    void Submit()
    {
        // --- Moved to renderGraph ---
        //buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    public void ExecuteBuffer()
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
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.NVIDIA;
using UnityEngine.Rendering;

public partial class CameraRenderer
{

    ScriptableRenderContext context;
    Camera camera;
    CullingResults cullingResults;


    CommandBuffer buffer;


    Lighting lighting = new Lighting();
    Material material;
    Material materialMotion, depthOnlyMaterial, motionVectorDebugMaterial;
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

    #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
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
        cachedDLSSQuality = -1;

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
            if (cameraBufferSettings.dlss.enabled)
            {
                // Dlss is upscaler, not downscaler
                renderScale = Mathf.Clamp01(renderScale);

                // We cache results, because its unnecessary spam request if they results are same. Not catch screen size change in current state
                if (cameraBufferSettings.dlss.useOptimalSettings && (int)cameraBufferSettings.dlss.dlssQuality != cachedDLSSQuality)
                    {
                    cachedDLSSQuality = (int)cameraBufferSettings.dlss.dlssQuality;
                    bufferSize.x = Mathf.Max((int)(camera.pixelWidth * renderScale), 1);
                    bufferSize.y = Mathf.Max((int)(camera.pixelHeight * renderScale), 1);
                    UnityEngine.NVIDIA.OptimalDLSSSettingsData optimalDLSSSettingsData;
                    bool fit = UnityDLSS.UnityDlssCommon.device.GetOptimalSettings((uint)bufferSize.x, (uint)bufferSize.y, cameraBufferSettings.dlss.dlssQuality, out optimalDLSSSettingsData);
                    bufferSize.x = (int)optimalDLSSSettingsData.outRenderWidth;
                    bufferSize.y = (int)optimalDLSSSettingsData.outRenderHeight;
                    float multJitter = 1.5f; // 2.254698f
                    float relation = Mathf.Clamp01((float)bufferSize.y * multJitter / Screen.height);
                    cameraBufferSettings.dlss.jitterScale *= relation;
                    cameraBufferSettings.dlss.sharpness = optimalDLSSSettingsData.sharpness;
                    Vector2 minRec = new Vector2(optimalDLSSSettingsData.minWidth, optimalDLSSSettingsData.minHeight);

                    cachedDLSSResolution = bufferSize;
                    cachedDLSSJitterScale = relation;
                    cachedDLSSSharpness = optimalDLSSSettingsData.sharpness;
                    //Debug.Log("bufferSize " + bufferSize + " " + relation + " minSize " + minRec);
                }
                else if (cameraBufferSettings.dlss.useOptimalSettings)
                {
                    bufferSize = cachedDLSSResolution;
                    cameraBufferSettings.dlss.jitterScale *= cachedDLSSJitterScale;
                    cameraBufferSettings.dlss.sharpness = cachedDLSSSharpness;
                }
                bufferSize = Vector2Int.Max(bufferSize, Vector2Int.one);
            }
        #endif

        if (camera.cameraType == CameraType.SceneView)
        {
            useDepthTexture = cameraBufferSettings.copyDepth;
            cameraBufferSettings.dlss.useJitter = false;
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
        cameraBufferSettings.allowHDR = true;
        bool hasActivePostFX = postFXSettings != null && PostFXSettings.AreApplicableTo(camera);


        postFXStack.Setup
        (
            /*context,*/ camera, 
            bufferSize, 
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
                postFXStack.bufferSize = bufferSize;
                #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
                    if (cameraBufferSettings.dlss.enabled)
                    {
                        postFXStack.bufferSize = new Vector2Int(Screen.width, Screen.height);
                    }
                #endif
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
        CoreUtils.Destroy(motionVectorDebugMaterial);
        CoreUtils.Destroy(materialMotion);
        CoreUtils.Destroy(depthOnlyMaterial);
        CameraDebugger.Cleanup();
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

    private static int m_DepthRTid = Shader.PropertyToID("_CameraDepthTexture");
    private static int m_MotionVectorRTid = Shader.PropertyToID("_CameraMotionVectorsTexture");
    private static RenderTargetIdentifier m_DepthRT = new RenderTargetIdentifier(m_DepthRTid);
    private static RenderTargetIdentifier m_MotionVectorRT = new RenderTargetIdentifier(m_MotionVectorRTid);
    private static readonly ShaderTagId m_PassName = new ShaderTagId("SRP0703_Pass"); //The shader pass tag just for SRP0703

    private Matrix4x4 _NonJitteredVP;
    private Matrix4x4 _PreviousVP;

    static Mesh s_FullscreenMesh = null;
    public static Mesh fullscreenMesh
    {
        get
        {
            if (s_FullscreenMesh != null)
                return s_FullscreenMesh;

            float topV = 1.0f;
            float bottomV = 0.0f;

            s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
            s_FullscreenMesh.SetVertices(new List<Vector3>
            {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f,  1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
                new Vector3(1.0f,  1.0f, 0.0f)
            });

            s_FullscreenMesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0.0f, bottomV),
                new Vector2(0.0f, topV),
                new Vector2(1.0f, bottomV),
                new Vector2(1.0f, topV)
            });

            s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
            s_FullscreenMesh.UploadMeshData(true);
            return s_FullscreenMesh;
        }
    }
    private static int depthBufferBits = 32;

    public void DrawMotionVectors(ScriptableRenderContext context, Camera camera, Material motionVectorMaterial, CullingResults cull)
    {
        CommandBuffer cmdTempId = new CommandBuffer();
        cmdTempId.name = "(" + camera.name + ")" + "Setup TempRT";

        //Depth
        RenderTextureDescriptor depthRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
        depthRTDesc.colorFormat = RenderTextureFormat.Depth;
        depthRTDesc.depthBufferBits = depthBufferBits;
        cmdTempId.GetTemporaryRT(m_DepthRTid, depthRTDesc, FilterMode.Bilinear);


        //MotionVector
        RenderTextureDescriptor motionvectorRTDesc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
        motionvectorRTDesc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat;
        motionvectorRTDesc.depthBufferBits = depthBufferBits;
        //colorRTDesc.sRGB = ;
        motionvectorRTDesc.msaaSamples = 1;
        motionvectorRTDesc.enableRandomWrite = false;
        cmdTempId.GetTemporaryRT(m_MotionVectorRTid, motionvectorRTDesc, FilterMode.Bilinear);
        
        context.ExecuteCommandBuffer(cmdTempId);
        cmdTempId.Release();


        var sortingSettings = new SortingSettings(camera);
        DrawingSettings drawSettingsMotionVector = new DrawingSettings(m_PassName, sortingSettings)
        {
            perObjectData = PerObjectData.MotionVectors,
            overrideMaterial = motionVectorMaterial,
            overrideMaterialPassIndex = 0
        };
        FilteringSettings filterSettingsMotionVector = new FilteringSettings(RenderQueueRange.all)
        {
            excludeMotionVectorObjects = false
        };

        DrawingSettings drawSettingsDepth = new DrawingSettings(m_PassName, sortingSettings)
        {
            //perObjectData = PerObjectData.None,
            overrideMaterial = depthOnlyMaterial,
            overrideMaterialPassIndex = 0,
        };
        FilteringSettings filterSettingsDepth = new FilteringSettings(RenderQueueRange.all);


        //************************** Rendering depth ************************************

        //Set RenderTarget & Camera clear flag
        CommandBuffer cmdDepth = new CommandBuffer();
        cmdDepth.name = "(" + camera.name + ")" + "Depth Clear Flag";
        cmdDepth.SetRenderTarget(m_DepthRT); //Set CameraTarget to the depth texture
        cmdDepth.ClearRenderTarget(true, true, Color.black);
        context.ExecuteCommandBuffer(cmdDepth);
        cmdDepth.Release();

        //Opaque objects
        sortingSettings.criteria = SortingCriteria.CommonOpaque;
        drawSettingsDepth.sortingSettings = sortingSettings;
        filterSettingsDepth.renderQueueRange = RenderQueueRange.opaque;
        RenderObjects("Render Opaque Objects Depth", context, cull, filterSettingsDepth, drawSettingsDepth);


        //To let shader has _CameraDepthTexture
        CommandBuffer cmdDepthTexture = new CommandBuffer();
        cmdDepthTexture.name = "(" + camera.name + ")" + "Depth Texture";
        cmdDepthTexture.SetGlobalTexture(m_DepthRTid, m_DepthRT);
        context.ExecuteCommandBuffer(cmdDepthTexture);
        cmdDepthTexture.Release();


        //************************** Rendering motion vectors ************************************

        //Camera clear flag
        CommandBuffer cmdMotionvector = new CommandBuffer();
        cmdMotionvector.SetRenderTarget(m_MotionVectorRT); //Set CameraTarget to the motion vector texture
        cmdMotionvector.ClearRenderTarget(true, true, Color.black);
        context.ExecuteCommandBuffer(cmdMotionvector);
        cmdMotionvector.Release();

        //Opaque objects
        sortingSettings.criteria = SortingCriteria.CommonOpaque;
        drawSettingsMotionVector.sortingSettings = sortingSettings;
        filterSettingsMotionVector.renderQueueRange = RenderQueueRange.opaque;
        RenderObjects("Render Opaque Objects Motion Vector", context, cull, filterSettingsMotionVector, drawSettingsMotionVector);

        //Camera motion vector
        CommandBuffer cmdCameraMotionVector = new CommandBuffer();
        cmdCameraMotionVector.name = "(" + camera.name + ")" + "Camera MotionVector";
        _NonJitteredVP = camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix;
        cmdCameraMotionVector.SetGlobalMatrix("_CamPrevViewProjMatrix", _PreviousVP);
        cmdCameraMotionVector.SetGlobalMatrix("_CamNonJitteredViewProjMatrix", _NonJitteredVP);
        cmdCameraMotionVector.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        cmdCameraMotionVector.DrawMesh(fullscreenMesh, Matrix4x4.identity, motionVectorMaterial, 0, 1, null); // draw full screen quad to make Camera motion
        cmdCameraMotionVector.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        context.ExecuteCommandBuffer(cmdCameraMotionVector);
        cmdCameraMotionVector.Release();

        //To let shader has MotionVectorTexture
        CommandBuffer cmdMotionVectorTexture = new CommandBuffer();
        cmdMotionVectorTexture.name = "(" + camera.name + ")" + "MotionVector Texture";
        cmdMotionVectorTexture.SetGlobalTexture(m_MotionVectorRTid, m_MotionVectorRT);
        context.ExecuteCommandBuffer(cmdMotionVectorTexture);
        cmdMotionVectorTexture.Release();
    }

    static void RenderObjects(string name, ScriptableRenderContext context, CullingResults cull, FilteringSettings filterSettings, DrawingSettings drawSettings)
    {
        RendererListParams rlp = new RendererListParams(cull, drawSettings, filterSettings);
        RendererList rl = context.CreateRendererList(ref rlp);
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = name;
        cmd.DrawRendererList(rl);
        context.ExecuteCommandBuffer(cmd);
        cmd.Release();
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
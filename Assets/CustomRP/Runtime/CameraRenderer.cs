using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.NVIDIA;
using UnityEngine.Rendering;

namespace NoesisRender
{
    using NoesisRender.ResourcesHolders;
    using NoesisRender.Passes;

    /// <summary>
    /// Main render class
    /// </summary>
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
        Material materialMotion, depthOnlyMaterial, motionVectorDebugMaterial, materialXeGTAO;
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


        // GBuffer textures
        /*private const int amountOfGTempTex = 2;
        RenderTexture[] tempTexs = new RenderTexture[amountOfGTempTex];
        RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[amountOfGTempTex];*/

        Material deferredMat;


        public CameraRenderer(Shader shader, Shader cameraDebuggerShader, Shader cameraMotionShader, Shader depthOnlyShader, Shader motionDebug, Shader deferredShader, Shader xeGTAOApply)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
            materialMotion = CoreUtils.CreateEngineMaterial(cameraMotionShader);
            depthOnlyMaterial = CoreUtils.CreateEngineMaterial(depthOnlyShader);
            motionVectorDebugMaterial = CoreUtils.CreateEngineMaterial(motionDebug);
            materialXeGTAO = CoreUtils.CreateEngineMaterial(xeGTAOApply);
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


            // GBuffer setup
            /*int gBufferDepth = (int)DepthBits.None;
            tempTexs[0] = new RenderTexture(Screen.width, Screen.height, gBufferDepth, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
            tempTexs[0].name = "GBuffer1 (RGB color A metallic)";
            gbufferID[0] = tempTexs[0];
            tempTexs[1] = new RenderTexture(Screen.width, Screen.height, gBufferDepth, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
            tempTexs[1].name = "GBuffer2 R smoothness GB normal A occlustion";
            gbufferID[1] = tempTexs[1];*/

            deferredMat = CoreUtils.CreateEngineMaterial(deferredShader);


            CameraDebugger.Initialize(cameraDebuggerShader);
        }


        public void Render
        (
            RenderGraph renderGraph,
            ScriptableRenderContext context,
            Camera camera, CustomRenderPipelineCamera crpCamera,
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
            /*if (camera.TryGetComponent(out CustomRenderPipelineCamera crpCamera))
            {
                cameraSampler = crpCamera.Sampler;
                cameraSettings = crpCamera.Settings;
            }*/
            if (crpCamera != null)
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
                    bufferSize = GetScaledBufferSize(renderScale, camera);
                    UnityEngine.NVIDIA.OptimalDLSSSettingsData optimalDLSSSettingsData;
                    bool fit = UnityDLSS.UnityDlssCommon.device.GetOptimalSettings((uint)bufferSize.x, (uint)bufferSize.y, cameraBufferSettings.dlss.dlssQuality, out optimalDLSSSettingsData);
                    bufferSize.x = (int)((float)optimalDLSSSettingsData.outRenderWidth * camera.rect.width);
                    bufferSize.y = (int)((float)optimalDLSSSettingsData.outRenderHeight * camera.rect.height);
                    float multJitter = 1.5f; // 2.254698f
                    float relation = Mathf.Clamp01((float)bufferSize.y * multJitter / GetCameraPixelSize(camera).y);
                    cameraBufferSettings.dlss.jitterScale *= relation;
                    cameraBufferSettings.dlss.sharpness = optimalDLSSSettingsData.sharpness;
                    Vector2 minRec = new Vector2(optimalDLSSSettingsData.minWidth, optimalDLSSSettingsData.minHeight);

                    cachedDLSSResolution = bufferSize;
                    cachedDLSSJitterScale = relation;
                    cachedDLSSSharpness = optimalDLSSSettingsData.sharpness;
                }
                else if (cameraBufferSettings.dlss.useOptimalSettings && !cameraBufferSettings.dlss.useDLAA)
                {
                    bufferSize = cachedDLSSResolution;
                    cameraBufferSettings.dlss.jitterScale *= cachedDLSSJitterScale;
                    cameraBufferSettings.dlss.sharpness = cachedDLSSSharpness;
                    //Debug.Log("bufferSize " + cachedDLSSResolution);
                }
                else if (cameraBufferSettings.dlss.useDLAA)
                {
                    renderScale = 1;
                    bufferSize = GetScaledBufferSize(renderScale, camera);
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
                postFXBufferSize = GetCameraPixelSize(camera);
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
                    useColorTexture, useDepthTexture, useHDR, settings.deferredSettings.enabled, bufferSize, camera
                );


                // Now motion Vectors required only dlss
                if (cameraBufferSettings.dlss.enabled)
                {
                    MotionVectorPass.Record(renderGraph, camera, cameraSettings, textures, cameraBufferSettings, materialMotion, cameraSettings.renderingLayerMask, cullingResults);
                }



                if (settings.deferredSettings.enabled)
                {
                    var gbResources = GBufferResources.GetGBResources(camera, bufferSize);
                    var gbufferID = gbResources._getTargets;
                    var gbufferTexs = gbResources._getTextures;
#if UNITY_EDITOR
                    // When switching unity scenes with scene camera active, we can get invalid renderer textures.
                    if (gbufferTexs[0] == null)
                    {
                        GBufferResources._instance.Dispose();
                        gbResources = GBufferResources.GetGBResources(camera, bufferSize);
                        gbufferID = gbResources._getTargets;
                        gbufferTexs = gbResources._getTextures;
                        //EditorApplication.ExecuteMenuItem("Window/General/Game");

                        UnityEditor.EditorUtility.DisplayDialog("Scene camera can be invalid.",
                                        "Renderer textures can be uninitialized. Render loop can be broken there. It can happend after you switch scenes with scene camera."
                                        , "OK");

                        EditorUtility.RequestScriptReload();

                        /*SceneView[] openSceneViews = Resources.FindObjectsOfTypeAll<SceneView>();

                        foreach (SceneView sceneView in openSceneViews)
                        {
                            sceneView.Close();
                        }
                        Debug.Log("Closed all Scene Views.");*/
                    }
#endif
                    GBufferPass.Record(renderGraph, camera, cullingResults, cameraSettings.renderingLayerMask, textures, gbufferID, useLightsPerObject);

                    bool xeGTAOEnabled = XeGTAO.CanRender(settings.xeGTAOsettings, materialXeGTAO) && gbufferTexs.Length > 1 && gbufferTexs[1] != null;

                    TextureDesc whiteDesc = new TextureDesc(1, 1);
                    whiteDesc.clearColor = Color.white;
                    whiteDesc.depthBufferBits = 0;
                    whiteDesc.colorFormat = GraphicsFormat.B8G8R8A8_SRGB;
                    TextureHandle xeHBAO = renderGraph.CreateTexture(whiteDesc);
                    if (xeGTAOEnabled)
                    {
                        var xeGTAOResources = XeGTAOResources.GetGTAOesources(camera, !settings.xeGTAOsettings.HalfRes ? bufferSize : bufferSize / 2);
                        xeHBAO = XeGTAO.Record(renderGraph, camera, in textures, settings.xeGTAOsettings, xeGTAOResources, bufferSize, materialXeGTAO, !settings.deferredSettings.enabled, gbufferTexs[1]);
                    }

                    DeferredPass.Record(renderGraph, camera, cullingResults, textures, ref gbufferTexs, deferredMat, lightResources, cameraSettings.renderingLayerMask, settings.deferredSettings.reflectionCubemap, xeGTAOEnabled, xeHBAO);
                }
                else
                {
                    VisibleGeometryPass.Record
                    (
                        renderGraph, camera,
                        cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask,
                        opaque: true, setTarget: !cameraSettings.renderMotionVectors,
                        textures, lightResources
                    );
                }

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

                if (settings.deferredSettings.enabled)
                {
                    var gbResources = GBufferResources.GetGBResources(camera, bufferSize);
                    var normalBuffer = gbResources.GetNormalBuffer();
                    DecalPass.Record(renderGraph, camera, cullingResults, cameraSettings.renderingLayerMask, textures, settings.decalsSettings, true, normalBuffer);
                }
                else
                {
                    DecalPass.Record(renderGraph, camera, cullingResults, cameraSettings.renderingLayerMask, textures, settings.decalsSettings, false, null);
                }

                VisibleGeometryPass.Record
                (
                    renderGraph, camera,
                    cullingResults, useLightsPerObject, cameraSettings.renderingLayerMask,
                    opaque: false, setTarget: false,
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


        private static Vector2Int GetScaledBufferSize(float renderScale, Camera camera)
        {
            Vector2Int screenSize = GetCameraPixelSize(camera);
            Vector2Int bufferSize = new Vector2Int();
            bufferSize.x = Mathf.Max((int)(screenSize.x * renderScale), 1);
            bufferSize.y = Mathf.Max((int)(screenSize.y * renderScale), 1);
            return bufferSize;
        }

        public static Vector2Int GetCameraPixelSize(Camera camera)
        {
            return Vector2Int.Max(new Vector2Int(camera.pixelWidth, camera.pixelHeight), new Vector2Int(1, 1));
        }


        public void Dispose()
        {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(motionVectorDebugMaterial);
            CoreUtils.Destroy(materialMotion);
            CoreUtils.Destroy(depthOnlyMaterial);
            CoreUtils.Destroy(materialXeGTAO);
            CoreUtils.Destroy(deferredMat);

            /*CoreUtils.Destroy(tempTexs[0]);
            CoreUtils.Destroy(tempTexs[1]);*/
            //Debug.Log("================Global dispose==============");
            GBufferResources._instance.Dispose();
            XeGTAOResources._instance.Dispose();

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
}
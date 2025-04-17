using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering;
using System;
using System.Text;


namespace NoesisRender.Passes
{
    using static NoesisRender.ResourcesHolders.GBufferResources;
    using ResourcesHolders;

    public class DeferredPass
    {
        static readonly ProfilingSampler samplerDeferred = new("Deferred Pass");

        static readonly ShaderTagId[] shaderTagIds =
        {
            new("SRPDefaultUnlit"),
            new("CustomLit")
        };



        CameraRenderer renderer;

        bool useDynamicBatching, useGPUInstancing;

        int renderingLayerMask;
        TextureHandle[] gBufferTexs;
        RenderTexture[] gBuffersTarget;
        TextureHandle depthTex;

        TextureHandle xeGTAOValue;
        bool xeGTAOEnabled;

        TextureHandle colorHandle;
        Camera camera;

        Material deferredMat;

        ComputeBufferHandle tilesBuffer;

        ComputeBufferHandle directionalLightDataBuffer;
        ComputeBufferHandle otherLightDataBuffer;
        TextureHandle directionalAtlas;
        TextureHandle otherAtlas;
        ComputeBufferHandle directionalShadowCascadesBuffer;
        ComputeBufferHandle directionalShadowMatricesBuffer;
        ComputeBufferHandle otherShadowDataBuffer;

        readonly int _vpMatrixInv = Shader.PropertyToID("_vpMatrixInv");
        readonly int _lightsPerObj = Shader.PropertyToID("_LIGHTS_PER_OBJECT");

        readonly int _GbufferTex0 = Shader.PropertyToID("_GBuffer0");
        readonly int _GbufferTex1 = Shader.PropertyToID("_GBuffer1");
        readonly int _GbufferTex2 = Shader.PropertyToID("_GBuffer2");
        readonly int _GbufferTex3 = Shader.PropertyToID("_GBuffer3");

        readonly int _XeGTAOValue = Shader.PropertyToID("_XeGTAOValue");


        readonly int _ReflectinSkybox = Shader.PropertyToID("_BaseRefl");
        readonly int _ReciveShadows = Shader.PropertyToID("_RECEIVE_SHADOWS");

        readonly int _DirectionalShadowAtlas = Shader.PropertyToID("_DirectionalShadowAtlas");
        readonly int _OtherShadowAtlas = Shader.PropertyToID("_OtherShadowAtlas");
        readonly int _DeferredEnvParams = Shader.PropertyToID("_DeferredEnvParams");
        Cubemap reflCubemap;

        private static readonly Mesh triangleMesh = new Mesh
        {
            vertices = new[] {
                new Vector3(0, 0, 0),    // Bottom-left
                new Vector3(1, 0, 0),    // Bottom-right
                new Vector3(0, 1, 0)     // Top-left
            },
            triangles = new[] { 0, 2, 1 },  // Winding order
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up }
        };

        void Render(RenderGraphContext context)
        {
            var cmd = context.cmd;
            cmd.SetGlobalTexture(_GbufferTex0, gBuffersTarget[0]);
            cmd.SetGlobalTexture(_GbufferTex1, gBuffersTarget[1]);
            cmd.SetGlobalTexture(_GbufferTex2, gBuffersTarget[2]);
            cmd.SetGlobalTexture(_GbufferTex3, gBuffersTarget[3]);


            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 vpMatrix = projMatrix * viewMatrix;
            Matrix4x4 vpMatrixInv = vpMatrix.inverse;
            cmd.SetGlobalMatrix(_vpMatrixInv, vpMatrixInv);
            Vector4 _DefEnvParams = new Vector4();
            var reflIntensity = RenderSettings.reflectionIntensity;
            var envLightingInt = RenderSettings.ambientIntensity;
            _DefEnvParams.x = reflIntensity * reflIntensity;
            _DefEnvParams.y = envLightingInt;
            _DefEnvParams.z = 1;
            _DefEnvParams.w = -5;
            cmd.SetGlobalVector(_DeferredEnvParams, _DefEnvParams);
            deferredMat.SetTexture(_ReflectinSkybox, reflCubemap);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();

            LocalKeyword useXeGTAO = new LocalKeyword(deferredMat.shader, "_AO");
            deferredMat.SetKeyword(useXeGTAO, xeGTAOEnabled);

            if (xeGTAOEnabled)
            {
                cmd.SetGlobalTexture(_XeGTAOValue, xeGTAOValue);
            }

            cmd.SetRenderTarget
            (
                colorHandle, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                depthTex, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
            );
            cmd.DrawProcedural(Matrix4x4.identity, deferredMat, 0, MeshTopology.Triangles, 3);
            //cmd.DrawMesh(triangleMesh, Matrix4x4.identity, deferredMat, 0, 0);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record
        (
            RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
            in CameraRendererTextures textures, ref RenderTexture[] renderTargets, Material deferredMat,
            in LightResources lightData, int renderingLayerMask, Cubemap reflCubemap, bool xeGTAOEnabled, TextureHandle xeGTAOValue
        )
        {
            if (deferredMat == null || renderTargets == null || renderTargets.Length < GBufferTextures.amountOfGBuffers || renderTargets[0] == null || !renderTargets[0].IsCreated())
            {
                //Debug.Log("Deferred invalid ");
                StringBuilder debugMessage = new StringBuilder("Deferred rendering invalid - ");

                if (deferredMat == null)
                {
                    debugMessage.Append("Deferred material is null. ");
                }

                if (renderTargets == null)
                {
                    debugMessage.Append("Render targets array is null. ");
                }
                else if (renderTargets.Length < GBufferTextures.amountOfGBuffers)
                {
                    debugMessage.Append($"Render targets array length ({renderTargets.Length}) is less than required GBuffers ({GBufferTextures.amountOfGBuffers}). ");
                }
                else if (renderTargets[0] == null)
                {
                    debugMessage.Append("First render target is null. ");
                }
                else if (!renderTargets[0].IsCreated())
                {
                    debugMessage.Append("First render target texture is not created. ");
                }

                Debug.Log(debugMessage.ToString());
                return;
            }
            ProfilingSampler sampler = samplerDeferred;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass
                (sampler.name, out DeferredPass pass, sampler);
            pass.deferredMat = deferredMat;


            // Readwrite not change renderTarget. Also allow RenderBufferLoadAction.DontCare
            pass.colorHandle = builder.ReadWriteTexture(textures.colorAttachment);
            //builder.ReadWriteTexture(textures.depthAttachment);
            //pass.colorTex = textures.colorAttachment;
            pass.depthTex = builder.ReadWriteTexture(textures.depthAttachment);
            pass.gBuffersTarget = renderTargets;
            pass.camera = camera;
            pass.reflCubemap = reflCubemap;


            bool tilesBufferValid = lightData.tilesBuffer.IsValid();
            bool useLightsPerObject = !tilesBufferValid;
            if (tilesBufferValid)
            {
                pass.tilesBuffer = builder.ReadComputeBuffer(lightData.tilesBuffer);
            }

            var list = builder.UseRendererList
            (
                renderGraph.CreateRendererList
                (
                    new RendererListDesc(shaderTagIds, cullingResults, camera)
                    {
                        sortingCriteria = SortingCriteria.CommonOpaque,
                        rendererConfiguration =
                            PerObjectData.ReflectionProbes |
                            PerObjectData.Lightmaps |
                            PerObjectData.ShadowMask |
                            PerObjectData.LightProbe |
                            PerObjectData.OcclusionProbe |
                            PerObjectData.LightProbeProxyVolume |
                            PerObjectData.OcclusionProbeProxyVolume |
                            (useLightsPerObject ?
                                PerObjectData.LightData | PerObjectData.LightIndices :
                                PerObjectData.None),
                        renderQueueRange = RenderQueueRange.opaque,
                        renderingLayerMask = (uint)renderingLayerMask
                    }
                )
            );

            if (xeGTAOEnabled)
            {
                pass.xeGTAOValue = builder.ReadTexture(xeGTAOValue);
            }
            pass.xeGTAOEnabled = xeGTAOEnabled;


            pass.directionalLightDataBuffer = builder.ReadComputeBuffer(lightData.directionalLightDataBuffer);
            pass.otherLightDataBuffer = builder.ReadComputeBuffer(lightData.otherLightDataBuffer);
            pass.directionalAtlas = builder.ReadTexture(lightData.shadowResources.directionalAtlas);
            pass.otherAtlas = builder.ReadTexture(lightData.shadowResources.otherAtlas);
            pass.directionalShadowCascadesBuffer = builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowCascadesBuffer);
            pass.directionalShadowMatricesBuffer = builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowMatricesBuffer);
            pass.otherShadowDataBuffer = builder.ReadComputeBuffer(lightData.shadowResources.otherShadowDataBuffer);

            builder.AllowRendererListCulling(false);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<DeferredPass>(static (pass, context) => pass.Render(context));
        }
    }

}

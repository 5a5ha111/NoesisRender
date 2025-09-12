using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace NoesisRender.Passes
{
    using NoesisRender.ResourcesHolders;

    public class VisualEffectGarphPass
    {
        static readonly ProfilingSampler samplerVFX = new("Visual Effect Graph Pass");


        CameraRenderer renderer;

        bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
        bool setTarget = false;

        TextureHandle colorTex, depthTex;

        Camera cam;
        VFXCameraXRSettings cameraXRSettings;
        CullingResults results;

        void Render(RenderGraphContext context)
        {
            if (setTarget)
            {
                context.cmd.SetRenderTarget
                (
                    colorTex, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                    depthTex, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                );
            }

            VFXManager.ProcessCameraCommand(cam, context.cmd, cameraXRSettings, results);

            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record
        (
            RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
            bool setTarget,
            in CameraRendererTextures textures,
            in LightResources lightData
        )
        {
            ProfilingSampler sampler = samplerVFX;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass
                (sampler.name, out VisualEffectGarphPass pass, sampler);
            


            if (lightData.tilesBuffer.IsValid())
            {
                builder.ReadComputeBuffer(lightData.tilesBuffer);
            }

            pass.cam = camera;
            pass.results = cullingResults;
            pass.cameraXRSettings = new VFXCameraXRSettings();


            // Readwrite not change renderTarget. Also allow RenderBufferLoadAction.DontCare
            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);
            pass.colorTex = textures.colorAttachment;
            pass.depthTex = textures.depthAttachment;
            pass.setTarget = setTarget;

            if (textures.colorCopy.IsValid())
            {
                builder.ReadTexture(textures.colorCopy);
            }
            if (textures.depthCopy.IsValid())
            {
                builder.ReadTexture(textures.depthCopy);
            }

            // Indicate that this resources is needed
            builder.ReadComputeBuffer(lightData.directionalLightDataBuffer);
            builder.ReadComputeBuffer(lightData.otherLightDataBuffer);
            builder.ReadTexture(lightData.shadowResources.directionalAtlas);
            builder.ReadTexture(lightData.shadowResources.otherAtlas);
            builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowCascadesBuffer);
            builder.ReadComputeBuffer(lightData.shadowResources.directionalShadowMatricesBuffer);
            builder.ReadComputeBuffer(lightData.shadowResources.otherShadowDataBuffer);

            builder.SetRenderFunc<VisualEffectGarphPass>(static (pass, context) => pass.Render(context));
        }
    }
}

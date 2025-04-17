using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering;
using NoesisRender.ResourcesHolders;


namespace NoesisRender.Passes
{
    public class DecalPass
    {
        static readonly ProfilingSampler samplerDecall = new("Decal Pass");

        static readonly ShaderTagId[] shaderTagIds =
        {
            new("DecallPass")
        };

        RendererListHandle list;
        CameraRenderer renderer;
        DecalsSettings decalsSettings;

        bool useDynamicBatching, useGPUInstancing, deferred;
        Camera camera;
        RenderTexture normalBuffer;

        readonly static int _NormalReconstructionMatrix = Shader.PropertyToID("_NormalReconstructionMatrix");
        readonly static int _NormalBuffer = Shader.PropertyToID("_GBuffer1");

        GlobalKeyword _DeferredLightning = GlobalKeyword.Create("_DEFFERED_LIGHTNING");
        GlobalKeyword _NormalRecTap3 = GlobalKeyword.Create("_TAP3");
        GlobalKeyword _NormalRecTap4 = GlobalKeyword.Create("_TAP4");
        GlobalKeyword _NormalRecImproved = GlobalKeyword.Create("_IMPROVED");
        GlobalKeyword _NormalRecAccurate = GlobalKeyword.Create("_ACCURATE");


        void Render(RenderGraphContext context)
        {
            SetDecalQulity(decalsSettings, deferred, context.cmd);
            if (deferred)
            {
                context.cmd.SetGlobalTexture(_NormalBuffer, normalBuffer);
            }
            else
            {
                Matrix4x4 matrix = camera.worldToCameraMatrix;
                context.cmd.SetGlobalMatrix(_NormalReconstructionMatrix, matrix);
            }
            context.cmd.DrawRendererList(list);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record
        (
            RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
            int renderingLayerMask,
            in CameraRendererTextures textures, DecalsSettings decalsSettings, bool deferred, RenderTexture normalBuffer
        )
        {
            ProfilingSampler sampler = samplerDecall;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass(sampler.name, out DecalPass pass, sampler);

            pass.list = builder.UseRendererList
            (
                renderGraph.CreateRendererList
                (
                    new RendererListDesc(shaderTagIds, cullingResults, camera)
                    {
                        //sortingCriteria = SortingCriteria.CommonOpaque,
                        renderQueueRange = RenderQueueRange.all,
                        renderingLayerMask = (uint)renderingLayerMask
                    }
                )
            );

            pass.camera = camera;
            pass.decalsSettings = decalsSettings;
            pass.deferred = deferred;
            pass.normalBuffer = normalBuffer;

            // Readwrite not change renderTarget. Also allow RenderBufferLoadAction.DontCare
            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadTexture(textures.depthAttachment);
            if (textures.colorCopy.IsValid())
            {
                builder.ReadTexture(textures.colorCopy);
            }
            if (textures.depthCopy.IsValid())
            {
                builder.ReadTexture(textures.depthCopy);
            }


            builder.SetRenderFunc<DecalPass>(static (pass, context) => pass.Render(context));
        }


        void SetDecalQulity(DecalsSettings decalsSettings, bool usedeferred, CommandBuffer cmd)
        {
            if (usedeferred)
            {
                cmd.SetKeyword(_DeferredLightning, true);
            }
            else
            {
                cmd.SetKeyword(_DeferredLightning, false);
                switch (decalsSettings.forwardNormalReconstructQuality)
                {
                    case DecalsSettings.DecalForwardNormalQuality.DDX:
                        cmd.SetKeyword(_NormalRecTap3, false);
                        cmd.SetKeyword(_NormalRecTap4, false);
                        cmd.SetKeyword(_NormalRecImproved, false);
                        cmd.SetKeyword(_NormalRecAccurate, false);
                        break;
                    case DecalsSettings.DecalForwardNormalQuality.Tap3:
                        cmd.SetKeyword(_NormalRecTap3, true);
                        cmd.SetKeyword(_NormalRecTap4, false);
                        cmd.SetKeyword(_NormalRecImproved, false);
                        cmd.SetKeyword(_NormalRecAccurate, false);
                        break;
                    case DecalsSettings.DecalForwardNormalQuality.Tap4:
                        cmd.SetKeyword(_NormalRecTap3, false);
                        cmd.SetKeyword(_NormalRecTap4, true);
                        cmd.SetKeyword(_NormalRecImproved, false);
                        cmd.SetKeyword(_NormalRecAccurate, false);
                        break;
                    case DecalsSettings.DecalForwardNormalQuality._IMPROVED:
                        cmd.SetKeyword(_NormalRecTap3, false);
                        cmd.SetKeyword(_NormalRecTap4, false);
                        cmd.SetKeyword(_NormalRecImproved, true);
                        cmd.SetKeyword(_NormalRecAccurate, false);
                        break;
                    case DecalsSettings.DecalForwardNormalQuality._ACCURATE:
                        cmd.SetKeyword(_NormalRecTap3, false);
                        cmd.SetKeyword(_NormalRecTap4, false);
                        cmd.SetKeyword(_NormalRecImproved, false);
                        cmd.SetKeyword(_NormalRecAccurate, true);
                        break;
                    default:
                        cmd.SetKeyword(_NormalRecTap3, false);
                        cmd.SetKeyword(_NormalRecTap4, false);
                        cmd.SetKeyword(_NormalRecImproved, false);
                        cmd.SetKeyword(_NormalRecAccurate, false);
                        break;
                }
            }

        }

    }

}

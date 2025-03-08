using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.NVIDIA;
using UnityEngine.Rendering;
using UnityEngine.PlayerLoop;
using System;




#if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
    using NVIDIA = UnityEngine.NVIDIA;
    using static UnityDLSS.UnityDlssCommon;
#endif

public class DLSSPass 
{

    #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
    static readonly ProfilingSampler sampler = new("DLSS");
    TextureHandle colorSRc, depthSource, motionSource, colorCopy;
    Camera camera;
    CameraBufferSettings.DLSS_Settings settings;
    Vector2Int attachmentSize;

    ViewState state;
    DlssViewData dlssData;
    static Matrix4x4 jitteredMatrix;
    static Matrix4x4 prevJitteredMatrix;
    int taaFrameIndex;

    void Render(RenderGraphContext context)
    {
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();

        //state.UpdateViewState(dlssData, context.cmd);
        //Debug.Log("" + (state == null) + " " + (dlssData.inputRes == new DlssViewData().inputRes));
        if (state == null)
        {
            state = new ViewState(device);
        }
        var cam = camera;
        var resolution = new UnityDLSS.UnityDlssCommon.Resolution();
        var resolutionOut = new UnityDLSS.UnityDlssCommon.Resolution();
        resolution.width = Mathf.Max(attachmentSize.x, 1);
        resolution.height = Mathf.Max(attachmentSize.y, 1);
        Vector2Int screenSize = CameraRenderer.GetCameraPixelSize(camera);
        resolutionOut.width = screenSize.x;
        resolutionOut.height = screenSize.y;
        dlssData.inputRes = resolution;
        dlssData.outputRes = resolutionOut;
        context.cmd.Blit(colorSRc, colorCopy);

        if (settings.enabled)
        {
            cam.ResetProjectionMatrix();
            context.cmd.SetProjectionMatrix(cam.projectionMatrix);
            context.cmd.SetViewport(camera.pixelRect);
            if (settings.useJitter)
            {
                cam.nonJitteredProjectionMatrix = cam.projectionMatrix;
                if (true)
                {
                    prevJitteredMatrix = jitteredMatrix;
                    jitteredMatrix = GetJitteredProjectionMatrix(settings.jitterScale, settings.jitterRand, cam.projectionMatrix, attachmentSize.x, attachmentSize.y, cam, taaFrameIndex);

                    const int kMaxSampleCount = 8;
                    if (++taaFrameIndex >= kMaxSampleCount)
                        taaFrameIndex = 0;
                }

                cam.projectionMatrix = jitteredMatrix;
                context.cmd.SetProjectionMatrix(jitteredMatrix);
                context.cmd.SetViewport(camera.pixelRect);
            }

            state.UpdateViewState(dlssData, context.cmd);
            state.SubmitDlssCommands(colorSRc, depthSource, motionSource, null, colorCopy, context.cmd, settings.sharpness);
            
            context.cmd.SetRenderTarget(colorCopy);
            //context.cmd.Blit(colorCopy, BuiltinRenderTextureType.CameraTarget);
            context.cmd.Blit(colorCopy, colorSRc);

            //cam.ResetProjectionMatrix();
        }

        //Cleanup(context.cmd);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }
    #endif

    public static void Record(RenderGraph renderGraph, Camera camera, in CameraRendererTextures textures, CameraBufferSettings settings, Vector2Int attachmentSize, bool useHDR)
    {
        #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE

            if (IsDlssSupported() == false)
            {
                return;
            }
            if (device == null || !settings.dlss.enabled)
            {
                return;
            }
            using RenderGraphBuilder builder = renderGraph.AddRenderPass
            (
                sampler.name, out DLSSPass pass, sampler
            );
            pass.camera = camera;
            pass.colorSRc = builder.ReadWriteTexture(textures.colorAttachment);
            pass.depthSource = builder.ReadTexture(textures.depthAttachment);
            pass.motionSource = builder.ReadTexture(textures.motionVectorsTexture);


            pass.settings = settings.dlss;
            pass.attachmentSize = attachmentSize;


            if (pass.state == null)
            {
                pass.state = new ViewState(device);
            }

            Vector2Int screenSize = CameraRenderer.GetCameraPixelSize(camera);
            var desc = new TextureDesc(screenSize.x, screenSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(useHDR ? DefaultFormat.HDR : DefaultFormat.LDR),
                name = "DLSS color Attachment",
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
            };
            pass.colorCopy = builder.WriteTexture(renderGraph.CreateTexture(desc));


            builder.SetRenderFunc<DLSSPass>(static (pass, context) => pass.Render(context));

        #else
            return;
        #endif
    }

    #if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
        void Cleanup(CommandBuffer commandBuffer)
        {
            if (commandBuffer != null)
            {
                state.Cleanup(commandBuffer);
                //cam.RemoveCommandBuffer(prevRenderPos, commandBuffer);
                //commandBuffer.Release();

                //commandBuffer = null;
            }

            //RestoreMipBias();
        }
    #endif

}

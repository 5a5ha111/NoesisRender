using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

using static PostFXStack;
using static CameraBufferSettings;
using System;

public class PostFXPass
{
    static readonly ProfilingSampler
        groupSampler = new("Post FX"),
        finalSampler = new("Final Post FX");

    static readonly int
        copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
        fxaaConfigId = Shader.PropertyToID("_FXAAConfig");

    static readonly GlobalKeyword
        fxaaLowKeyword = GlobalKeyword.Create("FXAA_QUALITY_LOW"),
        fxaaMediumKeyword = GlobalKeyword.Create("FXAA_QUALITY_MEDIUM");

    static readonly GraphicsFormat colorFormat =
        SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);


    PostFXStack stack;
    TextureHandle colorAttachment;
    bool keepAlpha;
    enum ScaleMode { None, Linear, Bicubic }
    ScaleMode scaleMode;
    TextureHandle colorSource, colorGradingResult, scaledResult, motionSource;
    Material motionDebug;

#if UNITY_EDITOR
    CameraType cameraType;
#endif


    void ConfigureFXAA(CommandBuffer buffer)
    {
        // If both keywords false, shader use high quality
        CameraBufferSettings.FXAA fxaa = stack.BufferSettings.fxaa;
        buffer.SetKeyword(fxaaLowKeyword, fxaa.quality == CameraBufferSettings.FXAA.Quality.Low);
        buffer.SetKeyword(fxaaMediumKeyword, fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium);
        buffer.SetGlobalVector
        (
            fxaaConfigId, new Vector4
            (
                stack.fxaa.fixedThreshold,  stack.fxaa.relativeThreshold, stack.fxaa.subpixelBlending
            )
        );
    }



    void Render(RenderGraphContext context)
    {
        CommandBuffer buffer = context.cmd;
        buffer.SetGlobalFloat(finalSrcBlendId, 1f);
        buffer.SetGlobalFloat(finalDstBlendId, 0f);


        stack.ConfigureColorAdjustments(buffer);
        stack.ConfigureWhiteBalance(buffer);
        stack.ConfigureSplitToning(buffer);
        stack.ConfigureChannelMixer(buffer);
        stack.ConfigureShadowsMidtonesHighlights(buffer);
#if UNITY_EDITOR
        stack.ConfigureDither(buffer, cameraType);
#else
        stack.ConfigureDither(buffer);
#endif


        RenderTargetIdentifier finalSource;
        Pass finalPass;
        if (stack.BufferSettings.fxaa.enabled)
        {
            finalSource = colorGradingResult;
            finalPass = keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma;
            ConfigureFXAA(buffer);
            stack.Draw(buffer, colorSource, finalSource, keepAlpha ?
                Pass.ApplyLut : Pass.ApplyLutWithLuma);
        }
        else
        {
            finalSource = colorSource;
            finalPass = Pass.ApplyLut;
        }

        if (scaleMode == ScaleMode.None)
        {
            stack.DrawFinal(buffer, finalSource, finalPass);
        }
        else
        {
            stack.Draw(buffer, finalSource, scaledResult, finalPass);
            buffer.SetGlobalFloat(copyBicubicId,
                scaleMode == ScaleMode.Bicubic ? 1f : 0f);
            stack.DrawFinal(buffer, scaledResult, Pass.FinalRescale);
        }
        //stack.DrawFinal(buffer, motionSource, Pass.Copy);
        //buffer.SetGlobalTexture("_CameraMotionVectorsTexture", motionSource);
        //buffer.Blit(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget, motionDebug);
        context.renderContext.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public static void Record
    (
        RenderGraph renderGraph,
        PostFXStack stack,
        int colorLUTResolution,
        bool keepAlpha,
        in CameraRendererTextures textures, Material motionDebug
    )
    {
        if (stack.settings.Material == null)
        {
            return;
        }


        using var _ = new RenderGraphProfilingScope(renderGraph, groupSampler);

        TextureHandle colorSource = BloomPass.Record(renderGraph, stack, textures);
        TextureHandle colorLUT = ColorLUTPass.Record(renderGraph, stack, colorLUTResolution);

        using RenderGraphBuilder builder = renderGraph.AddRenderPass
        (
            finalSampler.name, out PostFXPass pass, finalSampler
        );
        pass.keepAlpha = keepAlpha;
        pass.stack = stack;
        pass.colorSource = builder.ReadTexture(colorSource);
        //pass.motionSource = builder.ReadTexture(textures.motionVectorsTexture);
        //pass.motionSource = builder.ReadTexture(textures.motionVectorDepth);
        pass.motionDebug = motionDebug;
        builder.ReadTexture(colorLUT);

        if (stack.bufferSize.x == stack.camera.pixelWidth)
        {
            pass.scaleMode = ScaleMode.None;
        }
        else
        {
            pass.scaleMode =
                stack.BufferSettings.bicubicRescaling ==
                CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                stack.BufferSettings.bicubicRescaling ==
                CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                stack.bufferSize.x < stack.camera.pixelWidth ?
                ScaleMode.Bicubic : ScaleMode.Linear;
        }

        bool applyFXAA = stack.BufferSettings.fxaa.enabled;
        if (applyFXAA || pass.scaleMode != ScaleMode.None)
        {
            var desc = new TextureDesc(stack.bufferSize.x, stack.bufferSize.y)
            {
                colorFormat = colorFormat
            };
            if (applyFXAA)
            {
                desc.name = "Color Grading Result";
                pass.colorGradingResult = builder.CreateTransientTexture(desc);
            }
            if (pass.scaleMode != ScaleMode.None)
            {
                desc.name = "Scaled Result";
                pass.scaledResult = builder.CreateTransientTexture(desc);
            }
        }

#if UNITY_EDITOR
        pass.cameraType = stack.camera.cameraType;
#endif

        builder.SetRenderFunc<PostFXPass>(static (pass, context) => pass.Render(context));
    }
}
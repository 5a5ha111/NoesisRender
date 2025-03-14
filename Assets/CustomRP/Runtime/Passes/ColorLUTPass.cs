using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

using static PostFXSettings;
using static PostFXStack;

public class ColorLUTPass
{
    static readonly ProfilingSampler sampler = new("Color LUT");

    static readonly int
        colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),
        colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters"),
        colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC"),
        colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
        colorFilterId = Shader.PropertyToID("_ColorFilter"),
        whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
        splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows"),
        splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights"),
        channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
        channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
        channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
        smhShadowsId = Shader.PropertyToID("_SMHShadows"),
        smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
        smhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
        smhRangeId = Shader.PropertyToID("_SMHRange");

    static readonly GraphicsFormat colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);


    PostFXStack stack;
    int colorLUTResolution;
    TextureHandle colorLUT;

    void ConfigureColorAdjustments(CommandBuffer buffer, PostFXSettings settings)
    {
        ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
        buffer.SetGlobalVector
        (
            colorAdjustmentsId, new Vector4
            (
                Mathf.Pow(2f, colorAdjustments.postExposure),
                colorAdjustments.contrast * 0.01f + 1f,
                colorAdjustments.hueShift * (1f / 360f),
                colorAdjustments.saturation * 0.01f + 1f
            )
        );
        buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear);
    }

    void ConfigureWhiteBalance(CommandBuffer buffer, PostFXSettings settings)
    {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        buffer.SetGlobalVector
        (
            whiteBalanceId,
            ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature, whiteBalance.tint)
        );
    }

    void ConfigureSplitToning(CommandBuffer buffer, PostFXSettings settings)
    {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsId, splitColor);
        buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
    }

    void ConfigureChannelMixer(CommandBuffer buffer, PostFXSettings settings)
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
    }

    void ConfigureShadowsMidtonesHighlights(CommandBuffer buffer, PostFXSettings settings)
    {
        ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
        buffer.SetGlobalVector
        (
            smhRangeId, new Vector4
            (
                smh.shadowsStart,
                smh.shadowsEnd,
                smh.highlightsStart,
                smh.highLightsEnd
            )
        );
    }


    void Render(RenderGraphContext context)
    {
        PostFXSettings settings = stack.settings;
        CommandBuffer buffer = context.cmd;
        ConfigureColorAdjustments(buffer, settings);
        ConfigureWhiteBalance(buffer, settings);
        ConfigureSplitToning(buffer, settings);
        ConfigureChannelMixer(buffer, settings);
        ConfigureShadowsMidtonesHighlights(buffer, settings);

        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        buffer.SetGlobalVector
        (
            colorGradingLUTParametersId, new Vector4
            (
                lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
            )
        );

        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = Pass.ToneMappingNone + (int)mode;
        buffer.SetGlobalFloat
        (
            colorGradingLUTInLogId, stack.BufferSettings.allowHDR && pass != Pass.ToneMappingNone ? 1f : 0f
        );
        stack.Draw(buffer, colorLUT, pass);
        buffer.SetGlobalVector
        (
            colorGradingLUTParametersId, new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f)
        );
        buffer.SetGlobalTexture(colorGradingLUTId, colorLUT);
    }

    public static TextureHandle Record
    (
        RenderGraph renderGraph,PostFXStack stack,int colorLUTResolution
    )
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass
        (
            sampler.name, out ColorLUTPass pass, sampler
        );
        pass.stack = stack;
        pass.colorLUTResolution = colorLUTResolution;

        int lutHeight = colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        var desc = new TextureDesc(lutWidth, lutHeight)
        {
            colorFormat = colorFormat,
            name = "Color LUT",
            depthBufferBits = 0
        };
        pass.colorLUT = builder.WriteTexture(renderGraph.CreateTexture(desc));
        builder.SetRenderFunc<ColorLUTPass>(static (pass, context) => pass.Render(context));
        return pass.colorLUT;
    }

}

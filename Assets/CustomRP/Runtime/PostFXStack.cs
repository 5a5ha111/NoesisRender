using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


namespace NoesisRender
{
    using static CameraSettings;
    using static CustomRenderPipelineAsset;
    using static PostFXSettings;

    public partial class PostFXStack
    {

        /// Use Shader "Hidden/Custom RP/Post FX Stack" 

        public enum Pass
        {
            Copy,
            BloomHorizontal,
            BloomVertical,
            BloomAdd,
            BloomScatter,
            BloomPrefilter,
            BloomPrefilterFireflies,
            BloomScatterFinal,

            ToneMappingNone,
            ToneMappingNeutral,
            ToneMappingReinhard,
            ToneMappingACES,
            ToneMappingGranTurismo,
            ToneMappingUncharted,

            /// <summary>
            /// If there is renderScale == 1, it is a final pass
            /// </summary>
            ApplyLut,
            /// <summary>
            /// Apply lut and store luma in alpha channel
            /// </summary>
            ApplyLutWithLuma,
            /// <summary>
            /// If there is renderscale != 1, it is a rescale pass.
            /// </summary>
            FinalRescale,

            FXAA,
            /// <summary>
            /// Apply fxaa and use luma stored in alpha
            /// </summary>
            FXAAWithLuma,
        }

        const string bufferName = "Post FX";
        const string ditherKeyword = "_DITHER";
        const string ditherHQKeyword = "_DITHER_HIGH_QUALITY";
        const int maxBloomPyramidLevels = 16;
        static Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);

        public static readonly int
            bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
            bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
            bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
            fxSourceId = Shader.PropertyToID("_PostFXSource"),
            fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
            bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
            bloomResultId = Shader.PropertyToID("_BloomResult");


        int bloomPyramidId;

        int
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


        public static readonly int
            copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
            colorGradingResultId = Shader.PropertyToID("_ColorGradingResult"),
            finalResultId = Shader.PropertyToID("_FinalResult"),
            finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
            finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");

        int fxaaConfigId = Shader.PropertyToID("_FXAAConfig");
        const string
            fxaaQualityLowKeyword = "FXAA_QUALITY_LOW",
            fxaaQualityMediumKeyword = "FXAA_QUALITY_MEDIUM";


        // --- Moved to RenderGrah ---
        /*CommandBuffer buffer = new CommandBuffer
        {
            name = bufferName
        };
        ScriptableRenderContext context;*/
        CommandBuffer buffer;

        public Camera camera;
        public CameraBufferSettings BufferSettings { get; set; }

        public Vector2Int bufferSize;
        CameraBufferSettings.BicubicRescalingMode bicubicRescaling;
        public CameraBufferSettings.FXAA fxaa;


        public PostFXSettings settings;
        bool keepAlpha, useHDR;
        int colorLUTResolution;
        public CameraSettings.FinalBlendMode finalBlendMode;


        public bool IsActive => settings != null && settings.Enabled;


        public PostFXStack()
        {
            bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
            for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
            {
                Shader.PropertyToID("_BloomPyramid" + i);
            }
        }


        public void Setup
        (
            /*ScriptableRenderContext context,*/ Camera camera, Vector2Int bufferSize,
            CameraBufferSettings.BicubicRescalingMode bicubicRescaling,
            CameraBufferSettings.FXAA fxaa,
            PostFXSettings settings,
            bool keepAlpha, bool useHDR, int colorLUTResolution,
            CameraSettings.FinalBlendMode finalBlendMode
        )
        {
            //this.context = context;
            this.camera = camera;
            this.bufferSize = bufferSize;
            this.bicubicRescaling = bicubicRescaling;
            this.fxaa = fxaa;
            this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
            this.useHDR = useHDR;
            this.keepAlpha = keepAlpha;
            this.colorLUTResolution = colorLUTResolution;
            this.finalBlendMode = finalBlendMode;
            ApplySceneViewState();
        }

        public void Render(RenderGraphContext context, int sourceId)
        {
            //Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy); // copy
            buffer = context.cmd;
            if (settings.Enabled && settings.Material != null)
            {
                if (DoBloom(sourceId))
                {
                    DoPostFXMain(bloomResultId);
                    buffer.ReleaseTemporaryRT(bloomResultId);
                }
                else
                {
                    DoPostFXMain(sourceId);
                }
            }
            else
            {
                buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
            }
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }
        /// <summary>
        /// Render Graph overload
        /// </summary>
        /// <param name="context"></param>
        /// <param name="sourceId"></param>
        public void Render(RenderGraphContext context, TextureHandle sourceId)
        {
            //Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy); // copy
            buffer = context.cmd;
            if (settings.Enabled && settings.Material != null)
            {
                if (DoBloom(sourceId))
                {
                    DoPostFXMain(bloomResultId);
                    buffer.ReleaseTemporaryRT(bloomResultId);
                }
                else
                {
                    DoPostFXMain(sourceId);
                }
            }
            else
            {
                buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
            }
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
        {
            if (settings.Material == null)
            {
                buffer.Blit(from, BuiltinRenderTextureType.CameraTarget);
                return;
            }

            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
        }

        public void Draw(CommandBuffer buffer, RenderTargetIdentifier to, Pass pass)
        {
            buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass,
                MeshTopology.Triangles, 3);
        }
        public void Draw
        (
            CommandBuffer buffer,
            RenderTargetIdentifier from,
            RenderTargetIdentifier to,
            Pass pass
        )
        {
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
        }



        bool DoBloom(int sourceId)
        {
            // Setup & Global Parameters
            PostFXSettings.BloomSettings bloom = settings.Bloom;
            buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);

            // Determine Dimensions of color buffer
            int width, height;
            if (bloom.ignoreRenderScale)
            {
                width = camera.pixelWidth / 2;
                height = camera.pixelHeight / 2;
            }
            else
            {
                width = bufferSize.x / 2;
                height = bufferSize.y / 2;
            }

            // Early exit if bloom effect should not be applied.
            if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
                height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
            {
                return false;
            }

            buffer.BeginSample("Bloom");

            // Setup Threshold & Intensity
            Vector4 threshold;
            threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
            threshold.y = threshold.x * bloom.thresholdKnee;
            threshold.z = 2f * threshold.y;
            threshold.w = 0.25f / (threshold.y + 0.00001f);
            threshold.y -= threshold.x;
            buffer.SetGlobalVector(bloomThresholdId, threshold);
            buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);

            // Prefilter Pass
            RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
            Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);

            // Prepare dimensions for pyramid creation.
            width /= 2;
            height /= 2;
            int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;

            // Pyramid Generation (Downsampling)
            int i;
            for (i = 0; i < bloom.maxIterations; i++)
            {
                if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
                {
                    break;
                }
                int midId = toId - 1;
                buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
                buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
                Draw(fromId, midId, Pass.BloomHorizontal);
                Draw(midId, toId, Pass.BloomVertical);
                fromId = toId;
                toId += 2;
                width /= 2;
                height /= 2;
            }


            // Additive and scatter mode specifics
            Pass combinePass, finalPass;
            float finalIntensity;
            if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
            {
                combinePass = finalPass = Pass.BloomAdd;
                buffer.SetGlobalFloat(bloomIntensityId, 1f);
                finalIntensity = bloom.intensity;
            }
            else
            {
                combinePass = Pass.BloomScatter;
                finalPass = Pass.BloomScatterFinal;
                buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
                finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
            }

            // Pyramid Combination
            if (i > 1)
            {
                buffer.ReleaseTemporaryRT(fromId - 1);
                toId -= 5;

                for (i -= 1; i > 0; i--)
                {
                    buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                    Draw(fromId, toId, combinePass);
                    buffer.ReleaseTemporaryRT(fromId);
                    buffer.ReleaseTemporaryRT(toId + 1);
                    fromId = toId;
                    toId -= 2;
                }
            }
            else
            {
                buffer.ReleaseTemporaryRT(bloomPyramidId);
            }
            buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);


            // Final Composition
            buffer.SetGlobalTexture(fxSource2Id, sourceId);
            buffer.SetGlobalTexture(fxSource2Id, sourceId);
            buffer.GetTemporaryRT(bloomResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
            Draw(fromId, bloomResultId, finalPass);
            buffer.ReleaseTemporaryRT(fromId);
            buffer.ReleaseTemporaryRT(bloomPrefilterId);

            buffer.EndSample("Bloom");
            return true;
        }
        bool DoBloom(RenderTargetIdentifier sourceId)
        {
            // Setup & Global Parameters
            PostFXSettings.BloomSettings bloom = settings.Bloom;
            buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);

            // Determine Dimensions of color buffer
            int width, height;
            if (bloom.ignoreRenderScale)
            {
                width = camera.pixelWidth / 2;
                height = camera.pixelHeight / 2;
            }
            else
            {
                width = bufferSize.x / 2;
                height = bufferSize.y / 2;
            }

            // Early exit if bloom effect should not be applied.
            if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
                height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
            {
                return false;
            }

            buffer.BeginSample("Bloom");

            // Setup Threshold & Intensity
            Vector4 threshold;
            threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
            threshold.y = threshold.x * bloom.thresholdKnee;
            threshold.z = 2f * threshold.y;
            threshold.w = 0.25f / (threshold.y + 0.00001f);
            threshold.y -= threshold.x;
            buffer.SetGlobalVector(bloomThresholdId, threshold);
            buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);

            // Prefilter Pass
            RenderTextureFormat format = useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
            Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);

            // Prepare dimensions for pyramid creation.
            width /= 2;
            height /= 2;
            int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;

            // Pyramid Generation (Downsampling)
            int i;
            for (i = 0; i < bloom.maxIterations; i++)
            {
                if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
                {
                    break;
                }
                int midId = toId - 1;
                buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
                buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
                Draw(fromId, midId, Pass.BloomHorizontal);
                Draw(midId, toId, Pass.BloomVertical);
                fromId = toId;
                toId += 2;
                width /= 2;
                height /= 2;
            }


            // Additive and scatter mode specifics
            Pass combinePass, finalPass;
            float finalIntensity;
            if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
            {
                combinePass = finalPass = Pass.BloomAdd;
                buffer.SetGlobalFloat(bloomIntensityId, 1f);
                finalIntensity = bloom.intensity;
            }
            else
            {
                combinePass = Pass.BloomScatter;
                finalPass = Pass.BloomScatterFinal;
                buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
                finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
            }

            // Pyramid Combination
            if (i > 1)
            {
                buffer.ReleaseTemporaryRT(fromId - 1);
                toId -= 5;

                for (i -= 1; i > 0; i--)
                {
                    buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                    Draw(fromId, toId, combinePass);
                    buffer.ReleaseTemporaryRT(fromId);
                    buffer.ReleaseTemporaryRT(toId + 1);
                    fromId = toId;
                    toId -= 2;
                }
            }
            else
            {
                buffer.ReleaseTemporaryRT(bloomPyramidId);
            }
            buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);


            // Final Composition
            buffer.SetGlobalTexture(fxSource2Id, sourceId);
            buffer.SetGlobalTexture(fxSource2Id, sourceId);
            buffer.GetTemporaryRT(bloomResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
            Draw(fromId, bloomResultId, finalPass);
            buffer.ReleaseTemporaryRT(fromId);
            buffer.ReleaseTemporaryRT(bloomPrefilterId);

            buffer.EndSample("Bloom");
            return true;
        }



        void ConfigureColorAdjustments()
        {
            ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
            /// The color adjustments vector components are the exposure, contrast, hue shift, and saturation. Exposure is measured in stops, which means that we have to raise 2 to the power of the configured exposure value. Also convert contrast and saturation to the 0–2 range and hue shift to −1–1. The filter must be in linear linear color space.
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
        void ConfigureWhiteBalance()
        {
            WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
            buffer.SetGlobalVector
            (
                whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs
                (
                    whiteBalance.temperature, whiteBalance.tint
                )
            );
        }
        void ConfigureSplitToning()
        {
            SplitToningSettings splitToning = settings.SplitToning;
            Color splitColor = splitToning.shadows;
            splitColor.a = splitToning.balance * 0.01f;
            buffer.SetGlobalColor(splitToningShadowsId, splitColor);
            buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
        }
        void ConfigureChannelMixer()
        {
            ChannelMixerSettings channelMixer = settings.ChannelMixer;
            buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
            buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
            buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
        }
        void ConfigureShadowsMidtonesHighlights()
        {
            ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
            buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
            buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
            buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
            buffer.SetGlobalVector
            (
                smhRangeId, new Vector4
                (
                    smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd
                )
            );
        }
        void ConfigureDither()
        {
            if (settings.Material != null)
            {
                var mat = settings.Material;
                if (settings.dither.mode == 0)
                {
                    mat.DisableKeyword(ditherKeyword);
                }
                else
                {
                    mat.EnableKeyword(ditherKeyword);
                    if (settings.dither.mode == Dither.Mode.HighQuality)
                    {
                        mat.EnableKeyword(ditherHQKeyword);
                    }
                    else
                    {
                        mat.DisableKeyword(ditherHQKeyword);
                    }
                }
            }
        }

        public void ConfigureColorAdjustments(CommandBuffer buffer)
        {
            ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
            /// The color adjustments vector components are the exposure, contrast, hue shift, and saturation. Exposure is measured in stops, which means that we have to raise 2 to the power of the configured exposure value. Also convert contrast and saturation to the 0–2 range and hue shift to −1–1. The filter must be in linear linear color space.
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
        public void ConfigureWhiteBalance(CommandBuffer buffer)
        {
            WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
            buffer.SetGlobalVector
            (
                whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs
                (
                    whiteBalance.temperature, whiteBalance.tint
                )
            );
        }
        public void ConfigureSplitToning(CommandBuffer buffer)
        {
            SplitToningSettings splitToning = settings.SplitToning;
            Color splitColor = splitToning.shadows;
            splitColor.a = splitToning.balance * 0.01f;
            buffer.SetGlobalColor(splitToningShadowsId, splitColor);
            buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
        }
        public void ConfigureChannelMixer(CommandBuffer buffer)
        {
            ChannelMixerSettings channelMixer = settings.ChannelMixer;
            buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
            buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
            buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
        }
        public void ConfigureShadowsMidtonesHighlights(CommandBuffer buffer)
        {
            ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
            buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
            buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
            buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
            buffer.SetGlobalVector
            (
                smhRangeId, new Vector4
                (
                    smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd
                )
            );
        }
        public void ConfigureDither(CommandBuffer buffer)
        {
            if (settings.Material != null)
            {
                var mat = settings.Material;
                if (settings.dither.mode == 0)
                {
                    mat.DisableKeyword(ditherKeyword);
                }
                else
                {
                    mat.EnableKeyword(ditherKeyword);
                    if (settings.dither.mode == Dither.Mode.HighQuality)
                    {
                        mat.EnableKeyword(ditherHQKeyword);
                    }
                    else
                    {
                        mat.DisableKeyword(ditherHQKeyword);
                    }
                }
            }
        }
#if UNITY_EDITOR
        public void ConfigureDither(CommandBuffer buffer, CameraType cameraType)
        {
            if (settings.Material != null)
            {
                var mat = settings.Material;
                if (settings.dither.mode == 0 || (cameraType == CameraType.SceneView && !settings.dither.useInScene))
                {
                    mat.DisableKeyword(ditherKeyword);
                }
                else
                {
                    mat.EnableKeyword(ditherKeyword);
                    if (settings.dither.mode == Dither.Mode.HighQuality)
                    {
                        mat.EnableKeyword(ditherHQKeyword);
                    }
                    else
                    {
                        mat.DisableKeyword(ditherHQKeyword);
                    }
                }
            }
        }
#endif

        void ConfigureFXAA()
        {
            if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Low)
            {
                buffer.EnableShaderKeyword(fxaaQualityLowKeyword);
                buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
            }
            else if (fxaa.quality == CameraBufferSettings.FXAA.Quality.Medium)
            {
                buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
                buffer.EnableShaderKeyword(fxaaQualityMediumKeyword);
            }
            else
            {
                buffer.DisableShaderKeyword(fxaaQualityLowKeyword);
                buffer.DisableShaderKeyword(fxaaQualityMediumKeyword);
            }
            buffer.SetGlobalVector
            (
                fxaaConfigId, new Vector4
                (
                    fxaa.fixedThreshold, fxaa.relativeThreshold, fxaa.subpixelBlending
                )
            );
        }

        void DrawFinal(RenderTargetIdentifier from, Pass pass)
        {
            buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
            buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);

            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget
            (
                BuiltinRenderTextureType.CameraTarget,
                finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store
            );
            buffer.SetViewport(camera.pixelRect);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
        }
        public void DrawFinal
        (
            CommandBuffer buffer,
            RenderTargetIdentifier from,
            Pass pass
        )
        {
            buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
            buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget
            (
                BuiltinRenderTextureType.CameraTarget,
                finalBlendMode.destination == BlendMode.Zero &&
                    camera.rect == fullViewRect ?
                    RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store
            );
            buffer.SetViewport(camera.pixelRect);
            //Debug.Log("PostFXStack camera.pixelRect " + camera.name + " " + camera.pixelRect);
            buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
        }
        void DoPostFXMain(int sourceId)
        {
            ConfigureColorAdjustments();
            ConfigureWhiteBalance();
            ConfigureSplitToning();
            ConfigureChannelMixer();
            ConfigureShadowsMidtonesHighlights();
            ConfigureDither();
            bool bicubicSampling =
                    bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                    bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                    bufferSize.x < camera.pixelWidth;
            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);

            int lutHeight = colorLUTResolution;
            int lutWidth = lutHeight * lutHeight;
            buffer.GetTemporaryRT
            (
                colorGradingLUTId, lutWidth, lutHeight, 0,
                FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
            );
            buffer.SetGlobalVector
            (
                colorGradingLUTParametersId, new Vector4
                (
                    lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
                )
            );


            PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
            Pass pass = Pass.ToneMappingNone + (int)mode;
            buffer.SetGlobalFloat(colorGradingLUTInLogId, useHDR && pass != Pass.ToneMappingNone ? 1f : 0f);
            Draw(sourceId, colorGradingLUTId, pass);

            buffer.SetGlobalVector
            (
                colorGradingLUTParametersId, new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f)
            );


            buffer.SetGlobalFloat(finalSrcBlendId, 1f);
            buffer.SetGlobalFloat(finalDstBlendId, 0f);
            if (fxaa.enabled)
            {
                ConfigureFXAA();
                buffer.GetTemporaryRT
                (
                    colorGradingResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default
                );
                Draw(sourceId, colorGradingResultId, keepAlpha ? Pass.ApplyLut : Pass.ApplyLutWithLuma);
            }

            if (bufferSize.x == camera.pixelWidth)
            {
                if (fxaa.enabled)
                {
                    DrawFinal(colorGradingResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    DrawFinal(sourceId, Pass.ApplyLut);
                }
            }
            else
            {
                // If render scale != 1, first apply postFX, and then rescale. Else hdr effects will be aliased
                buffer.GetTemporaryRT
                (
                    finalResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default
                );

                if (fxaa.enabled)
                {
                    Draw(colorGradingResultId, finalResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    Draw(sourceId, finalResultId, Pass.ApplyLut);
                }
                DrawFinal(finalResultId, Pass.FinalRescale);
                buffer.ReleaseTemporaryRT(finalResultId);
            }
            buffer.ReleaseTemporaryRT(colorGradingLUTId);
        }
        /// <summary>
        /// RenderGraph overload
        /// </summary>
        /// <param name="sourceId"></param>
        void DoPostFXMain(RenderTargetIdentifier sourceId)
        {
            ConfigureColorAdjustments();
            ConfigureWhiteBalance();
            ConfigureSplitToning();
            ConfigureChannelMixer();
            ConfigureShadowsMidtonesHighlights();
            ConfigureDither();
            bool bicubicSampling =
                    bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                    bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                    bufferSize.x < camera.pixelWidth;
            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f);

            int lutHeight = colorLUTResolution;
            int lutWidth = lutHeight * lutHeight;
            buffer.GetTemporaryRT
            (
                colorGradingLUTId, lutWidth, lutHeight, 0,
                FilterMode.Bilinear, RenderTextureFormat.DefaultHDR
            );
            buffer.SetGlobalVector
            (
                colorGradingLUTParametersId, new Vector4
                (
                    lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)
                )
            );


            PostFXSettings.ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
            Pass pass = Pass.ToneMappingNone + (int)mode;
            buffer.SetGlobalFloat(colorGradingLUTInLogId, useHDR && pass != Pass.ToneMappingNone ? 1f : 0f);
            Draw(sourceId, colorGradingLUTId, pass);

            buffer.SetGlobalVector
            (
                colorGradingLUTParametersId, new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f)
            );


            buffer.SetGlobalFloat(finalSrcBlendId, 1f);
            buffer.SetGlobalFloat(finalDstBlendId, 0f);
            if (fxaa.enabled)
            {
                ConfigureFXAA();
                buffer.GetTemporaryRT
                (
                    colorGradingResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default
                );
                Draw(sourceId, colorGradingResultId, keepAlpha ? Pass.ApplyLut : Pass.ApplyLutWithLuma);
            }

            if (bufferSize.x == camera.pixelWidth)
            {
                if (fxaa.enabled)
                {
                    DrawFinal(colorGradingResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    DrawFinal(sourceId, Pass.ApplyLut);
                }
            }
            else
            {
                // If render scale != 1, first apply postFX, and then rescale. Else hdr effects will be aliased
                buffer.GetTemporaryRT
                (
                    finalResultId, bufferSize.x, bufferSize.y, 0,
                    FilterMode.Bilinear, RenderTextureFormat.Default
                );

                if (fxaa.enabled)
                {
                    Draw(colorGradingResultId, finalResultId, keepAlpha ? Pass.FXAA : Pass.FXAAWithLuma);
                    buffer.ReleaseTemporaryRT(colorGradingResultId);
                }
                else
                {
                    Draw(sourceId, finalResultId, Pass.ApplyLut);
                }
                DrawFinal(finalResultId, Pass.FinalRescale);
                buffer.ReleaseTemporaryRT(finalResultId);
            }
            buffer.ReleaseTemporaryRT(colorGradingLUTId);
        }
    }
}
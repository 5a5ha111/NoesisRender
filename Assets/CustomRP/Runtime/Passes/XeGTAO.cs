using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


namespace NoesisRender.Passes
{
    using static CustomRenderPipelineSettings;
    using static ResourcesHolders.XeGTAOResources;
    using NoesisRender.ResourcesHolders;

    public class XeGTAO
    {
        static readonly ProfilingSampler sampler = new("XeGTAO");


#pragma warning disable CS0414 // supress variable declared, but not used

        Camera camera;
        XeGTAOSettings xeGTAOSettings;
        XeGTAOTextures xeGTAOTextures;
        TextureHandle colorTex;
        TextureHandle depthTex;
        TextureHandle tempTex;
        Vector2Int attachmentSize;

        RenderTexture depthWithPackedMips;
        ComputeBufferHandle constanstBufferHandle;
        Material materialXeGTAOApply;

        // First Pass depth prefilter
        TextureHandle tempDepth; // id _g_srcRawDepth
        TextureHandle depthfilteredMip0;
        TextureHandle depthfilteredMip1;
        TextureHandle depthfilteredMip2;
        TextureHandle depthfilteredMip3;
        TextureHandle depthfilteredMip4;

        // Second Pass GTAO
        //TextureHandle srcWorkindDepth;
        TextureHandle srcNormalMap;
        RenderTexture srcNormalMapWorld;
        TextureHandle srcHilberLUT;
        TextureHandle outWorkingAOTerm;
        TextureHandle outWorkingEdges;
        TextureHandle outNormalMap;

        // Third Denoise Pass
        TextureHandle outDenoisedAOPing;

        //Out 
        TextureHandle OutXeHBAO;
        TextureHandle BicubicHBAO;

        readonly static int _resTex = Shader.PropertyToID("Result");
        readonly static int _attachmentSize = Shader.PropertyToID("attachmentSize");
        readonly static int _constantsBuffer = Shader.PropertyToID("GTAOConstantBuffer");
        readonly static int _UintTexture = Shader.PropertyToID("_UintTexture");
        readonly static int _UintTextureNormals = Shader.PropertyToID("_UintTextureNormals");
        readonly static int _SourceTexture = Shader.PropertyToID("_SourceTexture");


        // GTAO Textures id depthPrefilter
        readonly static int _g_srcRawDepth = Shader.PropertyToID("g_srcRawDepth");
        readonly static int _g_outDepthMIP0 = Shader.PropertyToID("g_outWorkingDepthMIP0");
        readonly static int _g_outDepthMIP1 = Shader.PropertyToID("g_outWorkingDepthMIP1");
        readonly static int _g_outDepthMIP2 = Shader.PropertyToID("g_outWorkingDepthMIP2");
        readonly static int _g_outDepthMIP3 = Shader.PropertyToID("g_outWorkingDepthMIP3");
        readonly static int _g_outDepthMIP4 = Shader.PropertyToID("g_outWorkingDepthMIP4");

        //
        //readonly static int g_srcNormalmap = Shader.PropertyToID("g_srcNormalmap");

        // GTAO Textures id GTAO
        readonly static int _g_srcWorkingDepth = Shader.PropertyToID("g_srcWorkingDepth");
        readonly static int _g_srcNormalmap = Shader.PropertyToID("g_srcNormalmap");
        readonly static int _g_srcNormalmapWorld = Shader.PropertyToID("g_srcNormalmapWorld");
        readonly static int _g_srcHilbertLUT = Shader.PropertyToID("g_srcHilbertLUT");
        readonly static int _g_outWorkingAOTerm = Shader.PropertyToID("g_outWorkingAOTerm");
        readonly static int _g_outWorkingEdges = Shader.PropertyToID("g_outWorkingEdges");
        readonly static int _g_outNormalmap = Shader.PropertyToID("g_outNormalmap");

        // GTAO Textures id Denoise
        readonly static int _g_srcWorkingAOTerm = Shader.PropertyToID("g_srcWorkingAOTerm");
        readonly static int _g_srcWorkingEdges = Shader.PropertyToID("g_srcWorkingEdges");
        readonly static int _g_outFinalAOTerm = Shader.PropertyToID("g_outFinalAOTerm");



        // GTAO Constants without buffer
        readonly static int _PackedViewport = Shader.PropertyToID("PackedViewport");
        readonly static int _Packed1 = Shader.PropertyToID("Packed1");
        readonly static int _PackedNDC = Shader.PropertyToID("PackedNDC");
        readonly static int _PackedEffect = Shader.PropertyToID("PackedEffect");
        readonly static int _Packed2 = Shader.PropertyToID("Packed2");
        readonly static int _Packed3 = Shader.PropertyToID("Packed3");
        readonly static int _NoiseIndex = Shader.PropertyToID("NoiseIndex");
        readonly static int _finalDenoise = Shader.PropertyToID("finalDenoise");
        readonly static int _worldToViewM = Shader.PropertyToID("WorldToViewM");
        readonly static int _generateNormals = Shader.PropertyToID("GenerateNormals");

        readonly static string KernelTest = "CSMain";
        readonly static string KernelDepthFilter = "CSPrefilterDepths16x16";
        readonly static string KernelCSGenerateNormals = "CSGenerateNormals";
        readonly static string KernelCSGTAOLow = "CSGTAOLow";
        readonly static string KernelCSGTAOMedium = "CSGTAOMedium";
        readonly static string KernelCSGTAOHigh = "CSGTAOHigh";
        readonly static string KernelCSGTAOUltra = "CSGTAOUltra";
        readonly static string KernelCSDenoisePass = "CSDenoisePass";


        private readonly static int _RemapUintToFloatPass = 0;
        private readonly static int _GetDepthNDCPass = 1;
        private readonly static int _BlitPass = 2;
        private readonly static int _DebugDepthPass = 3;
        private readonly static int _DebugNormals = 4;
        private readonly static int _DebugNormalsWorld = 5;
        private readonly static int _BicubicRescale = 6;


        private readonly static int XE_GTAO_NUMTHREADS_X = 8;
        private readonly static int XE_GTAO_NUMTHREADS_Y = 8;

#pragma warning restore CS0414


        GTAOConstants gTAOConstants;
        bool generateNormals;

        void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            ComputeShader vaXeGTAO = xeGTAOSettings.computeShader;

            cmd.Blit(colorTex, tempTex);

            int mainKernel = vaXeGTAO.FindKernel(KernelTest);

            int depthFilterKernel = vaXeGTAO.FindKernel(KernelDepthFilter);
            int GenerateNormalsKernel = vaXeGTAO.FindKernel(KernelCSGenerateNormals);
            int GTAOKernel = vaXeGTAO.FindKernel(KernelCSGTAOLow);
            switch (xeGTAOSettings.QualityLevel)
            {
                case XeGTAOSettings.GTAOQuality.low:
                    GTAOKernel = vaXeGTAO.FindKernel(KernelCSGTAOLow);
                    break;
                case XeGTAOSettings.GTAOQuality.medium:
                    GTAOKernel = vaXeGTAO.FindKernel(KernelCSGTAOMedium);
                    break;
                case XeGTAOSettings.GTAOQuality.hight:
                    GTAOKernel = vaXeGTAO.FindKernel(KernelCSGTAOHigh);
                    break;
                case XeGTAOSettings.GTAOQuality.ultra:
                    GTAOKernel = vaXeGTAO.FindKernel(KernelCSGTAOUltra);
                    break;
                default:
                    GTAOKernel = vaXeGTAO.FindKernel(KernelCSGTAOLow);
                    break;
            }
            int DenoiseKernel = vaXeGTAO.FindKernel(KernelCSDenoisePass);

            /*vaXeGTAO.SetTexture(mainKernel, _resTex, tempTex);
            cmd.SetComputeVectorParam(vaXeGTAO, _attachmentSize, new Vector4(attachmentSize.x, attachmentSize.y));
            GTAOConstants[] gTAOConstantsbuffer = new GTAOConstants[1];
            gTAOConstantsbuffer[0] = gTAOConstants;

            cmd.DispatchCompute(vaXeGTAO, mainKernel, numTheadsX, numTheadsY, 1);*/
            int numTheadsX = attachmentSize.x % 8 == 0 ? attachmentSize.x / 8 : (attachmentSize.x / 8) + 1;
            int numTheadsY = attachmentSize.y % 8 == 0 ? attachmentSize.y / 8 : (attachmentSize.y / 8) + 1;


            //cmd.Blit(depthTex, tempDepth);
            cmd.Blit(depthTex, tempDepth, materialXeGTAOApply, _GetDepthNDCPass);

            // Pack and send GTAO Constants without buffer
            Vector4 PackedViewport = new Vector4(gTAOConstants.ViewportSize.x, gTAOConstants.ViewportSize.y, gTAOConstants.ViewportPixelSize.x, gTAOConstants.ViewportPixelSize.y);
            Vector4 Packed1 = new Vector4(gTAOConstants.DepthUnpackConsts.x, gTAOConstants.DepthUnpackConsts.y, gTAOConstants.CameraTanHalfFOV.x, gTAOConstants.CameraTanHalfFOV.y);
            Vector4 PackedNDC = new Vector4(gTAOConstants.NDCToViewMul.x, gTAOConstants.NDCToViewMul.y, gTAOConstants.NDCToViewAdd.x, gTAOConstants.NDCToViewAdd.y);
            Vector4 PackedEffect = new Vector4(gTAOConstants.NDCToViewMul_x_PixelSize.x, gTAOConstants.NDCToViewMul_x_PixelSize.y, gTAOConstants.EffectRadius, gTAOConstants.EffectFalloffRange);
            Vector4 Packed2 = new Vector4(gTAOConstants.RadiusMultiplier, gTAOConstants.Padding0, gTAOConstants.FinalValuePower, gTAOConstants.DenoiseBlurBeta);
            Vector4 Packed3 = new Vector4(gTAOConstants.SampleDistributionPower, gTAOConstants.ThinOccluderCompensation, gTAOConstants.DepthMIPSamplingOffset, 0);
            int NoiseIndex = gTAOConstants.NoiseIndex;

            cmd.SetComputeVectorParam(vaXeGTAO, _PackedViewport, PackedViewport);
            cmd.SetComputeVectorParam(vaXeGTAO, _Packed1, Packed1);
            cmd.SetComputeVectorParam(vaXeGTAO, _PackedNDC, PackedNDC);
            cmd.SetComputeVectorParam(vaXeGTAO, _PackedEffect, PackedEffect);
            cmd.SetComputeVectorParam(vaXeGTAO, _Packed2, Packed2);
            cmd.SetComputeVectorParam(vaXeGTAO, _Packed3, Packed3);
            cmd.SetComputeIntParam(vaXeGTAO, _NoiseIndex, NoiseIndex);
            cmd.SetComputeIntParam(vaXeGTAO, _generateNormals, generateNormals ? 1 : 0);
            //cmd.SetComputeIntParam(vaXeGTAO, _generateNormals, 1);


            numTheadsX = (attachmentSize.x + 16 - 1) / 16;
            numTheadsY = (attachmentSize.y + 16 - 1) / 16;



            cmd.SetComputeTextureParam(vaXeGTAO, depthFilterKernel, _g_srcRawDepth, tempDepth);
            cmd.SetComputeTextureParam(vaXeGTAO, depthFilterKernel, _g_outDepthMIP0, depthfilteredMip0);
            cmd.SetComputeTextureParam(vaXeGTAO, depthFilterKernel, _g_outDepthMIP1, depthfilteredMip1);
            cmd.SetComputeTextureParam(vaXeGTAO, depthFilterKernel, _g_outDepthMIP2, depthfilteredMip2);
            cmd.SetComputeTextureParam(vaXeGTAO, depthFilterKernel, _g_outDepthMIP3, depthfilteredMip3);
            cmd.SetComputeTextureParam(vaXeGTAO, depthFilterKernel, _g_outDepthMIP4, depthfilteredMip4);


            if (generateNormals)
            {
                cmd.SetComputeTextureParam(vaXeGTAO, GenerateNormalsKernel, _g_srcRawDepth, tempDepth);
                cmd.SetComputeTextureParam(vaXeGTAO, GenerateNormalsKernel, _g_outNormalmap, srcNormalMap);

                cmd.DispatchCompute(vaXeGTAO, GenerateNormalsKernel, numTheadsX * 2, numTheadsY * 2, 1);
                cmd.SetGlobalTexture(_UintTextureNormals, srcNormalMap);
                //materialXeGTAOApply.SetTexture(_UintTextureNormals, outNormalMap);
                cmd.Blit(outNormalMap, colorTex, materialXeGTAOApply, _DebugNormals);
            }
            else
            {
                cmd.SetComputeMatrixParam(vaXeGTAO, _worldToViewM, camera.worldToCameraMatrix);
                cmd.Blit(srcNormalMapWorld, outNormalMap);

                cmd.SetComputeTextureParam(vaXeGTAO, GTAOKernel, _g_srcNormalmapWorld, outNormalMap);
                cmd.SetGlobalTexture(_SourceTexture, srcNormalMapWorld);
                cmd.Blit(srcNormalMapWorld, colorTex, materialXeGTAOApply, _DebugNormalsWorld);
            }


            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();


            cmd.DispatchCompute(vaXeGTAO, depthFilterKernel, numTheadsX, numTheadsY, 1);


            cmd.Blit(depthfilteredMip0, depthWithPackedMips);
            //depthWithPackedMips.GenerateMips();
            /*cmd.SetGlobalTexture(_SourceTexture, depthfilteredMip0);
            cmd.SetRenderTarget(depthWithPackedMips, 0);
            cmd.DrawProcedural(Matrix4x4.identity, materialXeGTAOApply, _BlitPass, MeshTopology.Triangles, 3);*/
            BlitProcToMip(depthfilteredMip0, depthWithPackedMips, cmd, 0);
            BlitProcToMip(depthfilteredMip1, depthWithPackedMips, cmd, 1);
            BlitProcToMip(depthfilteredMip2, depthWithPackedMips, cmd, 2);
            BlitProcToMip(depthfilteredMip3, depthWithPackedMips, cmd, 3);
            BlitProcToMip(depthfilteredMip4, depthWithPackedMips, cmd, 4);
            //cmd.Blit(depthfilteredMip0, depthWithPackedMips);

            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();

            cmd.SetComputeTextureParam(vaXeGTAO, GTAOKernel, _g_srcWorkingDepth, depthWithPackedMips);
            cmd.SetComputeTextureParam(vaXeGTAO, GTAOKernel, _g_srcNormalmap, srcNormalMap);
            cmd.SetComputeTextureParam(vaXeGTAO, GTAOKernel, _g_srcHilbertLUT, srcHilberLUT);
            cmd.SetComputeTextureParam(vaXeGTAO, GTAOKernel, _g_outWorkingAOTerm, outWorkingAOTerm);
            cmd.SetComputeTextureParam(vaXeGTAO, GTAOKernel, _g_outWorkingEdges, outWorkingEdges);
            cmd.SetComputeTextureParam(vaXeGTAO, GTAOKernel, _g_outNormalmap, outNormalMap);

            //CalculateNumThreads(8, attachmentSize);
            numTheadsX = (attachmentSize.x + XE_GTAO_NUMTHREADS_X - 1) / XE_GTAO_NUMTHREADS_X;
            numTheadsY = (attachmentSize.y + XE_GTAO_NUMTHREADS_Y - 1) / XE_GTAO_NUMTHREADS_Y;
            cmd.DispatchCompute(vaXeGTAO, GTAOKernel, numTheadsX, numTheadsY, 1);



            int denoisePasses = xeGTAOSettings.DenoisePasses;
            TextureHandle finalAO = outDenoisedAOPing;
            TextureHandle srcAO = outWorkingAOTerm;

            for (int i = 0; i < denoisePasses; i++)
            {
                cmd.SetComputeTextureParam(vaXeGTAO, DenoiseKernel, _g_srcWorkingAOTerm, srcAO);
                cmd.SetComputeTextureParam(vaXeGTAO, DenoiseKernel, _g_srcWorkingEdges, outWorkingEdges);
                cmd.SetComputeTextureParam(vaXeGTAO, DenoiseKernel, _g_outFinalAOTerm, finalAO);

                bool lastPass = i == denoisePasses - 1;
                cmd.SetComputeIntParam(vaXeGTAO, _finalDenoise, lastPass ? 1 : 0);

                // In denoise we sample 2 horizontal pixels at once
                numTheadsX = (attachmentSize.x + (XE_GTAO_NUMTHREADS_X * 2) - 1) / (XE_GTAO_NUMTHREADS_X * 2);
                numTheadsY = (attachmentSize.y + XE_GTAO_NUMTHREADS_Y - 1) / XE_GTAO_NUMTHREADS_Y;
                cmd.DispatchCompute(vaXeGTAO, DenoiseKernel, numTheadsX, numTheadsY, 1);

                // Ping pong texture in denoise passes
                if (!lastPass)
                {
                    TextureHandle tempPointr = finalAO;
                    finalAO = srcAO;
                    srcAO = tempPointr;
                }
            }
            if (denoisePasses == 0)
            {
                finalAO = outWorkingAOTerm;
            }


            //cmd.DispatchCompute(vaXeGTAO, DenoiseKernel, numTheadsX * 2, numTheadsY * 2, 1);


            cmd.SetGlobalTexture(_UintTexture, finalAO);
            cmd.SetRenderTarget(OutXeHBAO);
            LocalKeyword halfResKeyword = new LocalKeyword(materialXeGTAOApply.shader, "_HalfRes");
            materialXeGTAOApply.SetKeyword(halfResKeyword, xeGTAOSettings.HalfRes);
            cmd.DrawProcedural(Matrix4x4.identity, materialXeGTAOApply, _RemapUintToFloatPass, MeshTopology.Triangles, 3);

            if (xeGTAOSettings.BicubicRescale)
            {
                cmd.SetRenderTarget(BicubicHBAO);
                cmd.SetGlobalTexture(_SourceTexture, OutXeHBAO);
                cmd.DrawProcedural(Matrix4x4.identity, materialXeGTAOApply, _BicubicRescale, MeshTopology.Triangles, 3);
            }
            //cmd.Blit(colorTex, tempTex);
            /*cmd.Blit(outWorkingAOTerm, colorTex);
            cmd.Blit(depthWithPackedMips, colorTex);*/

            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static TextureHandle Record(RenderGraph renderGraph, Camera camera, in CameraRendererTextures textures, XeGTAOSettings xeGTAOsettings, XeGTAOTextures xeGTAOTextures, Vector2Int attachmentSize, Material applyXeGTAO, bool generateNormals, RenderTexture normalBuffer)
        {

            using RenderGraphBuilder builder = renderGraph.AddRenderPass
            (
                sampler.name, out XeGTAO pass, sampler
            );
            pass.camera = camera;
            pass.xeGTAOSettings = xeGTAOsettings;
            pass.xeGTAOTextures = xeGTAOTextures;
            pass.colorTex = builder.ReadWriteTexture(textures.colorAttachment);
            pass.depthTex = builder.ReadTexture(textures.depthAttachment);
            pass.materialXeGTAOApply = applyXeGTAO;
            pass.generateNormals = generateNormals;
            pass.srcNormalMapWorld = normalBuffer;

            Vector2Int safeSize = Vector2Int.Max(attachmentSize, Vector2Int.one);
            // Half res
            if (xeGTAOsettings.HalfRes)
            {
                safeSize /= 2;
            }
            pass.attachmentSize = safeSize;

            // Out
            var OutXeHBAO = new TextureDesc(safeSize.x, safeSize.y);
            OutXeHBAO.name = "XeHBAO value";
            OutXeHBAO.depthBufferBits = 0;
            OutXeHBAO.colorFormat = GraphicsFormat.R8_UNorm;
            OutXeHBAO.clearColor = Color.white;
            pass.OutXeHBAO = builder.ReadWriteTexture(renderGraph.CreateTexture(OutXeHBAO));


            if (attachmentSize.x <= 0 || attachmentSize.y <= 0)
            {
                Debug.Log("Attach not valid");
                return pass.OutXeHBAO;
            }

            if (xeGTAOsettings.BicubicRescale)
            {
                var reScaledTex = new TextureDesc(OutXeHBAO);
                reScaledTex.name = "Bicubic Rescaled HBAO Value";
                reScaledTex.width = attachmentSize.x;
                reScaledTex.height = attachmentSize.y;
                pass.BicubicHBAO = builder.ReadWriteTexture(renderGraph.CreateTexture(reScaledTex));
            }

            var defColorFormat = SystemInfo.GetGraphicsFormat(/*true ? DefaultFormat.HDR :*/ DefaultFormat.LDR);
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = defColorFormat,
                name = "Temp XeGTAO",
                enableRandomWrite = true,
                anisoLevel = 1,
                depthBufferBits = 0
            };
            pass.tempTex = builder.ReadWriteTexture(renderGraph.CreateTexture(desc));

            // Create temp depth and viewSpaceDepth + 5 mip
            var tempDepth = new TextureDesc(safeSize.x, safeSize.y);
            tempDepth.colorFormat = GraphicsFormat.R32_SFloat;
            tempDepth.name = "g_srcRawDepth";
            tempDepth.enableRandomWrite = true;
            tempDepth.depthBufferBits = 0;


            GraphicsFormat depthFormat = GraphicsFormat.R16_SFloat;
            Vector2Int depthSize = safeSize;
            var descDepthMip0 = new TextureDesc(depthSize.x, depthSize.y);
            descDepthMip0.name = "g_outWorkingDepthMIP0";
            descDepthMip0.colorFormat = depthFormat;
            descDepthMip0.enableRandomWrite = true;
            descDepthMip0.depthBufferBits = 0;
            depthSize /= 2;
            depthSize = Vector2Int.Max(depthSize, Vector2Int.one);

            var descDepthMip1 = new TextureDesc(descDepthMip0);
            descDepthMip1.name = "g_outWorkingDepthMIP1";
            descDepthMip1.width = depthSize.x;
            descDepthMip1.height = depthSize.y;
            depthSize /= 2;
            depthSize = Vector2Int.Max(depthSize, Vector2Int.one);

            var descDepthMip2 = new TextureDesc(descDepthMip0);
            descDepthMip2.name = "g_outWorkingDepthMIP2";
            descDepthMip2.width = depthSize.x;
            descDepthMip2.height = depthSize.y;
            depthSize /= 2;
            depthSize = Vector2Int.Max(depthSize, Vector2Int.one);

            var descDepthMip3 = new TextureDesc(descDepthMip0);
            descDepthMip3.name = "g_outWorkingDepthMIP3";
            descDepthMip3.width = depthSize.x;
            descDepthMip3.height = depthSize.y;
            depthSize /= 2;
            depthSize = Vector2Int.Max(depthSize, Vector2Int.one);

            var descDepthMip4 = new TextureDesc(descDepthMip0);
            descDepthMip4.name = "g_outWorkingDepthMIP4";
            descDepthMip4.width = depthSize.x;
            descDepthMip4.height = depthSize.y;
            depthSize /= 2;
            depthSize = Vector2Int.Max(depthSize, Vector2Int.one);


            // Pass 1
            pass.depthWithPackedMips = xeGTAOTextures._getTextures[0];
            pass.tempDepth = builder.CreateTransientTexture(tempDepth);
            pass.depthfilteredMip0 = builder.CreateTransientTexture(descDepthMip0);
            pass.depthfilteredMip1 = builder.CreateTransientTexture(descDepthMip1);
            pass.depthfilteredMip2 = builder.CreateTransientTexture(descDepthMip2);
            pass.depthfilteredMip3 = builder.CreateTransientTexture(descDepthMip3);
            pass.depthfilteredMip4 = builder.CreateTransientTexture(descDepthMip4);

            // Pass 2
            GraphicsFormat secondPassFormat = GraphicsFormat.R8_UInt;
            var sRCNormal = new TextureDesc(safeSize.x, safeSize.y);
            sRCNormal.name = "g_srcNormalmap";
            sRCNormal.depthBufferBits = 0;
            sRCNormal.colorFormat = GraphicsFormat.R32_UInt;
            //sRCNormal.colorFormat = secondPassFormat;
            sRCNormal.enableRandomWrite = true;

            var sRCHilbertLUT = new TextureDesc(safeSize.x, safeSize.y);
            sRCHilbertLUT.name = "g_srcHilbertLUT";
            sRCHilbertLUT.depthBufferBits = 0;
            sRCHilbertLUT.colorFormat = secondPassFormat;
            sRCHilbertLUT.enableRandomWrite = true;

            var OutAOTerm = new TextureDesc(safeSize.x, safeSize.y);
            OutAOTerm.name = "g_outWorkingAOTerm";
            OutAOTerm.depthBufferBits = 0;
            OutAOTerm.colorFormat = secondPassFormat;
            OutAOTerm.enableRandomWrite = true;

            var OutNormalMap = new TextureDesc(safeSize.x, safeSize.y);
            OutNormalMap.name = "g_outNormalmap";
            OutNormalMap.depthBufferBits = 0;
            //OutNormalMap.colorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            OutNormalMap.colorFormat = generateNormals ? GraphicsFormat.R32_UInt : normalBuffer.graphicsFormat;
            OutNormalMap.enableRandomWrite = true;

            var OutworkingEdges = new TextureDesc(safeSize.x, safeSize.y);
            OutworkingEdges.name = "g_outWorkingEdges";
            OutworkingEdges.depthBufferBits = 0;
            OutworkingEdges.colorFormat = GraphicsFormat.R8_UNorm;
            OutworkingEdges.enableRandomWrite = true;

            pass.srcNormalMap = builder.CreateTransientTexture(sRCNormal);
            pass.srcHilberLUT = builder.CreateTransientTexture(sRCHilbertLUT);
            pass.outWorkingAOTerm = builder.CreateTransientTexture(OutAOTerm);
            pass.outNormalMap = builder.CreateTransientTexture(OutNormalMap);
            pass.outWorkingEdges = builder.CreateTransientTexture(OutworkingEdges);


            // Pass 3
            var OutDenoisedAOTerm = new TextureDesc(safeSize.x, safeSize.y);
            OutDenoisedAOTerm.name = "g_outFinalAOTerm_Ping";
            OutDenoisedAOTerm.depthBufferBits = 0;
            OutDenoisedAOTerm.colorFormat = secondPassFormat;
            OutDenoisedAOTerm.enableRandomWrite = true;
            pass.outDenoisedAOPing = builder.CreateTransientTexture(OutDenoisedAOTerm);




            GTAOConstants gTAOConstants = new GTAOConstants();
            //GTAOUpdateConstants(ref gTAOConstants, attachmentSize.x, attachmentSize.y, xeGTAOsettings, camera.projectionMatrix, rowMajor: true, frameCounter: 0);

            float aspect = camera.aspect;
            int noiseIndex = xeGTAOsettings.TemporalStable ? 0 : Time.frameCount % 64;
            GTAOUpdateConstants(ref gTAOConstants, safeSize.x, safeSize.y, xeGTAOsettings, camera.farClipPlane, camera.nearClipPlane, Mathf.Deg2Rad * camera.fieldOfView, aspect, noiseIndex);
            pass.gTAOConstants = gTAOConstants;


            builder.AllowPassCulling(false);
            builder.SetRenderFunc<XeGTAO>
            (
                static (pass, context) => pass.Render(context)
            );

            if (xeGTAOsettings.BicubicRescale)
            {
                return pass.BicubicHBAO;
            }
            else
            {
                return pass.OutXeHBAO;
            }

        }

        public static bool CanRender(XeGTAOSettings xeGTAOSettings, Material applyXeGTAO)
        {
            if (xeGTAOSettings.computeShader != null && applyXeGTAO != null && xeGTAOSettings.enabled)
            {
                return true;
            }
            return false;
        }

        private void BlitProcToMip(Texture source, Texture destination, CommandBuffer cmd, int mip)
        {
            cmd.SetGlobalTexture(_SourceTexture, source);
            cmd.SetRenderTarget(destination, mip);
            cmd.DrawProcedural(Matrix4x4.identity, materialXeGTAOApply, _BlitPass, MeshTopology.Triangles, 3);
        }

        private Vector2Int CalculateNumThreads(int groupSize, Vector2Int attachmentSize)
        {
            Vector2Int res = Vector2Int.one;

            res.x = (attachmentSize.x + groupSize - 1) / groupSize;
            res.y = (attachmentSize.y + groupSize - 1) / groupSize;

            return res;
        }


        //private readonly uint GTAOConstantsStride = 80;
        [StructLayout(LayoutKind.Sequential)]
        struct GTAOConstants
        {
            public int2 ViewportSize;
            //public int ViewportSizeX;
            //public int ViewportSizeY;
            public Vector2 ViewportPixelSize;                  // .zw == 1.0 / ViewportSize.xy

            public Vector2 DepthUnpackConsts;
            public Vector2 CameraTanHalfFOV;

            public Vector2 NDCToViewMul;
            public Vector2 NDCToViewAdd;

            public Vector2 NDCToViewMul_x_PixelSize;
            public float EffectRadius;                       // world (viewspace) maximum depthSize of the shadow
            public float EffectFalloffRange;

            public float RadiusMultiplier;
            public float Padding0;
            public float FinalValuePower;
            public float DenoiseBlurBeta;

            public float SampleDistributionPower;
            public float ThinOccluderCompensation;
            public float DepthMIPSamplingOffset;
            public int NoiseIndex;                         // frameIndex % 64 if using TAA or 0 otherwise
        }

        // If using TAA then set noiseIndex to frameIndex % 64 - otherwise use 0
        static void GTAOUpdateConstants(ref GTAOConstants consts, int viewportWidth, int viewportHeight, in XeGTAOSettings settings, in Matrix4x4 projMatrix, bool rowMajor, int frameCounter)
        {
            consts.ViewportSize = new int2(viewportWidth, viewportHeight);
            //consts.ViewportSizeX = viewportWidth;
            //consts.ViewportSizeY = viewportHeight;
            consts.ViewportPixelSize = new Vector2(1.0f / (float)viewportWidth, 1.0f / (float)viewportHeight);

            float depthLinearizeMul = (rowMajor) ? (-projMatrix[3 * 4 + 2]) : (-projMatrix[3 + 2 * 4]);     // float depthLinearizeMul = ( clipFar * clipNear ) / ( clipFar - clipNear );
            float depthLinearizeAdd = (rowMajor) ? (projMatrix[2 * 4 + 2]) : (projMatrix[2 + 2 * 4]);     // float depthLinearizeAdd = clipFar / ( clipFar - clipNear );

            // correct the handedness issue. need to make sure this below is correct, but I think it is.
            if (depthLinearizeMul * depthLinearizeAdd < 0)
                depthLinearizeAdd = -depthLinearizeAdd;
            consts.DepthUnpackConsts = new Vector2(depthLinearizeMul, depthLinearizeAdd);

            float tanHalfFOVY = 1.0f / ((rowMajor) ? (projMatrix[1 * 4 + 1]) : (projMatrix[1 + 1 * 4]));    // = tanf( drawContext.Camera.GetYFOV( ) * 0.5f );
            float tanHalfFOVX = 1.0F / ((rowMajor) ? (projMatrix[0 * 4 + 0]) : (projMatrix[0 + 0 * 4]));    // = tanHalfFOVY * drawContext.Camera.GetAspect( );
            consts.CameraTanHalfFOV = new Vector2(tanHalfFOVX, tanHalfFOVY);

            consts.NDCToViewMul = new Vector2(consts.CameraTanHalfFOV.x * 2.0f, consts.CameraTanHalfFOV.y * -2.0f);
            consts.NDCToViewAdd = new Vector2(consts.CameraTanHalfFOV.x * -1.0f, consts.CameraTanHalfFOV.y * 1.0f);

            consts.NDCToViewMul_x_PixelSize = new Vector2(consts.NDCToViewMul.x * consts.ViewportPixelSize.x, consts.NDCToViewMul.y * consts.ViewportPixelSize.y);

            consts.EffectRadius = settings.Radius;

            consts.EffectFalloffRange = settings.FalloffRange;
            consts.DenoiseBlurBeta = (settings.DenoisePasses == 0) ? (1e4f) : (1.2f);    // high value disables denoise - more elegant & correct way would be do set all edges to 0

            consts.RadiusMultiplier = settings.RadiusMultiplier;
            consts.SampleDistributionPower = settings.SampleDistributionPower;
            consts.ThinOccluderCompensation = settings.ThinOccluderCompensation;
            consts.FinalValuePower = settings.FinalValuePower;
            consts.DepthMIPSamplingOffset = settings.DepthMIPSamplingOffset;
            consts.NoiseIndex = (settings.DenoisePasses > 0) ? (frameCounter % 64) : (0);
            consts.Padding0 = 0;
        }

        static void GTAOUpdateConstants(ref GTAOConstants consts, int viewportWidth, int viewportHeight, in XeGTAOSettings settings, in float clipFar, in float clipNear, float YFOV, float aspect, int frameCounter)
        {
            consts.ViewportSize = new int2(viewportWidth, viewportHeight);
            //consts.ViewportSizeX = viewportWidth;
            //consts.ViewportSizeY = viewportHeight;
            consts.ViewportPixelSize = new Vector2(1.0f / (float)viewportWidth, 1.0f / (float)viewportHeight);

            float depthLinearizeMul = (clipFar * clipNear) / (clipFar - clipNear);     // float depthLinearizeMul = ( clipFar * clipNear ) / ( clipFar - clipNear );
            float depthLinearizeAdd = clipFar / (clipFar - clipNear);     // float depthLinearizeAdd = clipFar / ( clipFar - clipNear );

            // correct the handedness issue. need to make sure this below is correct, but I think it is.
            if (depthLinearizeMul * depthLinearizeAdd < 0)
                depthLinearizeAdd = -depthLinearizeAdd;
            consts.DepthUnpackConsts = new Vector2(depthLinearizeMul, depthLinearizeAdd);

            float tanHalfFOVY = Mathf.Tan(YFOV * 0.5f);    // = tanf( drawContext.Camera.GetYFOV( ) * 0.5f );
            float tanHalfFOVX = tanHalfFOVY * aspect;    // = tanHalfFOVY * drawContext.Camera.GetAspect( );
            consts.CameraTanHalfFOV = new Vector2(tanHalfFOVX, tanHalfFOVY);

            consts.NDCToViewMul = new Vector2(consts.CameraTanHalfFOV.x * 2.0f, consts.CameraTanHalfFOV.y * -2.0f);
            consts.NDCToViewAdd = new Vector2(consts.CameraTanHalfFOV.x * -1.0f, consts.CameraTanHalfFOV.y * 1.0f);

            consts.NDCToViewMul_x_PixelSize = new Vector2(consts.NDCToViewMul.x * consts.ViewportPixelSize.x, consts.NDCToViewMul.y * consts.ViewportPixelSize.y);

            consts.EffectRadius = settings.Radius;

            consts.EffectFalloffRange = settings.FalloffRange;
            consts.DenoiseBlurBeta = (settings.DenoisePasses == 0) ? (1e4f) : (1.2f);    // high value disables denoise - more elegant & correct way would be do set all edges to 0

            consts.RadiusMultiplier = settings.RadiusMultiplier;
            consts.SampleDistributionPower = settings.SampleDistributionPower;
            consts.ThinOccluderCompensation = settings.ThinOccluderCompensation;
            consts.FinalValuePower = settings.FinalValuePower;
            consts.DepthMIPSamplingOffset = settings.DepthMIPSamplingOffset;
            consts.NoiseIndex = (settings.DenoisePasses > 0) ? (frameCounter % 64) : (0);
            consts.Padding0 = 0;
        }
    }

}

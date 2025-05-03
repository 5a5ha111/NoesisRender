using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


namespace NoesisRender.Passes
{
    using ResourcesHolders;
    using static Unity.Burst.Intrinsics.X86.Avx;


    /// <summary>
    /// Screen Space Reflection Pass
    /// </summary>
    public class SSRPass
    {
        static readonly ProfilingSampler sampler = new("SSRPass");

        Camera camera;
        Material m_ssr;
        RenderTexture[] gbuffers;

        TextureHandle colorHandle;
        TextureHandle colorCopyHandle;

        readonly int sourceTex = Shader.PropertyToID("_SourceTexture");

        readonly int _GbufferTex0 = Shader.PropertyToID("_GBuffer0");
        readonly int _GbufferTex1 = Shader.PropertyToID("_GBuffer1");
        readonly int _GbufferTex2 = Shader.PropertyToID("_GBuffer2");
        readonly int _GbufferTex3 = Shader.PropertyToID("_GBuffer3");

        void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            cmd.Blit(colorHandle, colorCopyHandle);
            cmd.SetGlobalTexture(sourceTex, colorCopyHandle);
            cmd.SetGlobalTexture(_GbufferTex1, gbuffers[1]);
            cmd.SetGlobalMatrix("_InverseView", camera.cameraToWorldMatrix);
            cmd.SetGlobalMatrix("_ViewProjectionMatrix", camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix);
            cmd.SetRenderTarget
            (
                colorHandle
            );
            cmd.DrawProcedural(Matrix4x4.identity, m_ssr, 0, MeshTopology.Triangles, 3);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record
        (
            RenderGraph renderGraph, Camera camera, in CameraRendererTextures textures, Material m_ssr, ref RenderTexture[] renderTargets, CustomRenderPipelineSettings.SSRSettings ssrSettings
        )
        {
            if (ssrSettings.enabled && ssrSettings.shader != null)
            {
                using RenderGraphBuilder builder = renderGraph.AddRenderPass
                (
                    sampler.name, out SSRPass pass, sampler
                );
                pass.camera = camera;
                pass.m_ssr = m_ssr;
                pass.gbuffers = renderTargets;
                builder.ReadWriteTexture(textures.colorAttachment);
                builder.ReadWriteTexture(textures.colorCopy);
                pass.colorHandle = textures.colorAttachment;
                pass.colorCopyHandle = textures.colorCopy;
                //builder.ReadTexture(textures.depthAttachment);
                builder.SetRenderFunc<SSRPass>
                (
                    static (pass, context) => pass.Render(context)
                );
            }
        }
    }
}

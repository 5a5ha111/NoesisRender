using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;


namespace NoesisRender.Passes
{
    using ResourcesHolders;

    public class SkyboxPass
    {
        static readonly ProfilingSampler sampler = new("Skybox");

        Camera camera;

        void Render(RenderGraphContext context)
        {
            context.renderContext.DrawSkybox(camera);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record(RenderGraph renderGraph, Camera camera, in CameraRendererTextures textures)
        {
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                using RenderGraphBuilder builder = renderGraph.AddRenderPass
                (
                    sampler.name, out SkyboxPass pass, sampler
                );
                pass.camera = camera;
                builder.ReadWriteTexture(textures.colorAttachment);
                builder.ReadTexture(textures.depthAttachment);
                builder.SetRenderFunc<SkyboxPass>
                (
                    static (pass, context) => pass.Render(context)
                );
            }
        }
    }
}

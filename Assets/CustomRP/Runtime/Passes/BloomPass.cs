using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

using static PostFXStack;

public class BloomPass
{
    const int maxBloomPyramidLevels = 16;

    static readonly int
        bicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        intensityId = Shader.PropertyToID("_BloomIntensity"),
        thresholdId = Shader.PropertyToID("_BloomThreshold");

    static readonly ProfilingSampler sampler = new("Bloom");

    readonly TextureHandle[] pyramid =
        new TextureHandle[2 * maxBloomPyramidLevels + 1];

    TextureHandle colorSource, bloomResult;

    PostFXStack stack;

    int stepCount;

    void Render(RenderGraphContext context)
    {
        CommandBuffer buffer = context.cmd;
        PostFXSettings.BloomSettings bloom = stack.settings.Bloom;

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(thresholdId, threshold);


        //stack.Draw(buffer, colorSource, colorSource, Pass.Copy);

        stack.Draw(buffer, colorSource, pyramid[0], bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);


        int fromId = 0, toId = 2;
        int i;
        for (i = 0; i < stepCount; i++)
        {
            int midId = toId - 1;
            stack.Draw(buffer, pyramid[fromId], pyramid[midId], Pass.BloomHorizontal);
            stack.Draw(buffer, pyramid[midId], pyramid[toId], Pass.BloomVertical);
            fromId = toId;
            toId += 2;
        }

        buffer.SetGlobalFloat(bicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);

        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(intensityId, 1f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(intensityId, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }

        if (i > 1)
        {
            toId -= 5;
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, pyramid[toId + 1]);
                stack.Draw(buffer, pyramid[fromId], pyramid[toId], combinePass);
                fromId = toId;
                toId -= 2;
            }
        }

        buffer.SetGlobalFloat(intensityId, finalIntensity);
        buffer.SetGlobalTexture(fxSource2Id, colorSource);
        stack.Draw(buffer, pyramid[fromId], bloomResult, finalPass);
    }

    /// <summary>
    /// Record returns a handle for the bloom result, or the original color attachment if the effect is skipped.
    /// </summary>
    /// <param name="renderGraph"></param>
    /// <param name="stack"></param>
    /// <param name="textures"></param>
    /// <returns></returns>
    public static TextureHandle Record
    (
        RenderGraph renderGraph,
        PostFXStack stack,
        in CameraRendererTextures textures
    )
    {
        PostFXSettings.BloomSettings bloom = stack.settings.Bloom;
        Vector2Int size = 
        (   
            bloom.ignoreRenderScale ?
            new Vector2Int(stack.camera.pixelWidth, stack.camera.pixelHeight) :
            stack.bufferSize
        ) / 2;

        if 
        (
            bloom.maxIterations == 0 ||
            bloom.intensity <= 0f ||
            size.y < bloom.downscaleLimit * 2 ||
            size.x < bloom.downscaleLimit * 2
        )
        {
            return textures.colorAttachment;
        }

        using RenderGraphBuilder builder = renderGraph.AddRenderPass
        (
            sampler.name, out BloomPass pass, sampler
        );
        pass.stack = stack;
        pass.colorSource = builder.ReadTexture(textures.colorAttachment);

        var desc = new TextureDesc(size.x, size.y)
        {
            colorFormat = SystemInfo.GetGraphicsFormat
            (
                stack.BufferSettings.allowHDR ? DefaultFormat.HDR : DefaultFormat.LDR
            ),
            name = "Bloom Prefilter"
        };
        TextureHandle[] pyramid = pass.pyramid;
        pyramid[0] = builder.CreateTransientTexture(desc);
        size /= 2;

        int pyramidIndex = 1;
        int i;
        for (i = 0; i < bloom.maxIterations; i++, pyramidIndex += 2)
        {
            if (size.y < bloom.downscaleLimit || size.x < bloom.downscaleLimit)
            {
                break;
            }
            desc.width = size.x;
            desc.height = size.y;
            desc.name = "Bloom Pyramid H";
            pyramid[pyramidIndex] = builder.CreateTransientTexture(desc);
            desc.name = "Bloom Pyramid V";
            pyramid[pyramidIndex + 1] = builder.CreateTransientTexture(desc);
            size /= 2;
        }
        pass.stepCount = i;

        desc.width = stack.bufferSize.x;
        desc.height = stack.bufferSize.y;
        desc.name = "Bloom Result";
        pass.bloomResult = builder.WriteTexture(renderGraph.CreateTexture(desc));
        builder.SetRenderFunc<BloomPass>(static (pass, context) => pass.Render(context));
        return pass.bloomResult;
    }
}
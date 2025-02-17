using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{

    enum Pass
    {
        Copy,
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        BloomPrefilter
    }

    const string bufferName = "Post FX";
    const int maxBloomPyramidLevels = 16;

    int
        bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
    int bloomPyramidId;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;

    Camera camera;

    PostFXSettings settings;

    public bool IsActive => settings != null;


    public PostFXStack()
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }


    public void Setup(
        ScriptableRenderContext context, Camera camera, PostFXSettings settings
    )
    {
        this.context = context;
        this.camera = camera;
        this.settings =
            camera.cameraType <= CameraType.SceneView ? settings : null;
        ApplySceneViewState();
    }

    public void Render(int sourceId)
    {
        //Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        DoBloom(sourceId);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    void Draw(
        RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass
    )
    {
        if (settings.Material == null)
        {
            buffer.Blit(from, BuiltinRenderTextureType.CameraTarget);
            return;
        }

        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.DrawProcedural(
            Matrix4x4.identity, settings.Material, (int)pass,
            MeshTopology.Triangles, 3
        );
    }



    void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");

        PostFXSettings.BloomSettings bloom = settings.Bloom;
        buffer.SetGlobalFloat(
            bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f
        );
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;


        if (
            bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2
        )
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom");
            return;
        }

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);

        RenderTextureFormat format = RenderTextureFormat.Default;
        buffer.GetTemporaryRT(
            bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format
        );
        Draw(sourceId, bloomPrefilterId, Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;

        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }
            int midId = toId - 1;
            buffer.GetTemporaryRT(
                midId, width, height, 0, FilterMode.Bilinear, format
            );
            buffer.GetTemporaryRT(
                toId, width, height, 0, FilterMode.Bilinear, format
            );
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }

        //Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomHorizontal);

        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;

            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, Pass.BloomCombine);
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
            

        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.ReleaseTemporaryRT(bloomPrefilterId);

        buffer.EndSample("Bloom");
    }

}
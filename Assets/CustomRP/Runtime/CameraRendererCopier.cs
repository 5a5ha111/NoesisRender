using UnityEngine;
using UnityEngine.Rendering;

public readonly struct CameraRendererCopier
{
    static readonly int sourceTextureID = Shader.PropertyToID("_SourceTexture"),
        srcBlendID = Shader.PropertyToID("_CameraSrcBlend"),
        dstBlendID = Shader.PropertyToID("_CameraDstBlend");

    static readonly Rect fullViewRect = new(0f, 0f, 1f, 1f);

    static readonly bool copyTextureSupported =
        SystemInfo.copyTextureSupport > CopyTextureSupport.None;

    readonly CameraSettings.FinalBlendMode finalBlendMode;

    public static bool RequiresRenderTargetResetAfterCopy => !copyTextureSupported;

    public readonly Camera Camera => camera;

    readonly Material material;

    readonly Camera camera;

    public CameraRendererCopier(Material material, Camera camera, CameraSettings.FinalBlendMode finalBlendMode)
    {
        this.material = material;
        this.camera = camera;
        this.finalBlendMode = finalBlendMode;
    }

    public readonly void Copy
    (
        CommandBuffer buffer,
        RenderTargetIdentifier from,
        RenderTargetIdentifier to,
        bool isDepth
    )
    {
        if (copyTextureSupported)
        {
            buffer.CopyTexture(from, to);
        }
        else
        {
            CopyByDrawing(buffer, from, to, isDepth);
        }
    }

    public readonly void CopyByDrawing
    (
        CommandBuffer buffer,
        RenderTargetIdentifier from,
        RenderTargetIdentifier to,
        bool isDepth
    )
    {
        buffer.SetGlobalTexture(sourceTextureID, from);
        buffer.SetRenderTarget
        (
            to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural
        (
            Matrix4x4.identity, material, isDepth ? 1 : 0,
            MeshTopology.Triangles, 3
        );
    }

    public readonly void CopyToCameraTarget(CommandBuffer buffer,RenderTargetIdentifier from)
    {
        buffer.SetGlobalFloat(srcBlendID, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(dstBlendID, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(sourceTextureID, from);
        buffer.SetRenderTarget
        (
            BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero && camera.rect == fullViewRect ?
                RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store
        );
        buffer.SetViewport(camera.pixelRect);
        buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
        buffer.SetGlobalFloat(srcBlendID, 1f);
        buffer.SetGlobalFloat(dstBlendID, 0f);
    }
}
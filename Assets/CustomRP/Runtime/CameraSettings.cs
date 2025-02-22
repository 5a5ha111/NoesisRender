using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable] public class CameraSettings
{

    public bool copyColor = true, copyDepth = false;

    [RenderingLayerMaskField] public int renderingLayerMask = -1;
    public bool maskLights = false;

    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;

    public enum RenderScaleMode { Inherit, Multiply, Override }

    public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

    [Space]
    [Space]
    [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)] public float renderScale = 1f;

    [Serializable] public struct FinalBlendMode
    {

        public BlendMode source, destination;
    }

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };

    public float GetRenderScale(float scale)
    {
        return
            renderScaleMode == RenderScaleMode.Inherit ? scale :
            renderScaleMode == RenderScaleMode.Override ? renderScale :
            scale * renderScale;
    }
}
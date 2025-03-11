using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public partial class CustomRenderPipeline : RenderPipeline 
{

    /*bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    CameraBufferSettings cameraBufferSettings;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    int colorLUTResolution;*/


    readonly CustomRenderPipelineSettings settings;
    readonly RenderGraph renderGraph = new("Custom SRP Render Graph");

    static CameraSettings defaultCameraSettings = new CameraSettings();


    CameraRenderer renderer;
    PortalsUnity.Portal[] portals;

    public CustomRenderPipeline(CustomRenderPipelineSettings settings)
    {
        this.settings = settings;
        GraphicsSettings.useScriptableRenderPipelineBatching =
            settings.useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
        renderer = new(settings.cameraRendererShader, settings.cameraDebuggerShader, settings.cameraMotionShader, settings.depthOnlyShader, settings.motionVectorDebug);
    }


    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    { 
        
    }

    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (portals == null)
        {
            var inst = PortalsUnity.ScenePortalManager.instance;
            if (inst != null)
            {
                portals = inst.RequestPortals();
            }
        }

        for (int i = 0; i < cameras.Count; i++)
        {
            var camera = cameras[i];


            if (portals != null && portals.Length > 1)
            {
                for (int j = 0; j < portals.Length; j++)
                {
                    var portal = portals[j];
                    if (portal.CanRender(camera) && portal.gameObject.activeSelf)
                    {
                        portal.PrePortalRender(camera);
                        var res = portal.GetPosAndRot(camera);
                        var preCam = portal.SetPreCamera(camera);
                        for (int k = res.startIndex; k < portal.recursionLimit; k++)
                        {
                            var setupRender = portal.SetCameraRender(camera, k, res.startIndex, res.positions, res.rotations);
                            if (setupRender.canRender)
                            {
                                //setupRender.portalCamera.ResetProjectionMatrix();
                                renderer.Render(renderGraph, context, setupRender.portalCamera, settings);
                            }
                        }
                        portal.EndRender(preCam);
                        portal.PostPortalRender(camera);
                    }
                }
            }


            renderer.Render(renderGraph, context, camera, settings);
        }

        renderGraph.EndFrame();
    }

    public void SetPortals(PortalsUnity.Portal[] portals)
    {
        this.portals = portals;
    }

    /*public override bool IsRenderRequestSupported<RequestData>(Camera camera, RequestData data)
    {
        return true;
    }*/


    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        renderer.Dispose();
        renderGraph.Cleanup();
    }

}
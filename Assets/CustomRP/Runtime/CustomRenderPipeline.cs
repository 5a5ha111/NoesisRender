using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using PortalsUnity;
using UnityEditor;

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

    // To do: make a setting
    private const float minDistanceToFullRender = 3f;

    public CustomRenderPipeline(CustomRenderPipelineSettings settings)
    {
        this.settings = settings;
        GraphicsSettings.useScriptableRenderPipelineBatching =
            settings.useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
        InitializeForEditor();
        renderer = new(settings.cameraRendererShader, settings.cameraDebuggerShader, settings.cameraMotionShader, settings.depthOnlyShader, settings.motionVectorDebug, settings.deferredSettings.deferredShader);
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
        else
        {
            for (var i = 0; i < portals.Length; i++)
            {
                var portal = portals[i];
                portal.ResetFlags();
            }
        }

        for (int i = 0; i < cameras.Count; i++)
        {
            var camera = cameras[i];

            CustomRenderPipelineCamera crpCamera = null;
            if (camera.TryGetComponent(out crpCamera))
            {
                /*cameraSampler = crpCamera.Sampler;
                cameraSettings = crpCamera.Settings;*/
            }

            if (portals != null && portals.Length > 1)
            {
                for (int j = 0; j < portals.Length; j++)
                {
                    var portal = portals[j];
                    if (portal.gameObject.activeSelf && portal.linkedPortal.gameObject.activeSelf && portal.IsValid() && portal.linkedPortal.IsValid() && portal.CanRender(camera, settings.cameraBuffer))
                    {
                        portal.PrePortalRender(camera);
                        var res = portal.GetPosAndRot(camera);
                        var preCam = portal.SetPreCamera(camera, crpCamera);
                        for (int k = res.startIndex; k < portal.recursionLimit; k++)
                        {
                            (bool canRender, Camera portalCamera) setupRender;
                            /*if (k == res.startIndex && res.startIndex >= portal.recursionLimit - 1)
                            {
                                setupRender = portal.SetCameraRender(camera, k, res.startIndex, res.positions, res.rotations, true);
                            }
                            else
                            {
                                setupRender = portal.SetCameraRender(camera, k, res.startIndex, res.positions, res.rotations, false);
                            }*/
                            Vector3 cameraPos = camera.transform.position;
                            Vector3 portalPos = portal.linkedPortal.screen.transform.position;
                            Vector3 portalPos2 = portal.screen.transform.position;
                            float distance = Vector3.Distance(cameraPos, portalPos);
                            float distance2 = Vector3.Distance(cameraPos, portalPos2);
                            distance = Mathf.Min(distance, distance2);
                            bool cropRender = distance > minDistanceToFullRender;
                            if (camera.cameraType == CameraType.SceneView)
                            {
                                cropRender = true;
                            }
                            //bool secondPortalRendered = portal.linkedPortal.CanRender(portal.linkedPortal._camera, settings.cameraBuffer);
                            bool secondPortalRendered = true;
                            /*secondPortalRendered |= portal.CanRender(portal._camera, settings.cameraBuffer);
                            secondPortalRendered |= portal.CanRender(portal.linkedPortal._camera, settings.cameraBuffer);*/

                            /*bool secondPortalRendered1 = PortalCameraUtility.VisibleFromCamera(portal.screen, portal.linkedPortal._camera);
                            bool secondPortalRendered2 = PortalCameraUtility.VisibleFromCamera(portal.linkedPortal.screen, portal._camera);
                            Debug.Log(portal.gameObject.name + " secondPortalRendered1 " + secondPortalRendered1 + " secondPortalRendered2 " + secondPortalRendered2 + " final " + (secondPortalRendered2 || secondPortalRendered1));*/
                            bool visible = portal.CallVisible(camera) || portal.linkedPortal.CallVisible(camera);
                            visible = res.recusrion;
                            secondPortalRendered = visible;
                            if (secondPortalRendered || !cropRender)
                            {
                                portal.linkedPortal._SetNotCroppedFlag = true;
                                portal._SetNotCroppedFlag = true;
                            }
                            //cropRender &= !secondPortalRendered;
                            //Debug.Log(portal.name + " cropRender " + cropRender + " " + distance);
                            //Debug.Log("camera " + cameraPos + " portal " + portalPos + " Distance " + distance);
                            setupRender = portal.SetCameraRender(camera, k, res.startIndex, res.positions, res.rotations, cropRender);
                            if (setupRender.canRender)
                            {
                                //setupRender.portalCamera.ResetProjectionMatrix();
                                renderer.Render(renderGraph, context, setupRender.portalCamera, portal._CRP, settings);
                            }
                        }
                        portal.EndRender(preCam);
                        portal.PostPortalRender(camera);
                    }
                }
            }


            renderer.Render(renderGraph, context, camera, crpCamera, settings);
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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;


namespace NoesisRender
{
    using Portals;

    public partial class CustomRenderPipeline : RenderPipeline
    {

        readonly CustomRenderPipelineSettings settings;
        readonly RenderGraph renderGraph = new("Custom SRP Render Graph");

        static CameraSettings defaultCameraSettings = new CameraSettings();


        CameraRenderer renderer;
        Portal[] portals;

        // To do: make a setting
        private const float minDistanceToFullRenderPortal = 3f;

        public CustomRenderPipeline(CustomRenderPipelineSettings settings)
        {
            this.settings = settings;
            GraphicsSettings.useScriptableRenderPipelineBatching =
                settings.useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            InitializeForEditor();
            renderer = new(settings.cameraRendererShader, settings.cameraDebuggerShader, settings.cameraMotionShader, settings.depthOnlyShader, settings.motionVectorDebug, settings.deferredSettings.deferredShader, settings.xeGTAOsettings.XeGTAOApply, settings.SSRsettings.shader);
        }


        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {

        }

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (portals == null)
            {
                var inst = ScenePortalManager.instance;
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
                                Vector3 cameraPos = camera.transform.position;
                                Vector3 portalPos = portal.linkedPortal.screen.transform.position;
                                Vector3 portalPos2 = portal.screen.transform.position;
                                float distance = Vector3.Distance(cameraPos, portalPos);
                                float distance2 = Vector3.Distance(cameraPos, portalPos2);
                                distance = Mathf.Min(distance, distance2);
                                bool cropRender = distance > minDistanceToFullRenderPortal;
                                if (camera.cameraType == CameraType.SceneView)
                                {
                                    cropRender = true;
                                }
                                bool secondPortalRendered = true;
                                bool visible = portal.CallVisible(camera) || portal.linkedPortal.CallVisible(camera);
                                visible = res.recusrion;
                                secondPortalRendered = visible;
                                if (secondPortalRendered || !cropRender)
                                {
                                    portal.linkedPortal._SetNotCroppedFlag = true;
                                    portal._SetNotCroppedFlag = true;
                                }
                                setupRender = portal.SetCameraRender(camera, k, res.startIndex, res.positions, res.rotations, cropRender);
                                if (setupRender.canRender)
                                {
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

        public void SetPortals(Portal[] portals)
        {
            this.portals = portals;
        }


        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DisposeForEditor();
            renderer.Dispose();
            renderGraph.Cleanup();
        }

    }
}
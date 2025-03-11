using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace PortalsUnity
{
    public class Portal : MonoBehaviour
    {
        [Header("Main Settings")]
        public Portal linkedPortal;
        public MeshRenderer screen;
        [SerializeField] Camera portalCam;
        public int recursionLimit = 5;

        [Header("Advanced Settings")]
        public float nearClipOffset = 0.05f;
        public float nearClipLimit = 0.2f;



        // Private variables
        RenderTexture viewTexture;
        RenderTexture shadowTexture;
        //Camera playerCam;
        Material firstRecursionMat;
        List<PortalTraveller> trackedTravellers;
        MeshFilter screenMeshFilter;

        private Matrix4x4? lastLoggedMatrix = null;


        private const string viewTex = "";
        private const string shadowTex = "_ShadowTex";
        //private const string mainTex = "_BaseMap";
        private const string mainTex = "_MainTex";
        private const int shadowWritePass = 0;

        private static readonly int ScreenSpaceShadowmapTexture = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");

        /// <summary>
        /// Based on thickness of the frame of screen. If camera near clip plane is too big, portal box will be too big, and can be visible outside frame.
        /// </summary>
        private const float maxNearClippingPlane = 0.06f;
        private const float minNearClippingPlane = 0.00001f;
        private const float maxFarClippingPlane = 1000f;


        void Awake()
        {
            //playerCam = Camera.main;
            if (portalCam == null)
            {
                portalCam = GetComponentInChildren<Camera>();
            }
            if (portalCam != null)
            {
                portalCam.enabled = false;
                var portalCamType = portalCam.GetComponent<CustomRenderPipelineCamera>();
                if (portalCamType != null )
                {
                    // Portals cameras render to texture, that later will be displayed on mesh and then render to main camera, so it cannot have postFX
                    portalCamType.Settings.CameraType = CameraSettings.CameraTypeSetting.Portal;
                    portalCamType.Settings.allowDLSS = false;
                    portalCamType.Settings.allowFXAA = false;
                    portalCamType.Settings.overridePostFX = true;
                    portalCamType.Settings.renderMotionVectors = false;
                }
            }
            trackedTravellers = new List<PortalTraveller>();
            if (screen != null)
            {
                screenMeshFilter = screen.GetComponent<MeshFilter>();
                screen.material.SetInt("displayMask", 1);
            }
        }

        void LateUpdate()
        {
            HandleTravellers();
        }

        [ContextMenu("Update tex&shadows")]
        public void UptEvr()
        {
            SetShadowTexture();
            linkedPortal.UptEvr();
        }

        void HandleTravellers()
        {

            for (int i = 0; i < trackedTravellers.Count; i++)
            {
                PortalTraveller traveller = trackedTravellers[i];
                Transform travellerT = traveller.transform;
                var m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;

                Vector3 offsetFromPortal = travellerT.position - transform.position;
                int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
                int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));
                // Teleport the traveller if it has crossed from one side of the portal to the other
                if (portalSide != portalSideOld)
                {
                    var positionOld = travellerT.position;
                    var rotOld = travellerT.rotation;
                    traveller.Teleport(transform, linkedPortal.transform, m.GetColumn(3), m.rotation);
                    traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotOld);
                    // Can't rely on OnTriggerEnter/Exit to be called next frame since it depends on when FixedUpdate runs
                    linkedPortal.OnTravellerEnterPortal(traveller);
                    trackedTravellers.RemoveAt(i);
                    i--;
                }
                else
                {
                    traveller.graphicsClone.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                    //UpdateSliceParams (traveller);
                    traveller.previousOffsetFromPortal = offsetFromPortal;
                }
            }
        }

        /// <summary>
        /// Called before any portal cameras are rendered for the current frame
        /// </summary>
        /// <param name="playerCam"></param>
        public void PrePortalRender(Camera playerCam)
        {
            foreach (var traveller in trackedTravellers)
            {
                UpdateSliceParams(playerCam, traveller);
            }
        }

        /// <summary>
        /// Manually render the camera attached to this portal
        /// Called after PrePortalRender, and before PostPortalRender
        /// </summary>
        /// <param name="playerCam"></param>
        public void Render(Camera playerCam)
        {
            if (linkedPortal == null || screen == null || portalCam == null || playerCam == null)
            {
                return;
            }


            // Skip rendering the view from this portal if player is not looking at the linked portal
            if (!PortalCameraUtility.VisibleFromCamera(linkedPortal.screen, playerCam))
            {
                return;
            }

            CreateViewTexture();
            //SetShadowTexture();

            var localToWorldMatrix = playerCam.transform.localToWorldMatrix;
            var renderPositions = new Vector3[recursionLimit];
            var renderRotations = new Quaternion[recursionLimit];

            int startIndex = 0;
            portalCam.projectionMatrix = playerCam.projectionMatrix;
            for (int i = 0; i < recursionLimit; i++)
            {
                if (i > 0)
                {
                    // No need for recursive rendering if linked portal is not visible through this portal
                    if (!PortalCameraUtility.BoundsOverlap(screenMeshFilter, linkedPortal.screenMeshFilter, portalCam))
                    {
                        break;
                    }
                    /*Camera recursiveCam = linkedPortal.portalCam;
                    MeshRenderer portalRenderer = screen;
                    if (i % 2 != 0) 
                    {
                        recursiveCam = portalCam;
                        portalRenderer = linkedPortal.screen;
                    }
                    if (!PortalCameraUtility.VisibleFromCamera(portalRenderer, recursiveCam))
                    {
                        break;
                    }*/
                }
                localToWorldMatrix = transform.localToWorldMatrix * linkedPortal.transform.worldToLocalMatrix * localToWorldMatrix;
                int renderOrderIndex = recursionLimit - i - 1;
                renderPositions[renderOrderIndex] = localToWorldMatrix.GetColumn(3);
                renderRotations[renderOrderIndex] = localToWorldMatrix.rotation;

                portalCam.transform.SetPositionAndRotation(renderPositions[renderOrderIndex], renderRotations[renderOrderIndex]);
                startIndex = renderOrderIndex;
            }

            // Hide screen so that camera can see through portal
            var tempShadowCastValue = screen.shadowCastingMode;
            screen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            screen.enabled = false;
            linkedPortal.screen.material.SetInt("displayMask", 0);

            for (int i = startIndex; i < recursionLimit; i++)
            {
                portalCam.transform.SetPositionAndRotation(renderPositions[i], renderRotations[i]);
                SetNearClipPlane(playerCam);
                HandleClipping(playerCam);

                // avoid frustrum on Corners
                Vector3[] frustumCorners = new Vector3[4];
                bool FrustumError = false;

                // Calculate Corners for my portalCamera :
                portalCam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), portalCam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

                // Check for NaN in any of the 4 vectors (checking x is enough since NaN occurs in all 3 components)
                for (int j = 0; j < 4; j++)
                {
                    if (float.IsNaN(frustumCorners[j].x))
                    {
                        Debug.Log("Frustrum NaN on Vector #" + j);
                        FrustumError = true;
                    }
                }

                if (!FrustumError)
                {
                    try
                    {
                        //portalCam.Render();
                        //SetShadowTexture();

                        // Create a standard request
                        RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest();
                        // Check if the request is supported by the active render pipeline
                        if (RenderPipeline.SupportsRenderRequest(playerCam, request))
                        {
                            Debug.Log("Support request");
                            request.destination = viewTexture;
                            RenderPipeline.SubmitRenderRequest(portalCam, request);
                            return;
                        }
                        else
                        {
                            Debug.Log("Dont support");
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Frustrum catched despite checking " + e);
                    }
                }


                if (i == startIndex)
                {
                    linkedPortal.screen.material.SetInt("displayMask", 1);
                }
            }

            // Unhide objects hidden at start of render
            screen.shadowCastingMode = tempShadowCastValue;
            screen.enabled = true;
        }

        /// <summary>
        /// Check if portal is working and if camera can see it
        /// </summary>
        /// <param name="playerCam"></param>
        /// <returns></returns>
        public bool CanRender(Camera playerCam)
        {
            if (linkedPortal == null || screen == null || portalCam == null || playerCam == null)
            {
                return false;
            }
            // Skip rendering the view from this portal if player is not looking at the linked portal
            if (!PortalCameraUtility.VisibleFromCamera(linkedPortal.screen, playerCam))
            {
                return false;
            }

            CreateViewTexture();
            return true;
        }
        public (Vector3[] positions, Quaternion[] rotations, int startIndex, int loopSize) GetPosAndRot(Camera playerCam)
        {
            var localToWorldMatrix = playerCam.transform.localToWorldMatrix;
            var renderPositions = new Vector3[recursionLimit];
            var renderRotations = new Quaternion[recursionLimit];

            int startIndex = 0;
            int loopSize = 0;
            portalCam.projectionMatrix = playerCam.projectionMatrix;
            for (int i = 0; i < recursionLimit; i++, loopSize++)
            {
                if (i > 0)
                {
                    // No need for recursive rendering if linked portal is not visible through this portal
                    if (!PortalCameraUtility.BoundsOverlap(screenMeshFilter, linkedPortal.screenMeshFilter, portalCam))
                    {
                        break;
                    }
                    /*Camera recursiveCam = linkedPortal.portalCam;
                    MeshRenderer portalRenderer = screen;
                    if (i % 2 != 0) 
                    {
                        recursiveCam = portalCam;
                        portalRenderer = linkedPortal.screen;
                    }
                    if (!PortalCameraUtility.VisibleFromCamera(portalRenderer, recursiveCam))
                    {
                        break;
                    }*/
                }
                localToWorldMatrix = transform.localToWorldMatrix * linkedPortal.transform.worldToLocalMatrix * localToWorldMatrix;
                int renderOrderIndex = recursionLimit - i - 1;
                renderPositions[renderOrderIndex] = localToWorldMatrix.GetColumn(3);
                renderRotations[renderOrderIndex] = localToWorldMatrix.rotation;

                portalCam.transform.SetPositionAndRotation(renderPositions[renderOrderIndex], renderRotations[renderOrderIndex]);
                startIndex = renderOrderIndex;
            }

            return (renderPositions, renderRotations, startIndex, loopSize);
        }
        public ShadowCastingMode SetPreCamera(Camera playerCam)
        {
            // Hide screen so that camera can see through portal
            var tempShadowCastValue = screen.shadowCastingMode;
            screen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            screen.enabled = false;
            linkedPortal.screen.material.SetInt("displayMask", 0);
            playerCam.fieldOfView = playerCam.fieldOfView;
            return tempShadowCastValue;
        }
        public (bool canRender, Camera portalCamera) SetCameraRender(Camera playerCam, int index, int startIndex, Vector3[] renderPositions, Quaternion[] renderRotations)
        {
            portalCam.transform.SetPositionAndRotation(renderPositions[index], renderRotations[index]);
            SetNearClipPlane(playerCam);
            HandleClipping(playerCam);

            // avoid frustrum on Corners
            Vector3[] frustumCorners = new Vector3[4];
            bool FrustumError = false;

            Rect portalCamRect = new Rect(0, 0f, 0.5f, 0.5f);
            Rect normalRect = new Rect(0f, 0f, 1, 1f);
            //portalCam.ResetProjectionMatrix();
            var resMeshProj = PortalCameraUtility.ApplyMeshProjection(portalCam, playerCam, portalCam.projectionMatrix, linkedPortal.screenMeshFilter, playerCam.fieldOfView);
            var meshRect = resMeshProj.rect;
            /*if (Mathf.Max(meshRect.max.x, meshRect.max.y) > 1)
            {
                Debug.LogError(meshRect);
                FrustumError = true;
            }*/
            FrustumError |= !resMeshProj.valid;

            //Rect meshViewportRect = PortalCameraUtility.GetMeshViewRect(playerCam, screenMeshFilter);
            //portalCam.rect = meshViewportRect;

            //var meshRect = normalRect;
            // Calculate Corners for my portalCamera :
            portalCam.CalculateFrustumCorners(meshRect, portalCam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);
            //portalCam.rect = meshRect;
            //portalCam.fieldOfView = Camera.VerticalToHorizontalFieldOfView(playerCam.fieldOfView, portalCamRect.width / portalCamRect.height);

            //portalCam.

            // Check for NaN in any of the 4 vectors (checking x is enough since NaN occurs in all 3 components)
            for (int j = 0; j < 4; j++)
            {
                if (float.IsNaN(frustumCorners[j].x))
                {
                    Debug.Log("Frustrum NaN on Vector #" + j);
                    FrustumError = true;
                }
                if (float.IsNaN(frustumCorners[j].y))
                {
                    Debug.Log("Frustrum NaN on Vector #" + j);
                    FrustumError = true;
                }
                if (float.IsNaN(frustumCorners[j].z))
                {
                    Debug.Log("Frustrum NaN on Vector #" + j);
                    FrustumError = true;
                }
            }

            Rect viewport = meshRect;
            float viewportXMin = viewport.x;
            float viewportXMax = viewport.x + viewport.width;
            float viewportYMin = viewport.y;
            float viewportYMax = viewport.y + viewport.height;
            Rect frustumRect = new Rect(viewportXMin, viewportYMin, viewportXMax - viewportXMin, viewportYMax - viewportYMin);
            portalCam.CalculateFrustumCorners(frustumRect, portalCam.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

            foreach (Vector3 corner in frustumCorners)
            {
                Vector4 worldPoint = new Vector4(corner.x, corner.y, corner.z, 1.0f);
                Vector4 projected = portalCam.projectionMatrix * worldPoint;

                if (projected.w == 0)
                {
                    Debug.LogError("Invalid projection matrix: division by zero in homogeneous coordinates.");
                    FrustumError = true;
                }

                /*Vector3 ndc = new Vector3(projected.x / projected.w, projected.y / projected.w, projected.z / projected.w);
                if (ndc.x < -1 || ndc.x > 1 || ndc.y < -1 || ndc.y > 1 || ndc.z < 0 || ndc.z > 1)
                {
                    Debug.LogError("Projection matrix produces out-of-bounds NDC coordinates.");
                    FrustumError = true;
                }*/
            }
            //FrustumError |= PortalCameraUtility.IsMatrixValid();

            if (index == startIndex)
            {
                linkedPortal.screen.material.SetInt("displayMask", 1);
            }
            //return (true, portalCam);

            if (!FrustumError)
            {
                try
                {
                    return (true, portalCam);
                    //portalCam.Render();
                    //SetShadowTexture();

                    // Create a standard request
                    //RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest();
                    // Check if the request is supported by the active render pipeline
                    /*if (RenderPipeline.SupportsRenderRequest(playerCam, request))
                    {
                        Debug.Log("Support request");
                        request.destination = viewTexture;
                        RenderPipeline.SubmitRenderRequest(portalCam, request);
                        return;
                    }
                    else
                    {
                        Debug.Log("Dont support");
                        return;
                    }*/
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Frustrum catched despite checking " + e);
                    return (false, null);
                }
            }
            return (false, null);
        }
        public void EndRender(ShadowCastingMode tempShadowCastValue)
        {
            linkedPortal.screen.material.SetInt("displayMask", 1);
            screen.shadowCastingMode = tempShadowCastValue;
            screen.enabled = true;
        }


        void HandleClipping(Camera playerCam)
        {
            // There are two main graphical issues when slicing travellers
            // 1. Tiny sliver of mesh drawn on backside of portal
            //    Ideally the oblique clip plane would sort this out, but even with 0 offset, tiny sliver still visible
            // 2. Tiny seam between the sliced mesh, and the rest of the model drawn onto the portal screen
            // This function tries to address these issues by modifying the slice parameters when rendering the view from the portal
            // Would be great if this could be fixed more elegantly, but this is the best I can figure out for now
            const float hideDst = -1000;
            const float showDst = 1000;
            float screenThickness = linkedPortal.ProtectScreenFromClipping(playerCam, playerCam.transform.position);

            foreach (var traveller in trackedTravellers)
            {
                if (SameSideOfPortal(traveller.transform.position, portalCamPos))
                {
                    // Addresses issue 1
                    traveller.SetSliceOffsetDst(hideDst, false);
                }
                else
                {
                    // Addresses issue 2
                    traveller.SetSliceOffsetDst(showDst, false);
                }

                // Ensure clone is properly sliced, in case it's visible through this portal:
                int cloneSideOfLinkedPortal = -SideOfPortal(traveller.transform.position);
                bool camSameSideAsClone = linkedPortal.SideOfPortal(portalCamPos) == cloneSideOfLinkedPortal;
                if (camSameSideAsClone)
                {
                    traveller.SetSliceOffsetDst(screenThickness, true);
                }
                else
                {
                    traveller.SetSliceOffsetDst(-screenThickness, true);
                }
            }

            var offsetFromPortalToCam = portalCamPos - transform.position;
            foreach (var linkedTraveller in /*linkedPortal.trackedTravellers*/trackedTravellers)
            {
                var travellerPos = linkedTraveller.graphicsObject.transform.position;
                var clonePos = linkedTraveller.graphicsClone.transform.position;
                // Handle clone of linked portal coming through this portal:
                bool cloneOnSameSideAsCam = linkedPortal.SideOfPortal(travellerPos) != SideOfPortal(portalCamPos);
                if (cloneOnSameSideAsCam)
                {
                    // Addresses issue 1
                    linkedTraveller.SetSliceOffsetDst(hideDst, true);
                }
                else
                {
                    // Addresses issue 2
                    linkedTraveller.SetSliceOffsetDst(showDst, true);
                }

                // Ensure traveller of linked portal is properly sliced, in case it's visible through this portal:
                bool camSameSideAsTraveller = linkedPortal.SameSideOfPortal(linkedTraveller.transform.position, portalCamPos);
                if (camSameSideAsTraveller)
                {
                    linkedTraveller.SetSliceOffsetDst(screenThickness, false);
                }
                else
                {
                    linkedTraveller.SetSliceOffsetDst(-screenThickness, false);
                }
            }
        }

        /// <summary>
        /// Called once all portals have been rendered, but before the player camera renders
        /// </summary>
        /// <param name="playerCam"></param>
        public void PostPortalRender(Camera playerCam)
        {
            foreach (var traveller in trackedTravellers)
            {
                UpdateSliceParams(playerCam, traveller);
            }
            ProtectScreenFromClipping(playerCam, playerCam.transform.position);
        }
        void CreateViewTexture()
        {
            if (viewTexture == null || viewTexture.width != Screen.width || viewTexture.height != Screen.height)
            {
                if (viewTexture != null)
                {
                    viewTexture.Release();
                }
                viewTexture = new RenderTexture(Screen.width, Screen.height, 0);
                viewTexture.autoGenerateMips = false;
                viewTexture.depth = 0;
                viewTexture.name = "PortalCam " + gameObject.name + " RenderTexture";
                // Render the view from the portal camera to the view texture
                portalCam.targetTexture = viewTexture;
                // Display the view texture on the screen of the linked portal
                linkedPortal.screen.material.SetTexture(mainTex, viewTexture);
            }
        }
        void SetShadowTexture()
        {
            var localToWorld = linkedPortal.screen.transform.localToWorldMatrix;


            return;
            /*if (shadowTexture == null || shadowTexture.width != Screen.width || shadowTexture.height != Screen.height)
            {
                if (shadowTexture != null)
                {
                    shadowTexture.Release();
                }
                shadowTexture = new RenderTexture(Screen.width, Screen.height, 0);
            }
            // Render the view from the portal camera to the view texture
            Graphics.Blit(shadowTexture, shadowTexture, screen.material, shadowWritePass);
            // Display the view texture on the screen of the linked portal
            linkedPortal.screen.material.SetTexture(shadowTex, shadowTexture);*/
        }

        /// <summary>
        /// Sets the thickness of the portal screen so as not to clip with camera near plane when player goes through
        /// </summary>
        /// <param name="playerCam"></param>
        /// <param name="viewPoint"></param>
        /// <returns></returns>
        float ProtectScreenFromClipping(Camera playerCam, Vector3 viewPoint)
        {
            float nearPlane = Mathf.Min(playerCam.nearClipPlane, maxNearClippingPlane);
            float halfHeight = nearPlane * Mathf.Tan(playerCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * playerCam.aspect;
            float dstToNearClipPlaneCorner = new Vector3(halfWidth, halfHeight, nearPlane).magnitude;
            float screenThickness = dstToNearClipPlaneCorner;

            Transform screenT = screen.transform;
            bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - viewPoint) > 0;
            screenT.localScale = new Vector3(screenT.localScale.x, screenT.localScale.y, screenThickness);
            screenT.localPosition = Vector3.forward * screenThickness * ((camFacingSameDirAsPortal) ? 0.5f : -0.5f);
            return screenThickness;
        }

        void UpdateSliceParams(Camera playerCam, PortalTraveller traveller)
        {
            // Calculate slice normal
            int side = SideOfPortal(traveller.transform.position);
            Vector3 sliceNormal = transform.forward * -side;
            Vector3 cloneSliceNormal = linkedPortal.transform.forward * side;

            // Calculate slice centre
            Vector3 slicePos = transform.position;
            Vector3 cloneSlicePos = linkedPortal.transform.position;

            // Adjust slice offset so that when player standing on other side of portal to the object, the slice doesn't clip through
            float sliceOffsetDst = 0;
            float cloneSliceOffsetDst = 0;
            float screenThickness = screen.transform.localScale.z;

            bool playerSameSideAsTraveller = SameSideOfPortal(playerCam.transform.position, traveller.transform.position);
            if (!playerSameSideAsTraveller)
            {
                sliceOffsetDst = -screenThickness;
            }
            bool playerSameSideAsCloneAppearing = side != linkedPortal.SideOfPortal(playerCam.transform.position);
            if (!playerSameSideAsCloneAppearing)
            {
                cloneSliceOffsetDst = -screenThickness;
            }

            // Apply parameters
            for (int i = 0; i < traveller.originalMaterials.Length; i++)
            {
                traveller.originalMaterials[i].SetVector("sliceCentre", slicePos);
                traveller.originalMaterials[i].SetVector("sliceNormal", sliceNormal);
                traveller.originalMaterials[i].SetFloat("sliceOffsetDst", sliceOffsetDst);

                traveller.cloneMaterials[i].SetVector("sliceCentre", cloneSlicePos);
                traveller.cloneMaterials[i].SetVector("sliceNormal", cloneSliceNormal);
                traveller.cloneMaterials[i].SetFloat("sliceOffsetDst", cloneSliceOffsetDst);

            }

        }

        // Use custom projection matrix to align portal camera's near clip plane with the surface of the portal
        // Note that this affects precision of the depth buffer, which can cause issues with effects like screenspace AO
        void SetNearClipPlane(Camera playerCam)
        {
            // Learning resource:
            // http://www.terathon.com/lengyel/Lengyel-Oblique.pdf
            Transform clipPlane = transform;
            int dot = System.Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCam.transform.position));

            Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
            Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
            float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

            // Don't use oblique clip plane if very close to portal as it seems this can cause some visual artifacts
            if (Mathf.Abs(camSpaceDst) > nearClipLimit)
            {
                Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);


                // Update projection based on new clip plane
                // Calculate matrix with player cam so that player camera settings (fov, etc) are used
                portalCam.projectionMatrix = playerCam.CalculateObliqueMatrix(clipPlaneCameraSpace);

                //LogMatrix(portalCam.projectionMatrix, " portalCam.projectionMatrix");
            }
            else
            {
                portalCam.projectionMatrix = playerCam.projectionMatrix;
            }

            if (portalCam.nearClipPlane < minNearClippingPlane)
            {
                portalCam.nearClipPlane = minNearClippingPlane;
            }
            if (portalCam.farClipPlane > maxFarClippingPlane)
            {
                portalCam.farClipPlane = maxFarClippingPlane;
            }
        }

        void OnTravellerEnterPortal(PortalTraveller traveller)
        {
            if (!trackedTravellers.Contains(traveller))
            {
                traveller.EnterPortalThreshold();
                traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
                trackedTravellers.Add(traveller);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var traveller = other.GetComponent<PortalTraveller>();
            if (traveller)
            {
                OnTravellerEnterPortal(traveller);
            }
        }

        void OnTriggerExit(Collider other)
        {
            var traveller = other.GetComponent<PortalTraveller>();
            if (traveller && trackedTravellers.Contains(traveller))
            {
                traveller.ExitPortalThreshold();
                trackedTravellers.Remove(traveller);
            }
        }

        /*
         ** Some helper/convenience stuff:
         */

        public void LogMatrix(Matrix4x4 matrix, string label = "Matrix")
        {
            if (lastLoggedMatrix.HasValue && matrix == lastLoggedMatrix.Value)
            {
                return;
            }

            lastLoggedMatrix = matrix;

            Debug.Log($"{label}:\n" +
                      $"[{matrix.m00,8:F4}, {matrix.m01,8:F4}, {matrix.m02,8:F4}, {matrix.m03,8:F4}]\n" +
                      $"[{matrix.m10,8:F4}, {matrix.m11,8:F4}, {matrix.m12,8:F4}, {matrix.m13,8:F4}]\n" +
                      $"[{matrix.m20,8:F4}, {matrix.m21,8:F4}, {matrix.m22,8:F4}, {matrix.m23,8:F4}]\n" +
                      $"[{matrix.m30,8:F4}, {matrix.m31,8:F4}, {matrix.m32,8:F4}, {matrix.m33,8:F4}]");
        }

        int SideOfPortal(Vector3 pos)
        {
            return System.Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
        }

        bool SameSideOfPortal(Vector3 posA, Vector3 posB)
        {
            return SideOfPortal(posA) == SideOfPortal(posB);
        }

        Vector3 portalCamPos
        {
            get
            {
                return portalCam.transform.position;
            }
        }

        void OnValidate()
        {
            if (linkedPortal != null)
            {
                linkedPortal.linkedPortal = this;
            }
        }
    }
}

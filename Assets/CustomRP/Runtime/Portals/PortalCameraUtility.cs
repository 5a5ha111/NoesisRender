using UnityEngine;

namespace NoesisRender.Portals
{
    public static class PortalCameraUtility
    {
        static readonly Vector3[] cubeCornerOffsets =
        {
        new Vector3 (1, 1, 1),
        new Vector3 (-1, 1, 1),
        new Vector3 (-1, -1, 1),
        new Vector3 (-1, -1, -1),
        new Vector3 (-1, 1, -1),
        new Vector3 (1, -1, -1),
        new Vector3 (1, 1, -1),
        new Vector3 (1, -1, 1),
    };

        // http://wiki.unity3d.com/index.php/IsVisibleFrom
        public static bool VisibleFromCamera(Renderer renderer, Camera camera)
        {
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
        }

        public static bool BoundsOverlap(MeshFilter nearObject, MeshFilter farObject, Camera camera)
        {
            var near = GetScreenRectFromBounds(nearObject, camera);
            var far = GetScreenRectFromBounds(farObject, camera);

            // ensure far object is indeed further away than near object
            if (far.zMax > near.zMin)
            {
                // Doesn't overlap on x axis
                if (far.xMax < near.xMin || far.xMin > near.xMax)
                {
                    return false;
                }
                // Doesn't overlap on y axis
                if (far.yMax < near.yMin || far.yMin > near.yMax)
                {
                    return false;
                }
                // Overlaps
                return true;
            }
            return false;
        }

        // With thanks to http://www.turiyaware.com/a-solution-to-unitys-camera-worldtoscreenpoint-causing-ui-elements-to-display-when-object-is-behind-the-camera/
        public static MinMax3D GetScreenRectFromBounds(MeshFilter renderer, Camera mainCamera)
        {
            MinMax3D minMax = new MinMax3D(float.MaxValue, float.MinValue);

            Vector3[] screenBoundsExtents = new Vector3[8];
            if (renderer == null)
            {
                Debug.LogError("renderer == null");
                return new MinMax3D();
            }
            if (renderer.sharedMesh == null)
            {
                Debug.LogError("renderer.sharedMesh == null");
                return new MinMax3D();
            }
            var localBounds = renderer.sharedMesh.bounds;
            bool anyPointIsInFrontOfCamera = false;

            for (int i = 0; i < 8; i++)
            {
                Vector3 localSpaceCorner = localBounds.center + Vector3.Scale(localBounds.extents, cubeCornerOffsets[i]);
                Vector3 worldSpaceCorner = renderer.transform.TransformPoint(localSpaceCorner);
                Vector3 viewportSpaceCorner = mainCamera.WorldToViewportPoint(worldSpaceCorner);

                if (viewportSpaceCorner.z > 0)
                {
                    anyPointIsInFrontOfCamera = true;
                }
                else
                {
                    // If point is behind camera, it gets flipped to the opposite side
                    // So clamp to opposite edge to correct for this
                    viewportSpaceCorner.x = (viewportSpaceCorner.x <= 0.5f) ? 1 : 0;
                    viewportSpaceCorner.y = (viewportSpaceCorner.y <= 0.5f) ? 1 : 0;
                }

                // Update bounds with new corner point
                minMax.AddPoint(viewportSpaceCorner);
            }

            // All points are behind camera so just return empty bounds
            if (!anyPointIsInFrontOfCamera)
            {
                return new MinMax3D();
            }

            return minMax;
        }

        public static Rect GetMeshViewRect(MeshFilter meshFilter, Camera camera)
        {
            // Compute the bounding box of the triangleMesh in local space.
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            // Transform all 8 corners from local to world space.
            Vector3[] worldCorners = new Vector3[8];
            worldCorners[0] = meshFilter.transform.TransformPoint(localBounds.min);
            worldCorners[1] = meshFilter.transform.TransformPoint(new Vector3(localBounds.min.x, localBounds.min.y, localBounds.max.z));
            worldCorners[2] = meshFilter.transform.TransformPoint(new Vector3(localBounds.min.x, localBounds.max.y, localBounds.min.z));
            worldCorners[3] = meshFilter.transform.TransformPoint(new Vector3(localBounds.min.x, localBounds.max.y, localBounds.max.z));
            worldCorners[4] = meshFilter.transform.TransformPoint(new Vector3(localBounds.max.x, localBounds.min.y, localBounds.min.z));
            worldCorners[5] = meshFilter.transform.TransformPoint(new Vector3(localBounds.max.x, localBounds.min.y, localBounds.max.z));
            worldCorners[6] = meshFilter.transform.TransformPoint(new Vector3(localBounds.max.x, localBounds.max.y, localBounds.min.z));
            worldCorners[7] = meshFilter.transform.TransformPoint(localBounds.max);

            // Project each world corner into viewport space.
            Vector2 minViewport = new Vector2(1f, 1f);
            Vector2 maxViewport = new Vector2(0f, 0f);
            foreach (Vector3 corner in worldCorners)
            {
                Vector3 vpPoint = camera.WorldToViewportPoint(corner);
                // Update the min and max viewport coordinates.
                minViewport = Vector2.Min(minViewport, new Vector2(vpPoint.x, vpPoint.y));
                maxViewport = Vector2.Max(maxViewport, new Vector2(vpPoint.x, vpPoint.y));
            }

            // Create a viewport rectangle from the computed min and max values.
            // This rectangle represents the triangleMesh's area in normalized viewport coordinates.
            Rect meshViewportRect = new Rect(minViewport.x, minViewport.y, maxViewport.x - minViewport.x, maxViewport.y - minViewport.y);

            // --- Adjust the camera's projection ---
            // Compute the original frustum boundaries at the near clip plane.
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            float fov = camera.fieldOfView;
            float aspect = camera.aspect;
            float halfHeight = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * aspect;
            float left = -halfWidth;
            float right = halfWidth;
            float bottom = -halfHeight;
            float top = halfHeight;

            // Use the triangleMesh's viewport rectangle to compute new frustum bounds.
            float newLeft = Mathf.Lerp(left, right, meshViewportRect.xMin);
            float newRight = Mathf.Lerp(left, right, meshViewportRect.xMax);
            float newBottom = Mathf.Lerp(bottom, top, meshViewportRect.yMin);
            float newTop = Mathf.Lerp(bottom, top, meshViewportRect.yMax);

            // Create the off-center projection matrix for the cropped frustum.
            Matrix4x4 newProj = PerspectiveOffCenter(newLeft, newRight, newBottom, newTop, near, far);
            camera.projectionMatrix = newProj;

            Matrix4x4 proj = PerspectiveOffCenter(newLeft, newRight, newBottom, newTop, near, far);

            // Compute offset from the triangleMesh's center to the screen center.
            Vector2 meshCenterViewport = meshViewportRect.center;
            Vector2 offset = meshCenterViewport - new Vector2(0.5f, 0.5f);
            // Apply a compensating offset to the projection matrix.
            // (The factor 2.0f here is heuristic; adjust as needed.)
            proj[0, 2] -= offset.x * 2.0f;
            proj[1, 2] -= offset.y * 2.0f;

            camera.projectionMatrix = proj;

            // Set the camera's rect so the rendered (cropped) image is drawn into the corresponding sub-region of the RenderTexture.
            camera.rect = meshViewportRect;
            return meshViewportRect;
        }

        public static Rect GetMeshViewRect(Camera cam, MeshFilter meshFilter)
        {
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            Vector3[] worldCorners = new Vector3[8];
            worldCorners[0] = meshFilter.transform.TransformPoint(localBounds.min);
            worldCorners[1] = meshFilter.transform.TransformPoint(new Vector3(localBounds.min.x, localBounds.min.y, localBounds.max.z));
            worldCorners[2] = meshFilter.transform.TransformPoint(new Vector3(localBounds.min.x, localBounds.max.y, localBounds.min.z));
            worldCorners[3] = meshFilter.transform.TransformPoint(new Vector3(localBounds.min.x, localBounds.max.y, localBounds.max.z));
            worldCorners[4] = meshFilter.transform.TransformPoint(new Vector3(localBounds.max.x, localBounds.min.y, localBounds.min.z));
            worldCorners[5] = meshFilter.transform.TransformPoint(new Vector3(localBounds.max.x, localBounds.min.y, localBounds.max.z));
            worldCorners[6] = meshFilter.transform.TransformPoint(new Vector3(localBounds.max.x, localBounds.max.y, localBounds.min.z));
            worldCorners[7] = meshFilter.transform.TransformPoint(localBounds.max);

            Vector2 minViewport = new Vector2(1f, 1f);
            Vector2 maxViewport = new Vector2(0f, 0f);
            foreach (Vector3 corner in worldCorners)
            {
                Vector3 vp = cam.WorldToViewportPoint(corner);
                minViewport = Vector2.Min(minViewport, new Vector2(vp.x, vp.y));
                maxViewport = Vector2.Max(maxViewport, new Vector2(vp.x, vp.y));
            }
            return new Rect(minViewport.x, minViewport.y, maxViewport.x - minViewport.x, maxViewport.y - minViewport.y);
        }

        public static (Rect rect, bool valid) ApplyMeshProjection(Camera cam, Camera origCam, Matrix4x4 origProj, MeshFilter meshFilter, float originalFOV)
        {
            // Compute the triangleMesh's bounding rectangle in normalized viewport coordinates.
            //Rect meshViewportRect = GetScreenRectFromBounds(meshFilter, cam).ToRect();
            Rect meshViewportRect = GetMeshViewRect(origCam, meshFilter);
            meshViewportRect = ValidateRect(meshViewportRect, origCam);

            // Calculate the original frustum boundaries at the near clip plane based on originalFOV.
            float near = origCam.nearClipPlane;
            float far = origCam.farClipPlane;
            float fov = origCam.fieldOfView;
            float aspect = origCam.aspect;
            float halfHeight = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * aspect;
            float left = -halfWidth;
            float right = halfWidth;
            float bottom = -halfHeight;
            float top = halfHeight;


            // Compute new frustum boundaries using the normalized meshViewportRect coordinates.
            float newLeft = Mathf.Lerp(left, right, meshViewportRect.xMin);
            float newRight = Mathf.Lerp(left, right, meshViewportRect.xMax);
            float newBottom = Mathf.Lerp(bottom, top, meshViewportRect.yMin);
            float newTop = Mathf.Lerp(bottom, top, meshViewportRect.yMax);

            // Build an off-center projection matrix using these new boundaries.
            //Matrix4x4 newProj = PerspectiveOffCenter(newLeft, newRight, newBottom, newTop, near, far);
            Matrix4x4 newProj = GetCroppedMatrix(origCam.projectionMatrix, meshViewportRect);
            //Matrix4x4 crop = GetCroppedMatrix(origProj, meshViewportRect);
            newProj.m20 = origProj.m20;
            newProj.m21 = origProj.m21;
            newProj.m22 = origProj.m22;
            newProj.m23 = origProj.m23;
            if (IsMatrixValid(newProj))
            {
                cam.projectionMatrix = newProj;
                cam.rect = meshViewportRect;
                return (meshViewportRect, true);
            }
            else
            {
                Debug.Log("Not walid");
                return (meshViewportRect, false);
            }

            // Set the camera's viewport so that the RenderTexture is drawn only to the triangleMesh's area.
            //return meshViewportRect;
        }

        /// <summary>
        /// Clamp values to valid range
        /// </summary>
        /// <param name="rect"></param>
        /// <returns></returns>
        public static Rect ValidateRect(Rect rect)
        {
            rect.width = Mathf.Clamp(rect.width, 0.1f, 1);
            rect.height = Mathf.Clamp(rect.height, 0.1f, 1);
            float maxDim = Mathf.Max(rect.width, rect.height);
            rect.width = maxDim;
            rect.height = maxDim;
            rect.x = Mathf.Clamp(rect.x, 0, 1 - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0, 1 - rect.height);
            return rect;
        }
        public static Rect ValidateRect(Rect rect, Camera camera)
        {
            // To avoid renderTexture errors pixel size of camera should be at least 250px
            float minWidth = 250f / (float)camera.pixelWidth;
            float minHeight = 250f / (float)camera.pixelHeight;
            rect.width = Mathf.Clamp(rect.width, minWidth, 1f);
            rect.height = Mathf.Clamp(rect.height, minHeight, 1f);
            float maxDim = Mathf.Max(rect.width, rect.height);
            rect.x = Mathf.Clamp(rect.x, 0, 1 - rect.width);
            rect.y = Mathf.Clamp(rect.y, 0, 1 - rect.height);
            return rect;
        }

        public static Matrix4x4 GetCroppedMatrix(Matrix4x4 original, Rect viewRect)
        {
            // Convert the viewRect boundaries to normalized device coordinates (NDC: -1 to 1).
            float left = viewRect.x * 2f - 1f;
            float right = (viewRect.x + viewRect.width) * 2f - 1f;
            float bottom = viewRect.y * 2f - 1f;
            float top = (viewRect.y + viewRect.height) * 2f - 1f;

            // Compute scale factors and offsets for remapping the sub-rectangle to [-1, 1]
            // For x:
            //   We want a linear transform: newX = A * oldX + B
            //   such that when oldX = left, newX = -1, and when oldX = right, newX = 1.
            //   Solving gives A = 2 / (right - left) and B = - (right + left) / (right - left).
            float scaleX = 2f / (right - left); // This is equivalent to 1/viewRect.width.
            float offsetX = -(right + left) / (right - left);
            // Similarly for y:
            float scaleY = 2f / (top - bottom);   // This is equivalent to 1/viewRect.height.
            float offsetY = -(top + bottom) / (top - bottom);

            // Create a crop matrix that will scale and translate the x and y coordinates.
            Matrix4x4 crop = Matrix4x4.identity;
            crop.m00 = scaleX;
            crop.m03 = offsetX;
            crop.m11 = scaleY;
            crop.m13 = offsetY;

            // The new projection matrix is the crop matrix multiplied by the original.
            // Note: Multiplication order matters. Here, the crop transform is applied after the original projection.
            return crop * original;
        }

        public static bool IsMatrixValid(Matrix4x4 matrix)
        {
            // Check each element in the matrix.
            for (int i = 0; i < 16; i++)
            {
                float element = matrix[i];
                if (float.IsNaN(element) || float.IsInfinity(element))
                {
                    //Debug.LogError($"Matrix invalid: element at index {i} is {element}");
                    return false;
                }
            }

            // Check if the determinant is too small (indicating a degenerate matrix).
            float det = matrix.determinant;
            if (Mathf.Abs(det) < 1e-6f)
            {
                //Debug.LogError($"Matrix invalid: determinant is too close to zero ({det})");
                return false;
            }

            return true;
        }

        static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
        {
            Matrix4x4 m = new Matrix4x4();
            m[0, 0] = (2.0f * near) / (right - left);
            m[0, 1] = 0;
            m[0, 2] = (right + left) / (right - left);
            m[0, 3] = 0;

            m[1, 0] = 0;
            m[1, 1] = (2.0f * near) / (top - bottom);
            m[1, 2] = (top + bottom) / (top - bottom);
            m[1, 3] = 0;

            m[2, 0] = 0;
            m[2, 1] = 0;
            m[2, 2] = -(far + near) / (far - near);
            m[2, 3] = -(2.0f * far * near) / (far - near);

            m[3, 0] = 0;
            m[3, 1] = 0;
            m[3, 2] = -1.0f;
            m[3, 3] = 0;
            return m;
        }

        public struct MinMax3D
        {
            public float xMin;
            public float xMax;
            public float yMin;
            public float yMax;
            public float zMin;
            public float zMax;

            public MinMax3D(float min, float max)
            {
                this.xMin = min;
                this.xMax = max;
                this.yMin = min;
                this.yMax = max;
                this.zMin = min;
                this.zMax = max;
            }

            public void AddPoint(Vector3 point)
            {
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
                zMin = Mathf.Min(zMin, point.z);
                zMax = Mathf.Max(zMax, point.z);
            }

            public Rect ToRect()
            {
                return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            }
        }

    }
}

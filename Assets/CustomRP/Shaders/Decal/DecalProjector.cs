using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class DecalProjector : MonoBehaviour
{
    [Header("Transform Settings")]
    public Vector3 position = Vector3.zero;
    public Vector3 rotation = Vector3.zero; // Euler angles (degrees)
    public Vector3 scale = Vector3.one;     // Scale values (1 by default)

    [Space]
    [Tooltip("Custom pivot point in object space (relative to the object's center)")]
    public Vector3 pivotPoint = Vector3.zero;

    [Space]
    [Tooltip("Target camera, for which clipPlane will be adjusted projector. If null, at start will be set to mainCamera")]public Camera targetCamera;

    private MeshRenderer meshRenderer;

    // Track previous values to detect changes
    private Vector3 lastPosition;
    private Vector3 lastRotation;
    private Vector3 lastScale;
    private Vector3 lastPivotPoint;

    // Track Transform's previous state
    private Vector3 lastTransformPosition;
    private Quaternion lastTransformRotation;
    private Vector3 lastTransformScale;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Prevent execution during play mode
        if (Application.isPlaying)
            return;

        transform.hideFlags = HideFlags.HideInInspector;

        // Get all components on the GameObject
        Component[] components = GetComponents<Component>();
        int currentIndex = System.Array.IndexOf(components, this);

        // Move up until we're in position 1 (right after Transform)
        while (currentIndex > 1)
        {
            // Move component up and check if successful
            bool moved = ComponentUtility.MoveComponentUp(this);
            if (!moved) break;

            // Refresh component list and index
            components = GetComponents<Component>();
            currentIndex = System.Array.IndexOf(components, this);
        }

        /*position = transform.position;
        rotation = transform.localRotation.eulerAngles;
        scale = transform.localScale;*/

        InitializeTrackers();
    }
#endif

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (!targetCamera) targetCamera = Camera.main;
    }

    void Update()
    {
        if (!targetCamera || !meshRenderer) return;

        // Only apply transformations if something has changed
        /*if (position != lastPosition || rotation != lastRotation || scale != lastScale || pivotPoint != lastPivotPoint)
        {
            PivotTransform.ApplyTransformation(this.transform, pivotPoint, position, rotation, scale);
            lastPosition = position;
            lastRotation = rotation;
            lastScale = scale;
            lastPivotPoint = pivotPoint;
        }*/

        // Check for external Transform changes or pivot point changes
        if (TransformHasChanged() || PivotPointHasChanged())
        {
            UpdatePivotTransformFromTransform();
            InitializeTrackers();
        }

        // Check for internal value changes
        if (InternalValuesHaveChanged())
        {
            ApplyTransformation();
            InitializeTrackers();
        }


        // Calculate near clip plane
        Vector3 cameraPoint = targetCamera.transform.position + targetCamera.transform.forward * (targetCamera.nearClipPlane);
        Plane nearPlane = new Plane(targetCamera.transform.forward, cameraPoint);

        


        // Check plane intersection
        if (PointInCube(cameraPoint))
        {
            Vector3 localCameraPos = InverseTransformPointIgnoringScale(cameraPoint);
            bool up = localCameraPos.y >= 0;
            float distanceToCamera;
            if (up)
            {
                pivotPoint.y = -0.5f;
                distanceToCamera = (localCameraPos.y - pivotPoint.y);
            }
            else
            {
                pivotPoint.y = 0.5f;
                distanceToCamera = 1 - (localCameraPos.y - pivotPoint.y);
            }
            scale.y = distanceToCamera;
            //AdjustCubePositionAndScale();
        }
        else
        {
            scale.y = 1;
            pivotPoint.y = 0;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        // Check if this object is currently selected in the Editor
        if (Selection.activeGameObject == gameObject)
        {
            /*Gizmos.color = Color.red * 10f;
            Vector3 worldPivot = transform.TransformPoint(pivotPoint);
            Gizmos.DrawSphere(worldPivot, 0.05f); // Draw a small sphere at the pivot point*/

            Vector3 worldPivot = transform.TransformPoint(pivotPoint);
            Handles.color = Color.red * 10f;

            // Override depth testing to always draw on top
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.SphereHandleCap(
                0, // Unique control ID
                worldPivot,
                Quaternion.identity,
                0.05f, // Radius
                EventType.Repaint // Ensure rendering during repaint
            );
        }
    }
#endif

    public void SetPosition(Vector3 position)
    {
        this.position = position;
    }

    void RestoreScale()
    {
        transform.localScale = Vector3.one;
    }

    #region pivoted Transform methods
    void InitializeTrackers()
    {
        lastTransformPosition = transform.position;
        lastTransformRotation = transform.rotation;
        lastTransformScale = transform.localScale;

        /*lastPosition = position;
        lastRotation = rotation;
        lastScale = scale;*/
    }
    bool TransformHasChanged()
    {
        return transform.position != lastTransformPosition ||
               transform.rotation != lastTransformRotation ||
               transform.localScale != lastTransformScale;
    }
    bool PivotPointHasChanged()
    {
        return pivotPoint != lastPivotPoint;
    }
    bool InternalValuesHaveChanged()
    {
        return position != lastPosition ||
               rotation != lastRotation ||
               scale != lastScale;
    }
    void UpdatePivotTransformFromTransform()
    {
        // Convert Transform state to PivotTransform values
        position = transform.TransformPoint(pivotPoint);
        rotation = transform.rotation.eulerAngles;
        scale = transform.localScale;
    }
    void ApplyTransformation()
    {
        PivotTransform.ApplyTransformation(this.transform, pivotPoint, position, rotation, scale);

        lastPosition = position;
        lastRotation = rotation;
        lastScale = scale;
        lastPivotPoint = pivotPoint;
    }
    #endregion

    #region methods for protecting from clipping
    bool PointInCube(Vector3 point) 
    {
        Vector3 minBounds = new Vector3(-0.5f, -0.5f, -0.5f);
        Vector3 maxBounds = new Vector3(0.5f, 0.5f, 0.5f);

        minBounds = TransformPointIgnoringScale(minBounds);
        maxBounds = TransformPointIgnoringScale(maxBounds);

        bool xIn = point.x >= minBounds.x && point.x <= maxBounds.x;
        bool yIn = point.y >= minBounds.y && point.y <= maxBounds.y;
        bool zIn = point.z >= minBounds.z && point.z <= maxBounds.z;
        bool res = xIn && yIn && zIn;

        return res;
    }
    #endregion

    public Vector3 TransformPointIgnoringScale(Vector3 localPoint)
    {
        // Apply rotation and translation without scaling
        return transform.position + transform.rotation * localPoint;
    }
    public Vector3 InverseTransformPointIgnoringScale(Vector3 worldPoint)
    {
        // Remove position offset and apply inverse rotation
        Vector3 positionDifference = worldPoint - transform.position;
        return Quaternion.Inverse(transform.rotation) * positionDifference;
    }
}

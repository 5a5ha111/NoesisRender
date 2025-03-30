#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class PivotTransform : MonoBehaviour
{
    // Custom pivot point in object space (relative to the object's center)
    public Vector3 pivotPoint = new Vector3(0.5f, 0.5f, 0.5f);

    // Position, rotation, and scale values exposed in the Inspector
    [Header("Transform Settings")]
    public Vector3 position = Vector3.zero;
    public Vector3 rotation = Vector3.zero; // Euler angles (degrees)
    public Vector3 scale = Vector3.one;     // Scale values (1 by default)

    // Track previous values to detect changes
    private Vector3 lastPosition;
    private Vector3 lastRotation;
    private Vector3 lastScale;

    void Start()
    {
        ApplyTransformation();
    }

    // Method to set object position with pivot consideration
    public void SetPosition(Vector3 newPosition)
    {
        /*// Calculate the world pivot point by transforming local pivot to world space
        Vector3 worldPivot = transform.TransformPoint(pivotPoint);

        // Move the object such that the pivot point aligns with the new position
        transform.position = newPosition + (transform.position - worldPivot);*/

        PivotTransform.SetPosition(transform, pivotPoint, newPosition);
    }

    // Method to set rotation around the pivot point
    public void SetRotation(Quaternion newRotation)
    {
        /*// Calculate the world pivot point by transforming local pivot to world space
        Vector3 worldPivot = transform.TransformPoint(pivotPoint);

        // Apply the new rotation
        transform.rotation = newRotation;

        // Recalculate the position to ensure the pivot remains at the correct point
        Vector3 newPosition = worldPivot - transform.TransformPoint(pivotPoint);
        transform.position += newPosition;*/

        PivotTransform.SetRotation(transform, pivotPoint, newRotation);
    }

    public void SetScale(Vector3 newScale)
    {
        // Calculate the world pivot point by transforming local pivot to world space
        /*Vector3 worldPivot = transform.TransformPoint(pivotPoint);

        // Calculate the scaling factor for the position offset relative to the pivot
        Vector3 scaleFactor = new Vector3(
            newScale.x / transform.localScale.x,
            newScale.y / transform.localScale.y,
            newScale.z / transform.localScale.z
        );

        transform.localScale = newScale;

        // Recalculate the position to keep the pivot in place
        Vector3 newPosition = worldPivot - transform.TransformPoint(pivotPoint);
        transform.position += newPosition;*/

        PivotTransform.SetScale(transform, pivotPoint, newScale);
    }

    public void ApplyTransformation()
    {
        /*SetPosition(position);

        // Apply rotation (converted from Euler angles to Quaternion)
        SetRotation(Quaternion.Euler(rotation));

        SetScale(scale);*/

        PivotTransform.ApplyTransformation(transform, pivotPoint, position, rotation, scale);
    }

    // Static methods for external access
    public static void SetPosition(Transform targetTransform, Vector3 pivotPoint, Vector3 newPosition)
    {
        Vector3 worldPivot = targetTransform.TransformPoint(pivotPoint);
        targetTransform.position = newPosition + (targetTransform.position - worldPivot);
    }
    public static void SetRotation(Transform targetTransform, Vector3 pivotPoint, Quaternion newRotation)
    {
        Vector3 worldPivot = targetTransform.TransformPoint(pivotPoint);
        Quaternion oldRotation = targetTransform.rotation;
        targetTransform.rotation = newRotation;
        Vector3 newPosition = worldPivot - targetTransform.TransformPoint(pivotPoint);
        targetTransform.position += newPosition;
    }
    public static void SetScale(Transform targetTransform, Vector3 pivotPoint, Vector3 newScale)
    {
        Vector3 worldPivot = targetTransform.TransformPoint(pivotPoint);
        Vector3 scaleFactor = new Vector3(
            newScale.x / targetTransform.localScale.x,
            newScale.y / targetTransform.localScale.y,
            newScale.z / targetTransform.localScale.z
        );
        targetTransform.localScale = newScale;
        Vector3 newPosition = worldPivot - targetTransform.TransformPoint(pivotPoint);
        targetTransform.position += newPosition;
    }
    public static void ApplyTransformation(Transform targetTransform, Vector3 pivotPoint, Vector3 position, Vector3 rotationEuler, Vector3 scale)
    {
        SetPosition(targetTransform, pivotPoint, position);
        SetRotation(targetTransform, pivotPoint, Quaternion.Euler(rotationEuler));
        SetScale(targetTransform, pivotPoint, scale);
    }

    // Detect changes in the Inspector and automatically apply them
    void Update()
    {
        // Only apply transformations if something has changed
        if (position != lastPosition || rotation != lastRotation || scale != lastScale)
        {
            ApplyTransformation();
            lastPosition = position;
            lastRotation = rotation;
            lastScale = scale;
        }

        /*if (Application.isPlaying)
        {
            // Example: Rotate around pivot over time for testing (can be removed if unnecessary)
            SetRotation(Quaternion.Euler(0, Time.deltaTime * 45f, 0) * transform.rotation); // Rotates 45 degrees/sec on Y-axis
        }*/
    }

    #if UNITY_EDITOR
        void OnDrawGizmos()
        {
            // Check if this object is currently selected in the Editor
            if (Selection.activeGameObject == gameObject)
            {
                Gizmos.color = Color.red * 10f;
                Vector3 worldPivot = transform.TransformPoint(pivotPoint);
                Gizmos.DrawSphere(worldPivot, 0.05f); // Draw a small sphere at the pivot point
            }
        }
    #endif
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CameraChangeProjMatrix : MonoBehaviour
{
    public float left = -0.2F;
    public float right = 0.2F;
    public float top = 0.2F;
    public float bottom = -0.2F;

    Camera cam;
    void LateUpdate()
    {
        Camera cam = GetComponent<Camera>();
        cam.ResetProjectionMatrix();
        Rect viewRect = new Rect(left, top, right, bottom);

        //Matrix4x4 m = PerspectiveOffCenter(left, right, bottom, top, cam.nearClipPlane, cam.farClipPlane);
        Matrix4x4 m = GetCroppedMatrix(cam.projectionMatrix, viewRect);
        cam.projectionMatrix = m;
    }

    static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        float x = 2.0F * near / (right - left);
        float y = 2.0F * near / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(far + near) / (far - near);
        float d = -(2.0F * far * near) / (far - near);
        float e = -1.0F;
        Matrix4x4 m = new Matrix4x4();
        m[0, 0] = x;
        m[0, 1] = 0;
        m[0, 2] = a;
        m[0, 3] = 0;
        m[1, 0] = 0;
        m[1, 1] = y;
        m[1, 2] = b;
        m[1, 3] = 0;
        m[2, 0] = 0;
        m[2, 1] = 0;
        m[2, 2] = c;
        m[2, 3] = d;
        m[3, 0] = 0;
        m[3, 1] = 0;
        m[3, 2] = e;
        m[3, 3] = 0;
        return m;
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

    public static Matrix4x4 GetCroppedMatrix2(Matrix4x4 original, Rect viewRect)
    {
        // Extract original projection matrix properties
        float near = original.m22 / (original.m32 + 1f);  // Near plane distance
        float far = original.m22 / (original.m32 - 1f);   // Far plane distance
        float top = near / original.m11;                  // Top extent in view space
        float bottom = -top;                              // Bottom extent in view space
        float right = top * (1f / original.m00);          // Right extent based on aspect ratio
        float left = -right;                              // Left extent

        // Compute new extents for the cropped view
        float newLeft = Mathf.Lerp(left, right, viewRect.xMin);
        float newRight = Mathf.Lerp(left, right, viewRect.xMax);
        float newBottom = Mathf.Lerp(bottom, top, viewRect.yMin);
        float newTop = Mathf.Lerp(bottom, top, viewRect.yMax);

        // Construct the new off-axis projection matrix
        return Matrix4x4.Frustum(newLeft, newRight, newBottom, newTop, near, far);
    }
}

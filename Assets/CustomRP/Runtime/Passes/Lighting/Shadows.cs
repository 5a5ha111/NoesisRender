using System.Runtime.InteropServices;
using UnityEngine;


namespace NoesisRender
{
    partial class Shadows
    {

        [StructLayout(LayoutKind.Sequential)]
        struct DirectionalShadowCascade
        {
            public const int stride = 4 * 4 * 2;

            public Vector4 cullingSphere, data;

            public DirectionalShadowCascade
            (
                Vector4 cullingSphere,
                float tileSize,
                ShadowSettings.FilterMode filterMode
            )
            {
                float texelSize = 2f * cullingSphere.w / tileSize;
                float filterSize = texelSize * ((float)filterMode + 1f);
                cullingSphere.w -= filterSize;
                cullingSphere.w *= cullingSphere.w;
                this.cullingSphere = cullingSphere;
                data = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
            }
        }



        [StructLayout(LayoutKind.Sequential)]
        struct OtherShadowData
        {
            public const int stride = 4 * 4 + 4 * 16;

            public Vector4 tileData;

            public Matrix4x4 shadowMatrix;

            public OtherShadowData
            (
                Vector2 offset,
                float scale,
                float bias,
                float border,
                Matrix4x4 matrix
            )
            {
                tileData.x = offset.x * scale + border;
                tileData.y = offset.y * scale + border;
                tileData.z = scale - border - border;
                tileData.w = bias;
                shadowMatrix = matrix;
            }
        }
    }
}

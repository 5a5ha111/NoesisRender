using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class VolumeBinder : MonoBehaviour
{

    private const string ltc_mat = "ltc_mat_test.bin";
    private const string ltc_amp = "ltc_amp_test.bin";
    private const int lutSize = 64;

    [SerializeField] Material ddsTestMat;
    [SerializeField] MeshRenderer ddsTestrender;

    private readonly int ltc_mat_test_tex = Shader.PropertyToID("_LtcMat");
    private readonly int ltc_amp_test_tex = Shader.PropertyToID("_LtcMag");

    private readonly int PolygonWS = Shader.PropertyToID("PolygonWS");
    private readonly int lightForward = Shader.PropertyToID("lightForward");
    private readonly int _LightTexture = Shader.PropertyToID("_LightTexture");

    MaterialPropertyBlock block = null;

    private void Awake()
    {
        block = new MaterialPropertyBlock();
        if (ddsTestrender == null)
        {
            TryGetComponent<MeshRenderer>(out ddsTestrender);
        }
    }

    /*private void OnValidate()
    {
        Twest();
    }*/

    [ContextMenu("Twest")]
    private void Twest()
    {
        var ltc_mat_values = DDSReader.ReadDDSAsFloat4(ltc_mat);
        Texture2D mat_lut_tex = CreateFloat4Texture(ltc_mat_values, lutSize);
        if (ddsTestMat != null && mat_lut_tex != null)
        {
            ddsTestMat.SetTexture(ltc_mat_test_tex, mat_lut_tex);
        }

        var ltc_amp_values = DDSReader.ReadDDSAsFloat4(ltc_mat);
        Texture2D amp_lut_tex = CreateFloat4Texture(ltc_mat_values, lutSize);
        if (ddsTestMat != null && amp_lut_tex != null)
        {
            ddsTestMat.SetTexture(ltc_amp_test_tex, amp_lut_tex);
        }

        /*Matrix4x4 lightVerts = new Matrix4x4();
        var m_Size = lightRenderer.bounds.extents;
        var t = lightRenderer.transform;
        for (int i = 0; i < 4; i++)
            lightVerts.SetRow(i, t.TransformPoint(new Vector3(m_Size.x, m_Size.y, 0) * 0.5f));
        ddsTestMat.SetMatrix("_LightVerts", lightVerts);*/
        //Debug.Log(ltc_mat_values[0]);
    }

    /*[ContextMenu("BindQuadVerts")]
    public void BindQuadVerts()
    {
        Vector4[] quadVerts = new Vector4[4];

        Mesh quadMesh = quadLight.sharedMesh;
        Debug.Assert(quadMesh != null);
        #if UNITY_EDITOR
            bool assert = quadMesh.vertices.Length + 1 != 4;
            Debug.Assert(assert);
            if (!assert)
            {
                Debug.Log("Lenght = " +  quadMesh.vertices.Length);
                Debug.LogWarning("These code is for binding quads!");
            }
        #endif
        for (int i = 0;i < 4; i++)
        {
            quadVerts[i] = quadLight.transform.TransformPoint(quadMesh.vertices[i]);
            //Debug.Log(i + " verts pos = " + quadVerts[i]);
        }

        //MaterialPropertyBlock block = new MaterialPropertyBlock();
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetVectorArray(PolygonWS, quadVerts);
        Vector3 lightfrwd = quadLight.transform.forward;
        block.SetVector(lightForward, lightfrwd);
        ddsTestrender.SetPropertyBlock(block);

        //ddsTestMat.SetVectorArray(PolygonWS, quadVerts);
    }*/

    public void BindQuadVerts(Mesh quadMesh, Transform quadTransform)
    {
        Vector4[] quadVerts = new Vector4[4];

        Debug.Assert(quadMesh != null);
        #if UNITY_EDITOR
            bool assert = quadMesh.vertices.Length + 1 != 4;
            Debug.Assert(assert);
            if (!assert)
            {
                Debug.Log("Lenght = " + quadMesh.vertices.Length);
                Debug.LogWarning("These code is for binding quads!");
            }
        #endif
        for (int i = 0; i < 4; i++)
        {
            quadVerts[i] = quadTransform.TransformPoint(quadMesh.vertices[i]);
            //Debug.Log(i + " verts pos = " + quadVerts[i]);
        }

        //MaterialPropertyBlock block = new MaterialPropertyBlock();
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetVectorArray(PolygonWS, quadVerts);
        Vector3 lightfrwd = quadTransform.forward;
        block.SetVector(lightForward, lightfrwd);
        ddsTestrender.SetPropertyBlock(block);

        //ddsTestMat.SetVectorArray(PolygonWS, quadVerts);
    }
    public void BindLightTexture(Texture lightTex)
    {
        //MaterialPropertyBlock block = new MaterialPropertyBlock();
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetTexture(_LightTexture, lightTex);
        ddsTestrender.SetPropertyBlock(block);
    }

    [ContextMenu("CheckFirstValues")]
    private void CheckFirstValues()
    {
        var ltc_mat_values = DDSReader.ReadDDSAsFloat4(ltc_mat);


        for (int i = 0; i < 10; i++)
        {
            Debug.Log(ltc_mat_values[i]);
        }
    }


    public Texture2D CreateFloat4Texture(System.Numerics.Vector4[] data, int n)
    {
        // Calculate texture dimensions from array length
        int total = data.Length;
        //int n = Mathf.FloorToInt(Mathf.Sqrt(total));
        if (n * n != total)
        {
            Debug.LogError($"Data length {total} is not a perfect square (NxN)");
            return null;
        }

        // Verify platform support
        if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
        {
            Debug.LogError("RGBAFloat texture format not supported on this platform");
            return null;
        }

        // Create texture with floating-point precision
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBAFloat, false);
        tex.filterMode = FilterMode.Point;  // Disable interpolation
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.anisoLevel = 0;

        // Convert Vector4 array to Color array (preserving raw values)
        Color[] pixels = new Color[n * n];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(data[i].X, data[i].Y, data[i].Z, data[i].W);
        }

        // Apply pixel data
        tex.SetPixels(pixels);
        tex.Apply(false);  // No mipmap generation

        return tex;
    }
}

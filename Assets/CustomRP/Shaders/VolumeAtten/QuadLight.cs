using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class QuadLight : MonoBehaviour
{
    [SerializeField] List<VolumeBinder> binders;
    [SerializeField] MeshFilter meshFilter;
    [SerializeField] MeshRenderer meshRenderer;
    [SerializeField] Texture lightTexture;
    private Vector3 posWS = Vector3.zero;
    private Quaternion rotWS = Quaternion.identity;
    private Vector3 scale = Vector3.one;
    private Texture tempTex;

    MaterialPropertyBlock block = null;


    private void OnValidate()
    {
        UpdateBinderstextures();
        UpdateBinders();
    }
    private void Awake()
    {
        // Can be created only after monobeh, by some reason
        block = new MaterialPropertyBlock();
    }
    void Start()
    {
        if (meshFilter == null)
        {
            TryGetComponent<MeshFilter>(out meshFilter);
        }
        if (meshRenderer == null)
        {
            TryGetComponent<MeshRenderer>(out meshRenderer);
        }
        if (lightTexture == null && meshRenderer != null)
        {
            //lightTexture = meshRenderer.sharedMaterial.mainTexture;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (transform.position != posWS || rotWS != transform.rotation || scale != transform.localScale)
        {
            posWS = transform.position;
            rotWS = transform.rotation;
            scale = transform.localScale;
            UpdateBinders();
        }
        if (lightTexture != tempTex)
        {
            tempTex = lightTexture;
            UpdateBinderstextures(lightTexture);
        }
    }


    // For integrating edges order is matter. Uncomment this code, to see in which order vertices passed.
    // To do: order them in moment of binding, so shader can just iterate through them.
    /*private float pointRadius = 0.01f;
    private Color pointColor = Color.gray;
    private Color lineColor = Color.cyan;
    private float arrowHeadLength = 0.2f;
    private float arrowHeadAngle = 20f;
    private void OnDrawGizmos()
    {
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Vector3[] vertices = meshFilter.sharedMesh.vertices;
        if (vertices == null || vertices.Length == 0) return;

        // Transform vertices from local to world space
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = transform.TransformPoint(vertices[i]);
        }

        // Draw points and lines
        //Gizmos.color = pointColor;
        for (int i = 0; i < vertices.Length; i++)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(vertices[i], 0.025f);

            int nextIndex = (i + 1) % vertices.Length;
            Gizmos.color = lineColor;
            Vector3 direction = (vertices[nextIndex] - vertices[i]).normalized;
            Gizmos.DrawLine(vertices[i], vertices[nextIndex] - direction * arrowHeadLength);

            Vector3 from = vertices[i];
            Vector3 to = vertices[nextIndex];
            Vector3 dir = (to - from);
            float length = dir.magnitude;
            if (length < 0.0001f) continue;
            dir /= length;
            float actualArrowHeadLength = Mathf.Min(arrowHeadLength, length * 0.5f);
            Vector3 shaftEnd = to - dir * actualArrowHeadLength;

            Camera currentCamera = Camera.current;
            if (currentCamera != null)
            {
                Vector3 toCamera = (currentCamera.transform.position - to).normalized;
                Vector3 right = Vector3.Cross(dir, toCamera).normalized;
                if (right.sqrMagnitude < 0.01f)
                {
                    right = Vector3.Cross(dir, Vector3.up).normalized;
                    if (right.sqrMagnitude < 0.01f)
                    {
                        right = Vector3.Cross(dir, Vector3.forward).normalized;
                    }
                }

                float arrowHeadWidth = actualArrowHeadLength * Mathf.Tan(arrowHeadAngle * Mathf.Deg2Rad);
                Vector3 leftPoint = shaftEnd - right * arrowHeadWidth;
                Vector3 rightPoint = shaftEnd + right * arrowHeadWidth;

                Gizmos.DrawLine(to, leftPoint);
                Gizmos.DrawLine(to, rightPoint);
                Gizmos.DrawLine(leftPoint, rightPoint);
            }
            else
            {
                Gizmos.DrawLine(shaftEnd, to);
            }
        }
    }*/


    private void UpdateBinders()
    {
        if (meshFilter.sharedMesh == null) return;
        foreach (var binder in binders)
        {
            if (binder == null) continue;
            binder.BindQuadVerts(meshFilter.sharedMesh, transform);
        }
    }
    private void UpdateBinderstextures(Texture texture)
    {
        if (meshFilter.sharedMesh == null) return;
        if (texture == null) texture = Texture2D.whiteTexture;
        foreach (var binder in binders)
        {
            if (binder == null) continue;
            binder.BindLightTexture(texture);
        }

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetTexture("_BaseMap", texture);
        meshRenderer.SetPropertyBlock(block);
    }
    [ContextMenu("Force UpdateTex")]
    private void UpdateBinderstextures()
    {
        UpdateBinderstextures(lightTexture);
    }
}

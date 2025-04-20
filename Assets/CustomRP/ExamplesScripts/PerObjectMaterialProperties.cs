using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{

    static int
        baseColorId = Shader.PropertyToID("_BaseColor"),
        cutoffId = Shader.PropertyToID("_Cutoff"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");
    static MaterialPropertyBlock block;


    [SerializeField] Color baseColor = Color.white;

    [SerializeField, Range(0f, 1f)]
    float alphaCutoff = 0.5f, metallic = 0f, smoothness = 0.5f;
    void OnValidate()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutoffId, alphaCutoff);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
        GetComponent<Renderer>().SetPropertyBlock(block);

        /*LocalKeyword localKeyword = new LocalKeyword(GetComponent<Renderer>().sharedMaterial.shader, "_CLIPPING");
        GetComponent<Renderer>().sharedMaterial.SetKeyword(localKeyword, true);*/
        GetComponent<Renderer>().sharedMaterial.EnableKeyword("_CLIPPING");

    }

    void Awake()
    {
        OnValidate();
    }

    private void Start()
    {
        GetComponent<Renderer>().material.EnableKeyword("_CLIPPING");
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MaterialgLTFUpgrde : MonoBehaviour
{
    [SerializeField] MeshRenderer m_Renderer;

    [SerializeField] Shader oldShader;
    [SerializeField] Shader newShader;

    [SerializeField] string passForNewMaterials;

    [ContextMenu("Upgrade")]
    public void UpgradeMaterials()
    {
#if UNITY_EDITOR
        if (m_Renderer == null)
        {
            Debug.LogError("Mesh Renderer is not assigned.", this);
            return;
        }

        if (oldShader == null)
        {
            Debug.LogError("Old Shader is not assigned.", this);
            return;
        }

        if (newShader == null)
        {
            Debug.LogError("New Shader is not assigned.", this);
            return;
        }

        if (string.IsNullOrEmpty(passForNewMaterials))
        {
            Debug.LogError("Save path for new materials is not specified.", this);
            return;
        }

        Material[] materials = m_Renderer.sharedMaterials;
        bool materialsModified = false;

        for (int i = 0; i < materials.Length; i++)
        {
            Material oldMat = materials[i];
            if (oldMat != null && oldMat.shader == oldShader)
            {
                // Create new material with the new shader
                Material newMat = new Material(newShader);
                newMat.CopyPropertiesFromMaterial(oldMat);

                // Generate a unique asset path
                string fileName = $"{oldMat.name}_Compat.mat";
                string assetPath = $"{passForNewMaterials}/{fileName}";
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                // Save the new material as an asset
                AssetDatabase.CreateAsset(newMat, assetPath);
                materials[i] = newMat;
                materialsModified = true;
            }
        }

        if (materialsModified)
        {
            m_Renderer.sharedMaterials = materials;
            AssetDatabase.SaveAssets();
            Debug.Log("Materials upgraded successfully.", this);
        }
#else
        Debug.LogWarning("Material upgrade is only supported in the Unity Editor.");
#endif
    }
}

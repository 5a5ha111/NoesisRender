using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{

    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;

    bool showPresets;




    bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }

    bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");
    bool PremultiplyAlpha
    {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }

    BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float)value);
    }

    bool ZWrite
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }


    public override void OnGUI(
        MaterialEditor materialEditor, MaterialProperty[] properties
    )
    {
        //base.OnGUI(materialEditor, properties);

        GUI.enabled = true;

        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;


        EditorGUI.BeginChangeCheck();


        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }


        /*MaterialProperty baseMap = FindProperty("_BaseMap", properties);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties);
        MaterialProperty cutoff = FindProperty("_Cutoff", properties);
        MaterialProperty metallic = FindProperty("_Metallic", properties);
        MaterialProperty smoothness = FindProperty("_Smoothness", properties);
        MaterialProperty clipping = FindProperty("_Clipping", properties);
        MaterialProperty srcBlend = FindProperty("_SrcBlend", properties);
        MaterialProperty dstBlend = FindProperty("_DstBlend", properties);
        MaterialProperty zWrite = FindProperty("_ZWrite", properties);
        MaterialProperty premulAlpha = FindProperty("_PremulAlpha", properties);

        materialEditor.TexturePropertySingleLine(new GUIContent("Texture"), baseMap, baseColor);

        EditorGUILayout.Space();
        materialEditor.ShaderProperty(cutoff, "Alpha Cutoff");
        materialEditor.ShaderProperty(metallic, "Metallic");
        materialEditor.ShaderProperty(smoothness, "Smoothness");

        EditorGUILayout.Space();
        materialEditor.ShaderProperty(clipping, "Alpha Clipping");
        materialEditor.ShaderProperty(premulAlpha, "Premultiply Alpha");

        EditorGUILayout.Space();
        materialEditor.ShaderProperty(srcBlend, "Src Blend");
        materialEditor.ShaderProperty(dstBlend, "Dst Blend");
        materialEditor.ShaderProperty(zWrite, "Z Write");*/


        if (EditorGUI.EndChangeCheck())
        {
            /*foreach (Material material in materialEditor.targets)
            {
                material.SetFloat("_Metallic", metallic.floatValue);
                material.SetFloat("_Smoothness", smoothness.floatValue);
            }*/
            materialEditor.PropertiesChanged();
        }

        if (Selection.activeGameObject != null)
        {
            //Debug.Log(Selection.activeGameObject.name);

            var Renderer = Selection.activeGameObject.GetComponent<Renderer>();
            Renderer.sharedMaterial = (Material)materialEditor.target;
            var sharedMat = Renderer.sharedMaterial;
            /*MaterialPropertyBlock matBlock = new MaterialPropertyBlock();
            
            
            matBlock.SetFloat("_Cutoff", cutoff.floatValue);
            matBlock.SetFloat("_Metallic", metallic.floatValue);
            matBlock.SetFloat("_Smoothness", smoothness.floatValue);
            matBlock.SetFloat("_Clipping", clipping.floatValue);
            matBlock.SetFloat("_PremulAlpha", premulAlpha.floatValue);
            matBlock.SetFloat("_SrcBlend", srcBlend.floatValue);
            matBlock.SetFloat("_DstBlend", dstBlend.floatValue);
            matBlock.SetFloat("_ZWrite", zWrite.floatValue);*/


            //Renderer.SetPropertyBlock(matBlock);
            Renderer.SetPropertyBlock(null);

        }

        foreach (MaterialProperty prop in properties)
        {
            if (prop.type == MaterialProperty.PropType.Texture)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(prop.displayName), prop);
            }
            else
            {
                materialEditor.ShaderProperty(prop, prop.displayName);
                /*if (prop.type == MaterialProperty.PropType.Float || prop.type == MaterialProperty.PropType.Range)
                {
                    matBlock.SetFloat(prop.name, prop.floatValue);
                }
                else if (prop.type == MaterialProperty.PropType.Color)
                {
                    matBlock.SetVector(prop.name, prop.colorValue);
                }
                else if (prop.type == MaterialProperty.PropType.Vector)
                {
                    matBlock.SetVector(prop.name, prop.vectorValue);
                }*/
            }
        }

    }


    bool HasProperty(string name) =>
        FindProperty(name, properties, false) != null;

    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }
    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
        {
            SetKeyword(keyword, value);
        }
    }

    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }

    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }

    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent"))
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }
}
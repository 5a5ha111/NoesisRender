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

    enum ShadowMode
    {
        On, Clip, Dither, RiemeresmaDither, Off
    }

    ShadowMode Shadows
    {
        set
        {
            if (SetProperty("_Shadows", (float)value))
            {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
                SetKeyword("_SHADOWS_RIEMERESMA_DITHER", value == ShadowMode.RiemeresmaDither);
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

        BakedEmission();

        foreach (MaterialProperty prop in properties)
        {
            if (prop.type == MaterialProperty.PropType.Texture)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(prop.displayName), prop);
            }
            else
            {
                materialEditor.ShaderProperty(prop, prop.displayName);
            }
        }


        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass();
            CopyLightMappingProperties();
            materialEditor.PropertiesChanged();

            foreach (Material m in editor.targets)
            {
                m.globalIlluminationFlags &=
                    ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        if (Selection.activeGameObject != null)
        {
            //Debug.Log(Selection.activeGameObject.name);

            var Renderer = Selection.activeGameObject.GetComponent<Renderer>();
            Renderer.sharedMaterial = (Material)materialEditor.target;
            var sharedMat = Renderer.sharedMaterial;


            Renderer.SetPropertyBlock(null);

        }

        

    }

    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        //Debug.Log("shadows value " + shadows.floatValue);
        if (shadows == null || shadows.hasMixedValue)
        {
            return;
        }
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        //Debug.Log("shadows enabled " +  enabled);
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }

    void BakedEmission()
    {
        editor.LightmapEmissionProperty();
    }

    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor =
            FindProperty("_BaseColor", properties, false);
        if (color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
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
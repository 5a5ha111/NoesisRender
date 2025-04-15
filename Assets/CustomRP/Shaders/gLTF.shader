Shader "Custom RP/gLTFO"
{
    Properties
    {
        [MainColor] baseColorFactor("Base Color", Color) = (1,1,1,1)
        [MainTexture] baseColorTexture("Base Color Tex", 2D) = "white" {}
        baseColorTexture_Rotation ("Base Color Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] baseColorTexture_texCoord ("Base Color Tex UV", Float) = 0

        alphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        roughnessFactor("Roughness", Range(0.0, 1.0)) = 1
        // _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        // [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0

        [Gamma] metallicFactor("Metallic", Range(0.0, 1.0)) = 1.0
        metallicRoughnessTexture("Metallic-Roughness Tex", 2D) = "white" {}
        metallicRoughnessTexture_Rotation ("Metallic-Roughness Map Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] metallicRoughnessTexture_texCoord ("Metallic-Roughness Tex UV", Float) = 0

        // [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        // [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

        normalTexture_scale("Normal Scale", Float) = 1.0
        [Normal] normalTexture("Normal Tex", 2D) = "bump" {}
        normalTexture_Rotation ("Normal Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] normalTexture_texCoord ("Normal Tex UV", Float) = 0

        // _Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
        // _ParallaxMap ("Height Map", 2D) = "black" {}

        occlusionTexture_strength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        occlusionTexture("Occlusion Tex", 2D) = "white" {}
        occlusionTexture_Rotation ("Occlusion Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] occlusionTexture_texCoord ("Occlusion Tex UV", Float) = 0

        [HDR] emissiveFactor("Emissive", Color) = (0,0,0)
        emissiveTexture("Emission Tex", 2D) = "white" {}
        emissiveTexture_Rotation ("Emission Tex Rotation", Vector) = (0,0,0,0)
        [Enum(UV0,0,UV1,1)] emissiveTexture_texCoord ("Emission Tex UV", Float) = 0

        // _DetailMask("Detail Mask", 2D) = "white" {}

        // _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
        // _DetailNormalMapScale("Scale", Float) = 1.0
        // [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // [Enum(UV0,0,UV1,1)] _UVSec ("UV Set for secondary textures", Float) = 0


        // Blending state
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull Mode", Float) = 2.0
    }
    SubShader
    {
        HLSLINCLUDE
            #include "../ShaderLibrary/Common.hlsl"
            #include "gLTFInput.hlsl"
            #define _CLIPPING
            #define _NORMAL_MAP
            #define _RECEIVE_SHADOWS
        ENDHLSL

        Pass 
        {

            Tags 
            {
                "LightMode" = "CustomLit"
            }

            
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull [_CullMode]

            HLSLPROGRAM
            
            #pragma target 4.5
            #pragma multi_compile_instancing

            //#pragma shader_feature _CLIPPING
            //#pragma shader_feature _RECEIVE_SHADOWS
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma shader_feature _REFLECTION_CUBEMAP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _LIGHTS_PER_OBJECT

            // Other light shadows
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7


            #pragma shader_feature _MASK_MAP
            #pragma shader_feature _DETAIL_MAP
            #pragma shader_feature _NORMAL_MAP

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "LitPass.hlsl"


            ENDHLSL
        }

        Pass 
        {
            Tags 
            {
                "LightMode" = "ShadowCaster"
            }

            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            //#pragma shader_feature _CLIPPING
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER _SHADOWS_RIEMERESMA_DITHER
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass 
        {
            Tags 
            {
                "LightMode" = "Meta"
            }

            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "MetaPass.hlsl"
            ENDHLSL
        }


        Pass 
        {
            Tags 
            {
                "LightMode" = "CustomGBuffer"
            }

            Name "GBuffer pass"

            //Cull Off

            HLSLPROGRAM
            #pragma target 4.5

            #pragma multi_compile_instancing

            //#pragma shader_feature _CLIPPING
            //#pragma shader_feature _RECEIVE_SHADOWS
            //#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            //#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma shader_feature _PREMULTIPLY_ALPHA
            //#pragma shader_feature _REFLECTION_CUBEMAP
            //#pragma multi_compile _ LIGHTMAP_ON
            //#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            //#pragma multi_compile _ _LIGHTS_PER_OBJECT

            // Other light shadows
            //#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7


            #pragma shader_feature _MASK_MAP
            #pragma shader_feature _DETAIL_MAP
            #pragma shader_feature _NORMAL_MAP

            #pragma vertex LitPassVertex
            #pragma fragment GBufferFragment
            #include "LitPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}

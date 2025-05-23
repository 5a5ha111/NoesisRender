Shader "Custom RP/Lit" 
{
	
	Properties 
	{
		_BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
		[Toggle(_REFLECTION_CUBEMAP)] _ReflectionCubemap ("Use reflection cubemap", Float) = 0
		_BaseRefl("ReflectionCubemap (for SPR batcher compatability)", Cube) = "black" {}

		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

		[Toggle(_MASK_MAP)] _MaskMapToggle ("Mask Map", Float) = 0
		[NoScaleOffset] _MaskMap("Mask (MODS(Metallic, Occlusion, Detail and Smoothness))", 2D) = "white" {}
		[Toggle(_DETAIL_MAP)] _DetailMapToggle ("Detail Maps", Float) = 0
		_DetailMap("Details", 2D) = "linearGrey" {}
		_DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
		_DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1

		[NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {}
		_DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1

		[Toggle(_NORMAL_MAP)] _NormalMapToggle ("Normal Map", Float) = 0
		[NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
		_NormalScale("Normal Scale", Range(0, 1)) = 1
		
		_Metallic ("Metallic", Range(0, 1)) = 0
		_Occlusion ("Occlusion", Range(0, 1)) = 1
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		_Fresnel ("Fresnel Strenght", Range(0, 1)) = 0.25

		[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)


		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[KeywordEnum(On, Clip, Dither, RIEMERESMA_DITHER, Off)] _Shadows ("Shadows", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1

		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1

		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0

		_ShadowDither("Shadow dither", Float) = 0


		// For transparency bake in global illumination
		[HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
	}
	
	SubShader 
	{

		HLSLINCLUDE
			#include "../ShaderLibrary/Common.hlsl"
			#include "LitInput.hlsl"
		ENDHLSL
		
		Pass 
		{

			Tags 
			{
				"LightMode" = "CustomLit"
			}

			
			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]

			HLSLPROGRAM
			
			#pragma target 4.5
			#pragma multi_compile_instancing

			#pragma shader_feature _CLIPPING
			#pragma shader_feature _RECEIVE_SHADOWS
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
			#pragma shader_feature _CLIPPING
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

			#pragma shader_feature _CLIPPING
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


	CustomEditor "CustomShaderGUI"
}
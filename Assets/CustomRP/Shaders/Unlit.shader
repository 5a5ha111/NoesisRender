Shader "Custom RP/Unlit" 
{
	
	Properties 
	{
		[HDR] _BaseMap("Texture", 2D) = "white" {}
		[HDR] _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
		[Toggle(_VERTEX_COLORS)] _VertexColors ("Vertex Colors", Float) = 0
		[Enum(UnityEngine.Rendering.RenderQueue)]_Quoue("Queue", Float) = 3000
		[Queue]_QuoueOffset("QueueOffset", Float) = 1
	}
	
	SubShader 
	{

		HLSLINCLUDE
			#include "../ShaderLibrary/Common.hlsl"
			#include "UnlitInput.hlsl"
		ENDHLSL
		
		Pass 
		{
			Tags 
			{
				"Queue" = "_Quoue + _QuoueOffset"
			}

			Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
			ZWrite [_ZWrite]

			HLSLPROGRAM
			
			#pragma multi_compile_instancing

			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment

			#pragma shader_feature _CLIPPING
			#pragma shader_feature _RECEIVE_SHADOWS

			#pragma shader_feature _VERTEX_COLORS

			#include "UnlitPass.hlsl"

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
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
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
				"LightMode" = "CustomGBuffer"
			}
			Name "GBuffer pass"

			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			//#pragma shader_feature _CLIPPING
			#pragma shader_feature _VERTEX_COLORS
			#pragma multi_compile_instancing

			#define Gbuffer

			#pragma vertex UnlitPassVertex
			#pragma fragment GBufferFragment
			#include "UnlitPass.hlsl"
			ENDHLSL
		}
	}



	CustomEditor "CustomShaderGUI"
}
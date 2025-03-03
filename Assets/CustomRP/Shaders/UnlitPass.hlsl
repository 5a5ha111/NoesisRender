#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED


#include "../ShaderLibrary/Common.hlsl"


//CBUFFER_START(UnityPerMaterial)
//	float4 _BaseColor;
//CBUFFER_END

/*TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)*/


struct Attributes 
{
	float3 positionOS : POSITION;
	float4 color : COLOR;
	#if defined(_FLIPBOOK_BLENDING)
		float4 baseUV : TEXCOORD0;
		float flipbookBlend : TEXCOORD1;
	#else
		float2 baseUV : TEXCOORD0;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings 
{
	float4 positionCS_SS : SV_POSITION;
	#if defined(_VERTEX_COLORS)
		float4 color : VAR_COLOR;
	#endif
	float2 baseUV : VAR_BASE_UV;
	#if defined(_FLIPBOOK_BLENDING)
		float3 flipbookUVB : VAR_FLIPBOOK;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



//float4 UnlitPassVertex(Attributes input) : SV_POSITION
//{
//	UNITY_SETUP_INSTANCE_ID(input);
//	float3 positionWS = TransformObjectToWorld(input.positionOS);
//	return TransformWorldToHClip(positionWS);
//}


Varyings UnlitPassVertex(Attributes input) 
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(positionWS);
	#if defined(_VERTEX_COLORS)
		output.color = input.color;
	#endif

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV.xy = TransformBaseUV(input.baseUV.xy);
	#if defined(_FLIPBOOK_BLENDING)
		output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
		output.flipbookUVB.z = input.flipbookBlend;
	#endif
	return output;
}

float4 UnlitPassFragment(Varyings input) : SV_TARGET
{
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV, 0);
	#if defined(_VERTEX_COLORS)
		config.color = input.color;
	#endif
	#if defined(_FLIPBOOK_BLENDING)
		config.flipbookUVB = input.flipbookUVB;
		config.flipbookBlending = true;
	#endif
	#if defined(_NEAR_FADE)
		config.nearFade = true;
	#endif
	#if defined(_SOFT_PARTICLES)
		config.softParticles = true;
	#endif
	//return float4(config.fragment.depth.xxx / 20.0, 1.0); //Debug depth
	//return float4(config.fragment.bufferDepth.xxx / 40.0, 1.0); //Debug frame buffer depth
	//return GetBufferColor(config.fragment, 0.05); Debug color buffer
	UNITY_SETUP_INSTANCE_ID(input);
	float4 base = GetBase(config);
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif
	#if defined(_DISTORTION)
		float2 distortion = GetDistortion(config) * base.a;
		base.rgb = lerp(
			GetBufferColor(config.fragment, distortion).rgb, base.rgb,
			saturate(base.a - GetDistortionBlend(config))
		);
	#endif
	return float4(base.rgb, GetFinalAlpha(base.a));
}
#endif
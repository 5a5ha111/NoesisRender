#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED


#include "../ShaderLibrary/Common.hlsl"




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
	#if defined(Gbuffer)
		float3 positionWS : VAR_POSITION_WS;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};




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
	#if defined(Gbuffer)
		output.positionWS = positionWS;
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
	//return float4(config.fragment.depth.xxx / 40.0, 1.0); //Debug depth
	//return float4(config.fragment.bufferDepth.xxx / 200.0, 1.0); //Debug frame buffer depth
	//return float4(GetMotion(config.fragment).rgb / 40.0, 1.0); //Debug frame buffer motion
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

void GBufferFragment(Varyings input, out float4 gBuffer0 : SV_Target0, out float4 gBuffer1 : SV_Target1, out float4 gBuffer2 : SV_Target2, out float4 gBuffer3 : SV_Target3)
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

	float metallic = 0;
	float4 packedGB0 = float4(base.rgb, metallic);

	#if defined(LOD_FADE_CROSSFADE)
		ClipLOD(config.fragment, unity_LODFade.x);
	#endif


	float smoothness = 0;
	float3 normal = float3(0,1,0);
	float occlusion = 0;
	float4 packedGB1 = float4(normal, smoothness);


	float fresnel = 0;
	float renderingLayerMask = unity_RenderingLayer.x;
	#if defined(Gbuffer)
		float3 positionWS = input.positionWS - _WorldSpaceCameraPos;
	#else
		float3 positionWS = float3(0,0,0);
	#endif
	float4 packedGB2 = float4(positionWS, occlusion);
	//packedGB2.rgb = lightColor;

	float3 emission = base;
	float4 packedGB3 = float4(emission, metallic);


	gBuffer0 = packedGB0;
	gBuffer1 = packedGB1;
	gBuffer2 = packedGB2;
	gBuffer3 = packedGB3;
}
#endif
#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED


#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"

#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"


//CBUFFER_START(UnityPerMaterial)
//	float4 _BaseColor;
//CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


struct Attributes 
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings 
{
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float2 baseUV : VAR_BASE_UV;
	float3 normalWS : VAR_NORMAL;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



//float4 UnlitPassVertex(Attributes input) : SV_POSITION
//{
//	UNITY_SETUP_INSTANCE_ID(input);
//	float3 positionWS = TransformObjectToWorld(input.positionOS);
//	return TransformWorldToHClip(positionWS);
//}


Varyings LitPassVertex(Attributes input)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;

	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;

	#if defined(_CLIPPING)
		clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
	#endif

	//base.rgb = normalize(input.normalWS);
	//return base;

	Surface surface;
	surface.position = input.positionWS;
	surface.normal = normalize(input.normalWS);
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
	surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);

	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif

	float3 color = GetLighting(surface, brdf);
	return float4(color, surface.alpha);
}
#endif
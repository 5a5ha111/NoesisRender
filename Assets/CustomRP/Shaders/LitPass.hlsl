#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED


#include "../ShaderLibrary/Surface.hlsl"

#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#include "../ShaderLibrary/CustomDither.hlsl"




//CBUFFER_START(UnityPerMaterial)
//	float4 _BaseColor;
//CBUFFER_END

/*UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)*/


struct Attributes 
{
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float4 tangentOS : TANGENT;
	float2 baseUV : TEXCOORD0;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings 
{
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float2 baseUV : VAR_BASE_UV;
	#if defined(_DETAIL_MAP)
		float2 detailUV : VAR_DETAIL_UV;
	#endif
	float3 normalWS : VAR_NORMAL;
	#if defined(_NORMAL_MAP)
		float4 tangentWS : VAR_TANGENT;
	#endif
	GI_VARYINGS_DATA
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
	TRANSFER_GI_DATA(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	//output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	output.baseUV = TransformBaseUV(input.baseUV);
	#if defined(_DETAIL_MAP)
		output.detailUV = TransformDetailUV(input.baseUV);
	#endif

	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	#if defined(_NORMAL_MAP)
		output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
	#endif
	return output;
}

float4 LitPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	/*float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;*/

	InputConfig config = GetInputConfig(input.baseUV);
	#if defined(_MASK_MAP)
		config.useMask = true;
	#endif
	#if defined(_DETAIL_MAP)
		config.detailUV = input.detailUV;
		config.useDetail = true;
	#endif

	
	float4 base = GetBase(config);

	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	#if defined(LOD_FADE_CROSSFADE)
		ClipLOD(input.positionCS.xy, unity_LODFade.x);
	#endif

	//base.rgb = normalize(input.normalWS);
	//return base;

	Surface surface = (Surface)0;
	surface.position = input.positionWS;
	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(
			GetNormalTS(config),
			input.normalWS, input.tangentWS
		);
		surface.interpolatedNormal = input.normalWS;
	#else
		surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
	#endif
	surface.interpolatedNormal = input.normalWS;
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(config);
	surface.occlusion = GetOcclusion(config);
	surface.smoothness = GetSmoothness(config);
	surface.fresnelStrength = GetFresnel(config);
	// InterleavedGradientNoise is the easiest function from the Core RP Library, which generates a rotated tiled dither pattern given a screen-space XY position. It also requires a second argument which is used to animate it, which we don't need.
	surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif

	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	float3 color = GetLighting(surface, brdf, gi);
	color += GetEmission(config);
	return float4(color, GetFinalAlpha(surface.alpha));
	//return float4(gi.diffuse, 1);
}
#endif
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
	float4 positionCS_SS : SV_POSITION;
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
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);

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

	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
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
		//ClipLOD(input.positionCS.xy, unity_LODFade.x);
		ClipLOD(config.fragment, unity_LODFade.x);
	#endif

	//base.rgb = normalize(input.normalWS);
	//return base;

	Surface surface = (Surface)0;
	surface.position = input.positionWS;
	//surface.position = float3(0,0,0);
	#if defined(_NORMAL_MAP)
		surface.normal = NormalTangentToWorld(
			GetNormalTS(config),
			input.normalWS, input.tangentWS
		);
		//surface.interpolatedNormal = input.normalWS;
	#else
		surface.normal = normalize(input.normalWS);
		surface.interpolatedNormal = surface.normal;
	#endif
	surface.interpolatedNormal = surface.normal;
	//surface.normal = surface.interpolatedNormal;
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(config);
	surface.occlusion = GetOcclusion(config);
	surface.smoothness = GetSmoothness(config);
	surface.fresnelStrength = GetFresnel(config);
	// InterleavedGradientNoise is the easiest function from the Core RP Library, which generates a rotated tiled dither pattern given a screen-space XY position. It also requires a second argument which is used to animate it, which we don't need.
	surface.dither = InterleavedGradientNoise(config.fragment.positionSS.xy, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif

	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	float3 color = GetLighting(config.fragment, surface, brdf, gi);
	color += GetEmission(config);

	float3 normals = TransformWorldToView(surface.interpolatedNormal);

	return float4(color, GetFinalAlpha(surface.alpha));
	//return float4(surface.metallic,0,0, GetFinalAlpha(surface.alpha));
	//return float4(gi.diffuse, 1);
}


// gBuffer0 RGB color, A metallic
// gBuffer1 R smoothness, GB normals, A occlusion
void GBufferFragment(Varyings input, out float4 gBuffer0 : SV_Target0, out float4 gBuffer1 : SV_Target1, out float4 gBuffer2 : SV_Target2, out float4 gBuffer3 : SV_Target3)
{
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV, 0);
	#if defined(_MASK_MAP)
		config.useMask = true;
	#endif
	#if defined(_DETAIL_MAP)
		config.detailUV = input.detailUV;
		config.useDetail = true;
	#endif

	float4 base = GetBase(config);
	float metallic = GetMetallic(config);
	float4 packedGB0 = float4(base.rgb, metallic);


	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	#if defined(LOD_FADE_CROSSFADE)
		//ClipLOD(input.positionCS.xy, unity_LODFade.x);
		ClipLOD(config.fragment, unity_LODFade.x);
	#endif


	float smoothness = GetSmoothness(config);
	float3 normal;
	#if defined(_NORMAL_MAP)
		normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
	#else
		normal = normalize(input.normalWS);
	#endif
	float occlusion = GetOcclusion(config);
	float4 packedGB1 = float4(normal, smoothness);


	float fresnel = GetFresnel(config);
	float renderingLayerMask = unity_RenderingLayer.x;
	float2 lightmapUV = GI_FRAGMENT_DATA(input);
	float3 positionWS = input.positionWS - _WorldSpaceCameraPos;
	float4 packedGB2 = float4(positionWS, occlusion);
	//packedGB2.rgb = lightColor;

	float3 emission = GetEmission(config);
	float4 packedGB3 = float4(emission, metallic);


	gBuffer0 = packedGB0;
	gBuffer1 = packedGB1;
	gBuffer2 = packedGB2;
	gBuffer3 = packedGB3;
}


#endif
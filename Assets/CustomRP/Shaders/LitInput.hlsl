#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED



TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_EmissionMap);

TEXTURE2D(_MaskMap);


UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
	UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)


float4 GetMask (float2 baseUV) 
{
	return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, baseUV);
}

float2 TransformBaseUV (float2 baseUV) 
{
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (float2 baseUV) 
{
	float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_BaseColor);
	return map * color;
}

float GetCutoff (float2 baseUV) 
{
	return INPUT_PROP(_Cutoff);
}

float GetMetallic (float2 baseUV) 
{
	float metallic = INPUT_PROP(_Metallic);
	metallic *= GetMask(baseUV).r;
	return metallic;
}

float GetSmoothness (float2 baseUV) 
{
	float smoothness = INPUT_PROP(_Smoothness);
	smoothness *= GetMask(baseUV).a;
	return smoothness;
}

float GetFresnel (float2 baseUV) 
{
	return INPUT_PROP(_Fresnel);
}

float3 GetEmission (float2 baseUV) 
{
	float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
	float4 color = INPUT_PROP(_EmissionColor);
	return map.rgb * color.rgb;
}




#endif
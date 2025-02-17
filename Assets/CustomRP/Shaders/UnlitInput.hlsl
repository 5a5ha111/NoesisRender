#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)


// Incapsulate uv
struct InputConfig 
{
	float2 baseUV;
	float2 detailUV;
};
InputConfig GetInputConfig(float2 baseUV, float2 detailUV = 0.0) 
{
	InputConfig c;
	c.baseUV = baseUV;
	c.detailUV = detailUV;
	return c;
}


float2 TransformBaseUV (float2 baseUV) 
{
	float4 baseST = INPUT_PROP(_BaseMap_ST);
	return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase (InputConfig c) 
{
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
	float4 baseColor = INPUT_PROP(_BaseColor);
	return baseMap * baseColor;
}

float3 GetEmission (InputConfig c) 
{
	return GetBase(c).rgb;
}

float GetCutoff (InputConfig c) 
{
	return INPUT_PROP(_Cutoff);
}

float GetMetallic (InputConfig c) 
{
	return 0.0;
}

float GetSmoothness (InputConfig c) 
{
	return 0.0;
}

float GetFresnel (InputConfig c) 
{
	return 0.0;
}

#endif
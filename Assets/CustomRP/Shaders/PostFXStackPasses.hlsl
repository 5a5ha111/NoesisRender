#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"


TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);

bool _BloomBicubicUpsampling;
float _BloomIntensity;

struct Varyings 
{
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};



// For passes
float4 _PostFXSource_TexelSize;

float4 GetSourceTexelSize () {
	return _PostFXSource_TexelSize;
}

float4 GetSource(float2 screenUV) 
{
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}
float4 GetSource2(float2 screenUV) 
{
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}
float4 GetSourceBicubic (float2 screenUV) 
{
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}

float4 BloomHorizontalPassFragment (Varyings input) : SV_TARGET 
{
	float3 color = 0.0;
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	//The weights are derived from Pascal's triangle. 9 numbers from 13 row, each divided by their sum
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	
	for (int i = 0; i < 9; i++) 
	{
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}
float4 BloomVerticalPassFragment (Varyings input) : SV_TARGET 
{
	float3 color = 0.0;
	float offsets[] = {
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	};
	float weights[] = {
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};

	for (int i = 0; i < 5; i++) 
	{
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

float4 BloomCombinePassFragment (Varyings input) : SV_TARGET 
{
	float3 lowRes;
	if (_BloomBicubicUpsampling) 
	{
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else 
	{
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lowRes * _BloomIntensity + highRes, 1.0);
}


float4 _BloomThreshold;

float3 ApplyBloomThreshold (float3 color) {
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}

float4 BloomPrefilterPassFragment (Varyings input) : SV_TARGET {
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}





Varyings DefaultPassVertex (uint vertexID : SV_VertexID) 
{
	Varyings output;
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);

	if (_ProjectionParams.x < 0.0) 
	{
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}


// Def fragment
float4 CopyPassFragment (Varyings input) : SV_TARGET 
{
	return GetSource(input.screenUV);
}

#endif
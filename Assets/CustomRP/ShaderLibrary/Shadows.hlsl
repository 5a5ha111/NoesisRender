#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
	#define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
	#define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
	#define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4


TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);


CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	float4 _ShadowAtlasSize;
	float4 _ShadowDistanceFade;
	float4 _CascadeData[MAX_CASCADE_COUNT];
CBUFFER_END


// Per light data
struct DirectionalShadowData 
{
	float strength;
	int tileIndex;
	float normalBias;
	int shadowMaskChannel;
};


struct ShadowMask 
{
	bool always;
	bool distance;
	float4 shadows;
};


// Global shadow data
struct ShadowData 
{
	int cascadeIndex;
	float cascadeBlend;
	float strength;
	ShadowMask shadowMask;
};


float FadedShadowStrength (float distance, float scale, float fade) 
{
	return saturate((1.0 - distance * scale) * fade);
}

ShadowData GetShadowData (Surface surfaceWS) 
{
	ShadowData data;
	data.shadowMask.always = false;
	data.shadowMask.distance = false;
	data.shadowMask.shadows = 1.0;

	data.cascadeBlend = 1.0;
	data.strength = FadedShadowStrength(
		surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y
	);

	int i;
	for (i = 0; i < _CascadeCount; i++) 
	{
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) 
		{
			float fade = FadedShadowStrength(
				distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
			);
			if (i == _CascadeCount - 1) 
			{
				/*data.strength *= FadedShadowStrength(
					distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z
				);*/
				data.strength *= fade;
			}
			else
			{
				data.cascadeBlend = fade;
			}
			break;
		}
	}

	if (i == _CascadeCount) 
	{
		data.strength = 0.0;
	}
	#if defined(_CASCADE_BLEND_DITHER)
		else if (data.cascadeBlend < surfaceWS.dither) 
		{
			i += 1;
		}
	#endif

	#if !defined(_CASCADE_BLEND_SOFT)
		data.cascadeBlend = 1.0;
	#endif

	data.cascadeIndex = i;
	return data;
}


//Samples the shadow atlas via the SAMPLE_TEXTURE2D_SHADOW macro, passing it the atlas, shadow sampler, and position in shadow texture space, which a corresponding parameter.
float SampleDirectionalShadowAtlas (float3 positionSTS) 
{
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}


float FilterDirectionalShadow (float3 positionSTS) {
	#if defined(DIRECTIONAL_FILTER_SETUP)
		float weights[DIRECTIONAL_FILTER_SAMPLES];
		float2 positions[DIRECTIONAL_FILTER_SAMPLES];
		float4 size = _ShadowAtlasSize.yyxx;
		DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
		float shadow = 0;
		for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++) 
		{
			shadow += weights[i] * SampleDirectionalShadowAtlas(
				float3(positions[i].xy, positionSTS.z)
			);
		}
		return shadow;
	#else
		return SampleDirectionalShadowAtlas(positionSTS);
	#endif
}


float GetCascadedShadow (
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS) 
{
	float3 normalBias = surfaceWS.normal *
		(directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(
		_DirectionalShadowMatrices[directional.tileIndex],
		float4(surfaceWS.position + normalBias, 1.0)
	).xyz;
	float shadow = FilterDirectionalShadow(positionSTS);
	if (global.cascadeBlend < 1.0) {
		normalBias = surfaceWS.normal *
			(directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
		positionSTS = mul(
			_DirectionalShadowMatrices[directional.tileIndex + 1],
			float4(surfaceWS.position + normalBias, 1.0)
		).xyz;
		shadow = lerp(
			FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend
		);
	}
	return shadow;
}

float GetBakedShadow (ShadowMask mask, int channel) 
{
	float shadow = 1.0;
	if (mask.always || mask.distance) 
	{
		//shadow = mask.shadows.r;
		if (channel >= 0) 
		{
			shadow = mask.shadows[channel];
		}
	}
	return shadow;
}
float GetBakedShadow (ShadowMask mask, int channel, float strength) 
{
	if (mask.always || mask.distance) {
		return lerp(1.0, GetBakedShadow(mask, channel), strength);
	}
	return 1.0;
}

float MixBakedAndRealtimeShadows (
	ShadowData global, float shadow, int shadowMaskChannel, float strength
) 
{
	float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);
	if (global.shadowMask.always) 
	{
		shadow = lerp(1.0, shadow, global.strength);
		shadow = min(baked, shadow);
		return lerp(1.0, shadow, strength);
	}
	if (global.shadowMask.distance) 
	{
		shadow = lerp(baked, shadow, global.strength);
		return lerp(1.0, shadow, strength);
	}
	return lerp(1.0, shadow, strength * global.strength);
}


float GetDirectionalShadowAttenuation (
	DirectionalShadowData directional, ShadowData global, Surface surfaceWS
) 
{
	#if !defined(_RECEIVE_SHADOWS)
		return 1.0;
	#endif

	float shadow;
	if (directional.strength * global.strength <= 0.0) 
	{
		shadow = GetBakedShadow(
			global.shadowMask, directional.shadowMaskChannel,
			abs(directional.strength)
		);
	}
	else 
	{
		shadow = GetCascadedShadow(directional, global, surfaceWS);
		shadow = MixBakedAndRealtimeShadows(
			global, shadow, directional.shadowMaskChannel, directional.strength
		);
	}
	return shadow;
}


#endif
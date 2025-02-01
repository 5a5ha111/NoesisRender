#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct DirectionalShadowData 
{
	float strength;
	int tileIndex;
};


//Samples the shadow atlas via the SAMPLE_TEXTURE2D_SHADOW macro, passing it the atlas, shadow sampler, and position in shadow texture space, which a corresponding parameter.
float SampleDirectionalShadowAtlas (float3 positionSTS) 
{
	return SAMPLE_TEXTURE2D_SHADOW(
		_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS
	);
}

#endif
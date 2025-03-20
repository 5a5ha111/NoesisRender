#ifndef MODULAR_AMBIENT_INCLUDED
#define MODULAR_AMBIENT_INCLUDED



// Saple environment reflections
float3 SampleEnvironment (float3 viewDirection, float3 NormalWS, float perceptualRoughness) 
{
	float3 uvw = reflect(-viewDirection, NormalWS);
	float mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);

	float4 environment;
	#if defined(_REFLECTION_CUBEMAP)
		environment = SAMPLE_TEXTURECUBE_LOD(
			_BaseRefl, sampler_BaseRefl, uvw, mip
	);
	#else
	{
		//environment = 0.02;
		environment = SAMPLE_TEXTURECUBE_LOD(
			unity_SpecCube0, samplerunity_SpecCube0, uvw, mip
		);
		//environment = 0.02;
	}
	#endif

	return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
}

float3 SimpleAmbient(float3 NormalWS, float3 baseColor, float3 environment, float ambientOcclusion, float smoothness, float metalic, float reflectance)
{
	float vertical = NormalWS.y;

	float3 metalSurface = lerp(baseColor, float3(0,0,0), metalic);
	float3 occludedEnv = environment * reflectance;
}



#endif
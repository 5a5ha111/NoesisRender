#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"



//Baked lightning
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

//Light probes
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

//Baked shadow mask
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

//reflections, skybox default 
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

TEXTURECUBE(_BaseRefl);
SAMPLER(sampler_BaseRefl);


#if defined(LIGHTMAP_ON)
	#define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
	#define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;
	#define TRANSFER_GI_DATA(input, output) \
		output.lightMapUV = input.lightMapUV * \
		unity_LightmapST.xy + unity_LightmapST.zw;
	#define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
	#define GI_ATTRIBUTE_DATA
	#define GI_VARYINGS_DATA
	#define TRANSFER_GI_DATA(input, output)
	#define GI_FRAGMENT_DATA(input) 0.0
#endif


struct GI 
{
	float3 diffuse;
	float3 specular;
	float ao;
	ShadowMask shadowMask;
};


float3 SampleLightMap (float2 lightMapUV) 
{
	#if defined(LIGHTMAP_ON)
  		return SampleSingleLightmap(
			TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV,
			float4(1.0, 1.0, 0.0, 0.0),
			#if defined(UNITY_LIGHTMAP_FULL_HDR)
				false,
			#else
				true,
			#endif
			float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
		);
	#else
		return 0.0;
	#endif
}

float3 SampleLightProbe (Surface surfaceWS) 
{
	#if defined(LIGHTMAP_ON)
		return 0.0;
	#else
		#if !defined(_DEFFERED_LIGHTNING)
			if (unity_ProbeVolumeParams.x) 
			{
				return SampleProbeVolumeSH4(
					TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
					surfaceWS.position, surfaceWS.normal,
					unity_ProbeVolumeWorldToObject,
					unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
					unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
				);
			}
			else 
			{
				float4 coefficients[7];
				coefficients[0] = unity_SHAr;
				coefficients[1] = unity_SHAg;
				coefficients[2] = unity_SHAb;
				coefficients[3] = unity_SHBr;
				coefficients[4] = unity_SHBg;
				coefficients[5] = unity_SHBb;
				coefficients[6] = unity_SHC;
				return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
			}
		#else
			float3 uvw = reflect(-surfaceWS.viewDirection, surfaceWS.normal);
			uvw = float3(0.5,0.5,0.5);
			float mip = 32;
			/*float4 environment = SAMPLE_TEXTURECUBE_LOD(
				_BaseRefl, sampler_BaseRefl, uvw, mip
			);*/
			float3 environment = float3(0.094525, 0.1201509, 0.199);
			return environment * _DeferredEnvParams.y;
		#endif
	#endif
}

float4 SampleLightProbeOcclusion (Surface surfaceWS) 
{
	return unity_ProbesOcclusion;
}

float4 SampleBakedShadows (float2 lightMapUV, Surface surfaceWS) 
{
	#if defined(LIGHTMAP_ON)
		return SAMPLE_TEXTURE2D(
			unity_ShadowMask, samplerunity_ShadowMask, lightMapUV
		);
	#else
		if (unity_ProbeVolumeParams.x) 
		{
			return SampleProbeOcclusion(
				TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
				surfaceWS.position, unity_ProbeVolumeWorldToObject,
				unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
				unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
			);
		}
		else 
		{
			return unity_ProbesOcclusion;
		}
	#endif
}


// Saple environment reflections
float3 SampleEnvironment (Surface surfaceWS, BRDF brdf) 
{
	float3 uvw = reflect(-surfaceWS.viewDirection, surfaceWS.normal);

	float mip;
	//float mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
	//float perceptualRoughness = RoughnessToPerceptualRoughness(1 - surfaceWS.smoothness);
	//float mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
	#if defined(_DEFFERED_LIGHTNING)
		float perceptualRoughness = RoughnessToPerceptualRoughness(1 - surfaceWS.smoothness);
		mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
		mip += 2; // In my case reflSkybox is too highres, so i add a indent 
	#else
		mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
		mip -= 0.1;
	#endif

	float4 environment;
	#if defined(_REFLECTION_CUBEMAP)
		environment = SAMPLE_TEXTURECUBE_LOD(
			_BaseRefl, sampler_BaseRefl, uvw, mip
		);
		//environment = 100;
	#else
	{
		// override for deffered procedural geometry
		#if defined(_DEFFERED_LIGHTNING)
			environment = SAMPLE_TEXTURECUBE_LOD(
				_BaseRefl, sampler_BaseRefl, uvw, mip
			);
		#else
			//environment = 0.02;
			environment = SAMPLE_TEXTURECUBE_LOD(
				unity_SpecCube0, samplerunity_SpecCube0, uvw, mip
			);
			//environment = 10;
		#endif
	}
	#endif

	//return DecodeHDREnvironment(environment, unity_SpecCube0_HDR);
	real4 decodeInstructions;
	#if defined(_DEFFERED_LIGHTNING)
		decodeInstructions = _DeferredEnvParams;
	#else
		decodeInstructions = unity_SpecCube0_HDR;
	#endif
	real4 encodedIrradiance = environment;
	// Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
    real alpha = max(decodeInstructions.w * (encodedIrradiance.a - 1.0) + 1.0, 0.0);
    // If Linear mode is not supported we can skip exponent part
    return (decodeInstructions.x * PositivePow(alpha, decodeInstructions.y)) * encodedIrradiance.rgb;
}



GI GetGI (float2 lightMapUV, Surface surfaceWS, BRDF brdf) 
{
	GI gi;
	gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
	gi.specular = SampleEnvironment(surfaceWS, brdf);
	gi.ao = 1;
	//gi.specular = 0;
	
	gi.shadowMask.always = false;
	gi.shadowMask.distance = false;
	gi.shadowMask.shadows = 1.0;
	#if defined(_SHADOW_MASK_ALWAYS)
		gi.shadowMask.always = true;
		gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
	#elif defined(_SHADOW_MASK_DISTANCE)
		gi.shadowMask.distance = true;
		gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
	#endif

	return gi;
}

#endif
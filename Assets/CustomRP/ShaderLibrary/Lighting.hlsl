#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED


#include "ModularLight/Specular.hlsl"
#include "ModularLight/Diffuse.hlsl"

float3 IncomingLight (Surface surface, Light light) 
{
	return
		saturate(dot(surface.normal, light.direction) * light.attenuation) *
		light.color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light) 
{
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}


/*float3 GetLighting (Surface surfaceWS, BRDF brdf) 
{
	ShadowData shadowData = GetShadowData(surfaceWS);
	float3 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++) 
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	return color;
}*/


// Returns whether the masks of a surface and light overlap. This is done by checking whether the bitwise-AND of the bit masks is nonzero.
bool RenderingLayersOverlap (Surface surface, Light light) 
{
	return (surface.renderingLayerMask & light.renderingLayerMask) != 0;
}


float3 GetLighting (Fragment fragment, Surface surfaceWS, BRDF brdf, GI gi) 
{
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	for (int i = 0; i < GetDirectionalLightCount(); i++) 
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		if (RenderingLayersOverlap(surfaceWS, light)) 
		{
			color += GetLighting(surfaceWS, brdf, light);
			//color = light.attenuation;
			//color += SpecularBlinn(surfaceWS.viewDirection, -light.direction, surfaceWS.normal, surfaceWS.smoothness) * light.color;
			//color *= DiffuseLambert(surfaceWS.normal, -light.direction);
			//color += light.attenuation;
		}
	}
	
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++) 
		{
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light)) 
			{
				color += GetLighting(surfaceWS, brdf, light);
			}
		}
	#else
		ForwardPlusTile tile = GetForwardPlusTile(fragment.screenUV);
		int lastLightIndex = tile.GetLastLightIndexInTile();
		for (int j = tile.GetFirstLightIndexInTile(); j <= lastLightIndex; j++) 
		{
			Light light = GetOtherLight(tile.GetLightIndex(j), surfaceWS, shadowData);
			if (RenderingLayersOverlap(surfaceWS, light)) 
			{
				color += GetLighting(surfaceWS, brdf, light);
				//color += light.attenuation;
			}
		}
	#endif
	//return gi.shadowMask.shadows.rgb; //Debug GI
	return color;
}

float3 GetAllLighting (Fragment fragment, Surface surfaceWS, BRDF brdf, GI gi) 
{
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	for (int i = 0; i < GetDirectionalLightCount(); i++) 
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
		//color += SpecularBlinn(surfaceWS.viewDirection, light.direction, surfaceWS.normal, surfaceWS.smoothness) * light.color;
		//color *= DiffuseLambert(surfaceWS.normal, -light.direction);
		//color = 0.5;
		//color = light.attenuation;
	}
	
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++) 
		{
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#else
		ForwardPlusTile tile = GetForwardPlusTile(fragment.screenUV);
		int lastLightIndex = tile.GetLastLightIndexInTile();
		for (int j = tile.GetFirstLightIndexInTile(); j <= lastLightIndex; j++) 
		{
			Light light = GetOtherLight(tile.GetLightIndex(j), surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#endif
	//return gi.shadowMask.shadows.rgb; //Debug GI
	return color;
}


#endif
#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED



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


float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) 
{
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular);
	for (int i = 0; i < GetDirectionalLightCount(); i++) 
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	
	#if defined(_LIGHTS_PER_OBJECT)
		for (int j = 0; j < min(unity_LightData.y, 8); j++) 
		{
			int lightIndex = unity_LightIndices[(uint)j / 4][(uint)j % 4];
			Light light = GetOtherLight(lightIndex, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#else
		for (int j = 0; j < GetOtherLightCount(); j++) 
		{
			Light light = GetOtherLight(j, surfaceWS, shadowData);
			color += GetLighting(surfaceWS, brdf, light);
		}
	#endif
	//return gi.shadowMask.shadows.rgb; //Debug GI
	return color;
}



#endif
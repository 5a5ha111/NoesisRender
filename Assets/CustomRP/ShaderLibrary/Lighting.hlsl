#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED



float3 IncomingLight (Surface surface, Light light) {
	return
		saturate(dot(surface.normal, light.direction) * light.attenuation) *
		light.color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light) 
{
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}


float3 GetLighting (Surface surfaceWS, BRDF brdf) {
	ShadowData shadowData = GetShadowData(surfaceWS);
	float3 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++) 
	{
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	return color;
}

float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) 
{
	ShadowData shadowData = GetShadowData(surfaceWS);
	shadowData.shadowMask = gi.shadowMask;
	
	float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular.rgb);
	for (int i = 0; i < GetDirectionalLightCount(); i++) {
		Light light = GetDirectionalLight(i, surfaceWS, shadowData);
		color += GetLighting(surfaceWS, brdf, light);
	}
	//return gi.shadowMask.shadows.rgb; //Debug GI
	return color;
}



#endif
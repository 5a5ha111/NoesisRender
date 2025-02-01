#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED



float3 IncomingLight(Surface surface, Light light) 
{
	return saturate(dot(surface.normal, light.direction)) * light.color;
}

float3 GetLighting(Surface surface, BRDF brdf, Light light) 
{
	return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surface, BRDF brdf) 
{
	float3 color = 0.0;
	for (int i = 0; i < GetDirectionalLightCount(); i++) 
	{
		color += GetLighting(surface, brdf, GetDirectionalLight(i));
	}
	return color;
}



#endif
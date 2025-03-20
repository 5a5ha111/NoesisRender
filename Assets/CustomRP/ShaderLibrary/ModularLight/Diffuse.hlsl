#ifndef MODULAR_DIFFUSE_INCLUDED
#define MODULAR_DIFFUSE_INCLUDED



float DiffuseLambert(float3 NormalWS, float3 MainLightDir)
{
	float3 lightDir = -1 * MainLightDir;
	float dotPr = saturate(dot(NormalWS, lightDir));
	return dotPr;
}

float DiffuseHalfLambert(float3 NormalWS, float3 MainLightDir)
{
	float3 lightDir = -1 * MainLightDir;
	float dotPr = dot(NormalWS, lightDir);
	dotPr = saturate(dotPr * 0.5 + 0.5);
	return dotPr;
}



#endif
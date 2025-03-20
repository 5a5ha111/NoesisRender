#ifndef MODULAR_SPECULAR_INCLUDED
#define MODULAR_SPECULAR_INCLUDED



float3 SpecularMetallicReflectance(float3 NormalWS, float3 ViewDir, float3 baseColor, float metalic)
{
	float fresnel = FresnelEffect(NormalWS, ViewDir, 8);

	const float minMetalRefl = 0;
	const float maxMetalRefl = 0.2;
	float metalReflValue = lerp(minMetalRefl, maxMetalRefl, fresnel);
	
	float3 res = lerp(metalReflValue, baseColor, metalic);
	return res; 
}


float SpecularBlinn(float3 ViewDir, float3 MainLightDir, float3 NormalWS, float smoothness)
{
	float3 halfAngle = normalize(ViewDir + (MainLightDir * -1));
	float dotPr = saturate(dot(halfAngle, NormalWS));
	float smoothnessExp = AdjustSmoothness(smoothness);
	return pow(dotPr, smoothnessExp);
}
float SpecularPhong(float3 ViewDir, float3 MainLightDir, float3 NormalWS, float smoothness)
{
	float3 refl = reflect(MainLightDir, NormalWS);
	float dotPr = saturate(dot(refl, ViewDir));
	const float SpecularSize = 8; // smaller value, bigger size
	float power = pow(dotPr, SpecularSize) * smoothness;
	return power;
}



#endif
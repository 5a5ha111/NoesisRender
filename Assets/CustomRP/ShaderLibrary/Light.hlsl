#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED


// Structured buffer dont have constant lenght
//#define MAX_DIRECTIONAL_LIGHT_COUNT 4
//#define MAX_OTHER_LIGHT_COUNT 64

CBUFFER_START(_CustomLight)
	int _DirectionalLightCount;
	/*float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
	// Light directions with Layer mask in .a
	float4 _DirectionalLightDirectionsAndMasks[MAX_DIRECTIONAL_LIGHT_COUNT];
	float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];*/

	int _OtherLightCount;
	/*float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightDirectionsAndMasks[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];
	float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];*/
CBUFFER_END



struct Light 
{
	float3 color;
	float3 direction;
	float attenuation;
	uint renderingLayerMask;
};



struct DirectionalLightData
{
	float4 color, directionAndMask, shadowData;
};
struct OtherLightData
{
	float4 color, position, directionAndMask, spotAngle, shadowData;
};
StructuredBuffer<DirectionalLightData> _DirectionalLightData;
StructuredBuffer<OtherLightData> _OtherLightData;



int GetDirectionalLightCount() 
{
	return _DirectionalLightCount;
}
int GetOtherLightCount () 
{
	return _OtherLightCount;
}



DirectionalShadowData GetDirectionalShadowData (float4 lightShadowData, ShadowData shadowData) 
{
	DirectionalShadowData data;
	data.strength = lightShadowData.x;
	data.tileIndex = lightShadowData.y + shadowData.cascadeIndex;
	data.normalBias = lightShadowData.z;
	data.shadowMaskChannel = lightShadowData.w;
	return data;
}
OtherShadowData GetOtherShadowData (float4 lightShadowData) 
{
	OtherShadowData data;
	data.strength = lightShadowData.x;
	data.tileIndex = lightShadowData.y;
	data.shadowMaskChannel = lightShadowData.w;
	data.isPoint = lightShadowData.z == 1.0;
	data.lightPositionWS = 0.0;
	data.lightDirectionWS = 0.0;
	data.spotDirectionWS = 0.0;
	return data;
}

Light GetDirectionalLight (int index, Surface surfaceWS, ShadowData shadowData) 
{
	DirectionalLightData data = _DirectionalLightData[index];
	Light light;
	light.color = data.color.rgb;
	light.direction = data.directionAndMask.xyz;
	DirectionalShadowData dirShadowData = GetDirectionalShadowData(data.shadowData, shadowData);
	light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowData, surfaceWS);
	light.renderingLayerMask = asuint(data.directionAndMask.w);
	return light;
}

Light GetOtherLight (int index, Surface surfaceWS, ShadowData shadowData) 
{
	Light light;
	OtherLightData data = _OtherLightData[index];
	light.color = data.color.rgb;
	float3 position = data.position.xyz;
	float3 ray = position - surfaceWS.position;
	light.direction = normalize(ray);
	float distanceSqr = dot(ray, ray);
	const float c = 0.00001;
	const float r = 1;
	float rSqr = r * r;
	float d = length(ray);
	float sqrtTerm = sqrt(distanceSqr + rSqr);
	//float coreAtten = 1.0 / distanceSqr; // correct for point light, but go to infinity at close distance
	//float coreAtten = max(1.0 / distanceSqr, c); // ad hoc
	//float coreAtten = 1.0 / (distanceSqr + c); // ad hoc 2
	//float coreAtten = 2.0 / (distanceSqr + 0.5 * rSqr); // much better ad hoc, but decay too slow 
	float coreAtten = 2.0 / (distanceSqr + rSqr + d * sqrtTerm); // just best solution
	
	float rangeAttenuation = Square(
		saturate(1.0 - Square(distanceSqr * data.position.w))
	); // reduce light power by its range
	
	//float4 spotAngles = _OtherLightSpotAngles[index];
	float3 spotDirection = data.directionAndMask.xyz;
	light.renderingLayerMask = asuint(data.directionAndMask.w);
	float spotAttenuation = Square(
		saturate(dot(spotDirection, light.direction) *
		data.spotAngle.x + data.spotAngle.y)
	);
	OtherShadowData otherShadowData = GetOtherShadowData(data.shadowData);
	otherShadowData.lightPositionWS = position;
	otherShadowData.lightDirectionWS = light.direction;
	otherShadowData.spotDirectionWS = spotDirection;
	light.attenuation = 
	GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS) *
		spotAttenuation * rangeAttenuation * coreAtten;;
	return light;
}

#endif
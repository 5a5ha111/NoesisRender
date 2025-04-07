#ifndef PROCEDURAL_HELPERS
#define PROCEDURAL_HELPERS





float2 Mirror(float2 value, float subdivisions, float height)
{
	float2 scaledUV = value * subdivisions;
	float2 temp2 = scaledUV - floor(scaledUV);
	float2 mirroredUV = min(temp2, 1 - temp2) * 2;
	float2 res = height * mirroredUV;
	return res;
}
float2 MirrorInverse(float2 value, float subdivisions, float height)
{
	float2 scaledUV = value * subdivisions;
	float2 temp2 = scaledUV - floor(scaledUV);
	float2 mirroredUV = min(temp2, 1 - temp2) * 2;
	mirroredUV = 1 - mirroredUV;
	float2 res = height * mirroredUV;
	res -= height + 1;
	return res;
}








#endif
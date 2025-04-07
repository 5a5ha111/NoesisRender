#ifndef PROCEDURAL_UV
#define PROCEDURAL_UB


// Smaller maskSize, bigger edge transition
float UvMaskSquere(float2 uv, float maskSize)
{
	float2 scaledUv = saturate(uv * maskSize);
	float2 minusUv = saturate((1 - uv) * maskSize);
	uv = scaledUv * minusUv;
	float mask = saturate(uv.x * uv.y);
	return mask;
}


// Smaller maskSize, bigger edge transition
float UvMaskRounded(float2 uv, float maskSize)
{
	uv = uv * (1 - uv);
	float mask = saturate((uv.x * uv.y) * maskSize);
	return mask;
}



#endif
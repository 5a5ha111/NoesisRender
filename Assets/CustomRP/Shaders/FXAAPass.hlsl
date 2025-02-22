#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED


#if defined(FXAA_QUALITY_LOW)
	// The original FXAA algorithm contains multiple quality presets that vary both the amount of steps and their sizes. Quality preset 22 is a fast low-quality preset that has three extra steps. The first extra step—after the initial offset of a single pixel—has an offset of 1.5. This extra half-pixel offset means that we end up sampling the average of a square of four pixels along the edge instead of a single pair. The two steps after that have size 2, each again sampling squares of four pixels instead of pairs. Thus it covers a distance of up to seven pixels with only four samples. And if it failed to detect the end it guesses that it is a least eight steps further away.
	// Using these settings we get low-quality results, but they're better able to deal with longer edges than if we fixed the extra step size at 1. The downsize is that the edges can appear to be a bit dithered. This is caused by the larger steps that produce less accurate results.

	#define EXTRA_EDGE_STEPS 3
	#define EDGE_STEP_SIZES 1.5, 2.0, 2.0
	#define LAST_EDGE_STEP_GUESS 8.0
#elif defined(FXAA_QUALITY_MEDIUM)
	// Quality preset 26
	#define EXTRA_EDGE_STEPS 8
	#define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
	#define LAST_EDGE_STEP_GUESS 8.0
#else
	// Quality preset 39. This is a high-quality configuration
	#define EXTRA_EDGE_STEPS 10
	#define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
	#define LAST_EDGE_STEP_GUESS 8.0
#endif
static const float edgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES };

float4 _FXAAConfig; // x is constant treshold, y is relative, z is subpixelBlending factor


struct LumaNeighborhood 
{
	float m, n, e, s, w; // 5 compas directions
	float ne, se, sw, nw; // The quality of the filter can be improved by also incorporating the diagonal neighbors into it
	float highest, lowest;
	float range; // look like sobel filter, but sobel is 3x3. Also lack info about direction
};
struct FXAAEdge 
{
	bool isHorizontal;
	float pixelStep;
	float lumaGradient, otherLuma;
};


float GetLuma (float2 uv, float uOffset = 0.0, float vOffset = 0.0) 
{
	uv += float2(uOffset, vOffset) * GetSourceTexelSize().xy;

	#if defined(FXAA_ALPHA_CONTAINS_LUMA)
		return GetSource(uv).a; // More accurate
	#else
		return GetSource(uv).g; // More faster approach
		// It still possible to calculate luma in this stage, if you have spare render budget
		//return sqrt(Luminance(GetSource(uv))); 
	#endif
}
LumaNeighborhood GetLumaNeighborhood (float2 uv) 
{
	LumaNeighborhood luma;
	luma.m = GetLuma(uv);
	luma.n = GetLuma(uv, 0.0, 1.0);
	luma.e = GetLuma(uv, 1.0, 0.0);
	luma.s = GetLuma(uv, 0.0, -1.0);
	luma.w = GetLuma(uv, -1.0, 0.0);

	#if defined(FXAA_NINE_SAMPLES)
		luma.ne = GetLuma(uv, 1.0, 1.0);
		luma.se = GetLuma(uv, 1.0, -1.0);
		luma.sw = GetLuma(uv, -1.0, -1.0);
		luma.nw = GetLuma(uv, -1.0, 1.0);
	#endif

	luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
	luma.range = luma.highest - luma.lowest;
	return luma;
}
bool CanSkipFXAA (LumaNeighborhood luma) 
{
	return luma.range < max(_FXAAConfig.x, _FXAAConfig.y * luma.highest);
}
float GetSubpixelBlendFactor (LumaNeighborhood luma) 
{
	#if defined(FXAA_NINE_SAMPLES)
		float filter = 2.0 * (luma.n + luma.e + luma.s + luma.w);
		filter += luma.ne + luma.nw + luma.se + luma.sw;
		filter *= 1.0 / 12.0;
	#else
		float filter = luma.n + luma.e + luma.s + luma.w;
		filter *= 1.0 / 4.0;
	#endif
	filter = abs(filter - luma.m);
	filter = saturate(filter / luma.range);
	filter = smoothstep(0, 1, filter);
	return filter * filter * _FXAAConfig.z;
}
// Calculate what direction have the highest contrast
bool IsHorizontalEdge (LumaNeighborhood luma) 
{	
	#if defined(FXAA_NINE_SAMPLES)
		float horizontal =
			2.0 * abs(luma.n + luma.s - 2.0 * luma.m) +
			abs(luma.ne + luma.se - 2.0 * luma.e) +
			abs(luma.nw + luma.sw - 2.0 * luma.w);
		float vertical =
			2.0 * abs(luma.e + luma.w - 2.0 * luma.m) +
			abs(luma.ne + luma.nw - 2.0 * luma.n) +
			abs(luma.se + luma.sw - 2.0 * luma.s);
	#else
		float horizontal = abs(luma.n + luma.s - 2.0 * luma.m);
		float vertical = abs(luma.e + luma.w - 2.0 * luma.m);
	#endif
	return horizontal >= vertical;
}
FXAAEdge GetFXAAEdge (LumaNeighborhood luma) 
{
	FXAAEdge edge;
	edge.isHorizontal = IsHorizontalEdge(luma);
	float lumaP, lumaN; // Positive and negative dir of the edge. Used to determine where need to be blend
	if (edge.isHorizontal) 
	{
		edge.pixelStep = GetSourceTexelSize().y;
		lumaP = luma.n;
		lumaN = luma.s;
	}
	else 
	{
		edge.pixelStep = GetSourceTexelSize().x;
		lumaP = luma.e;
		lumaN = luma.w;
	}
	float gradientP = abs(lumaP - luma.m);
	float gradientN = abs(lumaN - luma.m);

	if (gradientP < gradientN) 
	{
		edge.pixelStep = -edge.pixelStep;
		edge.lumaGradient = gradientN;
		edge.otherLuma = lumaN;
	}
	else
	{
		edge.lumaGradient = gradientP;
		edge.otherLuma = lumaP;
	}

	return edge;
}
float GetEdgeBlendFactor (LumaNeighborhood luma, FXAAEdge edge, float2 uv) 
{
	float2 edgeUV = uv;
	float2 uvStep = 0.0;
	if (edge.isHorizontal) 
	{
		edgeUV.y += 0.5 * edge.pixelStep;
		uvStep.x = GetSourceTexelSize().x;
	}
	else 
	{
		edgeUV.x += 0.5 * edge.pixelStep;
		uvStep.y = GetSourceTexelSize().y;
	}

	float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
	float gradientThreshold = 0.25 * edge.lumaGradient;

	// Search for the end of edge sampling luma
	float2 uvP = edgeUV + uvStep;
	float lumaDeltaP = GetLuma(uvP) - edgeLuma;
	bool atEndP = abs(lumaDeltaP) >= gradientThreshold;
	// Continue search untill found end of edge by luma changes
	int i;
	UNITY_UNROLL
	for (i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++) 
	{
		uvP += uvStep * edgeStepSizes[i];
		lumaDeltaP = GetLuma(uvP) - edgeLuma;
		atEndP = abs(lumaDeltaP) >= gradientThreshold;
	}
	// If we not find edge in EXTRA_EDGE_STEPS, consider edge at least on distance of EXTRA_EDGE_STEPS + 1 * LAST_EDGE_STEP_GUESS
	if (!atEndP) 
	{
		uvP += uvStep * LAST_EDGE_STEP_GUESS;
	}

	// Same in negative dir
	float2 uvN = edgeUV - uvStep;
	float lumaDeltaN = GetLuma(uvN) - edgeLuma;
	bool atEndN = abs(lumaDeltaN) >= gradientThreshold;
	// The original FXAA algorithm also combines both loops, searching in both directions in lockstep. Each iteration, only the directions that haven't finished yet advance and sample again. This might be faster in some cases, but in my case two separate loops performed slightly better than a single loop. As always, if you want the absolute best performance, test it yourself, per project, per target platform.
	UNITY_UNROLL
	for (i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++) 
	{
		uvN -= uvStep * edgeStepSizes[i];
		lumaDeltaN = GetLuma(uvN) - edgeLuma;
		atEndN = abs(lumaDeltaN) >= gradientThreshold;
	}
	if (!atEndN) 
	{
		uvN -= uvStep * LAST_EDGE_STEP_GUESS;
	}

	// Calculate distance to the end of edge
	float distanceToEndP, distanceToEndN;
	if (edge.isHorizontal) 
	{
		distanceToEndP = uvP.x - uv.x;
		distanceToEndN = uv.x - uvN.x;
	}
	else 
	{
		distanceToEndP = uvP.y - uv.y;
		distanceToEndN = uv.y - uvN.y;
	}

	float distanceToNearestEnd;
	bool deltaSign;
	if (distanceToEndP <= distanceToEndN) 
	{
		distanceToNearestEnd = distanceToEndP;
		deltaSign = lumaDeltaP >= 0;
	}
	else 
	{
		distanceToNearestEnd = distanceToEndN;
		deltaSign = lumaDeltaN >= 0;
	}

	//return edge.lumaGradient;
	// If the final sign matches the sign of the original edge then we're moving away from the edge and should skip blending, by returning zero.
	if (deltaSign == (luma.m - edgeLuma >= 0)) 
	{
		return 0.0;
	}
	else 
	{
		// If we're on the correct side of the edge then we blend by a factor of 0.5 minus the relative distance to the nearest end point along the edge. This means that we blend more the closer we are to the end point and won't blend at all in the middle of the edge.
		return 0.5 - distanceToNearestEnd / (distanceToEndP + distanceToEndN);
	}
}


float4 FXAAPassFragment (Varyings input) : SV_TARGET 
{
	LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);
	if (CanSkipFXAA(luma)) 
	{
		GetSource(input.screenUV);
		//return 0;
	}
	//return luma.range;
	FXAAEdge edge = GetFXAAEdge(luma);
	//return edge.isHorizontal ? float4(1.0, 0.0, 0.0, 0.0) : 1.0;
	//return edge.pixelStep > 0.0 ? float4(1.0, 0.0, 0.0, 0.0) : 1.0;


	//float blendFactor = GetSubpixelBlendFactor(luma);
	//float blendFactor = GetEdgeBlendFactor (luma, edge, input.screenUV);
	float blendFactor = max(GetSubpixelBlendFactor(luma), GetEdgeBlendFactor (luma, edge, input.screenUV)
	);
	//return blendFactor;
	float2 blendUV = input.screenUV;
	if (edge.isHorizontal) 
	{
		blendUV.y += blendFactor * edge.pixelStep;
	}
	else 
	{
		blendUV.x += blendFactor * edge.pixelStep;
	}
	return GetSource(blendUV);
}

#endif
#ifndef RIEMERSMA_DITHER
#define RIEMERSMA_DITHER


#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/CustomDither.hlsl"


TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);



struct Attributes 
{
	float3 positionOS : POSITION;
	float2 baseUV : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings 
{
	float4 positionCS : SV_POSITION;
	float2 baseUV : VAR_BASE_UV;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



float2 Nth_weyl(float2 p0, int n) 
{
    //return fract(p0 + float(n)*vec2(0.754877669, 0.569840296));
    return frac(p0 + float2(n*12664745, n*9560333)/exp2(24.0));	// integer mul to avoid round-off
}

float2 Weyl(int i)
{
	//return fract(float(n)*vec2(0.754877669, 0.569840296));
    return frac(float2(i*float2(12664745, 9560333))/exp2(24.0)); // integer mul to avoid round-off
}
float weyl_1d(int n) 
{
    return frac(float(n*10368871)/exp2(24.));
}

// from "The Unreasonable Effectiveness of Quasirandom Sequences"
// http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
float r_dither(float2 co)
{
	const float2 magic = float2(0.75487766624669276, 0.569840290998);
    return frac(dot(co, magic));
}

float r_dither(float2 co, float t)
{
	const float2 magic = float2(0.75487766624669276, 0.569840290998);
    return frac(dot(co, magic) + t);
}


//note: from "NEXT GENERATION POST PROCESSING IN CALL OF DUTY: ADVANCED WARFARE"
//      http://advances.realtimerendering.com/s2014/index.html
// (copied from https://www.shadertoy.com/view/MslGR8)
// Same as Unity built-in
float InterleavedGradientNoise(float2 uv )
{
    const float3 magic = float3( 0.06711056, 0.00583715, 52.9829189 );
    return frac(magic.z * frac(dot(uv, magic.xy)));
}


// from: https://blog.demofox.org/2022/02/01/two-low-discrepancy-grids-plus-shaped-sampling-ldg-and-r2-ldg/
// instead of having values 0/5, 1/5, 2/5, 3/5, 4/5, you could instead divide by 4 to get 0/4, 1/4, 2/4, 3/4, 4/4, which would average to 0.5 as well. You may very well want to do that situationally, depending on what you are using the numbers for. A reason NOT to do that though would be in situations where a value of 0 was the same as a value of 1, like if you were multiplying the value by 2*pi and using it as a rotation. In that situation, 0 degrees would occur twice as often as the other values and add bias that way, where if you were using it for a stochastic alpha test, it would not introduce bias to have both 0 and 1 values.
float PlusShapedLDG(int pixelX, int pixelY)
{
    return fmod((float(pixelX)+3.0f*float(pixelY)+0.5f)/5.0f, 1.0f);
}

float remapTri(float n)
{
    float orig = n * 2.0 - 1.0;
    n = orig * rsqrt(abs(orig));
    return max(-1.0, n) - sign(orig);
}

float3 ApplyColorQuantization(float3 color, float ditherValue, float steps)
{
	if (steps == 1)
    {
        // Compute a threshold that is shifted by the dither.
        float threshold = 0.5 + 0.5 * ditherValue;
        return float3(
            color.r < threshold ? 0.0 : 1.0,
            color.g < threshold ? 0.0 : 1.0,
            color.b < threshold ? 0.0 : 1.0
        );
    }
    else
    {
    	/*float3 quantized = floor(color * steps + 0.5 + ditherValue * 0.5) / steps;
        return saturate(quantized);*/

		float ditherAmount = 0.5 / steps;
		float3 quantized = floor( (color + ditherValue * ditherAmount) * steps + 0.5 ) / steps;
        return saturate(quantized);

    	//return round(color * steps + ditherValue) / steps;

        // When steps > 1 we use a rounding approach. 
        // (The division of dither by steps scales it to roughly one quantization step.)
        //float3 quantized = floor(color * steps + 0.5 + ditherValue / steps) / steps;
        // Clamp the result so that it stays in the [0,1] range.
        //return saturate(quantized);
    }

    //return round(color * steps + ditherValue) / steps;
}


/*Texture2D<float4> blueNoiseTex;
SamplerState samplerBlueNoiseTex;*/

TEXTURE2D(_BlueNoiseTex);
SAMPLER(sampler_BlueNoiseTex);

float2 hash2D(float2 p) 
{
    return frac(sin(float2(dot(p, float2(127.1, 311.7)), 
                    dot(p, float2(269.5, 183.3)))) * 43758.5453);
}

float hash1D(float2 p) 
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float BlueNoise(float2 pos, float t, float textureSize) 
{
    // Calculate adaptive parameters based on texture size
    float BLOCK_SIZE = 1.0 / floor(sqrt(textureSize)); // Auto-tiling density
    float PATCH_SCALE = 2.828 * BLOCK_SIZE; // 2*sqrt(2) * block size
    
    // Jittered grid calculations
    float2 blockIdx = floor(pos / BLOCK_SIZE);
    float2 jitter = hash2D(blockIdx + t) * BLOCK_SIZE * 0.5;
    float2 origin = blockIdx * BLOCK_SIZE + jitter;
    
    // Local coordinates transformation
    float2 localPos = pos - origin;
    float angle = hash1D(blockIdx + t + 0.1) * 6.283185;
    
    // Rotation matrix
    float2x2 rot = float2x2(
        cos(angle), -sin(angle),
        sin(angle), cos(angle)
    );
    
    // Apply rotation and scaling
    localPos = mul(rot, localPos) * PATCH_SCALE;
    
    // Texture sampling with size adaptation
    float2 uv = (localPos / (BLOCK_SIZE * 2.828)) + 0.5;
    uv *= BLOCK_SIZE * textureSize; // Scale to texture dimensions
    
    // Sample and validate
    float noise = 0.0;
    /*if (all(uv >= 0) && all(uv <= 1)) {
        noise = _BlueNoiseTex.SampleLevel(sampler_BlueNoiseTex, uv, 0).r;
    }*/
    noise = _BlueNoiseTex.SampleLevel(sampler_BlueNoiseTex, uv, 0).r;
    
    return noise;
}



float3 ApplyColorQuantization(float3 color, float ditherValue, float ditherPower, float steps)
{
    return round(color * steps + ditherValue * ditherPower) / steps;
}
float2 ApplyColorQuantization(float2 color, float ditherValue, float steps)
{
    return round(color * steps + ditherValue) / steps;
}
float ApplyColorQuantization(float color, float ditherValue, float steps)
{
    return round(color * steps + ditherValue) / steps;
}



static const int ERROR_LIST_SIZE = 32;
static const float DECAY_RATIO = 1.0 / 8.0;
float errorBuffer[ERROR_LIST_SIZE];

#define BIT_DEPTH 2



Varyings UnlitPassVertex(Attributes input) 
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);

	//float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV; // * baseST.xy + baseST.zw;
	return output;
}

float4 UnlitPassFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	float4 res = float4(input.baseUV, 0, 0);
	float2 pixelSize = 1.0 / _ScreenParams.xy;
	res = float4(input.baseUV.y, input.baseUV.y, input.baseUV.y, 1);

	float2 ndcUV = input.positionCS.xy / input.positionCS.w;
	float2 clipPixelSize = abs(ddx(ndcUV) + ddy(ndcUV));
	/*res.rg = clipPixelSize;
	res.rg = input.positionCS.xy / _ScreenParams.xy;
	res.rg = HilbertIndex(input.positionCS.xy) / (_ScreenParams.x * _ScreenParams.y);

	float mask = 0.0;
	float SHADES = 8.0;
	float amount = input.baseUV.y;*/
	/*uint hilbertIndex = HilbertIndex(input.positionCS.xy); // map pixel coords to hilbert curve index
	uint m = OwenHash(ReverseBits(hilbertIndex), 0xe7843fbfu);   // owen-scramble hilbert index
	m = OwenHash(ReverseBits(m), 0x8d8fb1e0u);   // map hilbert index to sobol sequence and owen-scramble
	mask = float(ReverseBits(m)) / 4294967296.0; // convert to float

	mask = ReshapeUniformToTriangle(mask);
	res.rgb = floor((input.baseUV.y + mask / SHADES) * SHADES) / SHADES; // quantise and apply dither mask  */

	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	//amount = LinearToGamma_sRGB_Approx(baseMap.a).r;
	/*amount = baseMap.a;
	amount = min(amount, 0.8);

	res.rgb = RiemeresmaDither(input.positionCS.xy, SHADES, amount);  

	float lowBorder = 0.25;
	float hightBorder = 0.3;
	if (amount < lowBorder)
	{
		res.rgb *= smoothstep(0, lowBorder, amount);
	}
	else if (amount > hightBorder)
	{
		res.rgb *= smoothstep(hightBorder, 1, amount) + 1;
	}*/

	/*res.r = RiemeresmaDither(input.positionCS.xy, SHADES, input.baseUV.x);
	res.g = RiemeresmaDither(input.positionCS.xy, SHADES, input.baseUV.y);
	res.b = 0;*/


	/*uint hilbertIdx = HilbertIndex(input.positionCS.xy);
	float sourcePixel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV).a * 255.0;
	sourcePixel = input.baseUV.y * 255;

	float adjustedPixel = sourcePixel;
    float weight = 1.0;
    float baseWeight = exp(log(max(DECAY_RATIO, 1e-6)) / (ERROR_LIST_SIZE - 1));

    for (int i = 0; i < ERROR_LIST_SIZE; i++) 
    {
        adjustedPixel += errorBuffer[i] * weight;
        weight *= baseWeight;
    }

    float quantizedPixel = (adjustedPixel > 127.5) ? 255.0 : 0.0;
    float quantizationError = sourcePixel - quantizedPixel;

    [unroll]
    for (int j = ERROR_LIST_SIZE - 1; j > 0; j--) 
    {
        errorBuffer[j] = errorBuffer[j - 1] * baseWeight;
    }
    errorBuffer[0] = quantizationError;

    res.rgb = quantizedPixel / 255.0;*/

    //float baseGradient = pow(input.baseUV.xx,4) - 0.1;

    half3x3 m = (half3x3)UNITY_MATRIX_M;
	half3 objectScale = half3(
	    length( half3( m[0][0], m[1][0], m[2][0] ) ),
	    length( half3( m[0][1], m[1][1], m[2][1] ) ),
	    length( half3( m[0][2], m[1][2], m[2][2] ) )
	);

    float baseGradient = input.baseUV.x;
    float2 pixel =  _ScreenParams.xy;
    //float2 pos = ceil(input.baseUV.xy * pixel);
    float2 pos = input.positionCS.xy;
    float steps = 1;

    float time = _Time.y * 144;
    time = 0;

    float dither;
    

    float3 col = baseGradient /** 2 -0.25*/;

    if (input.baseUV.y <= 0.25 & input.baseUV.y < 0.125)
    {
    	col = ApplyColorQuantization(col, 0, steps);
    }

    //col = smoothstep(0, 1, col);

    else if (input.baseUV.y > 0.25 & input.baseUV.y < 0.4)
    {
	    //dither = Nth_weyl(pos, 0.5).x;
	    dither = weyl_1d(pos.x + weyl_1d(frac(float(pos.y*245.9204))* _ScreenParams.y) * _ScreenParams.y).x;
	    dither = remapTri(dither);

	    //const float lsb = exp2(float(BIT_DEPTH)) - 1.0;
	    /*col += dither / lsb;
	    col = round(col * lsb) / lsb;*/

	    float threshold = 1 / (steps);
	    if (col.r < threshold)
	    {
	    	float endDither = dither * 0.5;
	    	float factor = col.r / threshold;
	    	dither = lerp(endDither, dither, factor);
	    }

	    col = ApplyColorQuantization(col, dither, steps);
	    col *= float3(1,0,0);
	    //col = dither;
    }
    else if (input.baseUV.y >= 0.4 & input.baseUV.y < 0.6)
    {
    	dither = r_dither(pos, time) * 2 - 1;
    	//dither = remapTri(dither);
    	//col -= dither;

    	/*float threshold = 1 / (steps);
	    if (col.r < threshold)
	    {
	    	float endDither = dither * 0.5;
	    	float factor = col.r / threshold;
	    	dither = lerp(endDither, dither, factor);
	    }*/
    	col.rgb = ApplyColorQuantization(col, dither, steps);
    	col *= float3(1,0.5,1);
    }
    else if (input.baseUV.y > 0.6 & input.baseUV.y < 0.85)
    {
    	dither = InterleavedGradientNoise(pos, time) * 2 - 1;
    	//dither += 1/9;
		//dither = remapTri(dither);

		/*float threshold = 1 / (steps);
	    if (col.r < threshold)
	    {
	    	float endDither = dither * 0.5;
	    	float factor = col.r / threshold;
	    	dither = lerp(endDither, dither, factor);
	    }*/
    	/*col += dither / lsb;
    	col = round(col * lsb) / lsb;*/
    	col = ApplyColorQuantization(col, dither, steps);
    	col *= float3(0.5,1,1);
    }
    else if (input.baseUV.y > 0.85 & input.baseUV.y < 1.1)
    {
    	dither = ( PlusShapedLDG(pos.x, pos.y));
    	float blueNoise = BlueNoise(pos, time, 16);
    	dither -= blueNoise / 5.0;
    	dither += 1.0/10.0;
    	dither = dither * 2.0 - 1.0;
    	//dither = _BlueNoiseTex.SampleLevel(sampler_BlueNoiseTex, input.baseUV, 0).r;
		//dither = remapTri(dither);
    	/*col += dither / lsb;
    	col = round(col * lsb) / lsb;*/
    	//col = ApplyColorQuantization(col, dither, steps);
    	/*if (col.r < dither)
    	{
    		col = 0;
    	}
    	else
    	{
    		col = 1;
    	}
*/
    	col = ApplyColorQuantization(col, dither, steps);
    	//col = dither;
    	col *= float3(0.5,1,0.5);
    }
    //int bytes = 4;
    //col = round(col * bytes) / bytes;

    res.rgb = col;


	return res;
	/*float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;
	return base;*/
}


#endif
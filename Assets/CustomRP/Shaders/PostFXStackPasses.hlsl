#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/CustomDither.hlsl"


TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);


// Bloom
bool _BloomBicubicUpsampling;
float _BloomIntensity;
float4 _BloomThreshold;

// Color grading
float4 _ColorAdjustments;
float4 _ColorFilter;
float4 _WhiteBalance;
float4 _SplitToningShadows, _SplitToningHighlights;
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue;
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;

// LUT
float4 _ColorGradingLUTParameters;
bool _ColorGradingLUTInLogC;
TEXTURE2D(_ColorGradingLUT);

// Rescale
bool _CopyBicubic;




struct Varyings 
{
	float4 positionCS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};



// Helpers
float4 _PostFXSource_TexelSize;

float4 GetSourceTexelSize () 
{
	return _PostFXSource_TexelSize;
}

float4 GetSource(float2 screenUV) 
{
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}
float4 GetSource2(float2 screenUV) 
{
	return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}
float4 GetSourceBicubic (float2 screenUV) 
{
	return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}



// Bloom
float4 BloomHorizontalPassFragment (Varyings input) : SV_TARGET 
{
	float3 color = 0.0;
	float offsets[] = {
		-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
	};
	//The weights are derived from Pascal's triangle. 9 numbers from 13 row, each divided by their sum
	float weights[] = {
		0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
	};
	
	for (int i = 0; i < 9; i++) 
	{
		float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
		color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
	}
	return float4(color, 1.0);
}
float4 BloomVerticalPassFragment (Varyings input) : SV_TARGET 
{
	float3 color = 0.0;
	float offsets[] = {
		-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
	};
	float weights[] = {
		0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
	};

	for (int i = 0; i < 5; i++) 
	{
		float offset = offsets[i] * GetSourceTexelSize().y;
		color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
	}
	return float4(color, 1.0);
}

float4 BloomAddPassFragment (Varyings input) : SV_TARGET 
{
	float3 lowRes;
	if (_BloomBicubicUpsampling) 
	{
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else 
	{
		lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
	return float4(lowRes * _BloomIntensity + highRes.rgb, highRes.a);
}
float4 BloomScatterPassFragment (Varyings input) : SV_TARGET 
{
	float3 lowRes;
	if (_BloomBicubicUpsampling) 
	{
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else 
	{
		lowRes = GetSource(input.screenUV).rgb;
	}
	float3 highRes = GetSource2(input.screenUV).rgb;
	return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float3 ApplyBloomThreshold (float3 color) 
{
	float brightness = Max3(color.r, color.g, color.b);
	float soft = brightness + _BloomThreshold.y;
	soft = clamp(soft, 0.0, _BloomThreshold.z);
	soft = soft * soft * _BloomThreshold.w;
	float contribution = max(soft, brightness - _BloomThreshold.x);
	contribution /= max(brightness, 0.00001);
	return color * contribution;
}

float4 BloomPrefilterPassFragment (Varyings input) : SV_TARGET 
{
	float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
	return float4(color, 1.0);
}

float4 BloomPrefilterFirefliesFragment (Varyings input) : SV_TARGET 
{
	float3 color = 0.0;
	float weightSum = 0.0;
	float2 offsets[] = {
		float2(0.0, 0.0),
		float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0),
		//float2(-1.0, 0.0), float2(1.0, 0.0), float2(0.0, -1.0), float2(0.0, 1.0)
	};
	for (int i = 0; i < 5; i++) 
	{
		float3 c =
			GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
		c = ApplyBloomThreshold(c);
		float w = 1.0 / (Luminance(c) + 1.0);
		color += c * w;
		weightSum += w;
	}
	//color *= 1.0 / 9.0;
	color /= weightSum;
	return float4(color, 1.0);
}

float4 BloomScatterFinalPassFragment (Varyings input) : SV_TARGET 
{
	float3 lowRes;
	if (_BloomBicubicUpsampling) 
	{
		lowRes = GetSourceBicubic(input.screenUV).rgb;
	}
	else 
	{
		lowRes = GetSource(input.screenUV).rgb;
	}
	float4 highRes = GetSource2(input.screenUV);
	lowRes += highRes.rgb - ApplyBloomThreshold(highRes.rgb);
	return float4(lerp(highRes.rgb, lowRes, _BloomIntensity), highRes.a);
}






// Color grading
float Luminance (float3 color, bool useACES) 
{
	return useACES ? AcesLuminance(color) : Luminance(color);
}
float3 ColorGradePostExposure (float3 color) 
{
	return color * _ColorAdjustments.x;
}
float3 ColorGradingContrast (float3 color, bool useACES) 
{
	color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color);
	color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
	color = useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
	return color;
}
float3 ColorGradeColorFilter (float3 color) 
{
	return color * _ColorFilter.rgb;
}
float3 ColorGradingHueShift (float3 color) 
{
	color = RgbToHsv(color);
	float hue = color.x + _ColorAdjustments.z;
	color.x = RotateHue(hue, 0.0, 1.0);
	return HsvToRgb(color);
}
float3 ColorGradingSaturation (float3 color, bool useACES) 
{
	float luminance = Luminance(color, useACES);
	return (color - luminance) * _ColorAdjustments.w + luminance;
}
// The LMS color space is a physiological color representation
// L (Long): Sensitive to long wavelengths (roughly corresponding to red light)
// M (Medium): Sensitive to medium wavelengths (roughly corresponding to green light)
// S (Short): Sensitive to short wavelengths (roughly corresponding to blue light)
// Usefull when when you want to change color temperature
// Used in pair in C# ColorUtils.ColorBalanceToLMSCoeffs(float temperature, float tint)
float3 ColorGradeWhiteBalance (float3 color) 
{
	color = LinearToLMS(color);
	color *= _WhiteBalance.rgb;
	return LMSToLinear(color);
}
float3 ColorGradeSplitToning (float3 color, bool useACES) 
{
	color = PositivePow(color, 1.0 / 2.2);
	float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
	float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
	float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
	color = SoftLight(color, shadows);
	color = SoftLight(color, highlights);
	return PositivePow(color, 2.2);
}
float3 ColorGradingChannelMixer (float3 color) 
{
	return mul(
		float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),
		color
	);
}
//  Multiply the color by the three colors separately, each scaled by their own weight, summing the results. The weights are based on luminance. The shadow weight starts at 1 and decreases to zero between its start and end, using the smoothstep function. The highlights weight increase from zero to one instead. And the midtones weight is equal to one minus the other two weights. The idea is that the shadows and highlights regions don't overlap—or just a little—so the midtones weight will never becomes negative. We don't enforce this in the inspector however, just like we don't enforce that start comes before end.
float3 ColorGradingShadowsMidtonesHighlights (float3 color, bool useACES) 
{
	float luminance = Luminance(color, useACES);
	float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
	float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
	float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
	return
		color * _SMHShadows.rgb * shadowsWeight +
		color * _SMHMidtones.rgb * midtonesWeight +
		color * _SMHHighlights.rgb * highlightsWeight;
}
float3 ColorGrade (float3 color, bool useACES = false) 
{
	//color = min(color, 60.0); // to limit precision errors clamp numbers. In LUT no need for these
	color = ColorGradePostExposure(color);
	color = ColorGradeWhiteBalance(color);
	color = ColorGradingContrast(color, useACES);
	color = ColorGradeColorFilter(color);
	color = max(color, 0.0);
	color = ColorGradeSplitToning(color, useACES);
	color = ColorGradingChannelMixer(color);
	color = max(color, 0.0);
	color = ColorGradingShadowsMidtonesHighlights(color, useACES);
	color = ColorGradingHueShift(color);
	color = ColorGradingSaturation(color, useACES);
	color = max(useACES ? ACEScg_to_ACES(color) : color, 0.0);
	return color;
}


// Apply dither
float3 ApplyDither(float3 color, float2 uv)
{
	const float minColorStep = 1.0 / 255.0;
	float luminance = Luminance(color);
	float dither = InterleavedGradientNoise(uv * _ScreenParams.xy, _Time.y * 144);

	#if defined(_DITHER_HIGH_QUALITY) 
		// Fade dither to black when color is dark
		if (luminance < minColorStep * 2.0)
	    {
	    	float endDither = 0;
	    	float factor = luminance / (minColorStep * 2.0);
	    	dither = lerp(endDither, dither, factor);
	    }
	    dither = ( (dither * 2.0 - 1.0) * minColorStep ) / 2.0;
	    color.rgb += dither;
		//color.g += dither;
	#else
	 	dither = ( (dither * 2.0 - 1.0) * minColorStep ) / 2.0;
		color.rb += dither;
		color.g -= dither;
	#endif
	return color;
}




// Lut functions
float3 GetColorGradedLUT (float2 uv, bool useACES = false) 
{
	float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
	return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}
float3 ApplyColorGradingLUT (float3 color) 
{
	return ApplyLut2D(
		TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
		saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
		_ColorGradingLUTParameters.xyz
	);
}
float4 FinalLUTPassFragment (Varyings input) : SV_TARGET 
{
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);

	#if defined(_DITHER)
		color.rgb = ApplyDither(color.rgb, input.screenUV.xy);
	#endif

	return color;
}
float4 ApplyColorGradingWithLumaPassFragment (Varyings input) : SV_TARGET 
{
	float4 color = GetSource(input.screenUV);
	color.rgb = ApplyColorGradingLUT(color.rgb);
	#if defined(_DITHER)
		color.rgb = ApplyDither(color.rgb, input.screenUV.xy);
	#endif
	color.a = sqrt(Luminance(color.rgb));
	return color;
}
float4 FinalPassFragmentRescale (Varyings input) : SV_TARGET 
{
	if (_CopyBicubic) 
	{
		return GetSourceBicubic(input.screenUV);
	}
	else 
	{
		return GetSource(input.screenUV);
	}
}





// Tonemapping
float4 ToneMappingNonePassFragment (Varyings input) : SV_TARGET 
{
	float3 color = GetColorGradedLUT(input.screenUV);
	return float4(color.rgb, 1.0);
}
float4 ToneMappingNeutralPassFragment (Varyings input) : SV_TARGET 
{
	/*float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb); 
	color.rgb = NeutralTonemap(color.rgb);
	return color;*/
	float3 color = GetColorGradedLUT(input.screenUV);
	color = NeutralTonemap(color);
	return float4(color.rgb, 1.0);
}
float4 ToneMappingReinhardPassFragment (Varyings input) : SV_TARGET 
{
	/*float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb); */
	float3 color = GetColorGradedLUT(input.screenUV);
	color.rgb /= color.rgb + 1.0;
	return float4(color.rgb, 1.0);
}
float4 ToneMappingACESPassFragment (Varyings input) : SV_TARGET 
{
	/*float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb, true); 
	color.rgb = AcesTonemap(color.rgb);
	return color;*/
	float3 color = GetColorGradedLUT(input.screenUV, true);
	color = AcesTonemap(color);
	return float4(color.rgb, 1.0);
}
float3 aces_approx(float3 v)
{
    v *= 0.6f;
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return clamp((v*(a*v+b))/(v*(c*v+d)+e), 0.0f, 1.0f);
}
// Gran Turismo Tonemapping
static const float e = 2.71828;
float W_f(float x,float e0,float e1) 
{
	if (x <= e0)
		return 0;
	if (x >= e1)
		return 1;
	float a = (x - e0) / (e1 - e0);
	return a * a*(3 - 2 * a);
}
float H_f(float x, float e0, float e1) 
{
	const float epsilon = 0.0001;
	if (x <= e0)
		return 0;
	if (x >= e1)
		return 1;
	return (x - e0) / max(e1 - e0, epsilon);
}

float GranTurismoTonemapper(float x) 
{
	float P = 1;
	float a = 1;
	float m = 0.22;
	float l = 0.4;
	float c = 1.33;
	float b = 0;
	float l0 = (P - m)*l / a;
	float L0 = m - m / a;
	float L1 = m + (1 - m) / a;
	float L_x = m + a * (x - m);
	float T_x = m * pow(x / m, c) + b;
	float S0 = m + l0;
	float S1 = m + a * l0;
	float C2 = a * P / (P - S1);
	float S_x = P - (P - S1)*pow(e,-(C2*(x-S0)/P));
	float w0_x = 1 - W_f(x, 0, m);
	float w2_x = H_f(x, m + l0, m + l0);
	float w1_x = 1 - w0_x - w2_x;
	float f_x = T_x * w0_x + L_x * w1_x + S_x * w2_x;
	return f_x;
}
float4 ToneMappingGTPassFragment (Varyings input) : SV_TARGET 
{
	/*float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb); */
	float3 color = GetColorGradedLUT(input.screenUV);
	//color.rgb = AcesTonemap(unity_to_ACES(color.rgb));
	float r = GranTurismoTonemapper(color.r);
	float g = GranTurismoTonemapper(color.g);
	float b = GranTurismoTonemapper(color.b);
	color = float3(r,g,b);	return float4(color, 1.0);
}

// Uncharted 2 Tone Mapping Operator
float3 Uncharted2Tonemap(float3 x)
{
    // Tone mapping constants from Uncharted 2.
    const float A = 0.15;
    const float B = 0.50;
    const float C = 0.10;
    const float D = 0.20;
    const float E = 0.02;
    const float F = 0.30;

    // Apply the tone mapping curve:
    //   result = ((x*(A*x + C*B) + D*E) / (x*(A*x + B) + D*F)) - E/F
    float3 numerator   = x * (A * x + C * B) + D * E;
    float3 denominator = x * (A * x + B)       + D * F;
    float3 mapped = (numerator / denominator) - E / F;
    
    // Clamp the result to [0, 1] to ensure valid color range.
    return saturate(mapped);
}
float3 ToneMappingUncharted2PassFragment(Varyings input) : SV_TARGET 
{
	/*float4 color = GetSource(input.screenUV);
	color.rgb = ColorGrade(color.rgb); */
	float3 color = GetColorGradedLUT(input.screenUV);
    // Adjust exposure (you can tweak this value as needed)
    float exposureBias = 3.0;
    color *= exposureBias;
    
    // Apply the Uncharted 2 tone mapping curve
    color.rgb = Uncharted2Tonemap(color);
    
    // Normalize the tone-mapped color by a white scale value.
    // Here 11.2 is used as the white point; adjust it to fit your scene.
    float3 whiteScale = Uncharted2Tonemap(11.2);
    color /= whiteScale;
    
    // Apply gamma correction (assuming a gamma of 2.2)
    color = pow(color, 1.0 / 0.8);
    
    return color;
}







Varyings DefaultPassVertex (uint vertexID : SV_VertexID) 
{
	Varyings output;
	output.positionCS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);

	if (_ProjectionParams.x < 0.0) 
	{
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}


// Def fragment
float4 CopyPassFragment (Varyings input) : SV_TARGET 
{
	return GetSource(input.screenUV);
}

#endif
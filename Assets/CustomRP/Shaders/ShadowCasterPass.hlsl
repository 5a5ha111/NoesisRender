#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED


#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/CustomDither.hlsl"


//CBUFFER_START(UnityPerMaterial)
//	float4 _BaseColor;
//CBUFFER_END

/*TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)*/


#define BIT_DEPTH 1
float _ShadowDither;
bool _ShadowPancaking;

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
	float3 positionWS : TEXCOORD1;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};



//float4 UnlitPassVertex(Attributes input) : SV_POSITION
//{
//	UNITY_SETUP_INSTANCE_ID(input);
//	float3 positionWS = TransformObjectToWorld(input.positionOS);
//	return TransformWorldToHClip(positionWS);
//}

float2 ComputeR2Dither(float x, float y) 
{
	float2 res;
    // Compute φ₂ as the unique positive root of x^3 = x + 1
    float phi2 = pow(0.5 * (sqrt(5) + 1), 1.0 / 3.0); 
    float g = 1.32471795724474602596;
    
    // Compute α₁ and α₂
    float alpha1 = 1.0 / phi2;
    float alpha2 = 1.0 / (phi2 * phi2);
    
    res.x = (0.5 + alpha1 * x) % 1;
    res.y = (0.5 + alpha2 * y) % 1;

    // Compute the fractional part of α₁x + α₂y (mod 1)
    float z = frac(alpha1 * x + alpha2 * y);
    
    // Triangular wave function to smooth discontinuity
    //return (z < 0.5) ? (2.0 * z) : (2.0 - 2.0 * z);

    return res;
}

float2 Nth_weyl(float2 p0, int n) 
{
    //return fract(p0 + float(n)*vec2(0.754877669, 0.569840296));
    return frac(p0 + float2(n*12664745, n*9560333)/exp2(24.0));	// integer mul to avoid round-off
}

float r_dither(float2 co)
{
	const float2 magic = float2(0.75487766624669276, 0.569840290998);
    return frac(dot(co, magic));
}

//note: from "NEXT GENERATION POST PROCESSING IN CALL OF DUTY: ADVANCED WARFARE"
//      http://advances.realtimerendering.com/s2014/index.html
// (copied from https://www.shadertoy.com/view/MslGR8)
float InterleavedGradientNoise(float2 uv )
{
    const float3 magic = float3( 0.06711056, 0.00583715, 52.9829189 );
    return frac(magic.z * frac(dot(uv, magic.xy)));
}

float remapTri(float n)
{
    float orig = n * 2.0 - 1.0;
    n = orig * rsqrt(abs(orig));
    return max(-1.0, n) - sign(orig);
}

float ApplyColorQuantization(float color, float ditherValue, float steps)
{
    return round(color * steps + ditherValue) / steps;
}


Varyings ShadowCasterPassVertex (Attributes input) 
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(positionWS);
	output.positionWS = positionWS;


	// Fix shadow clipping due being closer than shadow camera near clip plane.
	// We do this by taking the maximum of the clip space Z and W coordinates, or their minimum when UNITY_REVERSED_Z is defined. To use the correct sign for the W coordinate multiply it with UNITY_NEAR_CLIP_VALUE. Use only for dir lights
	if (_ShadowPancaking) 
	{
		#if UNITY_REVERSED_Z
			output.positionCS.z =
				min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
		#else
			output.positionCS.z =
				max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
		#endif
	}

	float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	return output;
}

void ShadowCasterPassFragment (Varyings input) 
{
	UNITY_SETUP_INSTANCE_ID(input);
	/*float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	float4 base = baseMap * baseColor;*/

	InputConfig config = GetInputConfig(input.baseUV, 0);
	float4 base = GetBase(config);

	ClipLOD(input.positionCS.xy, unity_LODFade.x);

	#if defined(_SHADOWS_CLIP)
		clip(base.a - GetSmoothness(config));
	#elif defined(_SHADOWS_DITHER)
		//float2 pos = input.positionCS.xy;
		//float2 pos = frac(input.positionWS.xz) * 10000;
		float dither = InterleavedGradientNoise(input.positionCS.xy, 1);
		/*uint SHADES = 32; 
		float mydither = RiemeresmaDither(pos, SHADES, base.a); */ 
		//clip(base.a - mydither);
		clip(base.a - dither);
	#elif defined(_SHADOWS_RIEMERESMA_DITHER)
		uint shades = 2; 
		float2 pos = input.positionCS.xy;
		//pos = frac(input.positionWS.xz) * 200;
		float2 pixel =  _ScreenParams.xy;
		//pos = round(input.positionCS.xy * pixel);
		float mydither = r_dither(pos);
		mydither = remapTri(mydither);
		float amount = smoothstep(0, 1, base.a);

		//clip(pow(base.a, 0.75) * 0.9 - mydither);

		// Interleaved gradient noise with remap
		float col = ApplyColorQuantization(base.a, mydither * 0.5, 1);
		clip(col- 0.5);


		/*float lowBorder = 0.025;
		if (amount < lowBorder)
		{
			amount *= smoothstep(0, lowBorder, amount);
		}*/

		float res = RiemeresmaDither(pos, shades, pow(abs(base.a), 1.5));
		float2 mydither2 = ComputeR2Dither(pos.x, pos.y);
		mydither = max(mydither2.x, mydither2.y);
		mydither = mydither2.x;
		mydither2 = Nth_weyl(pos/2, _ShadowDither);
		mydither = max(mydither2.x, mydither2.y);
		//mydither = pos.x % 0 == 1 ? mydither2.x : mydither2.y;
		mydither = remapTri(mydither2.x);

		mydither = InterleavedGradientNoise(pos);
		mydither = remapTri(mydither);

		mydither = r_dither(pos) - 0.5;

		const float lsb = exp2(float(BIT_DEPTH)) - 1.0;

		col = base.a;
		mydither = mydither / lsb;
		col += mydither;
		col = round(col * lsb) / lsb;
		//float res = RiemeresmaDither(pos, shades, amount);
		//mydither = (max(base.a, mydither) + min(base.a, mydither)) / 2;

		/*if (amount < 0.025)
		{
			mydither = 1;
		}
		if (amount > 0.95)
		{
			mydither = 0;
		}*/

		if (base.a < 0.01)
		{
			col = 0;
		}

		// Temporaly use Alpha _Cutoff for dither multiplayer
		//clip(base.a - 1 + (col * _Cutoff));
		//clip(col - _ShadowDither);


		
		//clip(res - _Cutoff);
	#endif
}


#endif
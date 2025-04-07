#ifndef CUSTOM_DITHER
#define CUSTOM_DITHER


#include "../ShaderLibrary/Common.hlsl"


uint HilbertIndex(uint2 p) 
{
    uint i = 0;
    for (uint l = 0x4000; l > 0; l >>= 1) {
        uint2 r = min(p & l, 1);
        
        i = (i << 2) | ((r.x * 3) ^ r.y);       
        p = (r.y == 0) ? ((0x7FFF * r.x) ^ uint2(p.y, p.x)) : p;
    }
    return i;
}

uint ReverseBits(uint x) 
{
    x = ((x & 0xaaaaaaaau) >> 1) | ((x & 0x55555555u) << 1);
    x = ((x & 0xccccccccu) >> 2) | ((x & 0x33333333u) << 2);
    x = ((x & 0xf0f0f0f0u) >> 4) | ((x & 0x0f0f0f0fu) << 4);
    x = ((x & 0xff00ff00u) >> 8) | ((x & 0x00ff00ffu) << 8);
    return (x >> 16) | (x << 16);
}

// from: https://psychopath.io/post/2021_01_30_building_a_better_lk_hash
uint OwenHash(uint x, uint seed) 
{ // seed is any random number
    x ^= x * 0x3d20adeau;
    x += seed;
    x *= (seed >> 16) | 1u;
    x ^= x * 0x05526c56u;
    x ^= x * 0x53a22864u;
    return x;
}

// adapted from: https://www.shadertoy.com/view/MslGR8
float ReshapeUniformToTriangle(float v) 
{
    v = v * 2.0 - 1.0;
    v = sign(v) * (1.0 - sqrt(max(0.0, 1.0 - abs(v)))); // [-1, 1], max prevents NaNs
    return v + 0.5; // [-0.5, 1.5]
}
float remapTri(float n)
{
    float orig = n * 2.0 - 1.0;
    n = orig * rsqrt(abs(orig));
    return max(-1.0, n) - sign(orig);
}

// Use hilbert curve for error diffusion. Produce more stable random pattern. From https://www.compuphase.com/riemer.htm and https://www.shadertoy.com/view/ssBBW1
float RiemeresmaDither(uint2 coord, uint shades, float amount)
{
	uint hilbertIndex = HilbertIndex(coord.xy); // map pixel coords to hilbert curve index
	uint m = OwenHash(ReverseBits(hilbertIndex), 0xe7843fbfu);   // owen-scramble hilbert index
	m = OwenHash(ReverseBits(m), 0x8d8fb1e0u);   // map hilbert index to sobol sequence and owen-scramble
	float mask = float(ReverseBits(m)) / 4294967296.0; // convert to float

	mask = ReshapeUniformToTriangle(mask);

	float res = floor((amount + mask / shades) * shades) / shades; // quantise and apply dither mask

	return res;
}

float RiemeresmaDither(uint2 coord)
{
	uint hilbertIndex = HilbertIndex(coord.xy); // map pixel coords to hilbert curve index
	uint m = OwenHash(ReverseBits(hilbertIndex), 0xe7843fbfu);   // owen-scramble hilbert index
	m = OwenHash(ReverseBits(m), 0x8d8fb1e0u);   // map hilbert index to sobol sequence and owen-scramble
	float mask = float(ReverseBits(m)) / 4294967296.0; // convert to float

	mask = ReshapeUniformToTriangle(mask);

	return mask;
}


// from "The Unreasonable Effectiveness of Quasirandom Sequences"
// http://extremelearning.com.au/unreasonable-effectiveness-of-quasirandom-sequences/
float Rdither(float2 co)
{
    const float2 magic = float2(0.75487766624669276, 0.569840290998);
    return frac(dot(co, magic));
}
float Rdither(float3 co)
{
    const float3 magic = float3(0.75487766624669276, 0.569840290998, 0.526876);
    return frac(dot(co, magic));
}

float Rdither(float2 pos, float t)
{
    const float2 magic = float2(0.75487766624669276, 0.569840290998);
    return frac(dot(pos, magic) + t);
}



void ClipLOD (Fragment fragment, float fade) 
{
    #if defined(LOD_FADE_CROSSFADE)
        //float dither = (positionCS.y % 32) / 32;
        //float dither = Rdither(positionCS);
        float dither = InterleavedGradientNoise(fragment.positionSS.xy, /*_Time.w * 1*/0);
        clip(fade + (fade < 0.0 ? dither : -dither));
    #endif
}

float hash1D(float2 p) 
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float HashBasedTriangularDither(float2 uv)
{
    float r = hash1D(uv) + hash1D(uv + (float2)1.1) - 0.5;
    return r;
}

// (https://sites.google.com/site/murmurhash/)
uint MurmurHash(uint x, uint seed)
{
    const uint m = 0x5bd1e995U;
    uint hash = seed;
    // process input
    uint k = x;
    k *= m;
    k ^= k >> 24;
    k *= m;
    hash *= m;
    hash ^= k;
    // some final mixing
    hash ^= hash >> 13;
    hash *= m;
    hash ^= hash >> 15;
    return hash;
}

float2 GradientNoise_dir(float2 p)
{
    p = p % 289;
    float x = (34 * p.x + 1) * p.x % 289 + p.y;
    x = (34 * x + 1) * x % 289;
    x = frac(x / 41) * 2 - 1;
    return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
}
float PerlinNoise(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float d00 = dot(GradientNoise_dir(ip), fp);
    float d01 = dot(GradientNoise_dir(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(GradientNoise_dir(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(GradientNoise_dir(ip + float2(1, 1)), fp - float2(1, 1));
    fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
    return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x);
}
float PerlinNoiseImpruved(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    float d00 = dot(GradientNoise_dir(ip), fp);
    float d01 = dot(GradientNoise_dir(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(GradientNoise_dir(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(GradientNoise_dir(ip + float2(1, 1)), fp - float2(1, 1));
    fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
    return FifthOrderInterpolate(FifthOrderInterpolate(d00, d01, fp.y), FifthOrderInterpolate(d10, d11, fp.y), fp.x);
}


// implementation of MurmurHash (https://sites.google.com/site/murmurhash/) for a  
// 3-dimensional unsigned integer input vector.
uint hash31(float3 x, uint seed)
{
    const uint m = 0x5bd1e995U;
    uint hash = seed;
    // process first vector element
    uint k = x.x; 
    k *= m;
    k ^= k >> 24;
    k *= m;
    hash *= m;
    hash ^= k;
    // process second vector element
    k = x.y; 
    k *= m;
    k ^= k >> 24;
    k *= m;
    hash *= m;
    hash ^= k;
    // process third vector element
    k = x.z; 
    k *= m;
    k ^= k >> 24;
    k *= m;
    hash *= m;
    hash ^= k;
    // some final mixing
    hash ^= hash >> 13;
    hash *= m;
    hash ^= hash >> 15;
    return hash;
}
uint3 hash33(uint p)
{
    uint3 v = (uint3) (int3) round(p);
    v.x ^= 110351;
    v.y ^= v.x + v.z;
    v.y = v.y * 134;
    v.z += v.x ^ v.y;
    v.y += v.x ^ v.z;
    v.x += v.y * v.z;
    v.x = v.x * 27;
    v.z ^= v.x << 3;
    v.y += v.z << 3; 
    uint3 Out = v * (1.0 / float(0xffffffff));
    return Out;
}

float3 random3D(float3 uvw)
{
    uvw = float3( dot(uvw, float3(127.1,311.7, 513.7) ),
               dot(uvw, float3(269.5,183.3, 396.5) ),
               dot(uvw, float3(421.3,314.1, 119.7) ) );
            
    return -1.0 + 2.0 * frac(sin(uvw) * 43758.5453123);
}
float PerlinNoise3D(float3 uvw)
{
    //uvw *= noise_scale;
    //uvw += noise_transform;
    
    float3 gridIndex = floor(uvw); 
    float3 gridFract = frac(uvw);
    
    //float3 blur = smoothstep(0.0, 1.0, gridFract);
    float3 blur = FifthOrderInterpolate(gridFract);
    
    float3 blb = gridIndex + float3(0.0, 0.0, 0.0);
    float3 brb = gridIndex + float3(1.0, 0.0, 0.0);
    float3 tlb = gridIndex + float3(0.0, 1.0, 0.0);
    float3 trb = gridIndex + float3(1.0, 1.0, 0.0);
    float3 blf = gridIndex + float3(0.0, 0.0, 1.0);
    float3 brf = gridIndex + float3(1.0, 0.0, 1.0);
    float3 tlf = gridIndex + float3(0.0, 1.0, 1.0);
    float3 trf = gridIndex + float3(1.0, 1.0, 1.0);
    
    float3 gradBLB = random3D(blb);
    float3 gradBRB = random3D(brb);
    float3 gradTLB = random3D(tlb);
    float3 gradTRB = random3D(trb);
    float3 gradBLF = random3D(blf);
    float3 gradBRF = random3D(brf);
    float3 gradTLF = random3D(tlf);
    float3 gradTRF = random3D(trf);
    
    
    float3 distToPixelFromBLB = gridFract - float3(0.0, 0.0, 0.0);
    float3 distToPixelFromBRB = gridFract - float3(1.0, 0.0, 0.0);
    float3 distToPixelFromTLB = gridFract - float3(0.0, 1.0, 0.0);
    float3 distToPixelFromTRB = gridFract - float3(1.0, 1.0, 0.0);
    float3 distToPixelFromBLF = gridFract - float3(0.0, 0.0, 1.0);
    float3 distToPixelFromBRF = gridFract - float3(1.0, 0.0, 1.0);
    float3 distToPixelFromTLF = gridFract - float3(0.0, 1.0, 1.0);
    float3 distToPixelFromTRF = gridFract - float3(1.0, 1.0, 1.0);
    
    float dotBLB = dot(gradBLB, distToPixelFromBLB);
    float dotBRB = dot(gradBRB, distToPixelFromBRB);
    float dotTLB = dot(gradTLB, distToPixelFromTLB);
    float dotTRB = dot(gradTRB, distToPixelFromTRB);
    float dotBLF = dot(gradBLF, distToPixelFromBLF);
    float dotBRF = dot(gradBRF, distToPixelFromBRF);
    float dotTLF = dot(gradTLF, distToPixelFromTLF);
    float dotTRF = dot(gradTRF, distToPixelFromTRF);
    
    
    return lerp(
        lerp(
            lerp(dotBLB, dotBRB, blur.x),
            lerp(dotTLB, dotTRB, blur.x), 
            blur.y
        ),
        lerp(
            lerp(dotBLF, dotBRF, blur.x),
            lerp(dotTLF, dotTRF, blur.x), 
            blur.y
        ), 
        blur.z
    ) + 0.5;
}


float3 hash( float3 p )      // this hash is not production ready, please
{                        // replace this by something better
    p = float3( dot(p,float3(127.1,311.7, 74.7)),
              dot(p,float3(269.5,183.3,246.1)),
              dot(p,float3(113.5,271.9,124.6)));

    return -1.0 + 2.0*frac(sin(p)*43758.5453123);
}

// return value noise (in x) and its derivatives (in yzw)
float4 noised( in float3 x )
{
    // grid
    float3 i = floor(x);
    float3 f = frac(x);
    
    #if INTERPOLANT==1
    // quintic interpolant
    float3 u = f*f*f*(f*(f*6.0-15.0)+10.0);
    float3 du = 30.0*f*f*(f*(f-2.0)+1.0);
    #else
    // cubic interpolant
    float3 u = f*f*(3.0-2.0*f);
    float3 du = 6.0*f*(1.0-f);
    #endif    
    
    // gradients
    /*#if METHOD==0
    float3 ga = hash( i+uint3(0,0,0) );
    float3 gb = hash( i+uint3(1,0,0) );
    float3 gc = hash( i+uint3(0,1,0) );
    float3 gd = hash( i+uint3(1,1,0) );
    float3 ge = hash( i+uint3(0,0,1) );
    float3 gf = hash( i+uint3(1,0,1) );
    float3 gg = hash( i+uint3(0,1,1) );
    float3 gh = hash( i+uint3(1,1,1) );
    #else*/
    float3 ga = hash( i+float3(0.0,0.0,0.0) );
    float3 gb = hash( i+float3(1.0,0.0,0.0) );
    float3 gc = hash( i+float3(0.0,1.0,0.0) );
    float3 gd = hash( i+float3(1.0,1.0,0.0) );
    float3 ge = hash( i+float3(0.0,0.0,1.0) );
    float3 gf = hash( i+float3(1.0,0.0,1.0) );
    float3 gg = hash( i+float3(0.0,1.0,1.0) );
    float3 gh = hash( i+float3(1.0,1.0,1.0) );
    //#endif
    
    // projections
    float va = dot( ga, f-float3(0.0,0.0,0.0) );
    float vb = dot( gb, f-float3(1.0,0.0,0.0) );
    float vc = dot( gc, f-float3(0.0,1.0,0.0) );
    float vd = dot( gd, f-float3(1.0,1.0,0.0) );
    float ve = dot( ge, f-float3(0.0,0.0,1.0) );
    float vf = dot( gf, f-float3(1.0,0.0,1.0) );
    float vg = dot( gg, f-float3(0.0,1.0,1.0) );
    float vh = dot( gh, f-float3(1.0,1.0,1.0) );
    
    /*// interpolations
    return vec4( va + u.x*(vb-va) + u.y*(vc-va) + u.z*(ve-va) + u.x*u.y*(va-vb-vc+vd) + u.y*u.z*(va-vc-ve+vg) + u.z*u.x*(va-vb-ve+vf) + (-va+vb+vc-vd+ve-vf-vg+vh)*u.x*u.y*u.z,    // value
                 ga + u.x*(gb-ga) + u.y*(gc-ga) + u.z*(ge-ga) + u.x*u.y*(ga-gb-gc+gd) + u.y*u.z*(ga-gc-ge+gg) + u.z*u.x*(ga-gb-ge+gf) + (-ga+gb+gc-gd+ge-gf-gg+gh)*u.x*u.y*u.z +   // derivatives
                 du * (vec3(vb,vc,ve) - va + u.yzx*vec3(va-vb-vc+vd,va-vc-ve+vg,va-vb-ve+vf) + u.zxy*vec3(va-vb-ve+vf,va-vb-vc+vd,va-vc-ve+vg) + u.yzx*u.zxy*(-va+vb+vc-vd+ve-vf-vg+vh) ));*/

    // Calculate coefficients
    float k0 = va;
    float k1 = vb - va;
    float k2 = vc - va;
    float k3 = ve - va;
    float k4 = va - vb - vc + vd;
    float k5 = va - vc - ve + vg;
    float k6 = va - vb - ve + vf;
    float k7 = -va + vb + vc - vd + ve - vf - vg + vh;

    // Value computation
    float val_lin = k0 + k1*u.x + k2*u.y + k3*u.z;
    float val_quad = k4*u.x*u.y + k5*u.y*u.z + k6*u.z*u.x;
    float val_cubic = k7*u.x*u.y*u.z;
    float value = val_lin + val_quad + val_cubic;

    // Gradient computation
    float3 grad_lin = ga + u.x*(gb-ga) + u.y*(gc-ga) + u.z*(ge-ga);
    float3 grad_quad = u.x*u.y*(ga-gb-gc+gd) + u.y*u.z*(ga-gc-ge+gg) + u.z*u.x*(ga-gb-ge+gf);
    float3 grad_cubic = (-ga+gb+gc-gd+ge-gf-gg+gh)*u.x*u.y*u.z;
    float3 gradients = grad_lin + grad_quad + grad_cubic;

    // Derivative computation
    float3 der_terms = float3(k1, k2, k3) 
                   + u.yzx*float3(k4, k5, k6) 
                   + u.zxy*float3(k6, k4, k5) 
                   + u.yzx*u.zxy*k7;
    float3 derivatives = du * der_terms;

    return float4(value, gradients + derivatives);
}









#endif
#ifndef CUSTOM_DITHER
#define CUSTOM_DITHER




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





#endif
// ──────────────────────────────────────────────────────────────────────────────
// CustomGraphFunctions.hlsl
//
// Custom overrides BEFORE the official ShaderGraph Functions.hlsl.
// Functions.hlsl uses #ifndef guards, so our #defines take priority.
// ──────────────────────────────────────────────────────────────────────────────
#ifndef CUSTOM_GRAPH_FUNCTIONS_INCLUDED
#define CUSTOM_GRAPH_FUNCTIONS_INCLUDED

// ── Ambient from SH L0 (override default zero) ──────────────────────────────
#define SHADERGRAPH_AMBIENT_SKY     half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w)
#define SHADERGRAPH_AMBIENT_EQUATOR half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w)
#define SHADERGRAPH_AMBIENT_GROUND  half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w)

// ── Official ShaderGraph Functions ───────────────────────────────────────────
// Provides: IsGammaSpace(), ComputeScreenPos(), Gradient, SRGBToLinear,
//           and all SHADERGRAPH_* fallback stubs with #ifndef guards.
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

#endif // CUSTOM_GRAPH_FUNCTIONS_INCLUDED

// ──────────────────────────────────────────────────────────────────────────────
// CustomForwardPass.hlsl
//
// Minimal unlit forward pass. Just writes BaseColor + Alpha to render target.
// No URP dependency. Similar to CRP Unlit.shader functionality.
// ──────────────────────────────────────────────────────────────────────────────

PackedVaryings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    output = BuildVaryings(input);
    PackedVaryings packedOutput = PackVaryings(output);
    return packedOutput;
}

half4 frag(PackedVaryings packedInput) : SV_Target0
{
    Varyings unpacked = UnpackVaryings(packedInput);
    UNITY_SETUP_INSTANCE_ID(unpacked);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(unpacked);

    SurfaceDescription surfaceDescription = BuildSurfaceDescription(unpacked);

    half3 color = surfaceDescription.BaseColor;

#if defined(_ALPHATEST_ON)
    half alpha = surfaceDescription.Alpha;
    clip(alpha - surfaceDescription.AlphaClipThreshold);
#else
    half alpha = half(1.0);
#endif

    return half4(color, alpha);
}

// ──────────────────────────────────────────────────────────────────────────────
// CustomSelectionPickingPass.hlsl
//
// Editor-only pass for scene view selection and picking.
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

    SurfaceDescription surfaceDescription = BuildSurfaceDescription(unpacked);

#if defined(_ALPHATEST_ON)
    half alpha = surfaceDescription.Alpha;
    clip(alpha - surfaceDescription.AlphaClipThreshold);
#endif

#ifdef SCENESELECTIONPASS
    // Output object ID for scene selection
    half4 outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
#elif defined(SCENEPICKINGPASS)
    half4 outColor = _SelectionID;
#else
    half4 outColor = half4(0, 0, 0, 1);
#endif

    return outColor;
}

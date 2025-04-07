#ifndef NORMAL_CALCULATION_INCLUDED
#define NORMAL_CALCULATION_INCLUDED



//TEXTURE2D(_CameraDepthTexture);
float4x4 _NormalReconstructionMatrix;

float GetRawDepth(float2 uv)
{
    float depth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;
    //depth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(depth) : LinearEyeDepth(depth, _ZBufferParams);
    //depth = LinearEyeDepth(depth, _ZBufferParams);
    return depth;
    //return SampleSceneDepth(uv.xy).r;
}
// inspired by keijiro's depth inverse projection
// https://github.com/keijiro/DepthInverseProjection
// constructs view space ray at the far clip plane from the screen uv
// then multiplies that ray by the linear 01 depth
float3 ViewSpacePosAtScreenUV(float2 uv)
{
    float3 viewSpaceRay = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 1.0, 1.0) * _ProjectionParams.z).xyz;
    float rawDepth = GetRawDepth(uv).r;
    return viewSpaceRay * Linear01Depth(rawDepth, _ZBufferParams);
}
float3 ViewSpacePosAtScreenUV(float2 uv, float3 cameraDir, float3 viewDirection)
{
    /*float3 viewSpaceRay = mul(_NormalReconstructionMatrix, float4(uv * 2.0 - 1.0, 1.0, 1.0) * _ProjectionParams.z).xyz;
    float rawDepth = GetRawDepth(uv);
    return viewSpaceRay * Linear01Depth(rawDepth, _ZBufferParams);*/

    float rawDepth = GetRawDepth(uv);
    float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
    //float linearDepth = rawDepth;
    float3 posWS = ComputeWSPosFromDepth(linearDepth, cameraDir, viewDirection);
    float3 posVS = WorldToViewSpace(float4(posWS, 1)).xyz;
    return posWS;
}
float3 ReconstructPosition(float2 uv, float z, float4x4 InvVP)
{
  float x = uv.x * 2.0 - 1.0;
  float y = (uv.y) * 2.0 - 1.0;
  float4 position_s = float4(x, y, z, 1.0);
  float4 position_v = mul(InvVP, position_s);
  float3 viewPos = position_v.xyz / position_v.w;
  return viewPos;
  //return TrasformViewToWorld(viewPos);
}
float3 ComputeViewSpacePosition(float3 ray, float2 uv)
{
    // Render settings
    float near = _ProjectionParams.y;
    float far = _ProjectionParams.z;
    float isOrtho = unity_OrthoParams.w; // 0: perspective, 1: orthographic

    // Z buffer sample
    float z = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, uv);

    // Far plane exclusion
    #if !defined(EXCLUDE_FAR_PLANE)
    float mask = 1;
    #elif defined(UNITY_REVERSED_Z)
    float mask = z > 0;
    #else
    float mask = z < 1;
    #endif

    // Perspective: view space position = ray * depth
    float3 vposPers = ray * Linear01Depth(z, _ZBufferParams);

    // Orthographic: linear depth (with reverse-Z support)
    #if defined(UNITY_REVERSED_Z)
    float depthOrtho = -lerp(far, near, z);
    #else
    float depthOrtho = -lerp(near, far, z);
    #endif

    // Orthographic: view space position
    float3 vposOrtho = float3(ray.xy, depthOrtho);

    // Result: view space position
    return lerp(vposPers, vposOrtho, isOrtho) * mask;
}
float3 ViewSpacePosAtPixelPosition(float2 positionSS)
{
    float4 _ScreenSize = _ScreenParams;
    float2 uv = positionSS * _ScreenSize.zw;
    return ViewSpacePosAtScreenUV(uv);
}
// inspired by keijiro's depth inverse projection
// https://github.com/keijiro/DepthInverseProjection
// constructs view space ray at the far clip plane from the screen uv
// then multiplies that ray by the linear 01 depth
float3 FreshReconstruction(float2 uv)
{
    float3 viewSpaceRay = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 1.0, 1.0) * _ProjectionParams.z).xyz;
    float rawDepth = GetRawDepth(uv).r;
    return viewSpaceRay * Linear01Depth(rawDepth, _ZBufferParams);
}



float3 CalculateNormal(float3 tangent, float3 bitangent)
{
    // Compute the normal using the cross product (right-hand rule)
    float3 normal = normalize(cross(tangent, bitangent));
    return normal;
}

// Reconstruct normal by view pos
float3 ReconstructNormalDerivative(float3 viewSpacePos)
{
    float3 hDeriv = ddy(viewSpacePos);
    float3 vDeriv = ddx(viewSpacePos);
    return half3(normalize(cross(hDeriv, vDeriv)));
}

// Taken from https://gist.github.com/bgolus/a07ed65602c009d5e2f753826e8078a0
// unity's compiled fragment shader stats: 33 math, 3 tex
half3 ReconstructNormalTap3(float2 positionSS)
{
    float2 screenPixelSize = _CameraBufferSize.zw;
    float2 uv = positionSS;
    // get current pixel's view space position
    float3 viewSpacePos_c = ViewSpacePosAtScreenUV(uv + float2(0.0, 0.0) * screenPixelSize);

    // get view space position at 1 pixel offsets in each major direction
    float3 viewSpacePos_r = ViewSpacePosAtScreenUV(uv + float2(1.0, 0.0) * screenPixelSize);
    float3 viewSpacePos_u = ViewSpacePosAtScreenUV(uv + float2(0.0, 1.0) * screenPixelSize);

    // get the difference between the current and each offset position
    float3 hDeriv = viewSpacePos_r - viewSpacePos_c;
    float3 vDeriv = viewSpacePos_u - viewSpacePos_c;

    // get view space normal from the cross product of the diffs
    half3 viewNormal = half3(normalize(cross(hDeriv, vDeriv)));

    return viewNormal;
}

// Taken from https://gist.github.com/bgolus/a07ed65602c009d5e2f753826e8078a0
// unity's compiled fragment shader stats: 50 math, 4 tex
half3 ReconstructNormalTap4(float2 positionSS)
{
    float2 screenPixelSize = _CameraBufferSize.zw;
    float2 uv = positionSS;
    // get view space position at 1 pixel offsets in each major direction
    float3 viewSpacePos_l = ViewSpacePosAtScreenUV(uv + float2(-1.0, 0.0) * screenPixelSize);
    float3 viewSpacePos_r = ViewSpacePosAtScreenUV(uv + float2(1.0, 0.0) * screenPixelSize);
    float3 viewSpacePos_d = ViewSpacePosAtScreenUV(uv + float2(0.0, -1.0) * screenPixelSize);
    float3 viewSpacePos_u = ViewSpacePosAtScreenUV(uv + float2(0.0, 1.0) * screenPixelSize);

    // get the difference between the current and each offset position
    float3 hDeriv = viewSpacePos_r - viewSpacePos_l;
    float3 vDeriv = viewSpacePos_u - viewSpacePos_d;

    // get view space normal from the cross product of the diffs
    half3 viewNormal = half3(normalize(cross(hDeriv, vDeriv)));

    return viewNormal;
}

// Taken from https://gist.github.com/bgolus/a07ed65602c009d5e2f753826e8078a0
// unity's compiled fragment shader stats: 54 math, 5 tex
half3 ReconstructNormalTap5(float2 positionSS)
{
    float2 screenPixelSize = _CameraBufferSize.zw;
    float2 uv = positionSS;
    // get current pixel's view space position
    half3 viewSpacePos_c = ViewSpacePosAtScreenUV(uv);

    // get view space position at 1 pixel offsets in each major direction
    float3 viewSpacePos_l = ViewSpacePosAtScreenUV(uv + float2(-1.0, 0.0) * screenPixelSize);
    float3 viewSpacePos_r = ViewSpacePosAtScreenUV(uv + float2(1.0, 0.0) * screenPixelSize);
    float3 viewSpacePos_d = ViewSpacePosAtScreenUV(uv + float2(0.0, -1.0) * screenPixelSize);
    float3 viewSpacePos_u = ViewSpacePosAtScreenUV(uv + float2(0.0, 1.0) * screenPixelSize);

    // get the difference between the current and each offset position
    float3 l = viewSpacePos_c - viewSpacePos_l;
    float3 r = viewSpacePos_r - viewSpacePos_c;
    float3 d = viewSpacePos_c - viewSpacePos_d;
    float3 u = viewSpacePos_u - viewSpacePos_c;

    // pick horizontal and vertical diff with the smallest z difference
    float3 hDeriv = abs(l.z) < abs(r.z) ? l : r;
    float3 vDeriv = abs(d.z) < abs(u.z) ? d : u;

    // get view space normal from the cross product of the two smallest offsets
    half3 viewNormal = half3(normalize(cross(hDeriv, vDeriv)));

    return viewNormal;
}

// Taken from https://gist.github.com/bgolus/a07ed65602c009d5e2f753826e8078a0
// unity's compiled fragment shader stats: 66 math, 9 tex
half3 ReconstructNormalTap9(float2 positionSS)
{
    float4 _ScreenSize = _ScreenParams;
    //float2 screenPixelSize = (1.0 / _CameraBufferSize.xy);
    float2 screenPixelSize = _CameraBufferSize.zw;
    // screen uv from positionSS
    float2 uv = positionSS;

    // current pixel's depth
    float c = GetRawDepth(uv);

    // get current pixel's view space position
    float3 viewSpacePos_c = ViewSpacePosAtScreenUV(uv);

    // get view space position at 1 pixel offsets in each major direction
    float3 viewSpacePos_l = ViewSpacePosAtScreenUV(uv + float2(-1.0, 0.0) * screenPixelSize);
    float3 viewSpacePos_r = ViewSpacePosAtScreenUV(uv + float2(1.0, 0.0) * screenPixelSize);
    float3 viewSpacePos_d = ViewSpacePosAtScreenUV(uv + float2(0.0, -1.0) * screenPixelSize);
    float3 viewSpacePos_u = ViewSpacePosAtScreenUV(uv + float2(0.0, 1.0) * screenPixelSize);

    // get the difference between the current and each offset position
    half3 l = viewSpacePos_c - viewSpacePos_l;
    half3 r = viewSpacePos_r - viewSpacePos_c;
    half3 d = viewSpacePos_c - viewSpacePos_d;
    half3 u = viewSpacePos_u - viewSpacePos_c;

    // get depth values at 1 & 2 pixels offsets from current along the horizontal axis
    half4 H = half4(
        GetRawDepth(uv + float2(-1.0, 0.0) * screenPixelSize),
        GetRawDepth(uv + float2( 1.0, 0.0) * screenPixelSize),
        GetRawDepth(uv + float2(-2.0, 0.0) * screenPixelSize),
        GetRawDepth(uv + float2( 2.0, 0.0) * screenPixelSize)
    );

    // get depth values at 1 & 2 pixels offsets from current along the vertical axis
    half4 V = half4(
        GetRawDepth(uv + float2(0.0,-1.0) * screenPixelSize),
        GetRawDepth(uv + float2(0.0, 1.0) * screenPixelSize),
        GetRawDepth(uv + float2(0.0,-2.0) * screenPixelSize),
        GetRawDepth(uv + float2(0.0, 2.0) * screenPixelSize)
    );

    // current pixel's depth difference from slope of offset depth samples
    // differs from original article because we're using non-linear depth values
    // see article's comments
    half2 he = abs((2 * H.xy - H.zw) - c);
    half2 ve = abs((2 * V.xy - V.zw) - c);

    // pick horizontal and vertical diff with the smallest depth difference from slopes
    half3 hDeriv = he.x < he.y ? l : r;
    half3 vDeriv = ve.x < ve.y ? d : u;

    // get view space normal from the cross product of the best derivatives
    half3 viewNormal = normalize(cross(hDeriv, vDeriv));

    return viewNormal;
}





#endif
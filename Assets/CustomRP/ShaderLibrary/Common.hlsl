#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"


#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection


#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
	#define SHADOWS_SHADOWMASK
#endif


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"


SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);


bool IsOrthographicCamera () 
{
	return unity_OrthoParams.w;
}
float OrthographicDepthBufferToLinear (float rawDepth) 
{
	#if UNITY_REVERSED_Z
		rawDepth = 1.0 - rawDepth;
	#endif
	//The near and far distances of camera plane are stored in the Y and Z components of _ProjectionParams.
	// To convert it to view-space depth we have to scale it by the camera's near–far range and then add the near plane distance.
	return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"
#include "ForwardPlus.hlsl"


//float3 TransformObjectToWorld(float3 positionOS) {
//	return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
//}
//
//float4 TransformWorldToHClip(float3 positionWS) {
//	return mul(unity_MatrixVP, float4(positionWS, 1.0));
//}

float Square (float v) 
{
	return v * v;
}

float DistanceSquared(float3 pA, float3 pB) 
{
	return dot(pA - pB, pA - pB);
}



float3 DecodeNormal (float4 sample, float scale) 
{
	#if defined(UNITY_NO_DXT5nm)
	    return normalize(UnpackNormalRGB(sample, scale));
	#else
	    return normalize(UnpackNormalmapRGorAG(sample, scale));
	#endif
}
float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS) 
{
	float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
	return TransformTangentToWorld(normalTS, tangentToWorld);
}

float3 TrasformViewToWorld(float3 view)
{
	float3 _Transform_Out = mul(UNITY_MATRIX_I_V, float4(view, 1)).xyz;
	return _Transform_Out;
}
float4 WorldToViewSpace(float4 worldPos)
{
    return mul(unity_MatrixV, worldPos);
}
float3 ViewToScreenSpace(float3 view)
{
	float4 hclipPosition = TransformWViewToHClip(view);
	float3 screenPos = hclipPosition.xyz / hclipPosition.w;
	float3 _Transform_Out = float3(screenPos.xy * 0.5 + 0.5, screenPos.z);
	return _Transform_Out;
}
// Transforms a direction from object space to world space
float3 UnityObjectToWorldDir(float3 dir) 
{
    // Use the model matrix (unity_ObjectToWorld) to transform the direction
    return normalize(mul((float3x3)unity_ObjectToWorld, dir));
}
float3 TransformTangentNormalToWorld(float3 tsNormal, float3 wsNormal, float4 osTangent) 
{
    // Transform object-space tangent to world space
    float3 wsTangent = UnityObjectToWorldDir(osTangent.xyz);
    
    // Calculate world-space bitangent (considering handedness from tangent.w)
    float3 wsBitangent = cross(wsNormal, wsTangent) * osTangent.w;
    
    // Construct the TBN matrix
    float3x3 tbn = float3x3(wsTangent, wsBitangent, wsNormal);
    
    // Transform tangent-space normal to world space
    return mul(tbn, tsNormal);
}

float4 ComputeClipSpacePositionCom(float2 positionNDC, float deviceDepth)
{
    float4 positionCS = float4(positionNDC * 2.0 - 1.0, deviceDepth, 1.0);

#if UNITY_UV_STARTS_AT_TOP
    // Our world space, view space, screen space and NDC space are Y-up.
    // Our clip space is flipped upside-down due to poor legacy Unity design.
    // The flip is baked into the projection matrix, so we only have to flip
    // manually when going from CS to NDC and back.
    positionCS.y = -positionCS.y;
#endif

    return positionCS;
}
// Use case examples:
// (position = positionCS) => (clipSpaceTransform = use default)
// (position = positionVS) => (clipSpaceTransform = UNITY_MATRIX_P)
// (position = positionWS) => (clipSpaceTransform = UNITY_MATRIX_VP)
/*float4 ComputeClipSpacePosition(float3 position, float4x4 clipSpaceTransform = k_identity4x4)
{
    return mul(clipSpaceTransform, float4(position, 1.0));
}*/
float3 ComputeWorldSpacePositionCom(float2 positionNDC, float deviceDepth, float4x4 invViewProjMatrix)
{
    float4 positionCS  = ComputeClipSpacePosition(positionNDC, deviceDepth);
    float4 hpositionWS = mul(invViewProjMatrix, positionCS);
    return hpositionWS.xyz / hpositionWS.w;
}


float3 ComputeWSPosFromDepth(float depthLinear, float3 cameraDir, float3 viewDirection)
{
	//Example input
    /*float3 viewDirection = normalize(_WorldSpaceCameraPos - i.posWS.xyz);
    float3 cameraDir = GetCameraDirection();*/
    float3 divideDot = viewDirection / dot(viewDirection, cameraDir);
    float3 surfacePosWS = (depthLinear * divideDot) + _WorldSpaceCameraPos;
    return surfacePosWS;
}
float4 LinearEyeDepth(float4 rawDepth, float4 _ZBufferParams)
{
	float4 res;
	res.x = LinearEyeDepth(rawDepth.x, _ZBufferParams);
	res.y = LinearEyeDepth(rawDepth.y, _ZBufferParams);
	res.z = LinearEyeDepth(rawDepth.z, _ZBufferParams);
	res.w = LinearEyeDepth(rawDepth.w, _ZBufferParams);
	return res;
}



float3 NormalReconstructZ(float2 In)
{
	float reconstructZ = sqrt(1.0 - saturate(dot(In.xy, In.xy)));
    float3 normalVector = float3(In.x, In.y, reconstructZ);
    float3 Out = normalize(normalVector);
    return Out;
}

float3 NormalStrength(float3 In, float Strength)
{
    float3 Out = float3(In.rg * Strength, lerp(1, In.b, saturate(Strength)));
    return Out;
}

float3 ReconstructViewPos(float2 positionViewXY, float rawDepth, float4x4 projMatrix)
{
    // Extract near and far plane values from the projection matrix
    float near = projMatrix[3][2] / (projMatrix[2][2] - 1.0);
    float far  = projMatrix[3][2] / (projMatrix[2][2] + 1.0);

    // Convert raw depth (0 to 1) back to view-space Z
    float viewZ = near * far / (far + rawDepth * (near - far));

    // Full view-space position
    return float3(positionViewXY * viewZ, viewZ);
}

float FresnelEffect(float3 Normal, float3 ViewDir, float Power)
{
    return pow((1.0 - saturate(dot(normalize(Normal), normalize(ViewDir)))), Power);
}

float3 GetObjectScale()
{
	float3 scale = float3(length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
                             length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)),
                             length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z)));

	return scale;
}
float3 GetCameraDirection()
{
	#pragma warning (disable : 3206) // Disable warning "implicit truncation of vector type", everything as intended and works well (Unity use same code)
	return -1 * mul(UNITY_MATRIX_M, transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V)) [2].xyz);
	#pragma warning (default : 3206)
}

float PointInBounds(float3 minBounds, float3 maxBounds, float3 _point)
{
	    float Out = 
        _point.x >= minBounds.x && _point.x <= maxBounds.x &&
        _point.y >= minBounds.y && _point.y <= maxBounds.y &&
        _point.z >= minBounds.z && _point.z <= maxBounds.z;
        return Out;
}
float Equal(float3 a, float3 b)
{
	if (a.x == b.x || a.y == b.y || a.z == b.z)
	{
		return 1;
	}
	return 0;
}

float Remap_float(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}
float2 Remap_float2(float2 In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}
float3 Remap_float3(float3 In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}
float4 Remap_float4(float4 In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

// This interpolation have continuous second order derivative, which can help in gradient discontinuity (in example, when we calculate normal on smoothStepped data)
float FifthOrderInterpolate(float x)
{
	float t = (6.0 * pow(x, 5.0)) - (15.0 * pow(x, 4.0)) + (10.0 * pow(x, 3.0));
	return t;
}
float2 FifthOrderInterpolate(float2 x)
{
	float2 t = (6.0 * pow(x, 5.0)) - (15.0 * pow(x, 4.0)) + (10.0 * pow(x, 3.0));
	return t;
}
float3 FifthOrderInterpolate(float3 x)
{
	float3 t = (6.0 * pow(x, 5.0)) - (15.0 * pow(x, 4.0)) + (10.0 * pow(x, 3.0));
	return t;
}

float FifthOrderInterpolate(float startRange, float endRange, float x) 
{
    float remappedX = Remap_float(x, float2(startRange, endRange), float2(0,1));
    float t = FifthOrderInterpolate(remappedX);
    return Remap_float(x, float2(0,1), float2(startRange, endRange));
}
/*float FifthOrderInterpolate(float startRange, float endRange, float x) 
{
    float t = (6.0 * pow(x, 5.0)) - (15.0 * pow(x, 4.0)) + (10.0 * pow(x, 3.0));
    return (t * (endRange - startRange)) + startRange;
}*/




// Remap smooth from linear to exponential
float AdjustSmoothness(float smoothnessLinear)
{
	return exp2((smoothnessLinear * 10) + 1);
}



#endif
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LODFade;
	real4 unity_WorldTransformParams;

	float4 unity_ProbesOcclusion;

	float4 unity_SpecCube0_HDR;

	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;

	// Plain ambient light contain in the L0 coefficient of spherical harmonic. You can get it with half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w);
	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	float4 unity_ProbeVolumeParams;

	real4 unity_LightData;
	real4 unity_LightIndices[2];
	
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;

	float4 unity_RenderingLayer; // X asuint rendering layer
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_worldToProjection;
float4x4 UNITY_MATRIX_P;
float4x4 unity_MatrixV;
float4x4 unity_WorldToCamera; // unity_MatrixV in postProc are identity, but it will be always camera transfrom matrix
float4x4 UNITY_MATRIX_I_V;
float4x4 unity_CameraToWorld;
float4x4 UNITY_MATRIX_I_P;
float4x4 unity_CameraInvProjection;
float4x4 UNITY_MATRIX_I_VP;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;
float3 _WorldSpaceCameraPos;
float4 _ScreenParams; //x is the width of the camera’s target texture in pixels, y is the height of the camera’s target texture in pixels, z is 1.0 + 1.0/width and w is 1.0 + 1.0/height.
float4 _Time;
float4 _ProjectionParams;
float4 unity_OrthoParams;
float4 _ZBufferParams;



#endif
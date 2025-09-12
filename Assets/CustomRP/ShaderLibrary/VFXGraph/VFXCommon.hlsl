
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"

#include "Assets/CustomRP/ShaderLibrary/UnityInput.hlsl"

#ifdef HAVE_VFX_MODIFICATION
#include "Packages/com.unity.visualeffectgraph/Shaders/VFXMatricesOverride.hlsl"
#endif




// Stereo-related bits
#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)

    #define SLICE_ARRAY_INDEX   unity_StereoEyeIndex

    #define TEXTURE2D_X(textureName)                                        TEXTURE2D_ARRAY(textureName)
    #define TEXTURE2D_X_PARAM(textureName, samplerName)                     TEXTURE2D_ARRAY_PARAM(textureName, samplerName)
    #define TEXTURE2D_X_ARGS(textureName, samplerName)                      TEXTURE2D_ARRAY_ARGS(textureName, samplerName)
    #define TEXTURE2D_X_HALF(textureName)                                   TEXTURE2D_ARRAY_HALF(textureName)
    #define TEXTURE2D_X_FLOAT(textureName)                                  TEXTURE2D_ARRAY_FLOAT(textureName)

    #define LOAD_TEXTURE2D_X(textureName, unCoord2)                         LOAD_TEXTURE2D_ARRAY(textureName, unCoord2, SLICE_ARRAY_INDEX)
    #define LOAD_TEXTURE2D_X_LOD(textureName, unCoord2, lod)                LOAD_TEXTURE2D_ARRAY_LOD(textureName, unCoord2, SLICE_ARRAY_INDEX, lod)
    #define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2)            SAMPLE_TEXTURE2D_ARRAY(textureName, samplerName, coord2, SLICE_ARRAY_INDEX)
    #define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod)   SAMPLE_TEXTURE2D_ARRAY_LOD(textureName, samplerName, coord2, SLICE_ARRAY_INDEX, lod)
    #define GATHER_TEXTURE2D_X(textureName, samplerName, coord2)            GATHER_TEXTURE2D_ARRAY(textureName, samplerName, coord2, SLICE_ARRAY_INDEX)
    #define GATHER_RED_TEXTURE2D_X(textureName, samplerName, coord2)        GATHER_RED_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))
    #define GATHER_GREEN_TEXTURE2D_X(textureName, samplerName, coord2)      GATHER_GREEN_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))
    #define GATHER_BLUE_TEXTURE2D_X(textureName, samplerName, coord2)       GATHER_BLUE_TEXTURE2D(textureName, samplerName, float3(coord2, SLICE_ARRAY_INDEX))

#else
    #define SLICE_ARRAY_INDEX       0

    #define TEXTURE2D_X(textureName)                                        TEXTURE2D(textureName)
    #define TEXTURE2D_X_PARAM(textureName, samplerName)                     TEXTURE2D_PARAM(textureName, samplerName)
    #define TEXTURE2D_X_ARGS(textureName, samplerName)                      TEXTURE2D_ARGS(textureName, samplerName)
    #define TEXTURE2D_X_HALF(textureName)                                   TEXTURE2D_HALF(textureName)
    #define TEXTURE2D_X_FLOAT(textureName)                                  TEXTURE2D_FLOAT(textureName)

    #define LOAD_TEXTURE2D_X(textureName, unCoord2)                         LOAD_TEXTURE2D(textureName, unCoord2)
    #define LOAD_TEXTURE2D_X_LOD(textureName, unCoord2, lod)                LOAD_TEXTURE2D_LOD(textureName, unCoord2, lod)
    #define SAMPLE_TEXTURE2D_X(textureName, samplerName, coord2)            SAMPLE_TEXTURE2D(textureName, samplerName, coord2)
    #define SAMPLE_TEXTURE2D_X_LOD(textureName, samplerName, coord2, lod)   SAMPLE_TEXTURE2D_LOD(textureName, samplerName, coord2, lod)
    #define GATHER_TEXTURE2D_X(textureName, samplerName, coord2)            GATHER_TEXTURE2D(textureName, samplerName, coord2)
    #define GATHER_RED_TEXTURE2D_X(textureName, samplerName, coord2)        GATHER_RED_TEXTURE2D(textureName, samplerName, coord2)
    #define GATHER_GREEN_TEXTURE2D_X(textureName, samplerName, coord2)      GATHER_GREEN_TEXTURE2D(textureName, samplerName, coord2)
    #define GATHER_BLUE_TEXTURE2D_X(textureName, samplerName, coord2)       GATHER_BLUE_TEXTURE2D(textureName, samplerName, coord2)
#endif

//#include "VFXDefines.hlsl"



// Wrappers to hook up vfx
/*float4x4 GetObjectToWorldMatrix()
{
    return UNITY_MATRIX_M;
}
float4x4 GetWorldToObjectMatrix()
{
    return UNITY_MATRIX_I_M;
}
float4x4 GetPrevObjectToWorldMatrix()
{
    return unity_prev_MatrixM;
}
float4x4 GetWorldToViewMatrix()
{
    return unity_MatrixV;
}

float3 TransformObjectToWorld(float3 pos)
{
    return mul(UNITY_MATRIX_M, float4(pos, 1)).xyz;
}

float3 TransformWorldToView(float3 worldPos)
{
    return mul(unity_MatrixV, float4(worldPos,1)).xyz;
}
float4 TransformWorldToHClip(float3 positionWS) 
{
  return mul(unity_MatrixVP, float4(positionWS, 1.0));
}*/
/*float ComputeFogFactor(float3 pos)
{
    return 0;
}
float ComputeFogIntensity(float i)
{
    return 1;
}*/


float3 _LightDirection;

#ifdef VFX_VARYING_PS_INPUTS
	void VFXTransformPSInputs(inout VFX_VARYING_PS_INPUTS input) {}

	float4 VFXApplyPreExposure(float4 color, float exposureWeight)
	{
	    return color;
	}

	float4 VFXApplyPreExposure(float4 color, VFX_VARYING_PS_INPUTS input)
	{
	    return color;
	}
#endif

float2 VFXGetNormalizedScreenSpaceUV(float4 clipPos)
{
    //TODO:仮。
    return clipPos.xy;
}

void VFXEncodeMotionVector(float2 velocity, out float4 outBuffer)
{
    outBuffer = float4(velocity.xy, 0, 0);
}

float4x4 VFXGetObjectToWorldMatrix()
{
    // NOTE: If using the new generation path, explicitly call the object matrix (since the particle matrix is now baked into UNITY_MATRIX_M)
    #if defined(HAVE_VFX_MODIFICATION) && !defined(SHADER_STAGE_COMPUTE)
        return GetSGVFXUnityObjectToWorld();
    #else
        return GetObjectToWorldMatrix();
    #endif
}

float4x4 VFXGetWorldToObjectMatrix()
{
    // NOTE: If using the new generation path, explicitly call the object matrix (since the particle matrix is now baked into UNITY_MATRIX_I_M)
    #if defined(HAVE_VFX_MODIFICATION) && !defined(SHADER_STAGE_COMPUTE)
        return GetSGVFXUnityWorldToObject();
    #else
        return GetWorldToObjectMatrix();
    #endif
}

float4 VFXTransformPositionWorldToClip(float3 posWS)
{
    return TransformWorldToHClip(posWS);
}

float4 VFXTransformPositionWorldToNonJitteredClip(float3 posWS)
{
    //TODO:仮。
    return mul(unity_MatrixVP, float4(posWS, 1.0f));
}

float4 VFXTransformPositionWorldToPreviousClip(float3 posWS)
{
    //TODO:仮。
    return mul(unity_MatrixVP, float4(posWS, 1.0f));
}

float4 VFXTransformPositionObjectToClip(float3 posOS)
{
    float3 posWS = mul(VFXGetObjectToWorldMatrix(), float4(posOS,1)).xyz;
    return VFXTransformPositionWorldToClip(posWS);
}

float4 VFXTransformPositionObjectToNonJitteredClip(float3 posOS)
{
    float3 posWS = mul(VFXGetObjectToWorldMatrix(), float4(posOS,1)).xyz;
    return VFXTransformPositionWorldToNonJitteredClip(posWS);
}

float3 VFXTransformPreviousObjectToWorld(float3 posOS)
{
    return mul(GetPrevObjectToWorldMatrix(), float4(posOS, 1.0)).xyz;
}

float4 VFXTransformPositionObjectToPreviousClip(float3 posOS)
{
    float3 posWS = VFXTransformPreviousObjectToWorld(posOS);
    return VFXTransformPositionWorldToPreviousClip(posWS);
}

float3 VFXTransformPositionWorldToView(float3 posWS)
{
    return TransformWorldToView(posWS);
}

float3 VFXTransformPositionWorldToCameraRelative(float3 posWS)
{
	#if SHADEROPTIONS_CAMERA_RELATIVE_RENDERING
		#error VFX Camera Relative rendering isn't supported in URP.
	#endif
    return posWS;
}

//Compatibility functions for the common ShaderGraph integration
float4x4 ApplyCameraTranslationToMatrix(float4x4 modelMatrix)
{
    return modelMatrix;
}
float4x4 ApplyCameraTranslationToInverseMatrix(float4x4 inverseModelMatrix)
{
    return inverseModelMatrix;
}
//End of compatibility functions

float3x3 VFXGetWorldToViewRotMatrix()
{
    return (float3x3)GetWorldToViewMatrix();
}

float3 VFXGetViewWorldPosition()
{
    return _WorldSpaceCameraPos;
}

float4x4 VFXGetViewToWorldMatrix()
{
    return UNITY_MATRIX_I_V;
}

#ifdef USING_STEREO_MATRICES
	float3 GetWorldStereoOffset()
	{
	    return unity_StereoWorldSpaceCameraPos[0].xyz - unity_StereoWorldSpaceCameraPos[1].xyz;
	}
#endif

/*
void VFXApplyShadowBias(inout float4 posCS, inout float3 posWS, float3 normalWS)
{
    posWS = ApplyShadowBias(posWS, normalWS, _LightDirection);
    posCS = VFXTransformPositionWorldToClip(posWS);
}

void VFXApplyShadowBias(inout float4 posCS, inout float3 posWS)
{
    posWS = ApplyShadowBias(posWS, _LightDirection, _LightDirection);
    posCS = VFXTransformPositionWorldToClip(posWS);
}
*/

float4 VFXApplyAO(float4 color, float4 posCS)
{
	#if defined(_SCREEN_SPACE_OCCLUSION) && !defined(_SURFACE_TYPE_TRANSPARENT)
	    float2 normalizedScreenSpaceUV = (posCS.xy);
	    //AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
	    //color.rgb *= aoFactor.directAmbientOcclusion;
	#endif

    return color;
}

float4 VFXTransformFinalColor(float4 color, float4 posCS)
{
    return color;
}

float4 VFXApplyFog(float4 color,float4 posCS,float3 posWS)
{
   float4 fog = (float4)0;
   fog.rgb = unity_FogColor.rgb;

   float fogFactor = ComputeFogFactor(posCS.z * posCS.w);
   fog.a = ComputeFogIntensity(fogFactor);

	#if VFX_BLENDMODE_ALPHA || IS_OPAQUE_PARTICLE
	   color.rgb = lerp(fog.rgb, color.rgb, fog.a);
	#elif VFX_BLENDMODE_ADD
	   color.rgb *= fog.a;
	#elif VFX_BLENDMODE_PREMULTIPLY
	   color.rgb = lerp(fog.rgb * color.a, color.rgb, fog.a);
	#endif
   return color;
}

float3 VFXGetCameraWorldDirection()
{
    return unity_CameraToWorld._m02_m12_m22;
}







float4 VFXTransformFinalColor(float4 color)
{
    //color.rgb = (color.r + color.g + color.b) / 3; 
    return color;
}

TEXTURE2D_X_FLOAT(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);


#if defined(UNITY_SINGLE_PASS_STEREO)
    float2 TransformStereoScreenSpaceTex(float2 uv, float w)
    {
        // TODO: RVS support can be added here, if Universal decides to support it
        float4 scaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
        return uv.xy * scaleOffset.xy + scaleOffset.zw * w;
    }

    float2 UnityStereoTransformScreenSpaceTex(float2 uv)
    {
        return TransformStereoScreenSpaceTex(saturate(uv), 1.0);
    }
#else
    #define UnityStereoTransformScreenSpaceTex(uv) uv
#endif // defined(UNITY_SINGLE_PASS_STEREO)

float SampleSceneDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
}


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

float VFXLinearEyeDepth2(float depth)
{
    return LinearEyeDepth(depth, _ZBufferParams);
}
float VFXLinearEyeDepthOrthographic2(float depth)
{
    #if UNITY_REVERSED_Z
        return float(_ProjectionParams.z - (_ProjectionParams.z - _ProjectionParams.y) * depth);
    #else
        return float(_ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * depth);
    #endif
}


float VFXSampleDepth(float4 posSS)
{
    float2 screenUV = GetNormalizedScreenSpaceUV(posSS.xy);

    //screenUV = posSS.xy * _ScaledScreenParams.xy;
    screenUV = posSS.xy / _ScreenParams.xy;

    // In URP, the depth texture is optional and could be 4x4 white texture, Load isn't appropriate in that case.
    //float depth = LoadSceneDepth(screenUV * _ScreenParams.xy);
    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV);
    //depth = 0.002;
    //depth = lerp(0.002, 0.002, screenUV.y);
    //depth ;
    //depth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(depth) : LinearEyeDepth(depth, _ZBufferParams);
    return depth;
}




// Copy code with modifications from "com.unity.visualeffectgraph/shaders/VFXCommonOutput.hlsl"

//#include "Assets/CustomRP/ShaderLibrary/NormalCalculation.hlsl"

#if defined(VFX_VARYING_PS_INPUTS)

float4 VFXGetCustomParticleColor(VFX_VARYING_PS_INPUTS i)
{
    float4 color = 1.0f;
    #if VFX_NEEDS_COLOR_INTERPOLATOR
    #ifdef VFX_VARYING_COLOR
    color.rgb *= i.VFX_VARYING_COLOR;
    #endif
    #ifdef VFX_VARYING_ALPHA
    color.a *= i.VFX_VARYING_ALPHA;
    #endif
    #endif
    return color;
}
float VFXGetCustomSoftParticleFade(VFX_VARYING_PS_INPUTS i)
{
    float fade = 1.0f;
    #if USE_SOFT_PARTICLE && defined(VFX_VARYING_INVSOFTPARTICLEFADEDISTANCE)
        float sceneZ, selfZ;
        float sampledDepth = VFXSampleDepth(i.VFX_VARYING_POSCS);
        if(IsPerspectiveProjection())
        {
            sceneZ = VFXLinearEyeDepth2(sampledDepth);
            selfZ = i.VFX_VARYING_POSCS.w;
        }
        else
        {
            sceneZ = VFXLinearEyeDepthOrthographic2(sampledDepth);
            selfZ = VFXLinearEyeDepthOrthographic2(i.VFX_VARYING_POSCS.z);
        }
        fade = saturate(i.VFX_VARYING_INVSOFTPARTICLEFADEDISTANCE * (sceneZ - selfZ));
        fade = fade * fade * (3.0 - (2.0 * fade)); // Smoothsteping the fade
    #endif
    return fade;
}
float4 VFXApplyCustomSoftParticleFade(VFX_VARYING_PS_INPUTS i, float4 color)
{
    float fade = VFXGetCustomSoftParticleFade(i);
    #if VFX_BLENDMODE_PREMULTIPLY
        color *= fade;
    #else
        color.a *= fade;
    #endif
    return color;
}
float4 VFXGetCustomFragmentColor(VFX_VARYING_PS_INPUTS i)
{
    float4 color = VFXGetCustomParticleColor(i);
    color = VFXApplyCustomSoftParticleFade(i, color);
    return color;
}
// There is no else fallback function, so i will belive, that VFX_VARYING_PS_INPUTS is always true at this stage
#endif
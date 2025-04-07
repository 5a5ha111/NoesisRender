Shader "Hidden/Custom RP/Deferred Calculate" 
{
    Properties 
    {
        // Possible to add reflection buffer or include reflection somewhere in albedo
        _BaseRefl("ReflectionCubemap (One global reflection)", Cube) = "black" {}
    }
    
    SubShader 
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE

            #define _DEFFERED_LIGHTNING

            half4 _DeferredEnvParams;

            #include "../ShaderLibrary/Common.hlsl"
            #include "CameraDebuggerPasses.hlsl"


            #include "../ShaderLibrary/Surface.hlsl"

            #include "../ShaderLibrary/Shadows.hlsl"
            #include "../ShaderLibrary/Light.hlsl"
            #include "../ShaderLibrary/BRDF.hlsl"

            #include "../ShaderLibrary/GI.hlsl"
            #include "../ShaderLibrary/Lighting.hlsl"

            #include "../ShaderLibrary/CustomDither.hlsl"


            //TEXTURE2D(_SourceTexture);
            TEXTURE2D(_GBuffer0);
            TEXTURE2D(_GBuffer1);
            TEXTURE2D(_GBuffer2);
            TEXTURE2D(_GBuffer3);

            SAMPLER(sampler_CameraDepthTexture);
            float4x4 _vpMatrixInv;

            float4 DeferredFragment(Varyings input) : SV_TARGET
            {
                //half depth =LinearEyeDepth(SAMPLE_TEXTURE2D_LOD(_GDepth, sampler_linear_clamp, input.screenUV, 0).r);
                //float4 res = float4(SAMPLE_TEXTURE2D_LOD(_GDepth, sampler_linear_clamp, input.screenUV, 0).rgb, 1);
                float2 uv = input.screenUV;

                float4 packedGB0 = SAMPLE_TEXTURE2D_LOD(_GBuffer0, sampler_linear_clamp, uv, 0);
                float4 packedGB1 = SAMPLE_TEXTURE2D_LOD(_GBuffer1, sampler_linear_clamp, uv, 0);
                float4 packedGB2 = SAMPLE_TEXTURE2D_LOD(_GBuffer2, sampler_linear_clamp, uv, 0);
                float4 packedGB3 = SAMPLE_TEXTURE2D_LOD(_GBuffer3, sampler_linear_clamp, uv, 0);


                float3 base = packedGB0.rgb;
                float metallic = packedGB3.a;

                float smoothness = packedGB1.a;
                float3 normal = packedGB1.rgb;
                float occlusion = packedGB2.a;

                float3 emission = packedGB3.rgb;
                //float3 emission = 0;

                //float2 positionView = packedGB2.xy;
                //float fresnelStrenght = packedGB2.b;
                float fresnelStrenght = 0.4f;
                uint renderingLayerMask = asuint(packedGB2.a);
                
                float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;

                /*float3 position = TrasformViewToWorld(ReconstructViewPos(positionView, rawDepth, UNITY_MATRIX_P));*/
                float2 lightMapUV = float2(0,0);

                float d = rawDepth;
                float4 ndcPos = float4(uv*2-1, d, 1);
                float4 worldPos = mul(_vpMatrixInv, ndcPos);
                worldPos /= worldPos.w;
                //float3 position = worldPos.xyz;
                float3 position = packedGB2.xyz + _WorldSpaceCameraPos;
                worldPos.xyz = position;
                float4 positionSS = TransformWorldToHClip(position);
                Fragment fragment = GetFragment(positionSS);
                fragment.screenUV = uv;


                Surface surface = (Surface)0;
                surface.position = position;
                surface.color = base.rgb;
                surface.interpolatedNormal = normal;
                surface.normal = normal;
                surface.alpha = 0;
                surface.metallic = metallic;
                surface.occlusion = occlusion;
                surface.smoothness = smoothness;
                surface.fresnelStrength = fresnelStrenght;
                surface.dither = 0;
                surface.renderingLayerMask = renderingLayerMask;
                //surface.depth = rawDepth;
                surface.depth = -TransformWorldToView(worldPos.xyz).z;
                surface.viewDirection = normalize(_WorldSpaceCameraPos - worldPos.xyz);


                lightMapUV = float2(0,0);
                BRDF brdf = GetBRDF(surface);
                GI gi = GetGI(lightMapUV, surface, brdf);
                float3 color = GetAllLighting(fragment, surface, brdf, gi);
                color += emission;

                float4 res = float4(color, 1);
                //float4 res = float4(packedGB2.rgb, 1);
                //float4 res = float4(smoothness, 0, 0, 1);
                return res;
                //return SAMPLE_TEXTURE2D_LOD(_GBuffer1, sampler_linear_clamp, input.screenUV, 0);
            }
        ENDHLSL

        Pass 
        {
            Name "Deferred Calculate Light"

            //Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
                #pragma target 4.5

                //#pragma multi_compile_instancing

                //#pragma shader_feature _CLIPPING
                //#pragma shader_feature _RECEIVE_SHADOWS
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
                //#pragma shader_feature _PREMULTIPLY_ALPHA
                //#pragma shader_feature _REFLECTION_CUBEMAP
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
                //#pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma multi_compile _ _LIGHTS_PER_OBJECT

                // Other light shadows
                #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7


                #pragma vertex DefaultPassVertex
                #pragma fragment DeferredFragment

            ENDHLSL
        }
    }
}
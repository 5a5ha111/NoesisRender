Shader "Hidden/Custom RP/XeGTAOApply"
{
    Properties
    {
        
    }
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE

            #include "Assets/CustomRP/ShaderLibrary/Common.hlsl"

            #define XE_GTAO_OCCLUSION_TERM_SCALE                    (1.5f)      // for packing in UNORM (because raw, pre-denoised occlusion term can overshoot 1 but will later average out to 1)

            // Physically based Standard lighting model, and enable shadows on all light types
            struct Varyings 
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : VAR_SCREEN_UV;
            };


            Varyings DefaultPassVertex (uint vertexID : SV_VertexID) 
            {
                Varyings output;
                output.positionCS = float4
                (
                    vertexID <= 1 ? -1.0 : 3.0,
                    vertexID == 1 ? 3.0 : -1.0,
                    0.0, 1.0
                );
                output.screenUV = float2
                (
                    vertexID <= 1 ? 0.0 : 2.0,
                    vertexID == 1 ? 2.0 : 0.0
                );

                if (_ProjectionParams.x < 0.0) 
                {
                    output.screenUV.y = 1.0 - output.screenUV.y;
                }
                return output;
            }


        ENDHLSL

        Pass 
        {
            Name "Remap uint to float"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM


                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment RemapPassFragment

                #pragma shader_feature _ _HalfRes


                //TEXTURE2D(_SourceTexture);
                Texture2D<uint> _UintTexture;

                
                float3 Uint3ToNormalizedFloat3(uint3 value, uint maxValue)
                {
                    // Avoid division by zero
                    if (maxValue == 0) return float3(0, 0, 0);
                    
                    // Split into high and low parts (16-bit each)
                    uint3 high = value >> 16;
                    uint3 low = value & 0xFFFF;
                    
                    // Compute normalized values for both parts
                    float3 normalized = float3(high) * (65536.0 / float(maxValue))
                                      + float3(low) / float(maxValue);
                    
                    // Clamp to [0, 1] to handle precision edge cases
                    return saturate(normalized);
                }

                float3 RemapUintToFloat(uint3 inValue)
                {
                    uint maxValue = 0xFFFFFFFF;
                    return Uint3ToNormalizedFloat3(inValue, maxValue);
                }


                float4 RemapPassFragment (Varyings input) : SV_TARGET 
                {
                    //uint3 inValue = SAMPLE_TEXTURE2D_LOD(_UintTexture, sampler_linear_clamp, input.screenUV, 0);
                    #if defined(_HalfRes)
                        uint3 inValue = _UintTexture[(input.screenUV * (_ScreenParams.xy / 2))];
                    #else
                        uint3 inValue = _UintTexture[(input.screenUV * _ScreenParams.xy)];
                    #endif
                    //float3 res = RemapUintToFloat(inValue);
                    uint upperValue = 255;

                    half tempv = (half)inValue;
                    tempv *= (half)XE_GTAO_OCCLUSION_TERM_SCALE;
                    //float3 res = Uint3ToNormalizedFloat3(inValue, upperValue);
                    float3 res = ((half)tempv / (half)upperValue) + 0.5;
                    res = saturate(res);
                    //res = inValue;
                    return float4(res, 1);
                }
            ENDHLSL
        }

        Pass
        {
            Name "Get depth in DirectX NDC"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

                //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hls"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // the input structure (Attributes) and the output structure (Varyings)
                //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment GetDepthFragment


                //TEXTURE2D(_CameraDepthTexture);
                SAMPLER(sampler_CameraDepthTexture);


                float4 GetDepthFragment (Varyings input) : SV_TARGET 
                {
                    float bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, input.screenUV, 0);
                    //bufferDepth = Linear01Depth(bufferDepth, _ZBufferParams);
                    //bufferDepth = LinearEyeDepth(bufferDepth, _ZBufferParams);

                    float outValue = ((1 - bufferDepth));
                    //float outValue = bufferDepth;
                    //return outValue;
                    return float4(outValue, outValue, outValue, 1);
                    //return float4(input.screenUV.xy, 0, 1);
                }
            ENDHLSL
        }

        Pass
        {
            Name "Blit"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

                //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hls"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // the input structure (Attributes) and the output structure (Varyings)
                //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BlitFragment


                //TEXTURE2D(_CameraDepthTexture);
                //SAMPLER(sampler_CameraDepthTexture);

                TEXTURE2D(_SourceTexture);


                float4 BlitFragment (Varyings input) : SV_TARGET 
                {
                    float4 blitSource = SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_point_clamp, input.screenUV, 0);
                    return blitSource;
                }
            ENDHLSL
        }

        Pass
        {
            Name "DisplayDepth"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

                //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hls"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // the input structure (Attributes) and the output structure (Varyings)
                //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BlitFragment


                //TEXTURE2D(_CameraDepthTexture);
                //SAMPLER(sampler_CameraDepthTexture);

                TEXTURE2D(_SourceTexture);


                float4 BlitFragment (Varyings input) : SV_TARGET 
                {
                    float4 blitSource = SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_point_clamp, input.screenUV, 0);
                    float outVal = blitSource.r / 25.0;
                    return float4(outVal,0,0,1);
                }
            ENDHLSL
        }

        Pass
        {
            Name "DebugNormals"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

                //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hls"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // the input structure (Attributes) and the output structure (Varyings)
                //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BlitFragment


                //TEXTURE2D(_CameraDepthTexture);
                //SAMPLER(sampler_CameraDepthTexture);

                // R11G11B10_UNORM <-> float3
                float3 XeGTAO_R11G11B10_UNORM_to_FLOAT3( uint packedInput )
                {
                    float3 unpackedOutput;
                    unpackedOutput.x = (float)( ( packedInput       ) & 0x000007ff ) / 2047.0f;
                    unpackedOutput.y = (float)( ( packedInput >> 11 ) & 0x000007ff ) / 2047.0f;
                    unpackedOutput.z = (float)( ( packedInput >> 22 ) & 0x000003ff ) / 1023.0f;
                    return unpackedOutput;
                }

                Texture2D<uint> _UintTextureNormals;


                float4 BlitFragment (Varyings input) : SV_TARGET 
                {
                    uint inValue = _UintTextureNormals[(input.screenUV * _ScreenParams.xy)].x;
                    float3 normal = XeGTAO_R11G11B10_UNORM_to_FLOAT3(inValue);
                    //normal = normalize(normal * 2.0 - 1.0);
                    normal = normalize(normal);
                    return float4(normal,1);
                }
            ENDHLSL
        }

        Pass
        {
            Name "DebugNormalsBuffer"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

                //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hls"
                // The Blit.hlsl file provides the vertex shader (Vert),
                // the input structure (Attributes) and the output structure (Varyings)
                //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BlitFragment


                TEXTURE2D(_SourceTexture);
                //SAMPLER(sampler_CameraDepthTexture);


                float4 BlitFragment (Varyings input) : SV_TARGET 
                {
                    float3 normal = float3(0,0,0);
                    float3 normalWS = SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_point_clamp, input.screenUV, 0).xyz;
                    //normal = mul(unity_WorldToCamera, float4(normal,0)).xyz;
                    normal = mul( (float3x3)unity_WorldToCamera, normalWS );
                    if (Equal(normalWS, float3(0,0,0)))
                    {
                        normal = float3(0,0,-1);
                    }
                    normal.y *= -1;
                    normal.z *= -1;
                    normal = normal * 0.5 + 0.5;
                    //normal = normal * 2.0 - 1.0;
                    normal = normalize(normal);
                    return float4(normal,1);
                }
            ENDHLSL
        }

        Pass
        {
            Name "Bicubic Rescale"

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM


                #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

                #pragma target 4.5
                #pragma vertex DefaultPassVertex
                #pragma fragment BlitFragment


                TEXTURE2D(_SourceTexture);
                float4 _SourceTexture_TexelSize;
                //SAMPLER(sampler_CameraDepthTexture);


                float4 BlitFragment (Varyings input) : SV_TARGET 
                {
                    return SampleTexture2DBicubic(
                        TEXTURE2D_ARGS(_SourceTexture, sampler_linear_clamp), input.screenUV,
                        _SourceTexture_TexelSize.zwxy, 1.0, 0.0
                    );
                }
            ENDHLSL
        }

        
    }
    FallBack "Diffuse"
}

Shader "Custom/DecalReconstructedShaderUnity" 
{
    Properties 
    {
        _ColorTexture ("Color", 2D) = "white" {}
        [Toggle(_USE_ANGLE_FADE)] _USE_ANGLE_FADE("Use angle fade", Float) = 1
        _AngleFadeStart("AngleFadeStart (Less opaque)", Float) = 0.3
        _AngleFadeEnd("AngleFadeEnd (More opaque)", Float) = 0.5
        //_DepthTexture ("Depth", 2D) = "gray" {}
        [Enum(UnityEngine.Rendering.RenderQueue)]_Quoue("Queue", Float) = 3000
        [Queue]_QuoueOffset("QueueOffset", Float) = 1
    }

    SubShader 
    {
        Pass 
        {
            Tags 
            {
                "Queue" = "_Quoue + _QuoueOffset"
                "LightMode" = "DecallPass"
            }

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma target 4.5

            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature _DEFFERED_LIGHTNING
            #pragma shader_feature _USE_ANGLE_FADE

            #pragma multi_compile _ _TAP3 _TAP4 _IMPROVED _ACCURATE

            #include "Assets/CustomRP/ShaderLibrary/Common.hlsl"
            SAMPLER(sampler_CameraDepthTexture);
            #include "Assets/CustomRP/ShaderLibrary/NormalCalculation.hlsl"
            #include "Assets/CustomRP/ShaderLibrary/ProceduralShapes/UV.hlsl"

            sampler2D _ColorTexture;
            sampler2D _NormalTexture;
            float4 _EffectParams;

            float _AngleFadeStart;
            float _AngleFadeEnd;
            TEXTURE2D(_GBuffer1);


            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 posWS : TEXCOORD2;
            };


            v2f vert (appdata v) 
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.screenPos = float4(o.pos.xy / _CameraBufferSize.xy, o.pos.zw);
                o.posWS = float4(TransformObjectToWorld(v.vertex.xyz),1);
                return o;
            }

            float4 frag (v2f i) : SV_Target 
            {
                // Get screen-space coordinates
                float2 screenUV = i.pos.xy / _CameraBufferSize.xy;
                float2 screenNDC = screenUV * 2.0 - 1.0;

                float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV, 0).r;

                // Sample depth and normal maps
                float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float3 normal = UnpackNormal(tex2D(_NormalTexture, screenUV));

                // Reconstruct view space position using inverse projection
                float4 clipPos = float4(screenNDC, depth, 1.0);
                float4x4 unity_CameraInvProjection = Inverse(UNITY_MATRIX_P);
                //float4 viewPos = mul(unity_CameraInvProjection, clipPos);

                float3 viewDirection = normalize(_WorldSpaceCameraPos - i.posWS.xyz);
                float3 cameraDir = GetCameraDirection();
                float3 divideDot = viewDirection / dot(viewDirection, cameraDir);
                float3 surfacePosWS = (depth * divideDot) + _WorldSpaceCameraPos;
                float3 objectPos = TransformWorldToObject(surfacePosWS);


                // Bounds point for default cube mesh
                float3 scale = GetObjectScale();
                const float3 minBoundsPoint = float3(-0.5,-0.5,-0.5) * scale;
                const float3 maxBoundsPoint = float3(0.5,0.5,0.5) * scale;

                // Clip if surface point outside box mesh
                float inBounds = PointInBounds(minBoundsPoint, maxBoundsPoint, objectPos) - 0.5;
                clip(inBounds);

                // Calculate texture coordinates using view position
                float2 baseUV = objectPos.xz + float2(0.5,0.5);
                
                // Edge detection and sampling
                float2 edgeFactors = saturate(50.0 * (1.0 - baseUV));
                float2 clampedUV = saturate(baseUV);
                float4 texColors = tex2D(_ColorTexture, clampedUV);
                float3 color = texColors.rgb;

                float normalFadeFactor = texColors.a;

                #ifdef _USE_ANGLE_FADE
                    float3 normalWS = float3(0,1,0);
                    float3 normalOS = float3(0,1,0);
                    #ifdef _DEFFERED_LIGHTNING
                        normalWS = SAMPLE_TEXTURE2D_LOD(_GBuffer1, sampler_linear_clamp, screenUV, 0).xyz;
                    #else
                        #if defined(_ACCURATE)
                            float3 normalView = ReconstructNormalTap9(screenUV).xyz;
                            normalWS = mul((float3x3)UNITY_MATRIX_I_V, normalView);
                        #elif defined(_IMPROVED)
                            float3 normalView = ReconstructNormalTap5(screenUV);
                            normalWS = mul((float3x3)UNITY_MATRIX_I_V, normalView);
                        #elif defined(_TAP4)
                            float3 normalView =  ReconstructNormalTap4(screenUV);
                            normalWS = mul((float3x3)UNITY_MATRIX_I_V, normalView);
                        #elif defined(_TAP3)
                            float3 normalView =  ReconstructNormalTap3(screenUV);
                            normalWS = mul((float3x3)UNITY_MATRIX_I_V, normalView);
                        #else
                            normalWS = ReconstructNormalDerivative(surfacePosWS.xyz);
                        #endif
                    #endif

                    normalOS = TransformWorldToObject(normalWS);


                    const float3 upNormal = float4(0,1,0,1).xyz;
                    normalFadeFactor = dot(normalOS, upNormal) * 0.5 + 0.5;
                    normalFadeFactor = saturate(normalFadeFactor);

                    float fadeFactor = 1;

                    float negate = 1;
                    if (_AngleFadeStart > _AngleFadeEnd)
                    {
                        if (normalFadeFactor > _AngleFadeStart)
                        {
                            fadeFactor = 0;
                        }
                        else if (normalFadeFactor < _AngleFadeEnd)
                        {
                            fadeFactor = 1;
                        }
                        else if (normalFadeFactor < _AngleFadeStart & normalFadeFactor > _AngleFadeEnd)
                        {
                            fadeFactor = 1.0 - (normalFadeFactor - _AngleFadeEnd) / (_AngleFadeStart - _AngleFadeEnd);
                        }
                    }
                    else
                    {
                        if (normalFadeFactor > _AngleFadeEnd)
                        {
                            fadeFactor = 1;
                        }
                        else if (normalFadeFactor < _AngleFadeStart)
                        {
                            fadeFactor = 0;
                        }
                        else if (normalFadeFactor > _AngleFadeStart & normalFadeFactor < _AngleFadeEnd)
                        {
                            fadeFactor = (normalFadeFactor - _AngleFadeStart) / (_AngleFadeEnd - _AngleFadeStart);
                        }
                    }

                    normalFadeFactor = fadeFactor;



                #endif

                float edgeMask = UvMaskSquere(clampedUV, 20);

                return float4(color, normalFadeFactor * edgeMask);
                //return float4(frac(objectPos.xyz), 1);
                //return float4(clampedUV,0, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
Shader "Hidden/Custom RP/SSR" 
{
    
    SubShader 
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
            #include "../ShaderLibrary/Common.hlsl"
            #include "CameraRendererPasses.hlsl"
            SAMPLER(sampler_CameraDepthTexture);
            #include "Assets/CustomRP/ShaderLibrary/NormalCalculation.hlsl"
            #include "Assets/CustomRP/ShaderLibrary/ProceduralShapes/UV.hlsl"
            #include "../ShaderLibrary/CustomDither.hlsl"


            Texture2D _GBuffer0; // spec color
            Texture2D _GBuffer1; // normal + smoothness
            float4 _SourceTexture_TexelSize;
            float4x4 _InverseView;
            float4x4 _ViewProjectionMatrix;

            struct v2f
            {
                float2 screenUV : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 rayDir : TEXCOORD1;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                float far = _ProjectionParams.z;
                float x = (vertexID != 1) ? -1 : 3;
                float y = (vertexID == 2) ? -3 : 1;
                float3 vPos = float3(x, y, 1.0);

                float3 rayPers = mul(unity_CameraInvProjection, vPos.xyzz * far).xyz;

                v2f o;
                o.vertex = float4(vPos.x, -vPos.y, 1, 1);
                o.screenUV = (vPos.xy + 1) / 2;
                o.rayDir = rayPers;
                return o;
            }


            float3 ComputeViewspacePosition(float3 ray, float2 uv)
            {
                float near = _ProjectionParams.y;
                float far = _ProjectionParams.z;

                float z = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;

                #if !defined(EXCLUDE_FAR_PLANE)
                    float mask = 1;
                #elif defined(UNITY_REVERSED_Z)
                    float mask z > 0;
                #else 
                    mask = z < 0;
                #endif

                float3 vposPers = ray * Linear01Depth(z, _ZBufferParams);
                return vposPers * mask;
            }
            float3 ScreenToWorldPos(float3 ray, float2 uv)
            {
                return mul((float3x3)_InverseView, ComputeViewspacePosition(ray, uv)) + _WorldSpaceCameraPos;
            }
            float2 WorldToScreenUv(float3 worldPos)
            {
                float4 projectedCoords = mul(_ViewProjectionMatrix, float4(worldPos, 1));
                float2 uv = (projectedCoords.xy / projectedCoords.w) * 0.5 + 0.5;
                return uv;
            }

            float Vingnette(float2 uv)
            {
                float2 k = abs(uv - 0.5) * 1;
                k.x *= _SourceTexture_TexelSize.y * _SourceTexture_TexelSize.x;
                return pow(saturate(1 - dot(k, k)), 1);
            }
            /*float3 hash33(float3 f3)
            {
                f3 = frac(f3 * float3(0.1031, 0.1030, 0.973));
                f3 += dot(f3, f3.yzx + 33.33);
                return frac(f3.xxy + f3.yxx) * f3.zyx;
            }*/


            float4 InvertColor (v2f input) : SV_TARGET 
            {
                float _StepSize = 0.06; // 0.06
                float _MaxSteps = 20; // 50 Main performance impact
                float _MaxDistance = 100; // 100
                uint _BinarySearchSteps = 5; // 5
                float _Thickness = _StepSize * 3;  // 0.1


                //return float4(input.screenUV, 0, 1);
                float3 color = SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, input.screenUV, 0).xyz;
                float4 gbuffer1Pack = SAMPLE_TEXTURE2D_LOD(_GBuffer1, sampler_linear_clamp, input.screenUV, 0);
                float3 normalWS = gbuffer1Pack.xyz;
                float smoothness = gbuffer1Pack.w;
                //smoothness = 1; // Debug


                float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, input.screenUV, 0).r;
                // Sample depth and normal maps
                float depth = LinearEyeDepth(rawDepth, _ZBufferParams);


                float3 worldPos = mul((float3x3)_InverseView, ComputeViewspacePosition(input.rayDir, input.screenUV)) + _WorldSpaceCameraPos;

                float3 rayDir = normalize(reflect(normalize(worldPos - _WorldSpaceCameraPos), normalWS));
                // Jitter
                rayDir += (hash(worldPos.xyz)) * (1 - smoothness);
                // worldPos = frac(worldPos); //Debug

                float distTravelled = 0;
                float prevDistance = 0;

                //color = 0;
                float2 uvCoord = input.screenUV;
                float visibility = 1;
                depth = _Thickness;
                int hit = 0;

                [loop]
                for(int i = 0; i < _MaxSteps; i++ )
                {
                    prevDistance = distTravelled;
                    distTravelled += _StepSize;

                    float3 rayPos = worldPos + rayDir * distTravelled;

                    float2 screenUV = WorldToScreenUv(rayPos);
                    [branch]
                    if (screenUV.x >= 1 || screenUV.x < 0 || screenUV.y >= 1 || screenUV.y < 0) 
                    {
                        break;
                    }

                    float3 projectedPos = ScreenToWorldPos(input.rayDir, screenUV);
                    float projectedPosDist = distance(projectedPos, _WorldSpaceCameraPos);
                    float rayPosDist = distance(rayPos, _WorldSpaceCameraPos);

                    depth = rayPosDist - projectedPosDist;
                    [branch]
                    if (depth > 0 && depth < _Thickness)
                    {
                        //uvCoord = WorldToScreenUv(rayPos);
                        //hit = 1;
                        [unroll]
                        for(uint j = 0; j < _BinarySearchSteps; j++)
                        {
                            float midPointDist = (distTravelled + prevDistance) * 0.5;
                            rayPos = worldPos + rayDir * midPointDist;
                            projectedPos = ScreenToWorldPos(input.rayDir, WorldToScreenUv(rayPos));
                            float projToCamDist = distance(projectedPos, _WorldSpaceCameraPos);
                            float rayPosToCamPos = distance(rayPos, _WorldSpaceCameraPos);
                            if (projToCamDist <= rayPosToCamPos)
                            {
                                distTravelled = midPointDist;
                                uvCoord = WorldToScreenUv(rayPos);
                            }
                            else
                            {
                                prevDistance = midPointDist;
                            }
                        }
                        break;
                    }
                } 

                visibility *= UvMaskRounded(input.screenUV, 10);
                visibility *= UvMaskSquere(input.screenUV, 8);
                visibility *= saturate(dot(rayDir, normalize(worldPos - _WorldSpaceCameraPos)));
                visibility *= (1 - saturate(length(ScreenToWorldPos(input.rayDir, uvCoord) - worldPos) / _MaxDistance)); // Fade by maxdist
                visibility *= smoothness;
                visibility *= (uvCoord.x < 0 || uvCoord.x > 1 ? 0 : 1) * (uvCoord.y < 0 || uvCoord.y > 1 ? 0 : 1); 
                visibility = saturate(visibility);

                //float3 res = float3(WorldToScreenUv(worldPos), 0);
                float3 reflectRes = SAMPLE_TEXTURE2D_LOD(_SourceTexture, sampler_linear_clamp, uvCoord, 0).xyz;
                float3 res = lerp(color, saturate(reflectRes), visibility);

                float2 debugUv = lerp(input.screenUV, uvCoord, visibility);

                return float4(res, 1);
            }


        ENDHLSL

        Pass 
        {
            Name "SSR Pass"

            //Blend SrcAlpha OneMinusSrcAlpha

            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
                #pragma target 4.5
                //#pragma vertex DefaultPassVertex
                #pragma vertex vert
                #pragma fragment InvertColor
            ENDHLSL
        }
    }
}
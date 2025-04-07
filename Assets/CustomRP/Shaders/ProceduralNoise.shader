Shader "Custom RP/ProceduralNoise" 
{
    Properties
    {
        _Albedo("Albedo", Color) = (0.5, 0.5, 0.5, 1.0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
    }
    SubShader
    {

        HLSLINCLUDE
            #include "../ShaderLibrary/Common.hlsl"
        ENDHLSL

        Pass
        {
            Tags 
            {
                "LightMode" = "CustomLit"
            }


            HLSLPROGRAM

                #pragma shader_feature _RECEIVE_SHADOWS
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
                #pragma shader_feature _REFLECTION_CUBEMAP
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                #pragma multi_compile _ _LIGHTS_PER_OBJECT

                // Other light shadows
                #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7


                #include "../ShaderLibrary/CustomDither.hlsl"
                #include "../ShaderLibrary/ProceduralShapes/Helpers.hlsl"
                #include "../ShaderLibrary/Surface.hlsl"
                #include "../ShaderLibrary/Shadows.hlsl"
                #include "../ShaderLibrary/Light.hlsl"
                #include "../ShaderLibrary/BRDF.hlsl"
                #include "../ShaderLibrary/GI.hlsl"
                #include "../ShaderLibrary/Lighting.hlsl"

                #pragma vertex vert
                #pragma fragment frag

                float4 _Albedo;
                float _Smoothness;

                struct appdata
                {
                    float4 vertex : POSITION;
                    float4 normalOS : NORMAL;
                    float4 tangent : TANGENT;
                    float4 baseUV : TEXCOORD0;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float4 posWS : POS_WS;
                    float4 normalWS : NORMAL;
                    float4 baseUV : VAR_BASE_UV;
                    float4 tangent : TANGENT;
                };
        
                v2f vert (appdata v)
                {
                    v2f o;
                    float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
                    o.posWS = float4(positionWS, 1);
                    o.vertex = TransformWorldToHClip(positionWS.xyz);
                    o.normalWS = float4(TransformObjectToWorldNormal(v.normalOS.xyz), 1);
                    o.baseUV = v.baseUV;
                    o.tangent = v.tangent;
                    return o;
                }
                
                float4 frag (v2f i) : SV_Target
                {
                    float2 uv = i.baseUV.xy;
                    //uv = Mirror(uv, 1, 1) * 10;
                    uv *= 50;
                    float3 posWS = i.posWS.xyz;
                    //float perlin = PerlinNoiseImpruved(uv) + 0.5;
                    //float perlin = PerlinNoise(uv) + 0.5;
                    float scale = 8;
                    float e = 0.1;

                    float perlin = PerlinNoise3D(posWS * scale);
                    float fX = PerlinNoise3D((posWS + float3(e, 0, 0)) * scale);
                    float fY = PerlinNoise3D((posWS + float3(0, e, 0)) * scale);
                    float fZ = PerlinNoise3D((posWS + float3(0, 0, e)) * scale);


                    float3 dF = abs(float3((fX - perlin) / e, (fY - perlin) / e, (fZ - perlin) / e));
                    dF = pow(dF, 1.5);
                    dF = 1 - dF;
                    dF *= 0.02;
                    dF = TransformTangentNormalToWorld(dF, i.normalWS.xyz, i.tangent);
                    float3 N = normalize(i.normalWS.xyz - dF);

                    float colorT = (dot(dF, float3(1,1,1)) + 1) / 2;

                    Surface surfaceWS = (Surface)0;
                    surfaceWS.interpolatedNormal = N;
                    surfaceWS.normal = N;
                    surfaceWS.position = posWS;
                    surfaceWS.color = _Albedo.rgb;
                    surfaceWS.viewDirection = normalize(_WorldSpaceCameraPos - posWS);
                    surfaceWS.depth = -TransformWorldToView(posWS).z;
                    surfaceWS.renderingLayerMask = asuint(unity_RenderingLayer.x);
                    surfaceWS.fresnelStrength = 0.2;
                    surfaceWS.smoothness = _Smoothness;
                    surfaceWS.occlusion = 1;

                    BRDF brdf = GetBRDF(surfaceWS);

                    float2 lightMapUV = float2(0,0);
                    Fragment fragment = GetFragment(i.vertex);
                    GI gi = GetGI(lightMapUV, surfaceWS, brdf);
                    float3 color = GetLighting(fragment, surfaceWS, brdf, gi);

                    //float3 color = float3(1,1,1);
                    /*for (int i = 0; i < GetDirectionalLightCount(); i++) 
                    {
                        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
                        color *= light.attenuation;
                        color = (dot(N, light.direction) + 1) / 2;
                        color = pow(color, 2);
                        //color += GetLighting(surfaceWS, brdf, light);
                        {
                            //color = light.attenuation;
                            //color += SpecularBlinn(surfaceWS.viewDirection, -light.direction, surfaceWS.normal, surfaceWS.smoothness) * light.color;
                            //color *= DiffuseLambert(surfaceWS.normal, -light.direction);
                            //color += light.attenuation;
                        }
                    }*/

                    //float4 outColor = float4(normalize(uv), 0, 1);
                    //float4 outColor = float4(perlin, perlin, perlin, 1);
                    float4 outColor = float4(color, 1); 
                    return outColor;
                }
            ENDHLSL
        }

        Pass
        {
            Tags 
            {
                "LightMode" = "CustomGBuffer"
            }
            Name "GBuffer pass"

            HLSLPROGRAM

                #pragma target 4.5

                #pragma multi_compile_instancing

                #pragma shader_feature _CLIPPING
                //#pragma shader_feature _RECEIVE_SHADOWS
                //#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
                //#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
                #pragma shader_feature _PREMULTIPLY_ALPHA
                //#pragma shader_feature _REFLECTION_CUBEMAP
                //#pragma multi_compile _ LIGHTMAP_ON
                //#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
                #pragma multi_compile _ LOD_FADE_CROSSFADE
                //#pragma multi_compile _ _LIGHTS_PER_OBJECT

                // Other light shadows
                //#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7


                #include "../ShaderLibrary/CustomDither.hlsl"
                #include "../ShaderLibrary/ProceduralShapes/Helpers.hlsl"
                #include "../ShaderLibrary/Surface.hlsl"
                #include "../ShaderLibrary/Shadows.hlsl"
                #include "../ShaderLibrary/Light.hlsl"
                #include "../ShaderLibrary/BRDF.hlsl"
                #include "../ShaderLibrary/GI.hlsl"
                #include "../ShaderLibrary/Lighting.hlsl"

                #pragma vertex vert
                #pragma fragment GBufferFragment

                float4 _Albedo;
                float _Smoothness;

                struct appdata
                {
                    float4 vertex : POSITION;
                    float4 normalOS : NORMAL;
                    float4 tangent : TANGENT;
                    float4 baseUV : TEXCOORD0;
                };

                struct v2f
                {
                    float4 vertex : SV_POSITION;
                    float4 posWS : POS_WS;
                    float4 normalWS : NORMAL;
                    float4 baseUV : VAR_BASE_UV;
                    float4 tangent : TANGENT;
                };
        
                v2f vert (appdata v)
                {
                    v2f o;
                    float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
                    o.posWS = float4(positionWS, 1);
                    o.vertex = TransformWorldToHClip(positionWS.xyz);
                    o.normalWS = float4(TransformObjectToWorldNormal(v.normalOS.xyz), 1);
                    o.baseUV = v.baseUV;
                    o.tangent = v.tangent;
                    return o;
                }
                
                void GBufferFragment(v2f i, out float4 gBuffer0 : SV_Target0, out float4 gBuffer1 : SV_Target1, out float4 gBuffer2 : SV_Target2, out float4 gBuffer3 : SV_Target3)
                {
                    float2 uv = i.baseUV.xy;
                    //uv = Mirror(uv, 1, 1) * 10;
                    uv *= 50;
                    float3 posWS = i.posWS.xyz;
                    //float perlin = PerlinNoiseImpruved(uv) + 0.5;
                    //float perlin = PerlinNoise(uv) + 0.5;
                    float scale = 8;
                    float e = 0.1;

                    float perlin = PerlinNoise3D(posWS * scale);
                    float fX = PerlinNoise3D((posWS + float3(e, 0, 0)) * scale);
                    float fY = PerlinNoise3D((posWS + float3(0, e, 0)) * scale);
                    float fZ = PerlinNoise3D((posWS + float3(0, 0, e)) * scale);


                    float3 dF = abs(float3((fX - perlin) / e, (fY - perlin) / e, (fZ - perlin) / e));
                    //dF = pow(dF, 1.5);
                    dF = 1 - dF;
                    dF *= 0.02;
                    dF = TransformTangentNormalToWorld(dF, i.normalWS.xyz, i.tangent);
                    float3 N = normalize(i.normalWS.xyz - dF);
                    //N = i.normalWS.xyz;

                    float colorT = (dot(dF, float3(1,1,1)) + 1) / 2;

                    /*Surface surfaceWS = (Surface)0;
                    surfaceWS.interpolatedNormal = N;
                    surfaceWS.normal = N;
                    surfaceWS.position = posWS;
                    surfaceWS.color = _Albedo.rgb;
                    surfaceWS.viewDirection = normalize(_WorldSpaceCameraPos - posWS);
                    surfaceWS.depth = -TransformWorldToView(posWS).z;
                    surfaceWS.renderingLayerMask = asuint(unity_RenderingLayer.x);
                    surfaceWS.fresnelStrength = 0.2;
                    surfaceWS.smoothness = _Smoothness;
                    surfaceWS.occlusion = 1;

                    BRDF brdf = GetBRDF(surfaceWS);

                    float2 lightMapUV = float2(0,0);
                    Fragment fragment = GetFragment(i.vertex);
                    GI gi = GetGI(lightMapUV, surfaceWS, brdf);
                    float3 color = GetLighting(fragment, surfaceWS, brdf, gi);*/

                    //float3 color = float3(1,1,1);
                    /*for (int i = 0; i < GetDirectionalLightCount(); i++) 
                    {
                        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
                        color *= light.attenuation;
                        color = (dot(N, light.direction) + 1) / 2;
                        color = pow(color, 2);
                        //color += GetLighting(surfaceWS, brdf, light);
                        {
                            //color = light.attenuation;
                            //color += SpecularBlinn(surfaceWS.viewDirection, -light.direction, surfaceWS.normal, surfaceWS.smoothness) * light.color;
                            //color *= DiffuseLambert(surfaceWS.normal, -light.direction);
                            //color += light.attenuation;
                        }
                    }*/

                    //float4 outColor = float4(normalize(uv), 0, 1);
                    //float4 outColor = float4(perlin, perlin, perlin, 1);
                    // float4 outColor = float4(color, 1); 
                    //return outColor;


                    float4 packedGB0 = float4(_Albedo.rgb, 0);
                    float4 packedGB1 = float4(N, _Smoothness);
                    float4 packedGB2 = float4(posWS, 1);
                    float4 packedGB3 = float4(0,0,0,0);


                    gBuffer0 = packedGB0;
                    gBuffer1 = packedGB1;
                    gBuffer2 = packedGB2;
                    gBuffer3 = packedGB3;
                }
            ENDHLSL
        }
    }
}
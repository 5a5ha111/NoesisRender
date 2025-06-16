/**************************************************************************************
 *                                                                                    *
 *  Copyright (C) 2025 Aleksandra Arakcheeva                                          *
 *                                                                                    *
 *  This work is licensed under the Creative Commons                                  *
 *  Attribution-NonCommercial-ShareAlike 3.0 Unported License.                        *
 *                                                                                    *
 *  To view a copy of this license, visit:                                            *
 *  https://creativecommons.org/licenses/by-nc-sa/3.0/                                *
 *                                                                                    *
 *  Key terms:                                                                        *
 *  — Attribution:     You must credit the original author.                           *
 *  — NonCommercial:   Prohibits commercial use without explicit permission.          *
 *  — ShareAlike:      Derivatives must be licensed under identical terms.            *
 *                                                                                    *
 *  Permitted use:                                                                    *
 *    - Modify, distribute, and use privately.                                        *
 *    - Include attribution in derivative works (see details below).                  *
 *                                                                                    *
 *  Prohibited without written permission:                                            *
 *    - Commercial exploitation (SaaS, paid apps, internal corporate tools).          *
 *    - Removing license terms from derivatives.                                      *
 *                                                                                    *
 *  Attribution requirement:                                                          *
 *    Include this header in source files OR display prominently in UI/docs:          *
 *    "Contains code from [Project Name] by [Author], licensed under CC BY-NC-SA 3.0" *
 *                                                                                    *
 *  DISCLAIMER: This code is provided "AS IS" without warranties of any kind.         *
 *  The author accepts no liability for damages arising from its use.                 *
 *                                                                                    *
 *************************************************************************************/


Shader "Custom/CustomVFX"
{
    Properties
    {
        [Header(Attention)]
        [ToggleUI]_DummyValue("Please, use ParticlesShader cs editor, to set properties. Material properties can be overrided by it in runtime.", Float) = 1
        [Header(Emmiter)]
        _EmitterDimensions("EmitterDimensions", Vector) = (2, 2, 2, 0)
        
        _StartColor("StartColor", Color) = (1, 1, 1, 1)
        _EndColor("EndColor", Color) = (1, 1, 1, 1)
        [Space]
        [Header(Fade in and out and Opacity)]
        _Opacity("Opacity", Float) = 4
        _FadeInPower("FadeInPower", Float) = 0.75
        _FadeOutPower("FadeOutPower", Float) = 2
        [ToggleUI]_UseTextureRGBAlpha("Use Texture RGB as alpha", Float) = 0
        _TextureAlphaSmoothstep("Smoothstep max value for alpha transition", Float) = 1
        [ToggleUI]_UseTextureAlpha("Use Texture Alpha. Override use Texture RGB as alpha", Float) = 0
        [Space]
        [Header(Scale)]
        _ParticleStartSize("ParticleStartSize", Float) = 2
        _ParticleEndSize("ParticleEndSize", Float) = 2
        [Space]
        [Header(Debug)]
        [ToggleUI]_DebugTime("DebugTime", Float) = 0
        _ManualTime("ManualTime", Range(0, 1)) = 0
        [Space]
        [Header(Movement)]
        _ParticleSpeed("ParticleSpeed", Float) = 1
        _ParticleDirectional("ParticleDirectional", Vector) = (0, 0, -1, 0)
        _ParticleSpread("ParticleSpread", Range(0, 360)) = 60
        _ParticleVelocityStart("ParticleVelocityStart", Float) = 0
        _ParticleVelocityEnd("ParticleVelocityEnd", Float) = 0
        [Space]
        [Header(Rotation)]
        _Rotation("Rotation", Range(-180, 180)) = 0
        [ToggleUI]_RotationRandomOffset("RotationRandomOffset", Float) = 1
        _RotationSpeed("RotationSpeed", Float) = 0
        [ToggleUI]_RandomizeRotationDirection("RandomizeRotationDirection", Float) = 1
        [Space]
        [Header(Forces)]
        _Wind("Wind", Vector) = (0, 0, 0, 0)
        _Gravityy("Gravity", Vector) = (0, 0, 0, 0)
        [Space]
        [Header(FlipBook)]
        _FlipBookDimenions("FlipBookDimenions", Vector) = (13, 13, 0, 0)
        [NoScaleOffset]_FlipBook("FlipBook", 2D) = "white" {}
        [ToggleUI]_FlipX("Inverse Flipbook X", float) = 0
        [ToggleUI]_FlipY("Inverse Flipbook Y", float) = 0
        [ToggleUI]_MatchParticlePhase("MatchParticlePhase", Float) = 1
        _FlipBookSpeed("FlipBookSpeed", Float) = 20
        [HideInInspector]_QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector]_QueueControl("_QueueControl", Float) = -1
    }


    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        LOD 100
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZTest LEqual
        //ZWrite On
        ZWrite Off

        HLSLINCLUDE

            #include "Assets/CustomRP/ShaderLibrary/Common.hlsl"

            //#pragma shader_feature _NON_LOOPED
            #pragma multi_compile _ _CUSTOM_TIME
            #pragma shader_feature _TIME_BOUND_X
            #pragma shader_feature _TIME_BOUND_Y
            #pragma multi_compile _ _CONSTANT_FLOW_DISABLE

            // Structs
			struct Attributes 
            {
				float4 positionOS	: POSITION;
				float2 uv		    : TEXCOORD0;
				float4 color		: COLOR;
                half3 normalOS      : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings 
            {
				float4 positionCS 	: SV_POSITION;
				float2 uv		    : TEXCOORD0;
				float4 color		: COLOR;
                float4 particleData : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};



            CBUFFER_START(UnityPerMaterial)
                half3 _EmitterDimensions;
                half _ParticleStartSize;
                half _ParticleEndSize;
                half _ParticleSpeed;
                half _DebugTime;
                half _ManualTime;
                half3 _ParticleDirectional;
                half _ParticleSpread;
                half _ParticleVelocityStart;
                half _ParticleVelocityEnd;
                half3 _Wind;
                half3 _Gravityy;
                half _Rotation;
                half _RotationRandomOffset;
                half _RotationSpeed;
                half _RandomizeRotationDirection;
                half2 _FlipBookDimenions;
                float _FlipX;
                float _FlipY;
                half _MatchParticlePhase;
                half _FlipBookSpeed;
                float4 _FlipBook_TexelSize;
                half _Opacity;
                half _FadeInPower;
                half _FadeOutPower;
                half _UseTextureRGBAlpha;
                half _TextureAlphaSmoothstep;
                half _UseTextureAlpha;
                half4 _StartColor;
                half4 _EndColor;

                float4 _FlipBook_ST;

                float2 _TimeBounds; // x spawn start, y spawn end 
            CBUFFER_END
            
            #ifdef _CUSTOM_TIME
                float _PlaybackTime;
            #endif
            float _TimeBoundX;
            float _TimeBoundY;
            
            #ifndef _CUSTOM_TIME
                #define _PlaybackTime _Time.y
            #endif

            TEXTURE2D(_FlipBook);
			SAMPLER(sampler_FlipBook);



            float3 BillbordFaceCamera(float3 PositionOS, float Scale)
            {
                float3 _Object_Scale = float3(length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
                                             length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)),
                                             length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z)));


                float3 tempPos = PositionOS * _Object_Scale * Scale;
                float3 worldPos = GetAbsolutePositionWS(UNITY_MATRIX_M._m03_m13_m23);

                float4 tempPosForTransform = float4(tempPos, 0);
                float3 OutMatrix = mul(UNITY_MATRIX_I_V, tempPosForTransform).xyz + worldPos;

                float3 res = TransformWorldToObject(OutMatrix);
                return res;
            }
        
            float3 Hash33(float3 InVector3)
            {
                uint3 v = (uint3) (int3) round(InVector3);
                v.x ^= 110351;
                v.y ^= v.x + v.z;
                v.y = v.y * 134;
                v.z += v.x ^ v.y;
                v.y += v.x ^ v.z;
                v.x += v.y * v.z;
                v.x = v.x * 27;
                v.z ^= v.x << 3;
                v.y += v.z << 3; 
                float3 Out = v * (1.0 / float(0xffffffff));
                return Out;
            }

            float3 GetLifeTime(half _DebugTime, half _ManualTime, half _ParticleSpeed, half colorR)
            {
                half lifeTime;
    
                // Determine the lifetime based on the debug time or the particle speed
                if (_DebugTime)
                {
                    lifeTime = _ManualTime;
                }
                else
                {
                    lifeTime = _PlaybackTime * _ParticleSpeed;
                }

                // Modify the lifetime based on the input color's red channel
                lifeTime -= colorR;

                // Calculate the fractional part and the ceiling of the lifetime
                half lifeTimeFrac = frac(lifeTime);
                half lifeTimeCeil = ceil(lifeTime);

                // Create the resulting half3
                half3 result = half3(lifeTime, lifeTimeFrac, lifeTimeCeil);

                return result;
            }


            /*float Remap_float(float In, float2 InMinMax, float2 OutMinMax)
            {
                float4 Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
                return Out;
            }*/

            // Possible to avoid most of the trigonometry here, but need to re-write rotation relative code.
            // See https://iquilezles.org/articles/noacos/
            float3 RotateAboutAxis_Degrees_float(float3 In, float3 Axis, float Rotation)
            {
                Rotation = radians(Rotation); // a
                float s = sin(Rotation);
                float c = cos(Rotation);
                float one_minus_c = 1.0 - c;

                Axis = normalize(Axis); // v
                float3x3 rot_mat =
                {   one_minus_c * Axis.x * Axis.x + c, one_minus_c * Axis.x * Axis.y - Axis.z * s, one_minus_c * Axis.z * Axis.x + Axis.y * s,
                    one_minus_c * Axis.x * Axis.y + Axis.z * s, one_minus_c * Axis.y * Axis.y + c, one_minus_c * Axis.y * Axis.z - Axis.x * s,
                    one_minus_c * Axis.z * Axis.x - Axis.y * s, one_minus_c * Axis.y * Axis.z + Axis.x * s, one_minus_c * Axis.z * Axis.z + c
                };
                float3 Out = mul(rot_mat,  In);
                return Out;
            }

            void FlipbookUV(float2 UV, float Width, float Height, float Tile, float2 Invert, out float2 Out)
            {
                Tile = floor(fmod(Tile + float(0.00001), Width*Height));
                float2 tileCount = float2(1.0, 1.0) / float2(Width, Height);
                float base = floor((Tile + float(0.5)) * tileCount.x);
                float tileX = (Tile - Width * base);
                float tileY = (Invert.y * Height - (base + Invert.y * 1));
                Out = (UV + float2(tileX, tileY)) * tileCount;
            }

            float2 GetflipBookUV(half FlipbookSpeed, half MatchParticlePhase, half2 FlipBookDimenions, half RandHalf, half ParticleLife, float2 UV)
            {
                half FlipBookRandPos = frac(FlipbookSpeed * _PlaybackTime) + RandHalf;
                half FlipbookSize = FlipBookDimenions.x * FlipBookDimenions.y - 1;
                half FlipBookPos;

                if (MatchParticlePhase)
                {
                    FlipBookPos = ParticleLife * FlipbookSize;
                }
                else
                {
                    FlipBookPos = FlipBookRandPos * FlipbookSize;
                }
                float2 resUV;
                
                float2 _Flipbook_Invert = float2(_FlipX, _FlipY);
                FlipbookUV(UV, FlipBookDimenions.x, FlipBookDimenions.y, FlipBookPos, _Flipbook_Invert, resUV);


                return resUV;    
            }
            




            Varyings UnlitPassVertex(Attributes IN) 
            {
				Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);


                float3 objectPostions = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;

                
                float3 HashedHash3 = Hash33((IN.color.rrr ) * 255);
                OUT.color = HashedHash3.xyzz;
                //OUT.color = IN.color;
                float3 ExtendedHash3 = HashedHash3 * 2 - 1;

                #ifdef _CONSTANT_FLOW_DISABLE
                    half randTimeOffset = 0;
                #else
                    // To do: make a property
                    // Optionally breaks regularity and spawn in unequal periods of time
                    half randTimeOffset = (HashedHash3.x * _ParticleStartSize) / 4;
                    // IN.color.r defines offset in spawn time
                    randTimeOffset = IN.color.r + randTimeOffset;
                #endif
                half3 lifeTime = GetLifeTime(_DebugTime, _ManualTime, _ParticleSpeed, randTimeOffset);
                OUT.color.r = HashedHash3.x;
                HashedHash3 = Hash33((IN.color.rrr + lifeTime.z) * 255); //re
                HashedHash3 =  saturate(HashedHash3);
                ExtendedHash3 = HashedHash3 * 2 - 1;

                float SpreadRemapped = Remap_float(_ParticleSpread, float2(0, 360), float2(0, 2));
                float3 DirectionToMove = normalize(SpreadRemapped * ExtendedHash3 + _ParticleDirectional);
                float3 VelocityNow = lerp(_ParticleVelocityStart, _ParticleVelocityEnd, lifeTime.y);

                
                float3 GravityAndWind = TransformWorldToObject(_Wind + _Gravityy + objectPostions) * lifeTime.y;

                float3 MoveAndVelocity = (DirectionToMove * VelocityNow + GravityAndWind) * lifeTime.y;
                float3 SpawnPoint = _EmitterDimensions * ExtendedHash3;

                float3 PositionToAdd = MoveAndVelocity + SpawnPoint / 2;

                float RotationRandDir = sign(ExtendedHash3.r);
                float RotationRandOffset = ExtendedHash3.g * 180;
                float RotationAmount = _RotationSpeed * _PlaybackTime + _Rotation;
                if (_RotationRandomOffset)
                {
                    RotationAmount += RotationRandOffset;
                }
                if (_RandomizeRotationDirection)
                {
                    RotationAmount *= RotationRandDir;
                }

                float3 RotatedPos = RotateAboutAxis_Degrees_float(IN.positionOS.xyz, float3(0,0,1), RotationAmount);
                //RotatedPos = RotateAboutAxis_Degrees_float(RotatedPos.xyz, float3(0,1,0), -90);


                float Scale = lerp(_ParticleStartSize, _ParticleEndSize, lifeTime.y);

                //Manually calculate position on view Space
                float4 origin = float4(0,0,0,1);
                float4 world_origin = mul(UNITY_MATRIX_M, origin);
                float4 view_origin = mul(UNITY_MATRIX_V, world_origin);
                float4 world_to_view_translation = view_origin - world_origin;


                float4 world_Pos = mul(UNITY_MATRIX_M, float4(RotatedPos + PositionToAdd, 1));
                float4 view_pos = world_Pos + world_to_view_translation;
                float4 clip_Pos = mul(UNITY_MATRIX_P, view_pos);


                OUT.positionCS = TransformObjectToHClip(BillbordFaceCamera(RotatedPos, Scale) + PositionToAdd);
                //OUT.positionCS = clip_Pos;
                //float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                //OUT.positionCS = TransformWorldToHClip(positionWS);

                float2 flipbookUV = GetflipBookUV(_FlipBookSpeed, _MatchParticlePhase, _FlipBookDimenions, HashedHash3.g, lifeTime.y, IN.uv);

                float fadeIn = pow(lifeTime.y, _FadeInPower);
                float fadeOut = pow(1 - lifeTime.y, _FadeInPower);
                float fadeInAndOut = saturate(fadeIn * fadeOut * _Opacity);


                
				//OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.uv = float2(flipbookUV.x, flipbookUV.y);
                //OUT.uv.r = IN.color.r;
                OUT.particleData = float4(lifeTime.x + randTimeOffset, lifeTime.z, lifeTime.y, fadeInAndOut);
				return OUT;
			}




			// Fragment Shader
			half4 UnlitPassFragment(Varyings IN) : SV_Target 
            {
                float lifetimeX = IN.particleData.x;
                float lifetimeY = IN.particleData.z;
                float lifetimeZ = IN.particleData.y;
                float particleBirthTime = lifetimeX - lifetimeY;

                #ifdef _TIME_BOUND_X
                    clip( (particleBirthTime > _TimeBoundX * _ParticleSpeed) - 0.5 );
                #endif

                #ifdef _TIME_BOUND_Y
                    clip( (_TimeBoundY * _ParticleSpeed > particleBirthTime) - 0.5 );
                #endif

                float2 uv = IN.uv.xy;
				half4 baseMap = SAMPLE_TEXTURE2D(_FlipBook, sampler_FlipBook, uv);

				//#ifdef _ALPHATEST_ON
					// Alpha Clipping
					//clip(baseMap.a - _Cutoff);
				//#endif
                half4 ColorToAdd = lerp(_StartColor, _EndColor, lifetimeY);
                float alph;
                if (_UseTextureRGBAlpha)
                {
                    float baseMapAlph = smoothstep(0, _TextureAlphaSmoothstep, (baseMap.r + baseMap.g + baseMap.b) / 3);
                    alph = IN.particleData.a * baseMapAlph * ColorToAdd.a;
                }
                else if (_UseTextureAlpha)
                {
                     alph = IN.particleData.a * baseMap.a * ColorToAdd.a;
                }
                else
                {
                    alph = IN.particleData.a * ColorToAdd.a;
                }

                clip(baseMap - 0.04);
				return half4(baseMap.rgb * ColorToAdd.rgb, alph);
                //return half4(baseMap.rgb, 1);
                //return half4(IN.color.rrr, 1);
			}

        ENDHLSL

        Pass
        {
            Name "VFX Pass"

            Blend SrcAlpha OneMinusSrcAlpha


            HLSLPROGRAM

                #pragma vertex UnlitPassVertex
                #pragma fragment UnlitPassFragment

            ENDHLSL
        }

    }
    FallBack "Diffuse"
}

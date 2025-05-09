Shader "Hidden/Custom RP/MotionVector"
{
    SubShader
    {
        HLSLINCLUDE
        //#include "../../_General/ShaderLibrary/Input/Transformation.hlsl"
        #include "../ShaderLibrary/Common.hlsl"

        // Object rendering things

        // #if defined(USING_STEREO_MATRICES)
        //     float4x4 _StereoNonJitteredVP[2];
        //     float4x4 _StereoPreviousVP[2];
        // #else
            //float4x4 _NonJitteredViewProjMatrix;
           // float4x4 _PrevViewProjMatrix;

        float4x4 _CamNonJitteredViewProjMatrix;
        float4x4 _CamPrevViewProjMatrix;

        float4x4 _NonJitteredViewProjMatrix;
        float4x4 _PrevViewProjMatrix;
        float4 unity_MotionVectorsParams;
        float4x4 unity_MatrixPreviousM;

        //#endif
        //float4x4 _PreviousM;
        bool _HasLastPositionData;
        //bool _ForceNoMotion;
        //float _MotionVectorDepthBias;

        struct MotionVectorData
        {
            float4 transferPos : TEXCOORD0;
            float4 transferPosOld : TEXCOORD1;
            float4 pos : SV_POSITION;
            //UNITY_VERTEX_OUTPUT_STEREO
        };

        struct MotionVertexInput
        {
            float4 vertex : POSITION;
            float3 oldPos : TEXCOORD4;
            //UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        MotionVectorData VertMotionVectors(MotionVertexInput v)
        {
            MotionVectorData o;
            //UNITY_SETUP_INSTANCE_ID(v);
            //UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = TransformObjectToHClip(v.vertex.xyz);

            float _MotionVectorDepthBias = unity_MotionVectorsParams.z;

            // this works around an issue with dynamic batching
            // potentially remove in 5.4 when we use instancing
            #if defined(UNITY_REVERSED_Z)
                o.pos.z -= _MotionVectorDepthBias * o.pos.w;
            #else
                o.pos.z += _MotionVectorDepthBias * o.pos.w;
            #endif

            //#if defined(USING_STEREO_MATRICES)
            //    o.transferPos = mul(_StereoNonJitteredVP[unity_StereoEyeIndex], mul(unity_ObjectToWorld, v.vertex));
            //    o.transferPosOld = mul(_StereoPreviousVP[unity_StereoEyeIndex], mul(_PreviousM, _HasLastPositionData ? float4(v.oldPos, 1) : v.vertex));
            //#else
                o.transferPos = mul(_NonJitteredViewProjMatrix, mul(unity_ObjectToWorld, v.vertex));
                o.transferPosOld = mul(_PrevViewProjMatrix, mul(unity_MatrixPreviousM, _HasLastPositionData ? float4(v.oldPos, 1) : v.vertex));
            //#endif
            return o;
        }

        half4 FragMotionVectors(MotionVectorData i) : SV_Target
        {
            float3 hPos = (i.transferPos.xyz / i.transferPos.w);
            float3 hPosOld = (i.transferPosOld.xyz / i.transferPosOld.w);

            // V is the viewport position at this pixel in the range 0 to 1.
            float2 vPos = (hPos.xy + 1.0f) / 2.0f;
            float2 vPosOld = (hPosOld.xy + 1.0f) / 2.0f;

            #if UNITY_UV_STARTS_AT_TOP
                vPos.y = 1.0 - vPos.y;
                vPosOld.y = 1.0 - vPosOld.y;
            #endif
            half2 uvDiff = vPos - vPosOld;
            bool _ForceNoMotion = unity_MotionVectorsParams.y == 0.0;
            return lerp(half4(uvDiff, 0, 1), 0, (half)_ForceNoMotion);
        }

        //Camera rendering things
        //UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        sampler2D _CameraMotionDepthTexture;

        struct CamMotionVectors
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 ray : TEXCOORD1;
            //UNITY_VERTEX_OUTPUT_STEREO
        };

        struct CamMotionVectorsInput
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            //UNITY_VERTEX_INPUT_INSTANCE_ID
        };


        float4 ComputeScreenPosMotion (float4 pos) 
        {
            float4 o = pos * 0.5f;
            #if defined(UNITY_HALF_TEXEL_OFFSET)
                o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w * _ScreenParams.zw;
            #else
                o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w;
            #endif

            o.zw = pos.zw;
            return o;
        }

        CamMotionVectors VertMotionVectorsCamera(CamMotionVectorsInput v)
        {
            CamMotionVectors o;
           // UNITY_SETUP_INSTANCE_ID(v);
           // UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            o.pos = TransformObjectToHClip(v.vertex.xyz);
           
            #ifdef UNITY_HALF_TEXEL_OFFSET
                o.pos.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1) * o.pos.w;
            #endif
            o.uv = ComputeScreenPosMotion(o.pos).xy;
            // we know we are rendering a quad,
            // and the normal passed from C++ is the raw ray.
            o.ray = v.normal;
            return o;
        }

        inline half2 CalculateMotion(float rawDepth, float2 inUV, float3 inRay)
        {
            float4x4 unity_CameraToWorld = UNITY_MATRIX_I_V; 

            float depth = Linear01Depth(rawDepth,_ZBufferParams);
            float3 ray = inRay * (_ProjectionParams.z / inRay.z);
            float3 vPos = ray * depth;
            float4 worldPos = mul(unity_CameraToWorld, float4(vPos, 1.0));

            // #if defined(USING_STEREO_MATRICES)
            //     float4 prevClipPos = mul(_StereoPreviousVP[unity_StereoEyeIndex], worldPos);
            //     float4 curClipPos = mul(_StereoNonJitteredVP[unity_StereoEyeIndex], worldPos);
            // #else
                float4 prevClipPos = mul(_CamPrevViewProjMatrix, worldPos);
                float4 curClipPos = mul(_CamNonJitteredViewProjMatrix, worldPos);
            //#endif
            float2 prevHPos = prevClipPos.xy / prevClipPos.w;
            float2 curHPos = curClipPos.xy / curClipPos.w;

            // V is the viewport position at this pixel in the range 0 to 1.
            float2 vPosPrev = (prevHPos.xy + 1.0f) / 2.0f;
            float2 vPosCur = (curHPos.xy + 1.0f) / 2.0f;
            #if UNITY_UV_STARTS_AT_TOP
                vPosPrev.y = 1.0 - vPosPrev.y;
                vPosCur.y = 1.0 - vPosCur.y;
            #endif
            return vPosCur - vPosPrev;
        }

        half4 FragMotionVectorsCamera(CamMotionVectors i) : SV_Target
        {
            //float depth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, i.uv, 0).r;
            float depth = tex2D(_CameraMotionDepthTexture, i.uv).r;
            //return float4(1,1,0,1);
            half2 motion = CalculateMotion(depth, i.uv, i.ray);
            return half4(motion, 0, min(motion.x+motion.y,1));
        }

        half4 FragMotionVectorsCameraWithDepth(CamMotionVectors i, out float outDepth : SV_Depth) : SV_Target
        {
            //float depth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, i.uv, 0).r;
            float depth = tex2D(_CameraMotionDepthTexture, i.uv).r;
            outDepth = depth;
            return half4(CalculateMotion(depth, i.uv, i.ray), 0, 1);
        }
        ENDHLSL

        // 0 - Motion vectors
        Pass
        {
            //Tags{ "LightMode" = "MotionVectors" }
            Tags { "LightMode" = "SRP0703_Pass" }

            ZTest LEqual
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VertMotionVectors
            #pragma fragment FragMotionVectors
            ENDHLSL
        }

        // 1 - Camera motion vectors
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex VertMotionVectorsCamera
            #pragma fragment FragMotionVectorsCamera
            ENDHLSL
        }

        // 2 - Camera motion vectors (With depth (msaa / no render texture))
        Pass
        {
            ZTest Always
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VertMotionVectorsCamera
            #pragma fragment FragMotionVectorsCameraWithDepth
            ENDHLSL
        }
    }

    Fallback Off
}

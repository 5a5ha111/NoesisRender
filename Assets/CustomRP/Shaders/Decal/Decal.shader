Shader "Custom/DecalReconstructedShaderUnity" 
{
    Properties 
    {
        _ColorTexture ("Color", 2D) = "white" {}
        _NormalTexture ("Normal", 2D) = "bump" {}
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
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //#include "UnityCG.cginc"
            #include "Assets/CustomRP/ShaderLibrary/Common.hlsl"

            sampler2D _ColorTexture;
            sampler2D _NormalTexture;
            //sampler2D _DepthTexture;
            float4 _EffectParams;

            SAMPLER(sampler_CameraDepthTexture);

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

            /*float4x4 inverse(float4x4 m) 
            {
                return determinant(m) * transpose(adjugate(m));
            }*/


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
                float4 viewPos = mul(unity_CameraInvProjection, clipPos);

                float3 viewDirection = normalize(_WorldSpaceCameraPos - i.posWS.xyz);
                float3 cameraDir = GetCameraDirection();
                float3 divideDot = viewDirection / dot(viewDirection, cameraDir);
                float3 surfacePosWS = (depth * divideDot) + _WorldSpaceCameraPos;
                float3 objectPos = TransformWorldToObject(surfacePosWS);


                // Bounds point for default cube mesh
                float3 scale = GetObjectScale();
                float3 minBoundsPoint = float3(-0.5,-0.5,-0.5) * scale;
                float3 maxBoundsPoint = float3(0.5,0.5,0.5) * scale;

                // Clip if surface point outside box mesh
                float inBounds =PointInBounds(minBoundsPoint, maxBoundsPoint, objectPos) - 0.5;
                clip(inBounds);

                // Calculate texture coordinates using view position
                float2 baseUV = objectPos.xz + float2(0.5,0.5);
                
                // Edge detection and sampling
                float2 edgeFactors = saturate(50.0 * (1.0 - baseUV));
                float2 clampedUV = saturate(baseUV);
                float3 color = tex2D(_ColorTexture, clampedUV).rgb;

                //return float4(color, clampedUV.x * edgeFactors.x * clampedUV.y * edgeFactors.y);
                return float4(color, 1);
                //return float4(frac(objectPos.xyz), 1);
                //return float4(clampedUV,0, 1);
            }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}
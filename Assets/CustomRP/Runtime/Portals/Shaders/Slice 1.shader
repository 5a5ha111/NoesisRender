Shader "Custom/Slice1"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        sliceNormal("normal", Vector) = (0,0,0,0)
        sliceCentre ("centre", Vector) = (0,0,0,0)
        sliceOffsetDst("offset", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "IgnoreProjector" = "True"  "RenderType"="Geometry" }
        LOD 200

        Pass
        {
            Name "Forward"
            CGPROGRAM
            // Physically based Standard lighting model, and enable shadows on all light types
            //#pragma surface surf Standard addshadow
            #pragma vertex vert
            #pragma fragment surf
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0


            struct Input
            {
                float2 uv_MainTex;
                float3 worldPos;
            };

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
            };

            half _Glossiness;
            half _Metallic;
            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            // World space normal of slice, anything along this direction from centre will be invisible
            float3 sliceNormal;
            // World space centre of slice
            float3 sliceCentre;
            // Increasing makes more of the mesh visible, decreasing makes less of the mesh visible
            float sliceOffsetDst;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            float4 surf (v2f IN) : SV_Target
            {
                float3 adjustedCentre = sliceCentre + sliceNormal * sliceOffsetDst;
                float3 offsetToSliceCentre = adjustedCentre - IN.worldPos;
                clip (dot(offsetToSliceCentre, sliceNormal));
            
                // Albedo comes from a texture tinted by color
                fixed4 c = tex2D (_MainTex, IN.uv) * _Color;
                fixed shadow = SHADOW_ATTENUATION(i);
                c.rgb *= shadow;
                return c;
                /*o.Albedo = c.rgb;

                // Metallic and smoothness come from slider variables
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = c.a;*/
            }
            ENDCG
        }

        Pass 
        {
            Name "ShadowCast"
            Tags{ "LightMode" = "ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment surf
            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 3.0
            
            #include "UnityCG.cginc"


            struct Input
            {
                float2 uv_MainTex;
                float3 worldPos;
            };

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Glossiness;
            half _Metallic;
            fixed4 _Color;

            // World space normal of slice, anything along this direction from centre will be invisible
            float3 sliceNormal;
            // World space centre of slice
            float3 sliceCentre;
            // Increasing makes more of the mesh visible, decreasing makes less of the mesh visible
            float sliceOffsetDst;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            float surf (v2f IN) : SV_Target
            {
                float3 adjustedCentre = sliceCentre + sliceNormal * sliceOffsetDst;
                float3 offsetToSliceCentre = adjustedCentre - IN.worldPos;
                clip (dot(offsetToSliceCentre, sliceNormal));
            
                // Albedo comes from a texture tinted by color
                //fixed4 c = tex2D (_MainTex, IN.uv) * _Color;
                /*o.Albedo = c.rgb;

                // Metallic and smoothness come from slider variables
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
                o.Alpha = c.a;*/
                return 0;
            }
            ENDCG

        }
    }
    FallBack "VertexLit"
}

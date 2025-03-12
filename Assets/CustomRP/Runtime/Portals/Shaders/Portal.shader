Shader "Custom RP/Portal"
{
    Properties
    {
        _InactiveColour ("Inactive Colour", Color) = (1, 1, 1, 1)
        _ShadowTex ("Shadow Texture", 2D) = "white" {} 
        _MainTex ("Main tex", 2D) = "white" {} 
        _ShaowSlider ("Shadow subtr", Float) = 0
        _ColorSlider ("Shadow subtr", Vector) = (0, 0, 1, 1)
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparrent" }
        LOD 100
        Cull Off
        ZWrite [_ZWrite]

        HLSLINCLUDE
        #include "Assets/CustomRP/ShaderLibrary/Common.hlsl"
        #include "Assets/CustomRP/Shaders/UnlitInput.hlsl"
        ENDHLSL


        Pass
        {
            
            HLSLPROGRAM

            #pragma shader_feature _CLIPPING
            #pragma shader_feature _RECEIVE_SHADOWS

            #pragma shader_feature _VERTEX_COLORS

            #pragma multi_compile_instancing

            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/CustomRP/Shaders/UnlitPass.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 uv : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _ShadowTex;
            float4 _InactiveColour;
            float4 _ColorSlider;
            int displayMask; // set to 1 to display texture, otherwise will draw test colour
            
            float4 UnityObjectToClipPos(float4 pos)
            {
                /*float4 world =  float4(TransformObjectToWorld(pos),1);
                float4 clipPos = TransformWorldToHClip(world);
                return clipPos;*/

                return mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, pos));
            }
            float4 ComputeScreenPos(float4 positionCS)
            {
                float4 o = positionCS * 0.5f;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = positionCS.zw;
                return o;
            }

            v2f vert (appdata v)
            {
                v2f o;
                //o.pos = UnityObjectToClipPos(v.vertex);
                o.pos = TransformWorldToHClip(TransformObjectToWorld(v.vertex));
                o.screenPos = ComputeScreenPos(o.pos);
                float4 screenPr = o.pos;
                screenPr.y = 1 - screenPr.y;
                //o.screenPos = o.pos;
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = i.screenPos.xy / i.screenPos.w;
                //fixed4 portalCol = tex2D(_ShadowTex, uv);
                //uv = i.uv;
                uv = (uv + _ColorSlider.xy) * _ColorSlider.zw;
                half4 portalCol = tex2D(_MainTex, uv);
                //return i.uv;
                //return portalCol * displayMask + _InactiveColour * (1-displayMask);
                return portalCol;
            }

            ENDHLSL
        }

        /*Pass 
        {
            Tags {"LightMode" = "ShadowCaster"}
            ZWrite On
            ZTest LEqual	    

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _ShadowTex;
            float _ShaowSlider;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };
            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                //float offset = 0;
                //o.vertex.xyz += float3(offset, offset, offset);
                o.vertex = UnityObjectToClipPos(o.vertex.xyz);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                //SHADOW_CASTER_FRAGMENT(i);
                //return saturate(i.depth - 0.5); // Add 0.5 to depth and clamp
                //fixed shadow = SHADOW_ATTENUATION(i);
                //clip(shadow);
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float4 portalCol = tex2D(_ShadowTex, i.uv);
                portalCol = portalCol;
                clip(portalCol.r -_ShaowSlider);
                return 0;
                // UnityEncodeCubeShadowDepth((length(i.V2F_SHADOW_CASTER.vec) + unity_LightShadowBias.x) * _LightPositionRange.w);
            }
            ENDHLSL
        }*/

    }
    Fallback "Standard" // for shadows
}

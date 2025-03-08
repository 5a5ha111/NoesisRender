Shader "Custom/Portal"
{
    Properties
    {
        _InactiveColour ("Inactive Colour", Color) = (1, 1, 1, 1)
        _ShadowTex ("Shadow Texture", 2D) = "white" {} 
        _ShaowSlider ("Shadow subtr", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off

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

            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            #include "Assets/CustomRP/Shaders/UnlitPass.hlsl"

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

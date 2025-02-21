Shader "Custom RP/UI Custom Blending" 
{
    Properties 
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
    }

    SubShader 
    {
        Tags 
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil 
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass 
        {
            Name "Default"
            
            CGPROGRAM
                #pragma vertex UIPassVertex
                #pragma fragment UIPassFragment
                #pragma target 2.0

                #include "UnityCG.cginc"
                #include "UnityUI.cginc"

                #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
                #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

                struct Attributes 
                {
                    float4 positionOS : POSITION;
                    float4 color : COLOR;
                    float2 baseUV : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct Varyings 
                {
                    float4 positionCS : SV_POSITION;
                    float2 positionUI : VAR_POSITION;
                    float2 baseUV : VAR_BASE_UV;
                    float4 color : COLOR;
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                sampler2D _MainTex;
                float4 _MainTex_ST;
                float4 _Color;
                float4 _TextureSampleAdd;
                float4 _ClipRect;

                Varyings UIPassVertex (Attributes input) 
                {
                    Varyings output;
                    UNITY_SETUP_INSTANCE_ID(input);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                    output.positionCS = UnityObjectToClipPos(input.positionOS);
                    output.positionUI = input.positionOS.xy;
                    output.baseUV = TRANSFORM_TEX(input.baseUV, _MainTex);
                    output.color = input.color * _Color;
                    return output;
                }

                float4 UIPassFragment (Varyings input) : SV_Target 
                {
                    float4 color =
                        (tex2D(_MainTex, input.baseUV) + _TextureSampleAdd) * input.color;
                    #if defined(UNITY_UI_CLIP_RECT)
                        color.a *= UnityGet2DClipping(input.positionUI, _ClipRect);
                    #endif
                    #if defined(UNITY_UI_ALPHACLIP)
                        clip (color.a - 0.001);
                    #endif
                    return color;
                }
            ENDCG
        }
    }
}
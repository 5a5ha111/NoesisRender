Shader "Custom/WaterfallParticle"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        //Tags { "RenderType"="Opaque" }

        Pass
        {

            LOD 300
            
            Cull Back
            ZWrite Off
            //Blend SrcAlpha One
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            //ColorMask RGB
            ZTest LEqual

            HLSLPROGRAM

                #pragma vertex vert
                #pragma fragment frag

                #include "Assets/CustomRP/ShaderLibrary/Common.hlsl"

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;
                };

                struct Varyings
                {
                    float4 positionHCS : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;
                };

                TEXTURE2D(_BaseMap);
                SAMPLER(sampler_BaseMap);
                float4 _BaseMap_ST;
                float4 _BaseColor;

                Varyings vert (Attributes v)
                {
                    Varyings o;
                    o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                    o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                    o.color = v.color;
                    return o;
                }

                half4 frag (Varyings i) : SV_Target
                {
                    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                    half4 col = tex * i.color * _BaseColor;

                    clip(tex - 0.04);
                    
                    float alph = (tex.r + tex.g + tex.b) / 3;
                    alph *= _BaseColor.a * i.color.a;

                    // Multiply effect: Preserve destination alpha
                    //col.rgb *= col.a;  // Premultiply alpha for correct blending
                    return half4(col.rgb, alph); // Keep destination alpha unchanged
                    //return half4(i.color.aaa, alph); // Keep destination alpha unchanged
                }
            ENDHLSL

        }
    }
    //FallBack "Diffuse"
}

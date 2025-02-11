Shader "Unlit/TestBetterDither"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BlueNoiseTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment

            #include "RiemersmaDither.hlsl"

            
            ENDHLSL
        }
    }
}

Shader "Unlit/CheckerBoardUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WhiteColor("WhiteCol", Color) = (1, 1, 1, 1)
        _BlackColor("BlackCol", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

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
                float4 posWS : TEXCOORD1;
            };

            uint GetCheckerValue(float3 position, float s)
            {
                // Convert world position to discrete grid coordinates based on step size
                uint3 gridPos;
                //float3 gridPos = (uint3)floor(position + 0.001 / s);
                float offset = 0.001; // fix flicker near 0

                if (position.x > 0)
                {
                    gridPos.x = floor((position.x + offset) / s);
                }
                else
                {
                    gridPos.x = floor(abs(position.x + offset - s) / s);
                }

                if (position.y > 0)
                {
                    gridPos.y = floor((position.y + offset) / s);
                }
                else
                {
                    gridPos.y = floor(abs(position.y + offset - s) / s);
                }

                if (position.z > 0)
                {
                    gridPos.z = floor((position.z + offset) / s);
                }
                else
                {
                    gridPos.z = floor(abs(position.z + offset - s) / s);
                }


                // Compute checkerboard pattern by summing the coordinates and taking modulo 2
                uint sum = gridPos.x + gridPos.y + gridPos.z;
                //float PatternMask = fmod(gridPos.z + fmod(gridPos.x + fmod(gridPos.y, 2.0), 2.0), 2.0);
                //uint sum = gridPos.x ^ gridPos.y ^ gridPos.z;
        
                //return PatternMask;
                return (sum % 2 == 0) ? 1 : 0;
                //return sum;
            }

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _WhiteColor;
            float4 _BlackColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                o.posWS = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                float checkerboardValue = GetCheckerValue(i.posWS, 0.2);
                return checkerboardValue * _WhiteColor + _BlackColor * (1 - checkerboardValue);
            }
            ENDCG
        }
    }
}

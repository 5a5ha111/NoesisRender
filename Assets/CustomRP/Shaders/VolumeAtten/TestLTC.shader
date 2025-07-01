Shader "Custom/TestLTC"
{
    Properties
    {
        _Smoothness("Smoothness", Range(0.0, 1)) = 0.5
        _LtcMat("LTC Matrix", 2D) = "black" {}
        _LtcMag("LTC Magnitude", 2D) = "black" {}
        _LightTexture("Light Texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            Tags { "RenderType"="Opaque" }
            LOD 200

            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #pragma target 4.5

                #include "Assets/CustomRP/ShaderLibrary/Common.hlsl"

                // Another approach for clipping light surface. Less effective than new one
                //#define QuadClipping

                float3 PolygonWS[4];
                float3 lightForward;
                float _Smoothness;

                TEXTURE2D(_LtcMat);
                SAMPLER(sampler_LtcMat);
                TEXTURE2D(_LtcMag);
                SAMPLER(sampler_LtcMag);

                TEXTURE2D(_LightTexture);
                SAMPLER(sampler_LightTexture);

                struct Attributes
                {
                    float4 positionOS : POSITION;
                    float4 normal : NORMAL;
                    float2 uv : TEXCOORD0;
                };

                struct Varyings
                {
                    float4 positionCS : SV_POSITION;
                    float2 baseUV : TEXCOORD0;
                    float4 positionWS : TEXCOORD1;
                    float4 normalWS: TEXCOORD2;
                };


                // Start Analytic diffuse area light


                // from: https://seblagarde.wordpress.com/2014/12/01/inverse-trigonometric-functions-gpu-optimization-for-amd-gcn-architecture/
                // max absolute error 9.0x10^-3
                // Eberly's polynomial degree 1 - respect bounds
                // 4 VGPR, 12 FR (8 FR, 1 QR), 1 scalar
                // input [-1, 1] and output [0, PI]
                float AcosFast(float inX) 
                { 
                    float x = abs(inX); 
                    float res = -0.156583f * x + HALF_PI; 
                    res *= sqrt(1.0f - x); 
                    return (inX >= 0) ? res : PI - res; 
                }

                // Since we need polygon only in upper hemisphere, we need to clip parts that dont
                void ClipQuadToHorizon(inout float3 L[5], out int n)
                {
                    // detect clipping config
                    uint config = 0;
                    if (L[0].z > 0.0) config += 1;
                    if (L[1].z > 0.0) config += 2;
                    if (L[2].z > 0.0) config += 4;
                    if (L[3].z > 0.0) config += 8;

                    n = 0;

                    switch (config) {
                        case 13: // V1 V3 V4 clip V2
                            n = 5;
                            L[4] = L[3];
                            L[3] = L[2];
                            L[2] = -L[1].z * L[2] + L[2].z * L[1];
                            L[1] = -L[1].z * L[0] + L[0].z * L[1];
                            break;
                        case 15: // V1 V2 V3 V4 - most common
                            n = 4;
                            break;
                        case 9: // V1 V4 clip V2 V3
                            n = 4;
                            L[1] = -L[1].z * L[0] + L[0].z * L[1];
                            L[2] = -L[2].z * L[3] + L[3].z * L[2];
                            break;
                        case 0: // clip all
                            break;
                        case 1: // V1 clip V2 V3 V4
                            n = 3;
                            L[1] = -L[1].z * L[0] + L[0].z * L[1];
                            L[2] = -L[3].z * L[0] + L[0].z * L[3];
                            L[3] =  L[0];
                            break;
                        case 2: // V2 clip V1 V3 V4
                            n = 3;
                            L[0] = -L[0].z * L[1] + L[1].z * L[0];
                            L[2] = -L[2].z * L[1] + L[1].z * L[2];
                            L[3] =  L[0];
                            break;
                        case 3: // V1 V2 clip V3 V4
                            n = 4;
                            L[2] = -L[2].z * L[1] + L[1].z * L[2];
                            L[3] = -L[3].z * L[0] + L[0].z * L[3];
                            break;
                        case 4: // V3 clip V1 V2 V4
                            n = 3;
                            L[0] = -L[3].z * L[2] + L[2].z * L[3];
                            L[1] = -L[1].z * L[2] + L[2].z * L[1];
                            L[3] =  L[0];
                            break;
                        case 5: // V1 V3 clip V2 V4) impossible
                            break;
                        case 6: // V2 V3 clip V1 V4
                            n = 4;
                            L[0] = -L[0].z * L[1] + L[1].z * L[0];
                            L[3] = -L[3].z * L[2] + L[2].z * L[3];
                            break;
                        case 7: // V1 V2 V3 clip V4
                            n = 5;
                            L[4] = -L[3].z * L[0] + L[0].z * L[3];
                            L[3] = -L[3].z * L[2] + L[2].z * L[3];
                            break;
                        case 8: // V4 clip V1 V2 V3
                            n = 3;
                            L[0] = -L[0].z * L[3] + L[3].z * L[0];
                            L[1] = -L[2].z * L[3] + L[3].z * L[2];
                            L[2] =  L[3];
                            break;
                        case 10: // V2 V4 clip V1 V3) impossible
                            break;
                        case 11: // V1 V2 V4 clip V3
                            n = 5;
                            L[4] = L[3];
                            L[3] = -L[2].z * L[3] + L[3].z * L[2];
                            L[2] = -L[2].z * L[1] + L[1].z * L[2];
                            break;
                        case 12: // V3 V4 clip V1 V2
                            n = 4;
                            L[1] = -L[1].z * L[2] + L[2].z * L[1];
                            L[0] = -L[0].z * L[3] + L[3].z * L[0];
                            break;
                        case 14: // V2 V3 V4 clip V1
                            n = 5;
                            L[4] = -L[0].z * L[3] + L[3].z * L[0];
                            L[0] = -L[0].z * L[1] + L[1].z * L[0];
                            break;
                    }
                    
                    if (n == 3)
                        L[3] = L[0];
                    if (n == 4)
                        L[4] = L[0];
                }
                float ClippedSphere(float3 meanFlux)
                {
                    // Real-Time Area Lighting: a Journey From Research to Production (2016) p. 102
                    // https://blog.selfshadow.com/publications/s2016-advances/s2016_ltc_rnd.pdf
                    // Approximation of sphere clipping given the flux towards a sphere
                    float l = length(meanFlux);
                    return max( (l*l + meanFlux.z) / (l + 1.0), 0);
                }

                // Several examples how to integrate edges.
                // Most closely match formula from paper. Start here to understand others
                float3 IrridOfPolygon(float3 vi, float3 vj)
                {
                    float d = dot(vi, vj);
                    float theta = acos(d);
                    float3 crossvij = cross(vi, vj);
                    float3 nij = normalize(crossvij);
                    float Aij = theta / 2;
                    float3 Iij = ( dot(nij, float3(0,0,1)) ) * Aij;
                    return Iij;
                }
                // If we work not in cosine space, pass needed light forward dir
                float3 IrridOfPolygon(float3 vi, float3 vj, float3 lightFwrd)
                {
                    float d = dot(vi, vj);
                    d = clamp(d, -0.99999, 0.99999); // sometimes flot precision mess with values, and acos return NaN
                    float theta = acos(d);
                    float3 crossvij = cross(vi, vj);
                    float3 nij = normalize(crossvij);
                    float Aij = theta / 2;
                    float Iij = ( dot(nij, lightFwrd) ) * Aij;
                    return Iij * 0.15915;
                }

                float3 IntegrateEdge(float3 v1, float3 v2)
                {
                    float x = dot( v1, v2 );
                    float y = abs( x );

                    // Real-Time Area Lighting: a Journey From Research to Production (2016) p. 74
                    // https://blog.selfshadow.com/publications/s2016-advances/s2016_ltc_rnd.pdf
                    // a cubic fit to theta / sin(theta)
                    float a = 0.8543985 + ( 0.4965155 + 0.0145206 * y ) * y;
                    float b = 3.4175940 + ( 4.1616724 + y ) * y;
                    float v = a / b;
                    float theta_sintheta = ( x > 0.0 ) ? v : 0.5 * rsqrt( max( 1.0 - x * x, 1e-7 ) ) - v;
                    return cross( v1, v2 ) * theta_sintheta;
                }

                float IntegrateEdge2(float3 v1, float3 v2)
                {
                    float cosTheta = dot(v1, v2);
                    float theta = acos(cosTheta);
                    theta = isnan (theta) ? 0 : theta;    
                    float res = cross(v1, v2).z * ((theta > 0.001) ? theta/sin(theta) : 1.0);

                    return res;
                }
                float IntegrateEdge3(float3 v1, float3 v2)
                {
                    float theta = acos(max(-0.9999, dot(v1,v2)));
                    float theta_sintheta = theta / saturate(sin(theta));
                    float res = theta_sintheta * (v1.x*v2.y - v1.y*v2.x) * 0.15915;
                    return isnan( res ) ? 0 : res;
                }

                // End Analytic diffuse area light

                // Start LTC code
                float3 FetchDiffuseFilteredTexture(Texture2D cookie, float3 f, float3 L[4])
                {
                    float3 V1 = (L[1] - L[0]);
                    float3 V2 = (L[3] - L[0]);
                    float3 n = cross(V1, V2);
                    float3 P = f * (dot(L[0], n) / dot(f, n)) - L[0];

                    float dot_V1_V2 = dot(V1, V2);
                    float inv_dot_V1_V1 = 1.0 / dot(V1, V1);
                    float3 V2_ = V2 - V1 * dot_V1_V2 * inv_dot_V1_V1;
                    float2 Puv;
                    Puv.y = dot(V2_, P) / dot(V2_, V2_);
                    Puv.x = dot(V1, P)*inv_dot_V1_V1 - dot_V1_V2*inv_dot_V1_V1*Puv.y ;

                     // LOD
                    float planeAreaSquared = dot(n, n);
                    float planeDistxPlaneArea = dot(n, L[0]);
                    float d = abs(planeDistxPlaneArea) / pow(planeAreaSquared, 0.75);
                    float lod = log(2048*d)/log(3.0);
                    //lod = 0;
                    float2 uv = clamp(Puv, float2(0,0), float2(1,1));
                    float lodRef = cookie.CalculateLevelOfDetail(sampler_LightTexture, uv);
                    lod = max(lod, lodRef);

                    return cookie.SampleLevel(sampler_LightTexture, uv, lod).rgb;
                }

                float3 LTC_Evaluate(float3 N, float3 V, float3 P, float3x3 Minv, bool twoSided, float3 lightVerts[4])
                {

                    float irradiance = 0;
                    float3 textureLight = 1;

                    #ifdef QuadClipping
                        float3 L[5];
                        L[0] = mul(Minv, lightVerts[0] - P);
                        L[1] = mul(Minv, lightVerts[1] - P);
                        L[2] = mul(Minv, lightVerts[3] - P);
                        L[3] = mul(Minv, lightVerts[2] - P);
                        L[4] = 0;

                        int n = 0;
                        ClipQuadToHorizon(L, n);

                        // early out if everything was clipped below horizon
                        [branch]
                        if (n == 0)
                            return 0;

                        L[0] = normalize(L[0]);
                        L[1] = normalize(L[1]);
                        L[2] = normalize(L[2]);
                        L[3] = normalize(L[3]);

                        // integrate
                        float sum = 0;
                        irradiance += IntegrateEdge(L[0], L[1]).z;
                        irradiance += IntegrateEdge(L[1], L[2]).z;
                        irradiance += IntegrateEdge(L[2], L[3]).z;
                        [branch]
                        if (n >= 4)
                        {
                            L[4] = normalize(L[4]);
                            irradiance += IntegrateEdge(L[3], L[4]).z;
                            [branch]
                            if (n == 5)
                                irradiance += IntegrateEdge(L[4], L[0]).z;
                        }
                    #else
                        // light vertices in cosine lobe space
                        float3 LC[4];
                        LC[0] = mul(Minv, lightVerts[0] - P);
                        LC[1] = mul(Minv, lightVerts[1] - P);
                        LC[2] = mul(Minv, lightVerts[3] - P);
                        LC[3] = mul(Minv, lightVerts[2] - P);

                        // light vertices in cosine lobe space projected to hemisphere
                        float3 LH[4];
                        LH[0] = normalize(LC[0]);
                        LH[1] = normalize(LC[1]);
                        LH[2] = normalize(LC[2]);
                        LH[3] = normalize(LC[3]);

                        float3 flux = 0;
                        flux += IntegrateEdge(LH[0], LH[1]);
                        flux += IntegrateEdge(LH[1], LH[2]);
                        flux += IntegrateEdge(LH[2], LH[3]);
                        flux += IntegrateEdge(LH[3], LH[0]);
                        textureLight = FetchDiffuseFilteredTexture(_LightTexture, flux, LC);
                        irradiance = ClippedSphere(flux);
                    #endif

                    // doublesided is accounted for with optimization at the start, so return abs
                    float3 Lo_i = max(irradiance, 0);

                    Lo_i *= textureLight;

                    return Lo_i;
                }
                // End LTC

                Varyings vert(Attributes IN)
                {
                    Varyings OUT;
                    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                    OUT.positionWS = float4(TransformObjectToWorld(IN.positionOS.xyz), 1);
                    OUT.baseUV = IN.uv;
                    OUT.normalWS = float4(UnityObjectToWorldDir(IN.normal.xyz), 1);
                    return OUT;
                }

                float4 frag (Varyings IN) : SV_Target
                {
                    float3 col = 0;
                    float3 posWS = IN.positionWS.xyz;
                    float3 posFlip = float3(1,1,-1);

                    float3 P = posWS;
                    float3 V = normalize(_WorldSpaceCameraPos - P);
                    float3 N = normalize(IN.normalWS.xyz);
                    float smoothness = 0.5;
                    float alpha = 1 - _Smoothness;


                    // Lambertian light
                    /*float3 T1, T2;
                    T1 = normalize( V - N * dot( V, N ) );
                    T2 = -cross( N, T1 );*/

                    /*float3 sum = 0;
                    float3 L[4] = PolygonWS; 
                    [unroll]
                    for (int i = 0; i < 4; i++)
                    {
                        L[i] = PolygonWS[i] - posWS;
                        L[i] = normalize(L[i]);
                    }
                    #define Irrid

                    #ifdef Irrid
                        sum += IrridOfPolygon(L[0], L[1]);
                        sum += IrridOfPolygon(L[1], L[2]);
                        sum += IrridOfPolygon(L[2], L[3]);
                        sum += IrridOfPolygon(L[3], L[0]);
                        sum = ClippedSphere(sum);
                    #else
                        sum += IntegrateEdge(L[0], L[1]);
                        sum += IntegrateEdge(L[1], L[2]);
                        sum += IntegrateEdge(L[2], L[3]);
                        sum += IntegrateEdge(L[3], L[0]);
                        sum = ClippedSphere(sum);
                    #endif

                    col = saturate(sum);*/


                    // LTC code
                    float theta = AcosFast(dot(N,V));
                    float2 uv = float2(alpha, theta / (0.5 * PI));
                    const float LUT_SIZE  = 64.0;
                    const float LUT_SCALE = (LUT_SIZE - 1.0)/LUT_SIZE;
                    const float LUT_BIAS  = 0.5/LUT_SIZE;
                    uv = uv * LUT_SCALE + LUT_BIAS;

                    // Cull backface
                    float3 Lw[4] = PolygonWS;
                    float3 screenNorm = cross(Lw[1] - Lw[0], Lw[2] - Lw[0]);
                    if (dot(screenNorm, Lw[0] - P) < 0)
                    {
                        return 0;
                    }

                    float4 t = SAMPLE_TEXTURE2D(_LtcMat, sampler_LtcMat, uv);
                    float amp = SAMPLE_TEXTURE2D(_LtcMag, sampler_LtcMag, uv).r;

                    float3x3 Minv = float3x3(
                        float3(  1,   0, t.w),
                        float3(  0, t.z,   0),
                        float3(t.y,   0, t.x)
                    );

                    // construct orthonormal basis around N
                    float3 T1, T2;
                    T1 = normalize(V - N*dot(V, N));
                    T2 = cross(N, T1);
                    
                    float3x3 identityBrdf = float3x3(float3(T1), float3(T2), float3(N));
                    Minv = mul(Minv, identityBrdf);

                    col = LTC_Evaluate(N, V, P, Minv, true, PolygonWS);
                    col *= amp;
                    
                    return float4(col, 1.0);

                }
            ENDHLSL
        }
    }
    FallBack "Diffuse"
}

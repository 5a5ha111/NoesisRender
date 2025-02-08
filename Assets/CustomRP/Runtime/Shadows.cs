using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{

    
    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;

    int ShadowedDirectionalLightCount;


    const int maxShadowedDirectionalLightCount = 4, maxCascades = 4;

    const string bufferName = "Shadows";
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");


    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };

    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };


    static string[] shadowMaskKeywords = {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };
    bool useShadowMask;


    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades];

    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];


    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    ShadowedDirectionalLight[] ShadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];


    public void Setup(
        ScriptableRenderContext context, CullingResults cullingResults,
        ShadowSettings settings
    )
    {
        useShadowMask = false;

        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        ShadowedDirectionalLightCount = 0;
    }

    public void Render()
    {
        if (ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            // For WebGL 2.0 compatability we must add texture for every sampler, even if we dont need them
            buffer.GetTemporaryRT(
                dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
            );
        }

        buffer.BeginSample(bufferName);
        SetKeywords(shadowMaskKeywords, useShadowMask ?
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 :
            -1
        );
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows() 
    {
        int atlasSize = (int)settings.directional.atlasSize;
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);

        buffer.SetRenderTarget(
            dirShadowAtlasId,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );

        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < ShadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
        buffer.SetGlobalVectorArray(
            cascadeCullingSpheresId, cascadeCullingSpheres
        );
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);

        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(
            shadowDistanceFadeId,
            new Vector4(
                1f / settings.maxDistance, 1f / settings.distanceFade,
                1f / (1f - f * f)
            )
        );


        SetKeywords(
            directionalFilterKeywords, (int)settings.directional.filter - 1
        );
        SetKeywords(
            cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1
        );

        buffer.SetGlobalVector(
            shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize)
        );
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex) 
    {
        if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f)
        {
            float maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            if (!cullingResults.GetShadowCasterBounds(
                visibleLightIndex, out Bounds b
            ))
            {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }

            ShadowedDirectionalLights[ShadowedDirectionalLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            return new Vector4(
                light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++,
                light.shadowNormalBias, maskChannel
            );
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSettings =
            new ShadowDrawingSettings(cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Orthographic);

        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor =
            Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

        for (int i = 0; i < cascadeCount; i++)
        {

            /*
             * The first argument is the visible light index. The next three arguments are two integers and a Vector3, which control the shadow cascade. We'll deal with cascades later, so for now use zero, one, and the zero vector. After that comes the texture size, for which we need to use the tile size. The sixth argument is the shadow near plane, which we'll ignore and set to zero for now.
                Those were the input arguments, the remaining three are output arguments. First is the view matrix, then the projection matrix, and the last argument is a ShadowSplitData struct.
            */
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );

            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = projectionMatrix * viewMatrix;

            /// splitData.shadowCascadeBlendCullingFactor cull some shadow casters from the larger cascades,
            /// if the will be covered in smaller cascade.
            /// The value is a factor that modulates the radius of the previous cascade used to 
            /// perform the culling.
            /// Unity is fairly conservative when culling, but we should decrease it by the cascade 
            /// fade ratio and a little extra to make sure that shadow casters in the transition region 
            /// never get culled.
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            if (index == 0)
            {
                cascadeCullingSpheres[i] = splitData.cullingSphere;
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            Vector4 cullingSphere = splitData.cullingSphere;
            cullingSphere.w *= cullingSphere.w;
            cascadeCullingSpheres[i] = cullingSphere;

            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), split
            );
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);


            /// Constant depth bias remove all shadow acne, but pushes shadow casters away from the light, 
            /// causing visual artifacts known as Peter-Panning.
            //buffer.SetGlobalDepthBias(500000f, 0f);

            ///An alternative approach is to apply a slope-scale bias, which is done by using a 
            ///nonzero value for the second argument of SetGlobalDepthBias. 
            ///This value is used to scale the highest of the absolute clip-space depth derivative along 
            ///the X and Y dimensions. So it is zero for surfaces that are lit head-on, 
            ///it's 1 when the light hits at a 45° angle in at least one of the two dimensions, 
            ///and approaches infinity when the dot product of the surface normal and light direction 
            ///reaches zero. So the bias increases automatically when more is needed, 
            ///but there's no upper bound. As a result a much lower factor is needed to eliminate acne, 
            ///for example 3 instead of 500000.
            //buffer.SetGlobalDepthBias(0f, 3f);

            // Set light slope-scale bias using their existing Bias slider
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }


    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[index] = cullingSphere;
        cascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            filterSize * 1.4142136f
        );
    }

    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
        ));
        return offset;
    }

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }
}
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class Shadows
{


    const int maxShadowedDirectionalLightCount = 4, maxShadowedOtherLightCount = 16;
    const int maxCascades = 4;


    const string bufferName = "Shadows";

    const string dirShadowsSampleName = "Directional Shadows";
    const string otherShadowsSampleName = "Other Shadows";

    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"),
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        cascadeDataId = Shader.PropertyToID("_CascadeData"),
        shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");


    // In RenderGraph use TextureHandle instead of direct texture ref
    TextureHandle directionalAtlas, otherAtlas;


    static string[] directionalFilterKeywords = 
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    static string[] otherFilterKeywords = 
    {
        "_OTHER_PCF3",
        "_OTHER_PCF5",
        "_OTHER_PCF7",
    };

    static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };


    static string[] shadowMaskKeywords = 
    {
        "_SHADOW_MASK_ALWAYS",
        "_SHADOW_MASK_DISTANCE"
    };
    bool useShadowMask;


    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades],
        cascadeData = new Vector4[maxCascades],
        otherShadowTiles = new Vector4[maxShadowedOtherLightCount];

    static Matrix4x4[]
        dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];


    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }


    ShadowedDirectionalLight[] shadowedDirectionalLights =
        new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    ShadowedOtherLight[] shadowedOtherLights =
        new ShadowedOtherLight[maxShadowedOtherLightCount];


    // --- Moved to RenderGraph --- 
    /*CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };*/
    CommandBuffer buffer;

    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings settings;

    int shadowedDirLightCount, shadowedOtherLightCount;
    /// <summary>
    /// In xy directional lights atlas size, in zw other lights atlas size
    /// </summary>
    Vector4 atlasSizes;

    public void Setup
    (
        /*ScriptableRenderContext*/ /*RenderGraphContext context,*/ CullingResults cullingResults, 
        ShadowSettings settings
    )
    {
        useShadowMask = false;


        //this.context = context;

        //buffer = context.cmd;
        //this.context = context.renderContext;
        this.cullingResults = cullingResults;
        this.settings = settings;

        shadowedDirLightCount = shadowedOtherLightCount = 0;
    }


    public ShadowTextures GetRenderTextures(RenderGraph renderGraph, RenderGraphBuilder builder)
    {
        int atlasSize = (int)settings.directional.atlasSize;
        var desc = new TextureDesc(atlasSize, atlasSize)
        {
            depthBufferBits = DepthBits.Depth32,
            // isShadowMap = true avoids the allocation of a stencil buffer.
            isShadowMap = true,
            name = "Directional Shadow Atlas"
        };
        directionalAtlas = shadowedDirLightCount > 0 ?
            builder.WriteTexture(renderGraph.CreateTexture(desc)) :
            renderGraph.defaultResources.defaultShadowTexture;

        atlasSize = (int)settings.other.atlasSize;
        desc.width = desc.height = atlasSize;
        desc.name = "Other Shadow Atlas";
        otherAtlas = shadowedOtherLightCount > 0 ?
                builder.WriteTexture(renderGraph.CreateTexture(desc)) :
                renderGraph.defaultResources.defaultShadowTexture;
        return new ShadowTextures(directionalAtlas, otherAtlas);
    }

    public void Render(RenderGraphContext context)
    {
        buffer = context.cmd;
        this.context = context.renderContext;

        if (shadowedDirLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        // Handle missing texture in texture handles
        /*else
        {
            // For WebGL 2.0 compatability we must add texture for every sampler, even if we dont need them
            buffer.GetTemporaryRT
            (
                dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
            );
        }*/
        if (shadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }
        /*else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }*/

        buffer.SetGlobalTexture(dirShadowAtlasId, directionalAtlas);
        buffer.SetGlobalTexture(otherShadowAtlasId, otherAtlas);

        //buffer.BeginSample(bufferName);
        SetKeywords
        (
            shadowMaskKeywords, useShadowMask ?
            QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 :
            -1
        );
        buffer.SetGlobalInt
        (
            cascadeCountId,
            shadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0
        );
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector
        (
            shadowDistanceFadeId, new Vector4
            (
                1f / settings.maxDistance, 1f / settings.distanceFade,
                1f / (1f - f * f)
            )
        );
        buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
        //buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows() 
    {
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        /*buffer.GetTemporaryRT
        (
            dirShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
        );*/
        buffer.SetRenderTarget
        (
            //dirShadowAtlasId,
            directionalAtlas,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 1f); // Turn on pancaking
        buffer.BeginSample(dirShadowsSampleName);
        ExecuteBuffer();

        int tiles = shadowedDirLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedDirLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        buffer.SetGlobalVectorArray
        (
            cascadeCullingSpheresId, cascadeCullingSpheres
        );
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
        SetKeywords
        (
            directionalFilterKeywords, (int)settings.directional.filter - 1
        );
        SetKeywords
        (
            cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1
        );
        buffer.EndSample(dirShadowsSampleName);
        ExecuteBuffer();
    }
    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;

        /*buffer.GetTemporaryRT
        (
            otherShadowAtlasId, atlasSize, atlasSize,
            32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
        );*/

        buffer.SetRenderTarget
        (
            //otherShadowAtlasId,
            otherAtlas,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
        );

        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.SetGlobalFloat(shadowPancakingId, 0f); // Turn off pancaking
        buffer.BeginSample(otherShadowsSampleName);
        ExecuteBuffer();

        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;

        for (int i = 0; i < shadowedOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }

        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
        SetKeywords
        (
            otherFilterKeywords, (int)settings.other.filter - 1
        );
        buffer.EndSample(otherShadowsSampleName);
        ExecuteBuffer();
    }

    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        if (shadowedOtherLightCount > 0)
        {
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        }
        ExecuteBuffer();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex) 
    {
        if 
        (
            shadowedDirLightCount < maxShadowedDirectionalLightCount &&
            light.shadows != LightShadows.None && light.shadowStrength > 0f
        )
        {
            float maskChannel = -1;
            LightBakingOutput lightBaking = light.bakingOutput;
            if 
            (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            if 
            (
                !cullingResults.GetShadowCasterBounds
                (
                    visibleLightIndex, out Bounds b
                )
            )
            {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }

            shadowedDirectionalLights[shadowedDirLightCount] =
                new ShadowedDirectionalLight
                {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    nearPlaneOffset = light.shadowNearPlane
                };
            return new Vector4
            (
                light.shadowStrength, settings.directional.cascadeCount * shadowedDirLightCount++,
                light.shadowNormalBias, maskChannel
            );
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }
    /// <summary>
    /// Reserve shadow mask, only 4 light per texel allowed. If >4 force less important to be baked
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    /// <returns></returns>
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f); // mask -1 signal no realtime shadows
        }

        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        if 
        (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
        )
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        bool isPoint = light.type == LightType.Point;
        int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
        if 
        (
            newLightCount > maxShadowedOtherLightCount ||
            !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
        )
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };

        Vector4 data = new Vector4
        (
            light.shadowStrength, shadowedOtherLightCount,
            isPoint ? 1f : 0f, maskChannel
        );
        shadowedOtherLightCount = newLightCount;
        return data;

    }


    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = shadowedDirectionalLights[index];
        var shadowSettings =
            new ShadowDrawingSettings
            (
                cullingResults, light.visibleLightIndex,
                BatchCullingProjectionType.Orthographic
            )
            {
                useRenderingLayerMaskTest = true
            };
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = settings.directional.CascadeRatios;
        float cullingFactor =
            Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
        float tileScale = 1f / split;

        for (int i = 0; i < cascadeCount; i++)
        {

            /*
             * The first argument is the visible light index. The next three arguments are two integers and a Vector3, which control the shadow cascade. We'll deal with cascades later, so for now use zero, one, and the zero vector. After that comes the texture size, for which we need to use the tile size. The sixth argument is the shadow near plane, which we'll ignore and set to zero for now.
                Those were the input arguments, the remaining three are output arguments. First is the view matrix, then the projection matrix, and the last argument is a ShadowSplitData struct.
            */
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives
            (
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );


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

            int tileIndex = tileOffset + i;
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix
            (
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), tileScale
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
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings
        (
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective
        );
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives
        (
            light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
        );
        shadowSettings.splitData = splitData;

        ///In world space, at distance 1 from the light plane the size of the shadow tile is twice the tangent of half the spot angle in radians. We need it for normal bias scaling to prevent shadow acne
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        Vector2 offset = SetTileViewport(index, split, tileSize);
        float tileScale = 1f / split;
        SetOtherTileData(index, offset, tileScale, bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrix
        (
            projectionMatrix * viewMatrix, offset, tileScale
        );

        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }
    /// <summary>
    /// For point map shadows, we rander a cubemap with depth
    /// </summary>
    /// <param name="index"></param>
    /// <param name="split"></param>
    /// <param name="tileSize"></param>
    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings
        (
            cullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective
        );

        // Fow of cubemap always 90. This means that we can hoist the calculation of the bias out of the loop.
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (int i = 0; i < 6; i++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives
            (
                light.visibleLightIndex, (CubemapFace)i, fovBias,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowSettings.splitData = splitData;
            int tileIndex = index + i;
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            SetOtherTileData(tileIndex, offset, tileScale, bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix
            (
                projectionMatrix * viewMatrix, offset, tileScale
            );

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
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
        cascadeData[index] = new Vector4
        (
            1f / cullingSphere.w,
            filterSize * 1.4142136f
        );
    }

    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport
        (
            new Rect
            (
                offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
            )
        );
        return offset;
    }
    /// <summary>
    /// For other lights (non directional) shadow tile data
    /// </summary>
    /// <param name="index"></param>
    /// <param name="bias"></param>
    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
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

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
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
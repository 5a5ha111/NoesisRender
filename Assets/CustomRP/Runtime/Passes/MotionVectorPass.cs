using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using static UnityEngine.Experimental.Rendering.RayTracingAccelerationStructure;

public class MotionVectorPass
{

    static readonly ProfilingSampler sampler = new("MotionVectors");

    private static readonly ShaderTagId m_PassName = new ShaderTagId("SRP0703_Pass"); //The shader pass tag just for SRP0703

    static readonly ShaderTagId[] shaderTagIds =
    {
        new("SRPDefaultUnlit"),
        new("CustomLit"),
        new("Always"),
        new("ForwardBase"),
        new("PrepassBase"),
        new("Vertex"),
        new("VertexLMRGBM"),
        new("VertexLM")
    };

    Camera camera;

    private Matrix4x4 _NonJitteredVP;
    private Matrix4x4 _PreviousVP;
    Material motionMaterial;
    RendererListHandle list;
    RendererListParams rlp;
    TextureHandle motionTexture;
    TextureHandle motionDepthTexture;
    TextureHandle colorSource;
    TextureHandle depthSource;

    static Mesh s_FullscreenMesh = null;
    public static Mesh fullscreenMesh
    {
        get
        {
            if (s_FullscreenMesh != null)
                return s_FullscreenMesh;

            float topV = 1.0f;
            float bottomV = 0.0f;

            s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
            s_FullscreenMesh.SetVertices(new List<Vector3>
            {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f,  1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
                new Vector3(1.0f,  1.0f, 0.0f)
            });

            s_FullscreenMesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0.0f, bottomV),
                new Vector2(0.0f, topV),
                new Vector2(1.0f, bottomV),
                new Vector2(1.0f, topV)
            });

            s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
            s_FullscreenMesh.UploadMeshData(true);
            return s_FullscreenMesh;
        }
    }


    void Render(RenderGraphContext context)
    {
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();

        CommandBuffer cmd = context.cmd;

        cmd.SetRenderTarget(motionTexture, motionDepthTexture); //Set CameraTarget to the motion vector texture
        cmd.ClearRenderTarget(true, true, Color.black);
        

        //RendererList rl = context.renderContext.CreateRendererList(ref rlp);
        cmd.DrawRendererList(list);
        cmd.SetGlobalTexture("_CameraMotionDepthTexture", motionDepthTexture);

        _NonJitteredVP = camera.nonJitteredProjectionMatrix * camera.worldToCameraMatrix;
        cmd.SetGlobalMatrix("_CamPrevViewProjMatrix", _PreviousVP);
        cmd.SetGlobalMatrix("_CamNonJitteredViewProjMatrix", _NonJitteredVP);
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, motionMaterial, 0, 1, null); // draw full screen quad to make Camera motion
        //cmd.DrawProcedural(Matrix4x4.identity, motionMaterial, 1, MeshTopology.Triangles, 3); // draw full screen to make Camera motion
        cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();

        _PreviousVP = _NonJitteredVP;

        /*context.renderContext.SetupCameraProperties(camera);
        cmd.SetRenderTarget
        (
            colorSource,
            RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
            depthSource,
            RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
        );
        _PreviousVP = _NonJitteredVP;

        context.renderContext.ExecuteCommandBuffer(cmd);
        cmd.Clear();*/
    }

    public static void Record(RenderGraph renderGraph, Camera camera, in CameraRendererTextures textures, CameraBufferSettings settings, Material materialMotion, int renderingLayerMask, CullingResults cullingResults)
    {
        using RenderGraphBuilder builder = renderGraph.AddRenderPass
        (
            sampler.name, out MotionVectorPass pass, sampler
        );
        pass.camera = camera;
        pass.motionMaterial = materialMotion;
        /*builder.ReadWriteTexture(textures.colorAttachment);
        builder.ReadTexture(textures.depthAttachment);*/
        /*pass.colorSource = builder.ReadWriteTexture(textures.colorAttachment);
        pass.depthSource = builder.ReadWriteTexture(textures.depthAttachment);*/
        pass.depthSource = builder.ReadTexture(textures.depthAttachment);
        pass.motionTexture = builder.ReadWriteTexture(textures.motionVectorsTexture);
        pass.motionDepthTexture = builder.ReadWriteTexture(textures.motionVectorDepth);

        var sortingSettings = new SortingSettings(camera);
        DrawingSettings drawSettingsMotionVector = new DrawingSettings(m_PassName, sortingSettings)
        {
            perObjectData = PerObjectData.MotionVectors,
            overrideMaterial = materialMotion,
            overrideMaterialPassIndex = 0
        };
        FilteringSettings filterSettingsMotionVector = new FilteringSettings(RenderQueueRange.opaque)
        {
            excludeMotionVectorObjects = false
        };

        //CustomSRPUtil.RenderObjects("Render Opaque Objects Motion Vector", context, cullingResults, filterSettingsMotionVector, drawSettingsMotionVector);

        pass.list = builder.UseRendererList
        (
            renderGraph.CreateRendererList
            (
                new RendererListDesc(shaderTagIds, cullingResults, camera)
                {
                    sortingCriteria = SortingCriteria.CommonOpaque,
                    rendererConfiguration = PerObjectData.MotionVectors,
                    renderQueueRange = RenderQueueRange.opaque,
                    renderingLayerMask = (uint)renderingLayerMask,
                    overrideMaterial = materialMotion,
                    overrideMaterialPassIndex = 0,
                    excludeObjectMotionVectors = false
                }
            )
        );

        pass.rlp = new RendererListParams(cullingResults, drawSettingsMotionVector, filterSettingsMotionVector);
        //RendererList rl = context.CreateRendererList(ref rlp);


        builder.SetRenderFunc<MotionVectorPass>
        (
            static (pass, context) => pass.Render(context)
        );
    }


}

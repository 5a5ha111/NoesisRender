#define RtHandleGbuffer

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace NoesisRender.ResourcesHolders
{

    // Currently RtHandle system tested to work, but not fully implemented. So it disabled using RtHandleGbuffer define

    public class GBufferResources : IDisposable
    {
        public static GBufferResources _instance = new();

        private static Dictionary<Camera, GBufferTextures> keyValuePairs = new();

        #if !RtHandleGbuffer
        public static GBufferTextures GetGBResources(Camera camera, Vector2Int bufferSize)
        {
            if (keyValuePairs == null)
            {
                keyValuePairs = new();
            }

            // Potentially, for less Dispose calls in games with dynamic resolution we can trade some memory for storing several unused GBuffers. But it will be just a waste for games without dynamic resolution.
            if (keyValuePairs.ContainsKey(camera))
            {
                GBufferTextures resource = keyValuePairs[camera];
                if (resource != null)
                {
                    if (resource.ValideBuffer(bufferSize))
                    {
                        return resource;
                    }
                    else
                    {
                        resource.Dispose();
                        resource = new GBufferTextures(camera, bufferSize);
                        return resource;
                    }
                }
                else
                {
                    resource = new GBufferTextures(camera, bufferSize);
                    keyValuePairs[camera] = resource;
                    return resource;
                }
            }
            else
            {
                GBufferTextures resource = new GBufferTextures(camera, bufferSize);
                keyValuePairs.Add(camera, resource);
                return resource;
            }
        }

        public void Dispose()
        {
            if (keyValuePairs == null)
            {
                return;
            }
            foreach (var key in keyValuePairs.Keys)
            {
                GBufferTextures resource = keyValuePairs[key];
                if (resource != null)
                {
                    resource.Dispose();
                    resource = null;
                }
            }
            keyValuePairs = null;
        }
        #endif


#if RtHandleGbuffer
        public static GBufferTextures GetGBResources(Camera camera, Vector2Int bufferSize, RTHandleSystem m_RTHandleSystem)
        {
            if (keyValuePairs == null)
            {
                keyValuePairs = new();
            }

            // Potentially, for less Dispose calls in games with dynamic resolution we can trade some memory for storing several unused GBuffers. But it will be just a waste for games without dynamic resolution.
            if (keyValuePairs.ContainsKey(camera))
            {
                GBufferTextures resource = keyValuePairs[camera];
                if (resource != null)
                {
                    if (resource.ValideBuffer(bufferSize))
                    {
                        return resource;
                    }
                    else
                    {
                        resource.Dispose(m_RTHandleSystem);
                        resource = new GBufferTextures(camera, bufferSize, m_RTHandleSystem);
                        return resource;
                    }
                }
                else
                {
                    resource = new GBufferTextures(camera, bufferSize, m_RTHandleSystem);
                    keyValuePairs[camera] = resource;
                    return resource;
                }
            }
            else
            {
                GBufferTextures resource = new GBufferTextures(camera, bufferSize, m_RTHandleSystem);
                keyValuePairs.Add(camera, resource);
                return resource;
            }
        }
        [Obsolete]
        public void Dispose()
        {
            Debug.LogWarning("This is incorrect dispose! Dispose object using Dispose(RTHandleSystem m_RTHandleSystem)");
        }

        public void Dispose(RTHandleSystem m_RTHandleSystem)
        {
            if (keyValuePairs == null)
            {
                return;
            }
            foreach (var key in keyValuePairs.Keys)
            {
                GBufferTextures resource = keyValuePairs[key];
                if (resource != null)
                {
                    resource.Dispose(m_RTHandleSystem);
                    resource = null;
                }
            }
            keyValuePairs = null;
        }
#endif


        public class GBufferTextures : IDisposable
        {

            public const int amountOfGBuffers = 4;
            private const int gBufferDepth = (int)DepthBits.None;
            RenderTexture[] gbuffers = new RenderTexture[amountOfGBuffers];
            RTHandle[] gbuffersRtHandle = new RTHandle[amountOfGBuffers];
            readonly RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[amountOfGBuffers];
            private Vector2Int bufferSize = new Vector2Int();

            public RenderTargetIdentifier[] _getTargets { get { return gbufferID; } }
            public RenderTexture[] _getTextures { get { return gbuffers; } }
            public RTHandle[] _getRTHandles { get { return gbuffersRtHandle; } }

            #if !RtHandleGbuffer
            public GBufferTextures(Camera camera, Vector2Int bufferSize)
            {
                this.bufferSize = bufferSize;

                // By some reson, Unity dont want use multiple TextureHandle in cmd.SetRenderTarget(RenderTargetIdentifier[])
                // So, if we want in one drawCall fill all GBuffers, we must stick to RenderTexture
                this.gbuffers[0] = new RenderTexture(bufferSize.x, bufferSize.y, gBufferDepth, RenderTextureFormat.RGB565, RenderTextureReadWrite.Linear);
                this.gbuffers[0].name = "GBuffer0 (RGB color)";
                this.gbuffers[0].Create();
                this.gbufferID[0] = gbuffers[0];

                this.gbuffers[1] = new RenderTexture(bufferSize.x, bufferSize.y, gBufferDepth, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                this.gbuffers[1].name = "GBuffer1 RGB normal A smoothness";
                this.gbuffers[1].Create();
                this.gbufferID[1] = gbuffers[1];

                this.gbuffers[2] = new RenderTexture(bufferSize.x, bufferSize.y, gBufferDepth, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                // We want to pack positionWS relative to camera, bocause we want to avoid precission errors of 16 bit float
                this.gbuffers[2].name = "GBuffer2 RGB positionWS relative to camera A occlusion";
                this.gbuffers[2].Create();
                this.gbufferID[2] = gbuffers[2];

                this.gbuffers[3] = new RenderTexture(bufferSize.x, bufferSize.y, gBufferDepth, RenderTextureFormat.DefaultHDR, RenderTextureReadWrite.Linear);
                this.gbuffers[3].name = "GBuffer3 RGB emission A metallic";
                this.gbuffers[3].Create();
                this.gbufferID[3] = gbuffers[3];
            }
            #endif
            public RenderTexture GetNormalBuffer()
            {
                if (gbuffers == null || gbuffers.Length == 0)
                {
                    return null;
                }
                return this.gbuffers[1];
            }

            public bool ValideBuffer(Vector2Int bufferSize)
            {
                if (this.bufferSize != bufferSize || gbuffers.Length == 0)
                {
                    return false;
                }
                return true;
            }
            #if !RtHandleGbuffer
            public void Dispose()
            {
                for (int i = 0; i < gbuffers.Length; i++)
                {
                    CoreUtils.Destroy(gbuffers[i]);
                }
                gbuffers = new RenderTexture[0];
            }
            #endif

#if RtHandleGbuffer
            public GBufferTextures(Camera camera, Vector2Int bufferSize, RTHandleSystem rTHandleSystem)
            {
                this.bufferSize = bufferSize;

                //TextureFormat format = TextureFormat.RGBAHalf;
                //var support = SystemInfo.SupportsTextureFormat(format);
                //Debug.Log("support " + support);

                // By some reson, Unity dont want use multiple TextureHandle in cmd.SetRenderTarget(RenderTargetIdentifier[])
                // So, if we want in one drawCall fill all GBuffers, we must stick to RenderTexture
                /*this.gbuffers[0] = new RenderTexture(bufferSize.x, bufferSize.y, gBufferDepth, RenderTextureFormat.RGB565, RenderTextureReadWrite.Linear);
                this.gbuffers[0].name = "GBuffer0 (RGB color)";
                this.gbuffers[0].Create();
                this.gbufferID[0] = gbuffers[0];*/
                //gbuffersRtHandle[0] = rTHandleSystem.Alloc(this.gbuffers[0]);

                GraphicsFormat colorFormat;
                DepthBits depth = DepthBits.None;
                if (SystemInfo.IsFormatSupported(GraphicsFormat.B5G6R5_UNormPack16, FormatUsage.Render))
                {
                    colorFormat = GraphicsFormat.B5G6R5_UNormPack16;
                }
                else
                {
                    colorFormat = GraphicsFormat.R8G8B8A8_SRGB;
                }

                gbuffersRtHandle[0] = rTHandleSystem.Alloc(bufferSize.x, bufferSize.y, 1, depth, colorFormat, name: "GBuffer0 RTHandle (RGB color)");
                this.gbuffers[0] = gbuffersRtHandle[0].rt;
                this.gbufferID[0] = gbuffersRtHandle[0].rt;

                gbuffersRtHandle[1] = rTHandleSystem.Alloc(bufferSize.x, bufferSize.y, 1, depth, GraphicsFormat.R16G16B16A16_SFloat, name: "GBuffer1 RGB normal A smoothness");
                this.gbuffers[1] = gbuffersRtHandle[1].rt;
                this.gbufferID[1] = gbuffers[1];

                gbuffersRtHandle[2] = rTHandleSystem.Alloc(bufferSize.x, bufferSize.y, 1, depth, GraphicsFormat.R16G16B16A16_SFloat, name: "GBuffer2 RGB posWS rel cam A occlusion");
                this.gbuffers[2] = gbuffersRtHandle[2];
                this.gbufferID[2] = gbuffers[2];

                gbuffersRtHandle[3] = rTHandleSystem.Alloc(bufferSize.x, bufferSize.y, 1, depth, GraphicsFormat.R16G16B16A16_SFloat, name: "GBuffer3 RGB emission A metallic");
                this.gbuffers[3] = gbuffersRtHandle[3].rt;
                this.gbufferID[3] = gbuffers[3];

                //Debug.Log("New resources in rTHandleSystem " + camera.name + " " + bufferSize);
            }

            [Obsolete]
            public void Dispose()
            {
                Debug.LogWarning("This is incorrect dispose! Dispose object using Dispose(RTHandleSystem m_RTHandleSystem)");
            }
            public void Dispose(RTHandleSystem m_RTHandleSystem)
            {
                for (int i = 0; i < gbuffers.Length; i++)
                {
                    m_RTHandleSystem.Release(gbuffersRtHandle[i]);
                    CoreUtils.Destroy(gbuffers[i]);
                }
                gbuffers = new RenderTexture[0];
                //Debug.Log("Dispose " + this.bufferSize);
            }
#endif
        }
    }

}

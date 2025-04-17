using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace NoesisRender.ResourcesHolders
{
    public class GBufferResources : IDisposable
    {
        public static GBufferResources _instance = new();

        private static Dictionary<Camera, GBufferTextures> keyValuePairs = new();

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

        public class GBufferTextures : IDisposable
        {
            // Current GBuffer alligment:
            /*
            GB0
                rgb color
                a metallic

            GB1
                r smoothness
                g normalX
                b normalY
                a occlusion

            GB2
                rg positionWS
                b fresnelStrength
                a renderingLayerMask asuint(unity_RenderingLayer.x)

            DepthAttach (in setupPass)
                r depth(posZ in view space)
            * 
            * 
            */

            public const int amountOfGBuffers = 4;
            private const int gBufferDepth = (int)DepthBits.None;
            RenderTexture[] gbuffers = new RenderTexture[amountOfGBuffers];
            readonly RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[amountOfGBuffers];
            private Vector2Int bufferSize = new Vector2Int();

            public RenderTargetIdentifier[] _getTargets { get { return gbufferID; } }
            public RenderTexture[] _getTextures { get { return gbuffers; } }

            public GBufferTextures(Camera camera, Vector2Int bufferSize)
            {
                this.bufferSize = bufferSize;

                //TextureFormat format = TextureFormat.RGBAHalf;
                //var support = SystemInfo.SupportsTextureFormat(format);
                //Debug.Log("support " + support);

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

                //Debug.Log("New resources " + camera.name + " " + bufferSize);
            }

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
                    //Debug.Log("this.bufferSize " + this.bufferSize + " valid " + bufferSize);
                    return false;
                }
                return true;
            }

            public void Dispose()
            {
                for (int i = 0; i < gbuffers.Length; i++)
                {
                    CoreUtils.Destroy(gbuffers[i]);
                }
                gbuffers = new RenderTexture[0];
                //Debug.Log("Dispose " + this.bufferSize);
            }
        }
    }

}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace NoesisRender.ResourcesHolders
{
    public class XeGTAOResources
    {
        public static XeGTAOResources _instance = new();

        private static Dictionary<Camera, XeGTAOTextures> keyValuePairs = new();

        public static XeGTAOTextures GetGTAOesources(Camera camera, Vector2Int bufferSize)
        {
            if (keyValuePairs == null)
            {
                keyValuePairs = new();
            }

            // Potentially, for less Dispose calls in games with dynamic resolution we can trade some memory for storing several unused GBuffers. But it will be just a waste for games without dynamic resolution.
            if (keyValuePairs.ContainsKey(camera))
            {
                XeGTAOTextures resource = keyValuePairs[camera];
                if (resource != null)
                {
                    if (resource.ValideBuffer(bufferSize))
                    {
                        return resource;
                    }
                    else
                    {
                        resource.Dispose();
                        resource = new XeGTAOTextures(camera, bufferSize);
                        return resource;
                    }
                }
                else
                {
                    resource = new XeGTAOTextures(camera, bufferSize);
                    keyValuePairs[camera] = resource;
                    return resource;
                }
            }
            else
            {
                XeGTAOTextures resource = new XeGTAOTextures(camera, bufferSize);
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
                XeGTAOTextures resource = keyValuePairs[key];
                if (resource != null)
                {
                    resource.Dispose();
                    resource = null;
                }
            }
            keyValuePairs = null;
        }

        public class XeGTAOTextures : IDisposable
        {

            private const int amountOfGTempTex = 1;
            private const int gBufferDepth = (int)DepthBits.None;
            RenderTexture[] tempTexs = new RenderTexture[amountOfGTempTex];
            //readonly RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[amountOfGTempTex];
            private Vector2Int bufferSize = new Vector2Int();
            public RenderTexture[] _getTextures { get { return tempTexs; } }

            public XeGTAOTextures(Camera camera, Vector2Int bufferSize)
            {
                this.bufferSize = bufferSize;


                RenderTextureDescriptor depthbufferDesc = new RenderTextureDescriptor(bufferSize.x, bufferSize.y, RenderTextureFormat.RHalf);
                depthbufferDesc.mipCount = 5;
                depthbufferDesc.autoGenerateMips = false;
                depthbufferDesc.useMipMap = true;
                depthbufferDesc.depthBufferBits = gBufferDepth;
                depthbufferDesc.enableRandomWrite = true;
                //depthbufferDesc.memoryless = RenderTextureMemoryless.Color;
                //depthbufferDesc.useDynamicScale = true;

                this.tempTexs[0] = new RenderTexture(depthbufferDesc);
                this.tempTexs[0].name = "Temp viewSpace depth";
                this.tempTexs[0].Create();
            }

            public RenderTexture GetNormalBuffer()
            {
                if (tempTexs == null || tempTexs.Length == 0)
                {
                    return null;
                }
                return this.tempTexs[1];
            }

            public bool ValideBuffer(Vector2Int bufferSize)
            {
                if (this.bufferSize != bufferSize || tempTexs.Length == 0)
                {
                    //Debug.Log("this.bufferSize " + this.bufferSize + " valid " + bufferSize);
                    return false;
                }
                return true;
            }

            public void Dispose()
            {
                for (int i = 0; i < tempTexs.Length; i++)
                {
                    CoreUtils.Destroy(tempTexs[i]);
                }
                tempTexs = new RenderTexture[0];
                //Debug.Log("Dispose " + this.bufferSize);
            }
        }
    }

}

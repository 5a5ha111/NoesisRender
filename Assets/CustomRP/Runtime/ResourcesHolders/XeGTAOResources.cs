#define RtHandleSystem

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;


namespace NoesisRender.ResourcesHolders
{
    public class XeGTAOResources
    {
        public static XeGTAOResources _instance = new();

        private static Dictionary<Camera, XeGTAOTextures> keyValuePairs = new();

        #if !RtHandleSystem
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
        #else

        public static XeGTAOTextures GetGTAOesources(Camera camera, Vector2Int bufferSize, RTHandleSystem m_RTHandleSystem)
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
                        resource.Dispose(m_RTHandleSystem);
                        resource = new XeGTAOTextures(camera, bufferSize, m_RTHandleSystem);
                        return resource;
                    }
                }
                else
                {
                    resource = new XeGTAOTextures(camera, bufferSize, m_RTHandleSystem);
                    keyValuePairs[camera] = resource;
                    return resource;
                }
            }
            else
            {
                XeGTAOTextures resource = new XeGTAOTextures(camera, bufferSize, m_RTHandleSystem);
                keyValuePairs.Add(camera, resource);
                return resource;
            }
        }

        [Obsolete]
        public void Dispose()
        {
            Debug.LogWarning("This is incorrect dispose! Dispose object using Dispose(RTHandleSystem m_RTHandleSystem)");
        }
        public void Dispose(RTHandleSystem rTHandleSystem)
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
                    resource.Dispose(rTHandleSystem);
                    resource = null;
                }
            }
            keyValuePairs = null;
        }


#endif

        public class XeGTAOTextures : IDisposable
        {

            private const int amountOfGTempTex = 1;
            private const int gBufferDepth = (int)DepthBits.None;
            RenderTexture[] tempTexs = new RenderTexture[amountOfGTempTex];
            RTHandle[] gbuffersRtHandle = new RTHandle[amountOfGTempTex];
            //readonly RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[amountOfGTempTex];
            private Vector2Int bufferSize = new Vector2Int();
            public RenderTexture[] _getTextures { get { return tempTexs; } }

            #if !RtHandleSystem
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

            #else

            public XeGTAOTextures(Camera camera, Vector2Int bufferSize, RTHandleSystem rTHandleSystem)
            {
                this.bufferSize = bufferSize;


                RenderTextureDescriptor depthbufferDesc = new RenderTextureDescriptor(bufferSize.x, bufferSize.y, RenderTextureFormat.RHalf);
                depthbufferDesc.mipCount = 5;
                depthbufferDesc.autoGenerateMips = false;
                depthbufferDesc.useMipMap = true;
                depthbufferDesc.depthBufferBits = gBufferDepth;
                depthbufferDesc.enableRandomWrite = true;
                
                gbuffersRtHandle[0] = rTHandleSystem.Alloc(bufferSize.x, bufferSize.y, 1, gBufferDepth, GraphicsFormat.R16_SFloat, name: "Temp viewSpace depth", useMipMap:true);
                tempTexs[0] = gbuffersRtHandle[0].rt;
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

            [Obsolete]
            public void Dispose()
            {
                Debug.LogWarning("This is incorrect dispose! Dispose object using Dispose(RTHandleSystem m_RTHandleSystem)");
            }
            public void Dispose(RTHandleSystem m_RTHandleSystem)
            {
                for (int i = 0; i < tempTexs.Length; i++)
                {
                    m_RTHandleSystem.Release(gbuffersRtHandle[i]);
                    CoreUtils.Destroy(tempTexs[i]);
                }
                tempTexs = new RenderTexture[0];
                //Debug.Log("Dispose " + this.bufferSize);
            }

#endif
        }
    }

}

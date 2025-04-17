using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.NVIDIA;
using Math = System.Math;

#if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
using NVIDIA = UnityEngine.NVIDIA;
#endif


namespace NoesisRender.UnityDLSS
{
    public class UnityDlssCommon 
    {


        internal static bool IsDlssSupported() 
        {

            
#if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE
            if (device == null) 
            {
                return false;
            }
            return device.IsFeatureAvailable(NVIDIA.GraphicsDeviceFeature.DLSS);
#else
            return false;
#endif
        }



        internal static float prevMipBias;
        internal static float statMipBias;
        internal static bool statUseMipBias;
        internal static bool statDontOverrideMips;


        [System.Serializable]
        public class DlssSettings 
        {

#if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE

            [Header("DLSS Settings")]

            [Tooltip("If disabled, will output bilinear upscale instead. Useful mostly for debugging.")]
            public bool enableDlss = true;


#if !DLSS_URP
            [Tooltip("Attempts to bias mip maps to allow sharper texture detail in upscaling.\n " +
                "This is pretty important for textures to look good with DLSS, but since the built in render pipeline does not support a global mip map bias, this setting is a workaround to enable a bias for all currently loaded textures.\n" +
                "This has a drawback of being persistent when set within the editor, and needs to be manually updated if you load textures outside of a scene load.\n" +
                "The changes to mip bias of textures is not permanent and will be reset to what it was after you reload the project.")]
            public bool useMipBias = false;
#else
            [Tooltip("Biases mip maps to allow sharper texture detail in upscaling.\n" +
                "This is pretty important for textures to look good with DLSS. Completely custom shaders in URP need to use the _GlobalMipBias property for this to work.")]
            public bool useMipBias = true;
#endif

#if !DLSS_URP
            [Tooltip("Will attempt to not change mip bias if you have manually set a bias on a texture yourself, but may not work if your manual bias is exactly the same as the DLSS requested bias.\n" +
                "But again, these changes are not permanent after you reload your project.")]
            /*[DisplayIf(nameof(useMipBias))]*/ public bool dontOverrideMipBias = false;
#endif

            [Tooltip("Whether to use motion vectors or not. DLSS doesn't work well without at least *some* motion vectors.\n" +
                "If certain things don't have them it can be alright, like grass moving in the wind. But especially for walls and stuff it's pretty necessary.\n" +
                "I recommend only to disable these for debugging.")]
            public bool useMotionVectors = true;

            [Tooltip("If true, will use the base resolution and sharpness settings recommended by DLSS. Disable this to manually set a scaling ratio.")]
            public bool useOptimalSettings = true;

            [Tooltip("Quality setting for automatic optimal DLSS settings.")]
            /*[DisplayIf(nameof(useOptimalSettings))]*/ public NVIDIA.DLSSQuality dlssQuality = NVIDIA.DLSSQuality.MaximumQuality;

            [Tooltip("Manual viewport scaling ratio.")]
            /*[DisplayIf(nameof(useOptimalSettings), false)]*/ public float viewportMult = 0.5f;

            [Tooltip("DLSS sharpening amount.")]
            /*[DisplayIf(nameof(useOptimalSettings), false)]*/ public float sharpness = 0.5f;


            [Header("Jitter")]
            [Tooltip("Wether to enable jitter. This should always be left on as DLSS is uselss without it pretty much. Useful for debugging/experimenting.")]
            public bool useJitter = true;
            [Tooltip("Scale of jitter to wiggle the camera for the temporal aspect of DLSS. This is best left at 1. Fun to experiment with.")]
            public float jitterScale = 1;
            [Tooltip("Additional slight randomness to jitter. At 0.1 this adds a slight imperceptable shimmering in the pixels but eliminates a lot of artifacts when the camera is still and looking at a complex pattern.\n" +
                "Also improves readability. Disable only if you feel like it's adding too much shimmering.")]
            public float jitterRand = 0.1f;


#endif
        }


#if ENABLE_NVIDIA && ENABLE_NVIDIA_MODULE

        internal static float CalculateMipBias(int inputWidth, int outputWidth, bool clear = false) 
        {
            if (!statUseMipBias || clear)
                return statMipBias = 0.0f;

            return statMipBias = Mathf.Log(inputWidth / (float)outputWidth, 2f);
        }

        internal static void RecreateOutTex(ref RenderTexture outputTex, int width, int height, RenderTextureFormat format) 
        {
            if (!outputTex) 
            {
                outputTex = new RenderTexture(0, 0, 0);
            }
            else 
            {
                outputTex.Release();
            }

            outputTex.name = "DLSS Output";
            outputTex.width = width;
            outputTex.height = height;
            outputTex.filterMode = FilterMode.Point;
            outputTex.useDynamicScale = false;
            outputTex.format = format;
            outputTex.enableRandomWrite = true;
            outputTex.Create();
        }


        internal static void UpdateDlssSettings(ref DlssViewData dlssSettings, out OptimalDLSSSettingsData optSettings, ViewState state, DlssSettings settings) 
        {
            dlssSettings.jitterX = -taaJitter.x;
            dlssSettings.jitterY = -taaJitter.y;

            dlssSettings.perfQuality = settings.dlssQuality;

            int outWidth = dlssSettings.outputRes.width;
            int outHeight = dlssSettings.outputRes.height;

            Rect finalViewport = new Rect(0, 0, outWidth, outHeight);
            //print(finalViewport);


            device.GetOptimalSettings((uint)outWidth, (uint)outHeight, dlssSettings.perfQuality, out optSettings);
            state.RequestUseAutomaticSettings(settings.useOptimalSettings, dlssSettings.perfQuality, finalViewport, in optSettings);


            if (settings.useOptimalSettings) 
            {
                dlssSettings.sharpness = optSettings.sharpness;
            }
            else 
            {
                dlssSettings.sharpness = settings.sharpness;
            }
        }



        //------------------------------------------------------------------------------------------------------------
        //-----------------JITTER-------------------------------------------------------------------------------------
        //------------------------------------------------------------------------------------------------------------

        static float GetHaltonValue(int index, int radix) 
        {
            float result = 0f;
            float fraction = 1f / radix;

            while (index > 0) 
            {
                result += (index % radix) * fraction;

                index /= radix;
                fraction /= radix;
            }

            return result;
        }


        internal static Vector2 taaJitter;

        public static Matrix4x4 GetJitteredProjectionMatrix(DlssSettings settings, Matrix4x4 origProj,
            int width, int height, Camera cam, int frameIndex) 
        {

            if (!settings.useJitter) 
            {
                taaJitter = Vector4.zero;
                return origProj;
            }
#if UNITY_2021_2_OR_NEWER
            if (UnityEngine.FrameDebugger.enabled) 
            {
                taaJitter = Vector4.zero;
                return origProj;
            }
#endif

            // The variance between 0 and the actual halton sequence values reveals noticeable
            // instability in Unity's shadow maps, so we avoid index 0.
            float jitterX = GetHaltonValue((frameIndex & 1023) + 1, 2) - 0.5f;
            float jitterY = GetHaltonValue((frameIndex & 1023) + 1, 3) - 0.5f;

            jitterX *= settings.jitterScale;
            jitterY *= settings.jitterScale;

            jitterX += UnityEngine.Random.Range(-settings.jitterRand, settings.jitterRand);
            jitterY += UnityEngine.Random.Range(-settings.jitterRand, settings.jitterRand);

            taaJitter = new Vector2(jitterX, jitterY);

            Matrix4x4 proj;

            if (cam.orthographic) 
            {
                float vertical = cam.orthographicSize;
                float horizontal = vertical * cam.aspect;

                var offset = taaJitter;
                offset.x *= horizontal / (0.5f * width);
                offset.y *= vertical / (0.5f * height);

                float left = offset.x - horizontal;
                float right = offset.x + horizontal;
                float top = offset.y + vertical;
                float bottom = offset.y - vertical;

                proj = Matrix4x4.Ortho(left, right, bottom, top, cam.nearClipPlane, cam.farClipPlane);
            }
            else 
            {
                var planes = origProj.decomposeProjection;

                float vertFov = Math.Abs(planes.top) + Math.Abs(planes.bottom);
                float horizFov = Math.Abs(planes.left) + Math.Abs(planes.right);

                var planeJitter = new Vector2(jitterX * horizFov / width,
                    jitterY * vertFov / height);

                planes.left += planeJitter.x;
                planes.right += planeJitter.x;
                planes.top += planeJitter.y;
                planes.bottom += planeJitter.y;

                // Reconstruct the far plane for the jittered matrix.
                // For extremely high far clip planes, the decomposed projection zFar evaluates to infinity.
                if (float.IsInfinity(planes.zFar))
                    planes.zFar = cam.farClipPlane;

                proj = Matrix4x4.Frustum(planes);
            }

            return proj;
        }

        public static Matrix4x4 GetJitteredProjectionMatrix(float jitterScale, float jitterRand, Matrix4x4 origProj,
            int width, int height, Camera cam, int frameIndex)
        {
#if UNITY_2021_2_OR_NEWER
            if (UnityEngine.FrameDebugger.enabled)
            {
                taaJitter = Vector4.zero;
                return origProj;
            }
#endif

            // The variance between 0 and the actual halton sequence values reveals noticeable
            // instability in Unity's shadow maps, so we avoid index 0.
            float jitterX = GetHaltonValue((frameIndex & 1023) + 1, 2) - 0.5f;
            float jitterY = GetHaltonValue((frameIndex & 1023) + 1, 3) - 0.5f;

            jitterX *= jitterScale;
            jitterY *= jitterScale;

            jitterX += UnityEngine.Random.Range(-jitterRand, jitterRand);
            jitterY += UnityEngine.Random.Range(-jitterRand, jitterRand);

            taaJitter = new Vector2(jitterX, jitterY);

            Matrix4x4 proj;

            if (cam.orthographic)
            {
                float vertical = cam.orthographicSize;
                float horizontal = vertical * cam.aspect;

                var offset = taaJitter;
                offset.x *= horizontal / (0.5f * width);
                offset.y *= vertical / (0.5f * height);

                float left = offset.x - horizontal;
                float right = offset.x + horizontal;
                float top = offset.y + vertical;
                float bottom = offset.y - vertical;

                proj = Matrix4x4.Ortho(left, right, bottom, top, cam.nearClipPlane, cam.farClipPlane);
            }
            else
            {
                var planes = origProj.decomposeProjection;

                float vertFov = Math.Abs(planes.top) + Math.Abs(planes.bottom);
                float horizFov = Math.Abs(planes.left) + Math.Abs(planes.right);

                var planeJitter = new Vector2(jitterX * horizFov / width,
                    jitterY * vertFov / height);

                planes.left += planeJitter.x;
                planes.right += planeJitter.x;
                planes.top += planeJitter.y;
                planes.bottom += planeJitter.y;

                // Reconstruct the far plane for the jittered matrix.
                // For extremely high far clip planes, the decomposed projection zFar evaluates to infinity.
                if (float.IsInfinity(planes.zFar))
                    planes.zFar = cam.farClipPlane;

                proj = Matrix4x4.Frustum(planes);
            }

            return proj;
        }




        //----------------------------------------------------------------------------------------------------------
        //-----------------DLSS PASS--------------------------------------------------------------------------------
        //----------------------------------------------------------------------------------------------------------




        internal static NVIDIA.GraphicsDevice _device;
        internal static NVIDIA.GraphicsDevice device 
        {
            get 
            {
                if( _device == null ) 
                {
                    SetupDevice();
                }

                return _device;
            }
        }

        internal static void SetupDevice() 
        {
            if (!NVIDIA.NVUnityPlugin.IsLoaded())
                return;

            if (!SystemInfo.graphicsDeviceVendor.ToLower().Contains("nvidia"))
                return;

            _device = NVIDIA.GraphicsDevice.CreateGraphicsDevice();
        }




        public struct OptimalSettingsRequest 
        {
            public NVIDIA.DLSSQuality quality;
            public Rect viewport;
            public NVIDIA.OptimalDLSSSettingsData optimalSettings;
            public bool CanFit(Resolution rect) 
            {
                return rect.width >= optimalSettings.minWidth && rect.height >= optimalSettings.minHeight
                    && rect.width <= optimalSettings.maxWidth && rect.height <= optimalSettings.maxHeight;
            }
        }
        private static bool IsOptimalSettingsValid(in NVIDIA.OptimalDLSSSettingsData optimalSettings) 
        {
            return optimalSettings.maxHeight > optimalSettings.minHeight
                && optimalSettings.maxWidth > optimalSettings.minWidth
                && optimalSettings.maxWidth != 0
                && optimalSettings.maxHeight != 0
                && optimalSettings.minWidth != 0
                && optimalSettings.minHeight != 0;
        }


        public  struct Resolution 
        {
            public int width;
            public int height;

            public static bool operator ==(Resolution a, Resolution b) =>
                a.width == b.width && a.height == b.height;

            public static bool operator !=(Resolution a, Resolution b) =>
                !(a == b);
            public override bool Equals(object obj) 
            {
                if (obj is Resolution)
                    return (Resolution)obj == this;
                return false;
            }

            public override int GetHashCode() 
            {
                return (int)(width ^ height);
            }
        }

        //[System.Serializable]
        public struct DlssViewData 
        {
            public NVIDIA.DLSSQuality perfQuality;
            public UnityDlssCommon.Resolution inputRes;
            public UnityDlssCommon.Resolution outputRes;
            public float sharpness;
            public float jitterX;
            public float jitterY;
            public bool reset;
            public bool CanFitInput(in UnityDlssCommon.Resolution inputRect) 
            {
                return inputRes.width >= inputRect.width && inputRes.height > inputRect.height;
            }
        }

        public class ViewState 
        {
            private NVIDIA.DLSSContext m_DlssContext = null;
            private NVIDIA.GraphicsDevice m_Device;
            private DlssViewData m_Data = new DlssViewData();
            private bool m_UsingOptimalSettings = false;
            private bool m_UseAutomaticSettings = false;
            private Resolution m_BackbufferRes;
            private OptimalSettingsRequest m_OptimalSettingsRequest = new OptimalSettingsRequest();

            public NVIDIA.DLSSContext DLSSContext { get { return m_DlssContext; } }
            public bool useAutomaticSettings { get { return m_UseAutomaticSettings; } }
            public OptimalSettingsRequest OptimalSettingsRequestData { get { return m_OptimalSettingsRequest; } }

            public ViewState(NVIDIA.GraphicsDevice device) 
            {
                m_Device = device;
                m_DlssContext = null;
            }

            public void RequestUseAutomaticSettings(bool useAutomaticSettings, NVIDIA.DLSSQuality quality, Rect viewport, in NVIDIA.OptimalDLSSSettingsData optimalSettings) 
            {
                m_UseAutomaticSettings = useAutomaticSettings;
                m_OptimalSettingsRequest.quality = quality;
                m_OptimalSettingsRequest.viewport = viewport;
                m_OptimalSettingsRequest.optimalSettings = optimalSettings;
            }

            public void ClearAutomaticSettings() 
            {
                m_UseAutomaticSettings = false;
            }

            private bool ShouldUseAutomaticSettings() 
            {
                if (!m_UseAutomaticSettings || m_DlssContext == null)
                    return false;

                return m_DlssContext.initData.quality == m_OptimalSettingsRequest.quality
                    && m_DlssContext.initData.outputRTHeight == (uint)m_OptimalSettingsRequest.viewport.height
                    && m_DlssContext.initData.outputRTWidth == (uint)m_OptimalSettingsRequest.viewport.width
                    && IsOptimalSettingsValid(m_OptimalSettingsRequest.optimalSettings);
            }

            public void UpdateViewState(in DlssViewData viewData, CommandBuffer cmdBuffer) 
            {
                bool shouldUseOptimalSettings = ShouldUseAutomaticSettings();
                bool isNew = false;
                if 
                (
                    viewData.outputRes != m_Data.outputRes ||
                    (viewData.inputRes.width > m_BackbufferRes.width || viewData.inputRes.height > m_BackbufferRes.height) ||
                    (viewData.inputRes != m_BackbufferRes && !m_OptimalSettingsRequest.CanFit(viewData.inputRes)) ||
                    viewData.perfQuality != m_Data.perfQuality ||
                    m_DlssContext == null ||
                    shouldUseOptimalSettings != m_UsingOptimalSettings
                ) 
                {
                    isNew = true;
                    m_BackbufferRes = viewData.inputRes;

                    if (m_DlssContext != null) 
                    {
                        m_Device.DestroyFeature(cmdBuffer, m_DlssContext);
                        m_DlssContext = null;
                    }

                    var settings = new NVIDIA.DLSSCommandInitializationData();
                    settings.SetFlag(NVIDIA.DLSSFeatureFlags.IsHDR, true);
                    settings.SetFlag(NVIDIA.DLSSFeatureFlags.MVLowRes, true);
                    settings.SetFlag(NVIDIA.DLSSFeatureFlags.DepthInverted, true);
                    settings.SetFlag(NVIDIA.DLSSFeatureFlags.DoSharpening, true);
                    settings.SetFlag(NVIDIA.DLSSFeatureFlags.MVJittered, true);
                    settings.inputRTWidth = (uint)m_BackbufferRes.width;
                    settings.inputRTHeight = (uint)m_BackbufferRes.height;
                    settings.outputRTWidth = (uint)viewData.outputRes.width;
                    settings.outputRTHeight = (uint)viewData.outputRes.height;
                    settings.quality = viewData.perfQuality;
                    m_UsingOptimalSettings = shouldUseOptimalSettings;
                    m_DlssContext = m_Device.CreateFeature(cmdBuffer, settings);
                }

                m_Data = viewData;
                m_Data.reset = isNew || viewData.reset;
            }

            public void SubmitDlssCommands
            (
                Texture source,
                Texture depth,
                Texture motionVectors,
                Texture biasColorMask,
                Texture output,
                CommandBuffer cmdBuffer
            ) 
            {
                if (m_DlssContext == null)
                    return;

                m_DlssContext.executeData.sharpness = m_UsingOptimalSettings ? m_OptimalSettingsRequest.optimalSettings.sharpness : m_Data.sharpness;
                m_DlssContext.executeData.mvScaleX = -((float)m_Data.inputRes.width);
                m_DlssContext.executeData.mvScaleY = -((float)m_Data.inputRes.height);
                m_DlssContext.executeData.subrectOffsetX = 0;
                m_DlssContext.executeData.subrectOffsetY = 0;
                m_DlssContext.executeData.subrectWidth = (uint)m_Data.inputRes.width;
                m_DlssContext.executeData.subrectHeight = (uint)m_Data.inputRes.height;
                m_DlssContext.executeData.jitterOffsetX = m_Data.jitterX;
                m_DlssContext.executeData.jitterOffsetY = m_Data.jitterY;
                m_DlssContext.executeData.preExposure = 1f;
                m_DlssContext.executeData.invertYAxis = 0u;
                m_DlssContext.executeData.invertXAxis = 0u;
                m_DlssContext.executeData.reset = m_Data.reset ? 1 : 0;

                var textureTable = new NVIDIA.DLSSTextureTable() 
                {
                    colorInput = source,
                    colorOutput = output,
                    depth = depth,
                    motionVectors = motionVectors,
                    biasColorMask = biasColorMask
                };

                device.ExecuteDLSS(cmdBuffer, m_DlssContext, textureTable);
            }
            public void SubmitDlssCommands
            (
                Texture source,
                Texture depth,
                Texture motionVectors,
                Texture biasColorMask,
                Texture output,
                CommandBuffer cmdBuffer,
                float sharpness
            )
            {
                if (m_DlssContext == null)
                    return;

                m_DlssContext.executeData.sharpness = sharpness;
                m_DlssContext.executeData.mvScaleX = -((float)m_Data.inputRes.width);
                m_DlssContext.executeData.mvScaleY = -((float)m_Data.inputRes.height);
                m_DlssContext.executeData.subrectOffsetX = 0;
                m_DlssContext.executeData.subrectOffsetY = 0;
                m_DlssContext.executeData.subrectWidth = (uint)m_Data.inputRes.width;
                m_DlssContext.executeData.subrectHeight = (uint)m_Data.inputRes.height;
                m_DlssContext.executeData.jitterOffsetX = m_Data.jitterX;
                m_DlssContext.executeData.jitterOffsetY = m_Data.jitterY;
                m_DlssContext.executeData.preExposure = 1f;
                m_DlssContext.executeData.invertYAxis = 0u;
                m_DlssContext.executeData.invertXAxis = 0u;
                m_DlssContext.executeData.reset = m_Data.reset ? 1 : 0;

                var textureTable = new NVIDIA.DLSSTextureTable()
                {
                    colorInput = source,
                    colorOutput = output,
                    depth = depth,
                    motionVectors = motionVectors,
                    biasColorMask = biasColorMask
                };

                device.ExecuteDLSS(cmdBuffer, m_DlssContext, textureTable);
            }

            public void Cleanup(CommandBuffer cmdBuffer) 
            {
                if (m_DlssContext != null) {
                    m_Device.DestroyFeature(cmdBuffer, m_DlssContext);
                    m_DlssContext = null;
                }
            }
        }
#endif


    }
}
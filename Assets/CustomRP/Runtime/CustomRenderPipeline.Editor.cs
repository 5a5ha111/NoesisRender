using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;


namespace NoesisRender
{

    public partial class CustomRenderPipeline
    {

        partial void InitializeForEditor();

        partial void DisposeForEditor();



#if UNITY_EDITOR

        partial void InitializeForEditor()
        {
            Lightmapping.SetDelegate(lightsDelegate);
        }
        partial void DisposeForEditor()
        {
            Lightmapping.ResetDelegate();
        }

        static Lightmapping.RequestLightsDelegate lightsDelegate =
        (Light[] lights, NativeArray<LightDataGI> output) =>
        {
            var lightData = new LightDataGI();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        var directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight);
                        lightData.falloff = FalloffType.InverseSquared;
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.falloff = FalloffType.InverseSquared;
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                        spotLight.angularFalloff =
                            AngularFalloffType.AnalyticAndInnerAngle;
                        lightData.falloff = FalloffType.InverseSquared;
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        rectangleLight.mode = LightMode.Baked;
                        lightData.falloff = FalloffType.InverseSquared;
                        lightData.Init(ref rectangleLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }
                output[i] = lightData;
            }
        };

#endif
    }
}
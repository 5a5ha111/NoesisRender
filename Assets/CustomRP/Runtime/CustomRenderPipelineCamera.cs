using UnityEngine;
using UnityEngine.Rendering;


[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class CustomRenderPipelineCamera : MonoBehaviour
{
    [SerializeField] CameraSettings settings = default;

    ProfilingSampler sampler;



    public CameraSettings Settings => settings ?? (settings = new CameraSettings());
    /// <summary>
    /// Camera's without our CustomRenderPipelineCamera will have a generic name, usually Game for a game camera, but you'll also see the scene view, reflection, and preview camera show up. These won't be shown in the frame debugger but you can find them in the render graph viewer and also in the profiler if it is in edit mode.
    /// The second downside is that, because we cache the samplers, changes to the camera name aren't picked up in not development builds.
    /// </summary>
    public ProfilingSampler Sampler => sampler ??= new(GetComponent<Camera>().name);


    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        void OnEnable() => sampler = null;
    #endif
}
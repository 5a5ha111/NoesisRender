/**************************************************************************************
 *                                                                                    *
 *  Copyright (C) 2025 Aleksandra Arakcheeva                                          *
 *                                                                                    *
 *  This work is licensed under the Creative Commons                                  *
 *  Attribution-NonCommercial-ShareAlike 3.0 Unported License.                        *
 *                                                                                    *
 *  To view a copy of this license, visit:                                            *
 *  https://creativecommons.org/licenses/by-nc-sa/3.0/                                *
 *                                                                                    *
 *  Key terms:                                                                        *
 *  — Attribution:     You must credit the original author.                           *
 *  — NonCommercial:   Prohibits commercial use without explicit permission.          *
 *  — ShareAlike:      Derivatives must be licensed under identical terms.            *
 *                                                                                    *
 *  Permitted use:                                                                    *
 *    - Modify, distribute, and use privately.                                        *
 *    - Include attribution in derivative works (see details below).                  *
 *                                                                                    *
 *  Prohibited without written permission:                                            *
 *    - Commercial exploitation (SaaS, paid apps, internal corporate tools).          *
 *    - Removing license terms from derivatives.                                      *
 *                                                                                    *
 *  Attribution requirement:                                                          *
 *    Include this header in source files OR display prominently in UI/docs:          *
 *    "Contains code from [Project Name] by [Author], licensed under CC BY-NC-SA 3.0" *
 *                                                                                    *
 *  DISCLAIMER: This code is provided "AS IS" without warranties of any kind.         *
 *  The author accepts no liability for damages arising from its use.                 *
 *                                                                                    *
 *************************************************************************************/


using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace NoesisRender.Particles
{
    /// <summary>
    /// This system is alternative to Unity Built-in particle system. Main difference - its particle count should be a constant.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public class ParticlesShader : MonoBehaviour
    {
        [SerializeField] private new ParticleSystem particleSystem;

        [Space]
        [Space]

        [SerializeField] Vector3 _EmitterDimensions;
        public Vector3 GetEmitterDimensions { get { return _EmitterDimensions; } }

        [SerializeField] Color _StartColor = Color.white;
        [SerializeField] Color _EndColor = Color.white;

        [Space]
        //[Header("Fade in and out and Opacity")]
        [SerializeField] float _Opacity = 1f;
        [SerializeField] float _FadeInPower = 0.7f;
        [SerializeField] float _FadeOutPower = 4f;

        [SerializeField] bool _UseTextureRGBAlpha = false;
        [SerializeField] float _TextureAlphaSmoothstep = 0.21f;
        [SerializeField] bool _UseTextureAlpha = false;

        [Space]
        //[Header("Scale")]
        [SerializeField] float _ParticleStartSize = 0.5f;
        [SerializeField] float _ParticleEndSize = 0.5f;

        [Space]
        //[Header("Debug")]
        [SerializeField] bool _DebugTime = false;
        public bool GetDebugTime { get { return _DebugTime; } }
        [SerializeField] float _ManualTime = 0;

        [Space]
        [SerializeField][HideInInspector] float _ParticleLifetime = 1;
        [SerializeField] float _ParticleSpeed = 0.7f; // Shader work with the particle speed, instead of lifetime. To get speed from lifetime in seconds use rcp()
        [SerializeField] Vector3 _ParticleDirectional = new Vector3(0, 0, -1);
        [SerializeField][Range(0, 360)] float _ParticleSpead = 0.7f;
        [SerializeField] float _ParticleVelocityStart = 2f;
        [SerializeField] float _ParticleVelocityEnd = 1.2f;

        [Space]
        //[Header("Rotation")]
        [SerializeField][Range(-180, 180)] float _Rotation = 0;
        [SerializeField] bool _RotationRandomOffset = false;
        [SerializeField] float _RotationSpeed = 0f; // To do: make a property for end rotation
        [SerializeField] bool _RandomizeRotationDirection = false;

        [Space]
        //[Header("Forces")]
        [SerializeField] Vector3 _Wind = Vector3.zero;
        [SerializeField] Vector3 _Gravityy = Vector3.zero;

        [Space]
        //[Header("Flipbook")]
        [SerializeField] Vector2 _FlipBookDimenions = Vector2.one;
        [SerializeField] Texture _FlipBook = null;
        [SerializeField] bool _FlipX = false;
        [SerializeField] bool _FlipY = false;
        [SerializeField] bool _MatchParticlePhase = false;
        [SerializeField] float _FlipbookFPS = 20;
        [SerializeField] float _FlipBookSpeed = 0.6f; // In shader used speed. It get by divide _FlipbookFPS by _FlipBookDimenions
        [SerializeField] FlipbookMode _FlipbookMode = FlipbookMode.fps;

        public FlipbookMode GetFlipbookMode { get { return _FlipbookMode; } }

        public enum FlipbookMode { fps=0, matchPhase=1}

        //[Header("Spawn and stop")]
        [SerializeField] bool _Loop = true; // If its a loop, no TimeBound or duration will be used
        [SerializeField] bool _Prewarn = true; // If its a loop, no TimeBound or duration will be used
        [SerializeField] bool _PlayOnAwake = true; // If its a loop, no TimeBound or duration will be used
        [SerializeField] float _Duration = 2;
        [SerializeField] float _StartDelay = 0;
        [SerializeField] bool _ConstantFlow = true;
        /// <summary>
        /// x defines spawn start time, y spawn end. 
        /// So if you set x = Time.time, so only particles that born after this time can be shown. 
        /// _TimeBounds.y = _TimeBounds.x + _Duration;
        /// If x or y is equal to 0, so clipping on this border is disabled.
        /// Only applied if nonLoopedFeature is enabled
        /// </summary>
        [HideInInspector] Vector2 _TimeBounds = Vector2.zero;

        // Maybe better use local PlaybackTime like it do particle system, but now i dont want to bother with setting right time every frame, so rely on already defined gloabal time. I also want to set all necessary data and forget about shader till it complete.



        private const string shaderName = "Custom/CustomVFX";

        #region Shader properties id

        // Emmiter
        private readonly int _EmitterDimensionsId = Shader.PropertyToID("_EmitterDimensions");
        private readonly int _StartColorId = Shader.PropertyToID("_StartColor");
        private readonly int _EndColorId = Shader.PropertyToID("_EndColor");

        // Fade and opacity
        private readonly int _OpacityId = Shader.PropertyToID("_Opacity");
        private readonly int _FadeInPowerId = Shader.PropertyToID("_FadeInPower");
        private readonly int _FadeOutPowerId = Shader.PropertyToID("_FadeOutPower");
        private readonly int _UseTextureRGBAlphaId = Shader.PropertyToID("_UseTextureRGBAlpha");
        private readonly int _TextureAlphaSmoothstepId = Shader.PropertyToID("_TextureAlphaSmoothstep");
        private readonly int _UseTextureAlphaId = Shader.PropertyToID("_UseTextureAlpha");

        // Scale
        private readonly int _ParticleStartSizeId = Shader.PropertyToID("_ParticleStartSize");
        private readonly int _ParticleEndSizeId = Shader.PropertyToID("_ParticleEndSize");

        // Debug
        private readonly int _DebugTimeId = Shader.PropertyToID("_DebugTime");
        private readonly int _ManualTimeId = Shader.PropertyToID("_ManualTime");

        // Movement
        private readonly int _ParticleSpeedId = Shader.PropertyToID("_ParticleSpeed");
        private readonly int _ParticleDirectionalId = Shader.PropertyToID("_ParticleDirectional");
        private readonly int _ParticleSpreadId = Shader.PropertyToID("_ParticleSpread");
        private readonly int _ParticleVelocityStartId = Shader.PropertyToID("_ParticleVelocityStart");
        private readonly int _ParticleVelocityEndId = Shader.PropertyToID("_ParticleVelocityEnd");

        // Rotation
        private readonly int _RotationId = Shader.PropertyToID("_Rotation");
        private readonly int _RotationRandomOffsetId = Shader.PropertyToID("_RotationRandomOffset");
        private readonly int _RotationSpeedId = Shader.PropertyToID("_RotationSpeed");
        private readonly int _RandomizeRotationDirectionId = Shader.PropertyToID("_RandomizeRotationDirection");

        // Force
        private readonly int _WindId = Shader.PropertyToID("_Wind");
        private readonly int _GravityyId = Shader.PropertyToID("_Gravityy");

        // Flipbook
        private readonly int _FlipBookDimenionsId = Shader.PropertyToID("_FlipBookDimenions");
        private readonly int _FlipBookId = Shader.PropertyToID("_FlipBook");
        private readonly int _FlipXId = Shader.PropertyToID("_FlipX");
        private readonly int _FlipYId = Shader.PropertyToID("_FlipY");
        private readonly int _MatchParticlePhaseId = Shader.PropertyToID("_MatchParticlePhase");
        private readonly int _FlipBookSpeedId = Shader.PropertyToID("_FlipBookSpeed");

        // Time Bounds
        private readonly int _TimeBoundsId = Shader.PropertyToID("_TimeBounds");
        private readonly int _TimeBoundXId = Shader.PropertyToID("_TimeBoundX");
        private readonly int _TimeBoundYId = Shader.PropertyToID("_TimeBoundY");

        private readonly int _PlaybackTimeId = Shader.PropertyToID("_PlaybackTime");

        private static string nonLoopedFeature = "_NON_LOOPED";
        private static string _CONSTANT_FLOW_DISABLE = "_CONSTANT_FLOW_DISABLE";
        private static string _CUSTOM_TIME_feature = "_CUSTOM_TIME";
        private static string _TIME_BOUND_X_feature = "_TIME_BOUND_X";
        private static string _TIME_BOUND_Y_feature = "_TIME_BOUND_Y";

        #endregion

        private Bounds tempBounds = new Bounds();
        [SerializeField] private MeshFilter tempFilter;
        [SerializeField] private MeshRenderer tempRenderer;

        [SerializeField] private double playbackTime = 0;
        [SerializeField] private double tempTime = 0;
        /// <summary>
        /// System can work with time provided from particlesShader cs script, or rely on global shader time. If you dont use custom time, you lose ability to use time related features (such as time bounds), but shader can work fully independently from cpu.
        /// </summary>
        [SerializeField] private bool _UseCustomTime = false;

        private bool playing = true;
        private bool stopped = false;
        private bool paused = false;
        private bool emitting = true;

        public bool IsPaused { get { return paused && _UseCustomTime; } }



        #if UNITY_EDITOR
        void OnValidate()
        {
            /*MeshRenderer meshRenderer;
            if (TryGetComponent(out meshRenderer))
            {
                var mat = meshRenderer.material;
                if (mat != null)
                {
                    //_Opacity = mat.GetFloat(_OpacityId);
                }
            }*/

            if (tempFilter == null)
            {
                tempFilter = GetComponent<MeshFilter>();
            }
            if (tempRenderer == null)
            {
                tempRenderer = GetComponent<MeshRenderer>();
            }

            playbackTime = 0;
            tempTime = Time.realtimeSinceStartupAsDouble;
        }

        #endif

        void Awake()
        {
            if (_PlayOnAwake)
            {
                Play();
            }
        }
        void Start()
        {
            /*if (_PlayOnAwake)
            {
                Play();
            }*/
        }

        private void Update()
        {
            SetParticleTime();
        }

        [ContextMenu("Test")]
        public void Test()
        {
            /* var main = particleSystem.main;
             main.duration = _Duration;

             particleSystem.Play();*/

            Material mat;
            mat = tempRenderer.sharedMaterial;
            Vector4 time = Shader.GetGlobalVector("_Time");
            Debug.Log("shader time = " + time.y);
        }

        [ContextMenu("Load properties values from material")]
        public void LoadValues()
        {
            Material mat;
            mat = tempRenderer.sharedMaterial;
            LoadValues(mat);
        }

        public void LoadValues(Material mat)
        {
            if (mat == null) return;

            // Emitter properties
            _EmitterDimensions = mat.GetVector(_EmitterDimensionsId);
            _StartColor = mat.GetColor(_StartColorId);
            _EndColor = mat.GetColor(_EndColorId);

            // Fade and opacity properties
            _Opacity = mat.GetFloat(_OpacityId);
            _FadeInPower = mat.GetFloat(_FadeInPowerId);
            _FadeOutPower = mat.GetFloat(_FadeOutPowerId);
            _UseTextureRGBAlpha = mat.GetFloat(_UseTextureRGBAlphaId) == 1 ? true : false;
            _TextureAlphaSmoothstep = mat.GetFloat(_TextureAlphaSmoothstepId);
            _UseTextureAlpha = mat.GetFloat(_UseTextureAlphaId) == 1 ? true : false;

            // Scale properties
            _ParticleStartSize = mat.GetFloat(_ParticleStartSizeId);
            _ParticleEndSize = mat.GetFloat(_ParticleEndSizeId);

            // Debug properties
            _DebugTime = mat.GetFloat(_DebugTimeId) == 1 ? true : false;
            _ManualTime = mat.GetFloat(_ManualTimeId);

            // Movement properties
            _ParticleSpeed = mat.GetFloat(_ParticleSpeedId);
            _ParticleDirectional = mat.GetVector(_ParticleDirectionalId);
            _ParticleSpead = mat.GetFloat(_ParticleSpreadId);
            _ParticleVelocityStart = mat.GetFloat(_ParticleVelocityStartId);
            _ParticleVelocityEnd = mat.GetFloat(_ParticleVelocityEndId);

            // Rotation properties
            _Rotation = mat.GetFloat(_RotationId);
            _RotationRandomOffset = mat.GetFloat(_RotationRandomOffsetId) == 1 ? true : false;
            _RotationSpeed = mat.GetFloat(_RotationSpeedId);
            _RandomizeRotationDirection = mat.GetFloat(_RandomizeRotationDirectionId) == 1 ? true : false;

            // Force properties
            _Wind = mat.GetVector(_WindId);
            _Gravityy = mat.GetVector(_GravityyId);

            // Flipbook properties
            _FlipBookDimenions = mat.GetVector(_FlipBookDimenionsId);
            _FlipBook = mat.GetTexture(_FlipBookId) as Texture2D;
            _FlipX = mat.GetFloat(_FlipXId) == 1 ? true : false;
            _FlipY = mat.GetFloat(_FlipYId) == 1 ? true : false;
            _MatchParticlePhase = mat.GetFloat(_MatchParticlePhaseId) == 1 ? true : false;
            _FlipBookSpeed = mat.GetFloat(_FlipBookSpeedId);

            #if UNITY_EDITOR
            MakeDirty();
            #endif
        }

        public void SaveValues(Material mat)
        {
            if (mat == null) return;

            //_ParticleSpeed = 1f / _Duration;

            // Emitter properties
            mat.SetVector(_EmitterDimensionsId, _EmitterDimensions);
            mat.SetColor(_StartColorId, _StartColor);
            mat.SetColor(_EndColorId, _EndColor);

            // Fade and opacity properties
            mat.SetFloat(_OpacityId, _Opacity);
            mat.SetFloat(_FadeInPowerId, _FadeInPower);
            mat.SetFloat(_FadeOutPowerId, _FadeOutPower);
            mat.SetFloat(_UseTextureRGBAlphaId, _UseTextureRGBAlpha ? 1 : 0);
            mat.SetFloat(_TextureAlphaSmoothstepId, _TextureAlphaSmoothstep);
            mat.SetFloat(_UseTextureAlphaId, _UseTextureAlpha ? 1 : 0);

            // Scale properties
            mat.SetFloat(_ParticleStartSizeId, _ParticleStartSize);
            mat.SetFloat(_ParticleEndSizeId, _ParticleEndSize);

            // Debug properties
            mat.SetFloat(_DebugTimeId, _DebugTime ? 1 : 0);
            mat.SetFloat(_ManualTimeId, _ManualTime);

            // Movement properties
            mat.SetFloat(_ParticleSpeedId, _ParticleSpeed);
            mat.SetVector(_ParticleDirectionalId, _ParticleDirectional);
            mat.SetFloat(_ParticleSpreadId, _ParticleSpead);
            mat.SetFloat(_ParticleVelocityStartId, _ParticleVelocityStart);
            mat.SetFloat(_ParticleVelocityEndId, _ParticleVelocityEnd);

            // Rotation properties
            mat.SetFloat(_RotationId, _Rotation);
            mat.SetFloat(_RotationRandomOffsetId, _RotationRandomOffset ? 1 : 0);
            mat.SetFloat(_RotationSpeedId, _RotationSpeed);
            mat.SetFloat(_RandomizeRotationDirectionId, _RandomizeRotationDirection ? 1 : 0);

            // Force properties
            mat.SetVector(_WindId, _Wind);
            mat.SetVector(_GravityyId, _Gravityy);

            // Flipbook properties
            mat.SetVector(_FlipBookDimenionsId, _FlipBookDimenions);
            mat.SetTexture(_FlipBookId, _FlipBook);
            mat.SetFloat(_FlipXId, _FlipX ? 1 : 0);
            mat.SetFloat(_FlipYId, _FlipY ? 1 : 0);
            _MatchParticlePhase = ((int)_FlipbookMode == 1);
            _FlipBookSpeed = _FlipbookFPS / (_FlipBookDimenions.x + _FlipBookDimenions.y);
            mat.SetFloat(_MatchParticlePhaseId, _MatchParticlePhase ? 1 : 0);
            mat.SetFloat(_FlipBookSpeedId, _FlipBookSpeed);

            #if UNITY_EDITOR
            MakeDirty();
            #endif

            if (_UseCustomTime)
            {
                mat.EnableKeyword(_CUSTOM_TIME_feature);
            }
            else
            {
                mat.DisableKeyword(_CUSTOM_TIME_feature);
            }

            if (_ConstantFlow)
            {
                mat.DisableKeyword(_CONSTANT_FLOW_DISABLE);
            }
            else
            {
                mat.EnableKeyword(_CONSTANT_FLOW_DISABLE);
            }

            UpdateBounds();
        }

        public void SetParticleTime()
        {
            if (paused) return;
            if (!_UseCustomTime) return;
            double delta = Time.realtimeSinceStartupAsDouble - tempTime;
            playbackTime += delta;
            tempTime = Time.realtimeSinceStartupAsDouble;
            //Debug.Log("Playback time = " + playbackTime);
            Material mat;
            mat = tempRenderer.sharedMaterial;
            mat.SetFloat(_PlaybackTimeId, (float)playbackTime);
        }

        public void SetTimeBounds(Material mat, bool enable, Vector2 timeBounds)
        {
            if (timeBounds == Vector2.zero)
            {
                enable = false;
                mat.DisableKeyword(_TIME_BOUND_X_feature);
                mat.DisableKeyword(_TIME_BOUND_Y_feature);
                return;
            }

            if (timeBounds.x != 0)
            {
                mat.EnableKeyword(_TIME_BOUND_X_feature);
            }
            else
            {
                mat.DisableKeyword(_TIME_BOUND_X_feature);
            }

            if (timeBounds.y != 0)
            {
                mat.EnableKeyword(_TIME_BOUND_Y_feature);
            }
            else
            {
                mat.DisableKeyword(_TIME_BOUND_Y_feature);
            }

            //mat.SetVector(_TimeBoundsId, timeBounds);
            mat.SetFloat(_TimeBoundXId, timeBounds.x);
            mat.SetFloat(_TimeBoundYId, timeBounds.y);
        }
        public void SetTimeBound(Material mat, bool x, float timeBound)
        {
            string feature = x ? _TIME_BOUND_X_feature : _TIME_BOUND_Y_feature;
            if (timeBound == 0)
            {
                mat.DisableKeyword(feature);
            }
            else
            {
                mat.EnableKeyword(feature);
                mat.SetFloat(x ? _TimeBoundXId : _TimeBoundYId, timeBound);
            }
        }

        public void SetTimeBounds(Material mat, bool enable)
        {
            if (enable)
            {
                mat.EnableKeyword(nonLoopedFeature);
                mat.EnableKeyword(_TIME_BOUND_X_feature);
                mat.EnableKeyword(_TIME_BOUND_Y_feature);
            }
            else
            {
                mat.DisableKeyword(nonLoopedFeature);
                mat.DisableKeyword(_TIME_BOUND_X_feature);
                mat.DisableKeyword(_TIME_BOUND_Y_feature);
            }
        }

#if UNITY_EDITOR
        private void MakeDirty()
        {
            if (Application.isPlaying) return;

            EditorUtility.SetDirty(this);

            if (PrefabUtility.IsPartOfPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
        }
        #endif

        [ContextMenu("UpdateBounds")]
        private void UpdateBounds()
        {
            Bounds newBounds = GetNewBounds();

            if (true)
            {
                tempBounds = newBounds;

                if (tempFilter != null)
                {
                    if (Application.isPlaying)
                    {
                        tempFilter.sharedMesh.bounds = tempBounds;
                    }
                    else
                    {
                        tempFilter.sharedMesh.bounds = tempBounds;
                    }
                }
                #if UNITY_EDITOR
                else
                {
                    if (Application.isPlaying)
                    {
                        tempFilter.sharedMesh.bounds = tempBounds;
                    }
                    else
                    {
                        /*tempFilter.mesh.bounds = tempBounds;
                        tempFilter.sharedMesh.bounds = tempBounds;*/

                        var mesh = GetMeshForEditing(tempFilter);
                        mesh.bounds = tempBounds;

                        //Debug.Log(mesh.name);

                        tempFilter.sharedMesh.UploadMeshData(false);

                        RefreshMeshInEditor(mesh);

                        /*UnityEditor.EditorUtility.SetDirty(tempFilter.sharedMesh);
                        UnityEditor.EditorUtility.SetDirty(tempFilter.mesh);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);*/
                    }
                }
                #endif
            }
        }


        #region Particle System like Methods

        public void Play()
        {
            if (!_UseCustomTime)
            {
                SetTimeBounds(tempRenderer.sharedMaterial, false);
                if (_UseCustomTime)
                {
                    tempRenderer.sharedMaterial.EnableKeyword(_CUSTOM_TIME_feature);
                }
                else
                {
                    tempRenderer.sharedMaterial.DisableKeyword(_CUSTOM_TIME_feature);
                }
                return;
            }

            if (!_ConstantFlow)
            {
                tempTime = Time.realtimeSinceStartup;
                playbackTime = 0;
            }
            // I cannot stable sync shader time with cpu without customTime with any other way, so just grab value directly. It may have undefined behavior (on some devices work, on some return 0 or stall frame until get value), so use custom time instead.
            //float currentShaderTime = Shader.GetGlobalVector("_Time").y;

            float currentShaderTime = (float)playbackTime;

            if (_Prewarn)
            {
                // Just play
                float startTimeBound = 0;
                float endTimeBound = 0;
                if (_Loop)
                {
                    SetTimeBounds(tempRenderer.sharedMaterial, false);
                }
                else
                {
                    endTimeBound = startTimeBound + _Duration;
                    SetTimeBounds(tempRenderer.sharedMaterial, true, new Vector2(startTimeBound, endTimeBound));
                }
            }
            else
            {
                float startTimeBound = currentShaderTime + _StartDelay;
                float endTimeBound = 0;
                if (!_Loop)
                {
                    endTimeBound = startTimeBound + _Duration - 0.05f; // due to float precision, i must have some offset, and hope that artist make a propper fade out
                }
                SetTimeBounds(tempRenderer.sharedMaterial, true, new Vector2(startTimeBound, endTimeBound));
            }
        }

        public void Stop(bool stopImmideately = false)
        {
            if (!_UseCustomTime)
            {
                SetTimeBounds(tempRenderer.sharedMaterial, false);
                return;
            }
            float currentShaderTime = (float)playbackTime;

            if (stopImmideately)
            {
                // In theory even without custom time you can clear particles with timeBound -1, but it will no have visual difference from meshRenderer.enabled = false.
                SetTimeBounds(tempRenderer.sharedMaterial, true, new Vector2(-1, -1));
            }
            else
            {
                SetTimeBound(tempRenderer.sharedMaterial, false, currentShaderTime);
            }
        }
        public void Pause()
        {
            paused = true;
        }
        public void Unpause()
        {
            paused = false;
            tempTime = Time.realtimeSinceStartupAsDouble;
        }
        public void Clear()
        {

        }

        [Obsolete("This is a mesh+shader based particle system, so it have constant particle count", true)]
        public void Emit(int count)
        {

        }

        /// <summary>
        /// Add some value to particle time.
        /// </summary>
        /// <param name="t"></param>
        public void Simulate(float t)
        {
            playbackTime += t;
        }
        /// <summary>
        /// Add some value to particle time.
        /// </summary>
        /// <param name="t"></param>
        public void Simulate(double t)
        {
            playbackTime += t;
        }

        #endregion

        private Mesh GetMeshForEditing(MeshFilter meshFilter)
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Create mesh instance in editor to avoid modifying asset
                if (meshFilter.sharedMesh != null &&
                   (meshFilter.sharedMesh.name.Contains("Instance") ||
                    meshFilter.sharedMesh != GetOriginalMeshAsset()))
                {
                    return meshFilter.sharedMesh;
                }

                Mesh originalMesh = meshFilter.sharedMesh;
                Mesh newMesh = Instantiate(originalMesh);
                newMesh.name = originalMesh.name + " (Instance)";
                meshFilter.sharedMesh = newMesh;
                return newMesh;
            }
            #endif

            return meshFilter.mesh;
        }

        private Mesh GetOriginalMeshAsset()
        {
            return null;
        }

        private void RefreshMeshInEditor(Mesh mesh)
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Force mesh regeneration
                mesh.UploadMeshData(false);

                // Mark objects as dirty
                EditorUtility.SetDirty(mesh);
                EditorUtility.SetDirty(gameObject);
                //EditorUtility.SetDirty(meshFilter);
                SceneView.RepaintAll();
            }
            #endif
        }

        private Bounds GetNewBounds()
        {
            // Calculate maximum possible position offsets
            Vector3 maxSpawnOffset = new Vector3(
                Mathf.Abs(_EmitterDimensions.x),
                Mathf.Abs(_EmitterDimensions.y),
                Mathf.Abs(_EmitterDimensions.z)
            );

            // Calculate maximum velocity effect (using worst-case lifetime = 1)
            float maxVelocity = Mathf.Max(
                Mathf.Abs(_ParticleVelocityStart),
                Mathf.Abs(_ParticleVelocityEnd)
            );

            // Calculate maximum directional spread effect
            float maxSpreadEffect = Mathf.Tan(_ParticleSpead * Mathf.Deg2Rad * 0.5f) * maxVelocity;
            Vector3 maxDirectionalEffect = new Vector3(
                Mathf.Abs(_ParticleDirectional.x) + maxSpreadEffect,
                Mathf.Abs(_ParticleDirectional.y) + maxSpreadEffect,
                Mathf.Abs(_ParticleDirectional.z) + maxSpreadEffect
            );

            // Calculate maximum force effects (wind + gravity)
            Vector3 maxForceEffect = new Vector3(
                Mathf.Abs(_Wind.x + _Gravityy.x),
                Mathf.Abs(_Wind.y + _Gravityy.y),
                Mathf.Abs(_Wind.z + _Gravityy.z)
            );

            float maxParticleSize = Mathf.Max(_ParticleStartSize, _ParticleEndSize);
            Vector3 maxParticleSizeVector = new Vector3(maxParticleSize, maxParticleSize, maxParticleSize);

            // Calculate total maximum possible offset
            Vector3 maxTotalOffset = maxSpawnOffset +
                                    maxDirectionalEffect +
                                    maxForceEffect + maxParticleSizeVector;

            // Create bounds that encompass all possible positions
            Bounds bounds = new Bounds(Vector3.zero, 2 * maxTotalOffset);
            return bounds;
        }
    }

}

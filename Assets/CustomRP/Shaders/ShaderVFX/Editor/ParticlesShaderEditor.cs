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



using UnityEditor;
using UnityEngine;

namespace NoesisRender.Particles
{
    [CustomEditor(typeof(ParticlesShader))]
    public class ParticlesShaderEditor : Editor
    {

        #region SerializedProperties

        SerializedProperty _EmitterDimensions;
        SerializedProperty _StartColor;
        SerializedProperty _EndColor;

        // Fade properties
        SerializedProperty _Opacity;
        SerializedProperty _FadeInPower;
        SerializedProperty _FadeOutPower;
        SerializedProperty _UseTextureRGBAlpha;
        SerializedProperty _TextureAlphaSmoothstep;
        SerializedProperty _UseTextureAlpha;

        // Scale properties
        SerializedProperty _ParticleStartSize;
        SerializedProperty _ParticleEndSize;

        // Debug properties
        SerializedProperty _DebugTime;
        SerializedProperty _ManualTime;

        // Particle motion properties
        SerializedProperty _ParticleSpeed;
        SerializedProperty _ParticleDirectional;
        SerializedProperty _ParticleSpead;
        SerializedProperty _ParticleVelocityStart;
        SerializedProperty _ParticleVelocityEnd;

        // Rotation properties
        SerializedProperty _Rotation;
        SerializedProperty _RotationRandomOffset;
        SerializedProperty _RotationSpeed;
        SerializedProperty _RandomizeRotationDirection;

        // Forces properties
        SerializedProperty _Wind;
        SerializedProperty _Gravityy;

        // Flipbook properties
        SerializedProperty _FlipBookDimenions;
        SerializedProperty _FlipbookMode;
        SerializedProperty _FlipBook;
        SerializedProperty _FlipX;
        SerializedProperty _FlipY;
        SerializedProperty _MatchParticlePhase;
        SerializedProperty _FlipBookSpeed;
        SerializedProperty _FlipbookFPS;

        // Spawn/stop properties
        SerializedProperty _Loop;
        SerializedProperty _Prewarn;
        SerializedProperty _UseCustomTime;
        SerializedProperty _PlayOnAwake;
        SerializedProperty _Duration;
        SerializedProperty _StartDelay;
        SerializedProperty _ConstantFlow;

        #endregion

        private bool _undoRedoPerformed = false;
        private GUIStyle _darkBackgroundStyle;


        private bool showMain = false;
        private bool showFade = false;
        private bool showScale = false;
        private bool showDebug = false;
        private bool showParticleMotion = false;
        private bool showRotation = false;
        private bool showForces = false;
        private bool showFlipbook = false;
        private bool showSpawnStop = false;
        private bool showEmission = false;
        private bool showShape = false;
        private bool showVelocityOverLifetime = false;
        private bool showForceOverLifetime = false;
        private bool showColorOverLifetime = false;
        private bool showSizeOverLifetime = false;
        private bool showTextureSheet = false;
        private bool showRenderer = false;


        private void OnEnable()
        {
            // Initialize all SerializedProperties by name
            _EmitterDimensions = serializedObject.FindProperty("_EmitterDimensions");
            _StartColor = serializedObject.FindProperty("_StartColor");
            _EndColor = serializedObject.FindProperty("_EndColor");

            _Opacity = serializedObject.FindProperty("_Opacity");
            _FadeInPower = serializedObject.FindProperty("_FadeInPower");
            _FadeOutPower = serializedObject.FindProperty("_FadeOutPower");
            _UseTextureRGBAlpha = serializedObject.FindProperty("_UseTextureRGBAlpha");
            _TextureAlphaSmoothstep = serializedObject.FindProperty("_TextureAlphaSmoothstep");
            _UseTextureAlpha = serializedObject.FindProperty("_UseTextureAlpha");

            _ParticleStartSize = serializedObject.FindProperty("_ParticleStartSize");
            _ParticleEndSize = serializedObject.FindProperty("_ParticleEndSize");

            _DebugTime = serializedObject.FindProperty("_DebugTime");
            _ManualTime = serializedObject.FindProperty("_ManualTime");

            _ParticleSpeed = serializedObject.FindProperty("_ParticleSpeed");
            _ParticleDirectional = serializedObject.FindProperty("_ParticleDirectional");
            _ParticleSpead = serializedObject.FindProperty("_ParticleSpead");
            _ParticleVelocityStart = serializedObject.FindProperty("_ParticleVelocityStart");
            _ParticleVelocityEnd = serializedObject.FindProperty("_ParticleVelocityEnd");

            _Rotation = serializedObject.FindProperty("_Rotation");
            _RotationRandomOffset = serializedObject.FindProperty("_RotationRandomOffset");
            _RotationSpeed = serializedObject.FindProperty("_RotationSpeed");
            _RandomizeRotationDirection = serializedObject.FindProperty("_RandomizeRotationDirection");

            _Wind = serializedObject.FindProperty("_Wind");
            _Gravityy = serializedObject.FindProperty("_Gravityy");

            _FlipbookMode = serializedObject.FindProperty("_FlipbookMode");
            _FlipBookDimenions = serializedObject.FindProperty("_FlipBookDimenions");
            _FlipBook = serializedObject.FindProperty("_FlipBook");
            _FlipX = serializedObject.FindProperty("_FlipX");
            _FlipY = serializedObject.FindProperty("_FlipY");
            _MatchParticlePhase = serializedObject.FindProperty("_MatchParticlePhase");
            _FlipBookSpeed = serializedObject.FindProperty("_FlipBookSpeed");
            _FlipbookFPS = serializedObject.FindProperty("_FlipbookFPS");

            _Loop = serializedObject.FindProperty("_Loop");
            _Prewarn = serializedObject.FindProperty("_Prewarn");
            _UseCustomTime = serializedObject.FindProperty("_UseCustomTime");
            _PlayOnAwake = serializedObject.FindProperty("_PlayOnAwake");
            _Duration = serializedObject.FindProperty("_Duration");
            _StartDelay = serializedObject.FindProperty("_StartDelay");
            _ConstantFlow = serializedObject.FindProperty("_ConstantFlow");

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        public override void OnInspectorGUI()
        {
            //base.OnInspectorGUI();

            serializedObject.Update();

            ParticlesShader particlesShader = (ParticlesShader)target;

            /*GUI.color = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.2f, 0.2f)
                : new Color(0.75f, 0.75f, 0.75f);*/

            /*GUI.backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0, 0f)
                : new Color(0.75f, 0, 0f);

            GUI.contentColor = EditorGUIUtility.isProSkin
                ? new Color(1.5f, 1.5f, 1.5f)
                : new Color(0, 0.75f, 0f);*/

            // Main properties
            showMain = EditorGUILayout.BeginFoldoutHeaderGroup(showMain, "Main");
            if (showMain)
            {

                EditorGUILayout.LabelField(new GUIContent("Details about system", "This system has a constant particle count. As base, they just loops in cycle. Currently, all calculations performs in local space. So you cannot emit more particles if needed, choose a contant. Read Custom Time tooltip for info about time. "));
                EditorGUILayout.Space(10);

                EditorGUILayout.PropertyField(_Duration, new GUIContent("Duration", "Duration of a single loop in seconds"));
                EditorGUILayout.PropertyField(_Loop, new GUIContent("Looping"));
                EditorGUILayout.PropertyField(_Prewarn, new GUIContent("Prewarn"));
                EditorGUILayout.PropertyField(_UseCustomTime, new GUIContent("Custom Time (!)", "System can work with time provided from particlesShader cs script, or rely on global shader time. If you dont use custom time, you lose ability to use time related features (such as time bounds or pause), but shader can work fully independently from cpu."));
                EditorGUILayout.PropertyField(_ConstantFlow, new GUIContent("Constant Flow", "If disabled, all particles spawn at once. Usefull for explosions. Dont support negative start delay if disabled."));
                EditorGUILayout.PropertyField(_StartDelay, new GUIContent("StartDelay"));
                
                EditorGUILayout.LabelField(new GUIContent("Start Lifetime", "Currently equal to duration of 1 cycle"), new GUIContent(_Duration.floatValue.ToString()));

                EditorGUILayout.PropertyField(_ParticleVelocityStart, new GUIContent("Start Speed"));
                EditorGUILayout.PropertyField(_ParticleStartSize, new GUIContent("Start Size"));

                EditorGUILayout.PropertyField(_Rotation, new GUIContent("Start Rotation"));
                EditorGUILayout.PropertyField(_RotationRandomOffset, new GUIContent("Random Rotation offset", "Each particle will be individually rotated by random degree"));
                EditorGUILayout.PropertyField(_RotationSpeed, new GUIContent("Rotation Speed"));
                EditorGUILayout.PropertyField(_RandomizeRotationDirection, new GUIContent("Random Rotation direction"));

                EditorGUILayout.PropertyField(_StartColor, new GUIContent("Start Color"));

                EditorGUILayout.PropertyField(_Gravityy, new GUIContent("Gravity Vector", "Just can directly type gravity vector"));

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(new GUIContent("Next settings is in development", "Currently just placeholders"));
                EditorGUILayout.LabelField(new GUIContent("Simulation Space", "Currently support only local"), new GUIContent("Local"));
                EditorGUILayout.LabelField(new GUIContent("Simulation Speed", "Currently support only 1"), new GUIContent("1"));
                EditorGUILayout.LabelField(new GUIContent("Delta Time", "Its a shader, update every frame"), new GUIContent("1"));
                EditorGUILayout.LabelField(new GUIContent("Scaling Mode", "Currently support only local"), new GUIContent("Local"));
                EditorGUILayout.PropertyField(_PlayOnAwake, new GUIContent("Play on Awake"));
                EditorGUILayout.LabelField(new GUIContent("Emitter Velocity Mode"), new GUIContent("Rigidbody"));
                EditorGUILayout.IntField(new GUIContent("Max Particles", "Since we use mesh, particles count is always constant"), -1);
                EditorGUILayout.Toggle("Auto Random Seed", true);
                EditorGUILayout.LabelField(new GUIContent("Stop Action"), new GUIContent("None"));
                EditorGUILayout.LabelField(new GUIContent("Culling Mode"), new GUIContent("Automatic"));
                EditorGUILayout.LabelField(new GUIContent("Ring Buffer Mode", "No particles relly died, so there is no allocations"), new GUIContent("Disabled"));

            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showEmission = EditorGUILayout.BeginFoldoutHeaderGroup(showEmission, "Emission");
            if (showEmission)
            {
                EditorGUILayout.IntField(new GUIContent("Rate over Time", "Currently is always constant and equal to particlesCount / duration"), -1);
                EditorGUILayout.IntField(new GUIContent("Rate over Distance"), 0);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showShape = EditorGUILayout.BeginFoldoutHeaderGroup(showShape, "Shape");
            if (showShape)
            {
                EditorGUILayout.LabelField(new GUIContent("Shape"), new GUIContent("Box"));
                EditorGUILayout.LabelField(new GUIContent("Emit from"), new GUIContent("Volume"));
                EditorGUILayout.PropertyField(_EmitterDimensions, new GUIContent("Scale"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showVelocityOverLifetime = EditorGUILayout.BeginFoldoutHeaderGroup(showVelocityOverLifetime, "Velocity over Lifetime");
            if (showVelocityOverLifetime)
            {
                EditorGUILayout.PropertyField(_ParticleDirectional, new GUIContent("Forward", "What direction considered as forward for particle"));
                EditorGUILayout.Slider(_ParticleSpead, 0, 360, new GUIContent("Particle Spread", "Particles can randomly choose direction inside this angles"));
                EditorGUILayout.PropertyField(_ParticleVelocityStart, new GUIContent("Particles Start Velocity", "Same as in main. Control what speed particle starts moving along forward direction"));
                EditorGUILayout.PropertyField(_ParticleVelocityEnd, new GUIContent("Particles End Velocity", "Control what speed particle end moving at the end lifetime"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showForceOverLifetime = EditorGUILayout.BeginFoldoutHeaderGroup(showForceOverLifetime, "Force over Lifetime");
            if (showForceOverLifetime)
            {
                EditorGUILayout.PropertyField(_Wind, new GUIContent("Wind"));
                EditorGUILayout.PropertyField(_Gravityy, new GUIContent("Gravity", "Same property as in main"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showColorOverLifetime = EditorGUILayout.BeginFoldoutHeaderGroup(showColorOverLifetime, "Color over Lifetime");
            if (showColorOverLifetime)
            {
                EditorGUILayout.PropertyField(_StartColor, new GUIContent("Start Color", "Same as in main"));
                EditorGUILayout.PropertyField(_EndColor, new GUIContent("End Color"));

                EditorGUILayout.PropertyField(_Opacity, new GUIContent("Alpha multiplayer"));
                EditorGUILayout.PropertyField(_FadeInPower, new GUIContent("Fade in power"));
                EditorGUILayout.PropertyField(_FadeOutPower, new GUIContent("Fade out power"));

                EditorGUILayout.PropertyField(_UseTextureAlpha, new GUIContent("Use Texture alpha instead of color"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showSizeOverLifetime = EditorGUILayout.BeginFoldoutHeaderGroup(showSizeOverLifetime, "Size over Lifetime");
            if (showSizeOverLifetime)
            {
                EditorGUILayout.PropertyField(_ParticleStartSize, new GUIContent("Start Size", "Same as in main"));
                EditorGUILayout.PropertyField(_ParticleEndSize, new GUIContent("End Size"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showTextureSheet = EditorGUILayout.BeginFoldoutHeaderGroup(showTextureSheet, "Texture Sheet Animation");
            if (showTextureSheet)
            {
                EditorGUILayout.PropertyField(_FlipBook, new GUIContent("Texture"));
                EditorGUILayout.PropertyField(_FlipBookDimenions, new GUIContent("Tiles"));
                EditorGUILayout.LabelField(new GUIContent("Animation"), new GUIContent("Whole Sheet"));
                EditorGUILayout.PropertyField(_FlipX, new GUIContent("FlipX"));
                EditorGUILayout.PropertyField(_FlipY, new GUIContent("FlipY"));

                EditorGUILayout.PropertyField(_FlipbookMode, new GUIContent("Time Mode", "FPS - frames changes some time per second. Match phase - select tiles based on lifetime"));

                if (particlesShader.GetFlipbookMode == ParticlesShader.FlipbookMode.fps)
                {
                    EditorGUILayout.PropertyField(_FlipbookFPS, new GUIContent("FPS"));
                }
                else
                {
                    EditorGUILayout.LabelField(new GUIContent("StartFrame"), new GUIContent("0"));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            showRenderer = EditorGUILayout.BeginFoldoutHeaderGroup(showRenderer, "Renderer");
            if (showRenderer)
            {
                EditorGUILayout.LabelField(new GUIContent("Render Mode"), new GUIContent("Billboard"));
                EditorGUILayout.LabelField(new GUIContent("Normal Direction"), new GUIContent("1"));
                EditorGUILayout.LabelField(new GUIContent("Material", "Same as in MeshFilter"), new GUIContent("None"));
                EditorGUILayout.LabelField(new GUIContent("Render Alignment"), new GUIContent("View"));
                EditorGUILayout.LabelField(new GUIContent("Cast Shadows"), new GUIContent("Off"));
                EditorGUILayout.LabelField(new GUIContent("Recive Shadows"), new GUIContent("Off"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            EditorGUILayout.Space(20);
            EditorGUILayout.Space();


            // Debug section
            showDebug = EditorGUILayout.BeginFoldoutHeaderGroup(showDebug, "Debug");
            if (showDebug)
            {
                EditorGUILayout.PropertyField(_DebugTime);
                if (particlesShader.GetDebugTime)
                {
                    //EditorGUILayout.Slider(_ManualTime, 0, 1, "Manual Time");
                    EditorGUILayout.PropertyField(_ManualTime, new GUIContent("Manual Time"));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            

            if (_undoRedoPerformed)
            {
                OnPropertiesChanged();
                _undoRedoPerformed = false;
            }


            bool wasModified = serializedObject.ApplyModifiedProperties();
            if (wasModified)
            {
                OnPropertiesChanged();
            }
            else if (Event.current.commandName == "UndoRedoPerformed")
            {
                // This handles direct undo/redo events
                OnPropertiesChanged();
            }

            particlesShader.SetParticleTime();


        }

        void OnSceneGUI()
        {
            ParticlesShader targetScript = (ParticlesShader)target;

            Vector3 emitterDimensions = targetScript.GetEmitterDimensions;
            Transform targetTransform = targetScript.transform;

            Handles.color = Color.blue;
            // Draw based on emitter dimensions
            if (emitterDimensions == Vector3.zero)
            {
                // Draw blue point at origin
                float pointSize = HandleUtility.GetHandleSize(targetTransform.position) * 0.1f;
                Handles.DrawWireDisc(targetTransform.position,
                                     SceneView.currentDrawingSceneView.camera.transform.forward,
                                     pointSize);
            }
            else
            {
                // Calculate absolute dimensions for the box
                Vector3 absDimensions = new Vector3(
                    Mathf.Abs(emitterDimensions.x),
                    Mathf.Abs(emitterDimensions.y),
                    Mathf.Abs(emitterDimensions.z)
                );

                // Apply object's transformation matrix
                Matrix4x4 originalMatrix = Handles.matrix;
                Handles.matrix = targetTransform.localToWorldMatrix;

                // Draw wireframe box in local space
                Handles.color = Color.blue;
                Handles.DrawWireCube(Vector3.zero, absDimensions);

                // Restore original matrix
                Handles.matrix = originalMatrix;
            }


            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(Screen.width - 210, Screen.height - 155, 200, 120));

            float buttonHeight = 25;

            if (GUILayout.Button("Start Spawn", GUILayout.Height(buttonHeight)))
            {
                targetScript.Play();
            }

            if (GUILayout.Button("Stop Spawn", GUILayout.Height(buttonHeight)))
            {
                targetScript.Stop();
            }

            string pauseText = targetScript.IsPaused ? "Unpause" : "Pause";
            if (GUILayout.Button(pauseText, GUILayout.Height(buttonHeight)))
            {
                if (targetScript.IsPaused)
                {
                    targetScript.Unpause();
                }
                else
                {
                    targetScript.Pause();
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Height(buttonHeight)))
            {
                targetScript.Stop(true);
            }

            targetScript.SetParticleTime();

            GUILayout.EndArea();
            Handles.EndGUI();
        }


        private void OnPropertiesChanged()
        {
            ParticlesShader particlesShader = (ParticlesShader)target;
            // Add your custom property change handling logic here
            // Example: shader.UpdateMaterialProperties();

            if (particlesShader.TryGetComponent<Renderer>(out var renderer))
            {
                if (renderer.sharedMaterial != null)
                {
                    particlesShader.SaveValues(renderer.sharedMaterial);
                }
                /*MaterialPropertyBlock props = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(props);

                // Set material properties based on new values
                props.SetColor("_StartColor", shader.StartColor);
                renderer.SetPropertyBlock(props);*/
            }
        }

        private void OnUndoRedoPerformed()
        {
            _undoRedoPerformed = true;
        }

    }
}

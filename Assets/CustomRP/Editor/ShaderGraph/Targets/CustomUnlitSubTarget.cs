using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;

// ──────────────────────────────────────────────────────────────────────────────
// CustomUnlitSubTarget — zero URP runtime dependency at HLSL level.
//
// All includes point to either:
//   - SRP Core (com.unity.render-pipelines.core)
//   - Our own custom HLSL files
//
// Place in: com.unity.render-pipelines.universal/Editor/ShaderGraph/Targets/
// ──────────────────────────────────────────────────────────────────────────────
namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    sealed class CustomUnlitSubTarget : SubTarget<CustomTarget>
    {
        static readonly GUID kSourceCodeGuid = new GUID("196285c06c642b447be3f23af33c8a9a");

        public override int latestVersion => 0;

        public CustomUnlitSubTarget()
        {
            displayName = "Custom Unlit";
        }

        public override bool IsActive() => true;

        // ── Setup ────────────────────────────────────────────────────────────
        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);
            context.AddSubShader(BuildSubShader());
        }

        SubShaderDescriptor BuildSubShader()
        {
            return new SubShaderDescriptor
            {
                pipelineTag = "",
                renderType = "Opaque",
                renderQueue = "Geometry",
                generatesPreview = true,
                passes = new PassCollection
                {
                    ForwardPass(),
                    SceneSelectionPass(),
                    ScenePickingPass(),
                }
            };
        }

        // ── Forward pass ─────────────────────────────────────────────────────
        PassDescriptor ForwardPass()
        {
            var result = new PassDescriptor
            {
                displayName = "Custom Forward",
                referenceName = "SHADERPASS_UNLIT",
                lightMode = "CustomLit",               // same as your CRP reference shader
                useInPreview = true,

                passTemplatePath = CustomTarget.kTemplatePath,
                sharedTemplateDirectories = CustomTarget.kSharedTemplateDirectories,

                validVertexBlocks = new[]
                {
                    BlockFields.VertexDescription.Position,
                    BlockFields.VertexDescription.Normal,
                    BlockFields.VertexDescription.Tangent,
                },
                validPixelBlocks = new[]
                {
                    BlockFields.SurfaceDescription.BaseColor,
                    BlockFields.SurfaceDescription.Alpha,
                    BlockFields.SurfaceDescription.AlphaClipThreshold,
                },

                structs = CustomStructCollections.Default,
                requiredFields = CustomRequiredFields.Forward,
                fieldDependencies = CoreFieldDependencies.Default,

                renderStates = new RenderStateCollection
                {
                    { RenderState.ZTest(ZTest.LEqual) },
                    { RenderState.ZWrite(ZWrite.On) },
                    { RenderState.Cull(Cull.Back) },
                    { RenderState.Blend(Blend.One, Blend.Zero) },
                },

                pragmas = CustomPragmas.Forward,
                defines = new DefineCollection(),
                keywords = new KeywordCollection(),
                includes = CustomIncludes.Forward,

                customInterpolators = CoreCustomInterpDescriptors.Common,
            };

            if (target.alphaClip)
                result.defines.Add(CoreKeywordDescriptors.AlphaTestOn, 1);

            return result;
        }

        PassDescriptor SceneSelectionPass()
        {
            var result = new PassDescriptor
            {
                displayName = "SceneSelectionPass",
                referenceName = "SHADERPASS_DEPTHONLY",
                lightMode = "SceneSelectionPass",
                useInPreview = false,

                passTemplatePath = CustomTarget.kTemplatePath,
                sharedTemplateDirectories = CustomTarget.kSharedTemplateDirectories,

                validVertexBlocks = new[]
                {
                    BlockFields.VertexDescription.Position,
                    BlockFields.VertexDescription.Normal,
                    BlockFields.VertexDescription.Tangent,
                },
                validPixelBlocks = new[]
                {
                    BlockFields.SurfaceDescription.Alpha,
                    BlockFields.SurfaceDescription.AlphaClipThreshold,
                },

                structs = CustomStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,

                renderStates = new RenderStateCollection
                {
                    { RenderState.Cull(Cull.Off) },
                },

                pragmas = CustomPragmas.Default,
                defines = new DefineCollection
                {
                    CoreDefines.SceneSelection,
                    { CoreKeywordDescriptors.AlphaClipThreshold, 1 },
                },
                keywords = new KeywordCollection(),
                includes = CustomIncludes.SelectionPicking,

                customInterpolators = CoreCustomInterpDescriptors.Common,
            };

            if (target.alphaClip)
                result.defines.Add(CoreKeywordDescriptors.AlphaTestOn, 1);

            return result;
        }

        PassDescriptor ScenePickingPass()
        {
            var result = new PassDescriptor
            {
                displayName = "ScenePickingPass",
                referenceName = "SHADERPASS_DEPTHONLY",
                lightMode = "Picking",
                useInPreview = false,

                passTemplatePath = CustomTarget.kTemplatePath,
                sharedTemplateDirectories = CustomTarget.kSharedTemplateDirectories,

                validVertexBlocks = new[]
                {
                    BlockFields.VertexDescription.Position,
                    BlockFields.VertexDescription.Normal,
                    BlockFields.VertexDescription.Tangent,
                },
                validPixelBlocks = new[]
                {
                    BlockFields.SurfaceDescription.Alpha,
                    BlockFields.SurfaceDescription.AlphaClipThreshold,
                },

                structs = CustomStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,

                renderStates = new RenderStateCollection
                {
                    { RenderState.Cull(Cull.Back) },
                },

                pragmas = CustomPragmas.Default,
                defines = new DefineCollection
                {
                    CoreDefines.ScenePicking,
                    { CoreKeywordDescriptors.AlphaClipThreshold, 1 },
                },
                keywords = new KeywordCollection(),
                includes = CustomIncludes.SelectionPicking,

                customInterpolators = CoreCustomInterpDescriptors.Common,
            };

            if (target.alphaClip)
                result.defines.Add(CoreKeywordDescriptors.AlphaTestOn, 1);

            return result;
        }

        // ── Blocks / Fields ──────────────────────────────────────────────────

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            context.AddBlock(BlockFields.SurfaceDescription.Alpha, target.alphaClip);
            context.AddBlock(BlockFields.SurfaceDescription.AlphaClipThreshold, target.alphaClip);
        }

        public override void GetFields(ref TargetFieldContext context) { }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<string> registerUndo) { }

        // ──────────────────────────────────────────────────────────────────────
        // Static data: structs, pragmas, includes
        // ALL includes are either SRP Core or our own custom HLSL.
        // ──────────────────────────────────────────────────────────────────────

        static class CustomStructCollections
        {
            public static readonly StructCollection Default = new StructCollection
            {
                { Structs.Attributes },
                { UniversalStructs.Varyings },
                { Structs.SurfaceDescriptionInputs },
                { Structs.VertexDescriptionInputs },
            };
        }

        static class CustomRequiredFields
        {
            public static readonly FieldCollection Forward = new FieldCollection
            {
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
            };
        }

        static class CustomPragmas
        {
            public static readonly PragmaCollection Default = new PragmaCollection
            {
                { Pragma.Target(ShaderModel.Target20) },
                { Pragma.MultiCompileInstancing },
                { Pragma.Vertex("vert") },
                { Pragma.Fragment("frag") },
            };

            public static readonly PragmaCollection Forward = new PragmaCollection
            {
                { Pragma.Target(ShaderModel.Target20) },
                { Pragma.MultiCompileInstancing },
                { Pragma.Vertex("vert") },
                { Pragma.Fragment("frag") },
            };
        }

        static class CustomIncludes
        {
            // Our standalone HLSL files — no URP dependency
            // TODO: replace /Includes with absolute project path
            const string kCustomCore = "Assets/CustomRP/Editor/ShaderGraph/Includes/CustomCore.hlsl";
            const string kCustomGraphFuncs = "Assets/CustomRP/Editor/ShaderGraph/Includes/CustomGraphFunctions.hlsl";
            const string kCustomVaryings = "Assets/CustomRP/Editor/ShaderGraph/Includes/CustomVaryings.hlsl";
            const string kCustomForwardPass = "Assets/CustomRP/Editor/ShaderGraph/Includes/CustomForwardPass.hlsl";
            const string kCustomSelectionPickingPass = "Assets/CustomRP/Editor/ShaderGraph/Includes/CustomSelectionPickingPass.hlsl";

            // ShaderPass.hlsl only defines SHADERPASS_* enum constants — fine to reuse
            const string kShaderPass = "Assets/CustomRP/Editor/ShaderGraph/Includes/ShaderPass.hlsl";

            public static readonly IncludeCollection Forward = new IncludeCollection
            {
                // Pre-graph: foundation
                { kCustomCore, IncludeLocation.Pregraph },
                { kCustomGraphFuncs, IncludeLocation.Pregraph },

                // Post-graph: varyings builder + pass
                { kShaderPass, IncludeLocation.Pregraph },
                { kCustomVaryings, IncludeLocation.Postgraph },
                { kCustomForwardPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection SelectionPicking = new IncludeCollection
            {
                { kCustomCore, IncludeLocation.Pregraph },
                { kCustomGraphFuncs, IncludeLocation.Pregraph },

                { kShaderPass, IncludeLocation.Pregraph },
                { kCustomVaryings, IncludeLocation.Postgraph },
                { kCustomSelectionPickingPass, IncludeLocation.Postgraph },
            };
        }
    }
}

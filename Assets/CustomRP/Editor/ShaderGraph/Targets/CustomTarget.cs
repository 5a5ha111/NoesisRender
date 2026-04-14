using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Rendering.BuiltIn;
using UnityEditor.Rendering.BuiltIn.ShaderGraph;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

// ──────────────────────────────────────────────────────────────────────────────
// CustomTarget — standalone ShaderGraph Target that lives *inside* the URP
// Editor assembly (same namespace, same access to sealed internals).
//
// This is NOT a SubTarget of UniversalTarget. It appears as a separate entry
// in the ShaderGraph "Active Targets" list and owns its own SubTarget tree.
//
// Place this file in:
//   Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Targets/
// ──────────────────────────────────────────────────────────────────────────────
namespace UnityEditor.Rendering.Universal.ShaderGraph
{

    enum SurfaceType
    {
        Opaque,
        Transparent,
    }

    enum AlphaMode
    {
        Alpha,
        Premultiply,
        Additive,
        Multiply,
    }

    internal enum RenderFace
    {
        Front = 2,      // = CullMode.Back -- render front face only
        Back = 1,       // = CullMode.Front -- render back face only
        Both = 0        // = CullMode.Off -- render both faces
    }


    sealed class CustomTarget : Target
    {
        // ── constants ────────────────────────────────────────────────────────
        // GUID of THIS .cs file's .meta — triggers shader reimport on edit.
        // Replace with the real guid from CustomTarget.cs.meta after Unity imports it.
        static readonly GUID kSourceCodeGuid = new GUID("11d0c6f29a21b8a4baf9b34b13717e9f");

        public const string kPipelineTag = "UniversalPipeline";

        // Reuse the URP uber template — it already handles $splice() for all
        // standard collections (structs, pragmas, includes, render states…).
        //public const string kTemplatePath = UniversalTarget.kUberTemplatePath;
        public const string kTemplatePath = "Assets/CustomRP/Editor/ShaderGraph/Templates/ShaderPass.template";

        // SharedTemplateDirectories supply $include() search paths for the template.
        //public static readonly string[] kSharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories;
        public static readonly string[] kSharedTemplateDirectories = GenerationUtils.GetDefaultSharedTemplateDirectories().Union(new string[]
        {
            "Assets/CustomRP/Editor/ShaderGraph/Templates"
            #if HAS_VFX_GRAPH
                , "Packages/com.unity.visualeffectgraph/Editor/ShaderGraph/Templates"
            #endif
        }).ToArray();

        // ── subtarget management ─────────────────────────────────────────────
        List<SubTarget> m_SubTargets;
        List<string> m_SubTargetNames;
        int activeSubTargetIndex => m_SubTargets.IndexOf(m_ActiveSubTarget);

        [SerializeField]
        JsonData<SubTarget> m_ActiveSubTarget;

        // ── serialized settings (expose whatever you need) ───────────────────
        /*[SerializeField] SurfaceType m_SurfaceType = SurfaceType.Opaque;
        [SerializeField] AlphaMode m_AlphaMode = AlphaMode.Alpha;
        [SerializeField] RenderFace m_RenderFace = RenderFace.Front;
        [SerializeField] bool m_AlphaClip;

        // ── public accessors (subtargets read these) ─────────────────────────
        public SurfaceType surfaceType => m_SurfaceType;
        public AlphaMode alphaMode => m_AlphaMode;
        public RenderFace renderFace => m_RenderFace;
        public bool alphaClip => m_AlphaClip;

        public SubTarget activeSubTarget => m_ActiveSubTarget.value;

        public string renderType =>
            surfaceType == SurfaceType.Transparent ? $"{RenderType.Transparent}" : $"{RenderType.Opaque}";

        public string renderQueue =>
            surfaceType == SurfaceType.Transparent ? $"{UnityEngine.Rendering.RenderQueue.Transparent}" : $"{UnityEngine.Rendering.RenderQueue.Geometry}";*/

        [SerializeField] bool m_AlphaClip;

        public bool alphaClip => m_AlphaClip;
        public SubTarget activeSubTarget => m_ActiveSubTarget.value;

        // ── version ──────────────────────────────────────────────────────────
        public override int latestVersion => 1;

        // ── constructor ──────────────────────────────────────────────────────
        public CustomTarget()
        {
            displayName = "Noesis";

            // Auto-discover all SubTarget<CustomTarget> in the domain.
            m_SubTargets = TargetUtils.GetSubTargets(this);
            m_SubTargetNames = m_SubTargets.Select(x => x.displayName).ToList();
            TargetUtils.ProcessSubTargetList(ref m_ActiveSubTarget, ref m_SubTargets);
        }

        // ── Target API ───────────────────────────────────────────────────────

        public override bool IsActive()
        {
            return GraphicsSettings.currentRenderPipeline is NoesisRender.CustomRenderPipelineAsset
                && activeSubTarget != null
                && activeSubTarget.IsActive();
        }

        public override bool WorksWithSRP(RenderPipelineAsset scriptableRenderPipeline)
        {
            //return scriptableRenderPipeline?.GetType() == typeof(UniversalRenderPipelineAsset);
            return true;
        }

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);

            TargetUtils.ProcessSubTargetList(ref m_ActiveSubTarget, ref m_SubTargets);
            m_ActiveSubTarget.value.target = this;
            m_ActiveSubTarget.value.Setup(ref context);
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            var descs = context.blocks.Select(x => x.descriptor);
            context.AddField(Fields.GraphVertex,
                descs.Contains(BlockFields.VertexDescription.Position) ||
                descs.Contains(BlockFields.VertexDescription.Normal) ||
                descs.Contains(BlockFields.VertexDescription.Tangent));
            context.AddField(Fields.GraphPixel);

            m_ActiveSubTarget.value.GetFields(ref context);
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            // Always-on vertex blocks
            context.AddBlock(BlockFields.VertexDescription.Position);
            context.AddBlock(BlockFields.VertexDescription.Normal);
            context.AddBlock(BlockFields.VertexDescription.Tangent);

            // Always-on fragment block
            context.AddBlock(BlockFields.SurfaceDescription.BaseColor);

            m_ActiveSubTarget.value.GetActiveBlocks(ref context);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            base.CollectShaderProperties(collector, generationMode);
            m_ActiveSubTarget.value.CollectShaderProperties(collector, generationMode);
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            m_ActiveSubTarget.value.ProcessPreviewMaterial(material);
        }

        public override object saveContext => m_ActiveSubTarget.value?.saveContext;

        // ── GUI ──────────────────────────────────────────────────────────────

        PopupField<string> m_SubTargetField;

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<string> registerUndo)
        {
            m_SubTargetField = new PopupField<string>(m_SubTargetNames, activeSubTargetIndex);
            context.AddProperty("Material", m_SubTargetField, (evt) =>
            {
                if (Equals(activeSubTargetIndex, m_SubTargetField.index)) return;
                registerUndo("Change Material");
                m_ActiveSubTarget = m_SubTargets[m_SubTargetField.index];
                onChange();
            });

            context.AddProperty("Alpha Clip", new Toggle { value = m_AlphaClip }, (evt) =>
            {
                if (Equals(m_AlphaClip, evt.newValue)) return;
                registerUndo("Change Alpha Clip");
                m_AlphaClip = evt.newValue;
                onChange();
            });

            m_ActiveSubTarget.value.GetPropertiesGUI(ref context, onChange, registerUndo);
        }

        // ── IHasMetadata ─────────────────────────────────────────────────────
        /*string IHasMetadata.identifier =>
            (m_ActiveSubTarget.value is IHasMetadata sub) ? sub.identifier : null;

        ScriptableObject IHasMetadata.GetMetadataObject(GraphDataReadOnly graph) =>
            (m_ActiveSubTarget.value is IHasMetadata sub) ? sub.GetMetadataObject(graph) : null;*/
    }


    #region FieldDependencies
    static class CoreFieldDependencies
    {
        public static readonly DependencyCollection Default = new DependencyCollection()
        {
            { FieldDependencies.Default },
            new FieldDependency(UniversalStructFields.Varyings.stereoTargetEyeIndexAsRTArrayIdx,    StructFields.Attributes.instanceID),
            new FieldDependency(UniversalStructFields.Varyings.stereoTargetEyeIndexAsBlendIdx0,     StructFields.Attributes.instanceID),
        };
    }
    #endregion


    #region Pragmas
    static class CorePragmas
    {
        public static readonly PragmaCollection Default = new PragmaCollection
        {
            { Pragma.Target(ShaderModel.Target20) },
            { Pragma.Vertex("vert") },
            { Pragma.Fragment("frag") },
        };

        public static readonly PragmaCollection Instanced = new PragmaCollection
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
            { Pragma.MultiCompileFog },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer) },
            { Pragma.Vertex("vert") },
            { Pragma.Fragment("frag") },
        };

        public static readonly PragmaCollection _2DDefault = new PragmaCollection
        {
            { Pragma.Target(ShaderModel.Target20) },
            { Pragma.ExcludeRenderers(new[] { Platform.D3D9 }) },
            { Pragma.MultiCompileInstancing },
            { Pragma.MultiCompileFog },
            { Pragma.Vertex("vert") },
            { Pragma.Fragment("frag") },
        };

        public static readonly PragmaCollection GBuffer = new PragmaCollection
        {
            { Pragma.Target(ShaderModel.Target45) },
            { Pragma.ExcludeRenderers(new[] { Platform.GLES, Platform.GLES3, Platform.GLCore }) },
            { Pragma.MultiCompileInstancing },
            { Pragma.MultiCompileFog },
            { Pragma.InstancingOptions(InstancingOptions.RenderingLayer) },
            { Pragma.Vertex("vert") },
            { Pragma.Fragment("frag") },
        };
    }
    #endregion


    #region Defines
    static class CoreDefines
    {
        public static readonly DefineCollection UseLegacySpriteBlocks = new DefineCollection
        {
            { CoreKeywordDescriptors.UseLegacySpriteBlocks, 1, new FieldCondition(CoreFields.UseLegacySpriteBlocks, true) },
        };

        /*public static readonly DefineCollection UseFragmentFog = new DefineCollection()
        {
            {CoreKeywordDescriptors.UseFragmentFog, 1},
        };*/

        public static readonly DefineCollection SceneSelection = new DefineCollection
        {
            { CoreKeywordDescriptors.SceneSelectionPass, 1 },
        };

        public static readonly DefineCollection ScenePicking = new DefineCollection
        {
            { CoreKeywordDescriptors.ScenePickingPass, 1 },
        };
    }
    #endregion


    #region CustomInterpolators
    static class CoreCustomInterpDescriptors
    {
        public static readonly CustomInterpSubGen.Collection Common = new CustomInterpSubGen.Collection
        {
            // Custom interpolators are not explicitly defined in the SurfaceDescriptionInputs template.
            // This entry point will let us generate a block of pass-through assignments for each field.
            CustomInterpSubGen.Descriptor.MakeBlock(CustomInterpSubGen.Splice.k_spliceCopyToSDI, "output", "input"),

            // sgci_PassThroughFunc is called from BuildVaryings in Varyings.hlsl to copy custom interpolators from vertex descriptions.
            // this entry point allows for the function to be defined before it is used.
            CustomInterpSubGen.Descriptor.MakeFunc(CustomInterpSubGen.Splice.k_splicePreSurface, "CustomInterpolatorPassThroughFunc", "Varyings", "VertexDescription", "CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC", "FEATURES_GRAPH_VERTEX")
        };
    }
    #endregion



    #region KeywordDescriptors
    static class CoreKeywordDescriptors
    {

        public static readonly KeywordDescriptor UseLegacySpriteBlocks = new KeywordDescriptor()
        {
            displayName = "UseLegacySpriteBlocks",
            referenceName = "USELEGACYSPRITEBLOCKS",
            type = KeywordType.Boolean,
        };

        public static readonly KeywordDescriptor SceneSelectionPass = new KeywordDescriptor()
        {
            displayName = "Scene Selection Pass",
            referenceName = "SCENESELECTIONPASS",
            type = KeywordType.Boolean,
        };


        public static readonly KeywordDescriptor ScenePickingPass = new KeywordDescriptor()
        {
            displayName = "Scene Picking Pass",
            referenceName = "SCENEPICKINGPASS",
            type = KeywordType.Boolean,
        };


        public static readonly KeywordDescriptor AlphaClipThreshold = new KeywordDescriptor()
        {
            displayName = "AlphaClipThreshold",
            referenceName = "ALPHA_CLIP_THRESHOLD",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.Predefined,
        };

        public static readonly KeywordDescriptor LightCookies = new KeywordDescriptor()
        {
            displayName = "Light Cookies",
            referenceName = "_LIGHT_COOKIES",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
            stages = KeywordShaderStage.Fragment,
        };


        public static readonly KeywordDescriptor AlphaTestOn = new KeywordDescriptor()
        {
            displayName = ShaderKeywordStrings._ALPHATEST_ON,
            referenceName = ShaderKeywordStrings._ALPHATEST_ON,
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
            stages = KeywordShaderStage.Fragment,
        };


        public static readonly KeywordDescriptor StaticLightmap = new KeywordDescriptor()
        {
            displayName = "Static Lightmap",
            referenceName = "LIGHTMAP_ON",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static readonly KeywordDescriptor DynamicLightmap = new KeywordDescriptor()
        {
            displayName = "Dynamic Lightmap",
            referenceName = "DYNAMICLIGHTMAP_ON",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };

        public static readonly KeywordDescriptor DirectionalLightmapCombined = new KeywordDescriptor()
        {
            displayName = "Directional Lightmap Combined",
            referenceName = "DIRLIGHTMAP_COMBINED",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
        };


        // ====================================================
        // To do: create ShaderKeywordStrings class for easy of control of noesis keywords in cs files.
        // ====================================================

        /*public static readonly KeywordDescriptor SurfaceTypeTransparent = new KeywordDescriptor()
        {
            displayName = ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT,
            referenceName = ShaderKeywordStrings._SURFACE_TYPE_TRANSPARENT,
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Global, // needs to match HDRP
            stages = KeywordShaderStage.Fragment,
        };

        public static readonly KeywordDescriptor AlphaPremultiplyOn = new KeywordDescriptor()
        {
            displayName = ShaderKeywordStrings._ALPHAPREMULTIPLY_ON,
            referenceName = ShaderKeywordStrings._ALPHAPREMULTIPLY_ON,
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
            stages = KeywordShaderStage.Fragment,
        };

        public static readonly KeywordDescriptor AlphaModulateOn = new KeywordDescriptor()
        {
            displayName = ShaderKeywordStrings._ALPHAMODULATE_ON,
            referenceName = ShaderKeywordStrings._ALPHAMODULATE_ON,
            type = KeywordType.Boolean,
            definition = KeywordDefinition.ShaderFeature,
            scope = KeywordScope.Local,
            stages = KeywordShaderStage.Fragment,
        };*/

        public static readonly KeywordDescriptor EvaluateSh = new KeywordDescriptor()
        {
            displayName = "Evaluate SH",
            referenceName = "EVALUATE_SH",
            type = KeywordType.Enum,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
            entries = new KeywordEntry[]
            {
                new KeywordEntry() { displayName = "Off", referenceName = "" },
                new KeywordEntry() { displayName = "Evaluate SH Mixed", referenceName = "MIXED" },
                new KeywordEntry() { displayName = "Evaluate SH Vertex", referenceName = "VERTEX" },
            }
        };


        /*public static readonly KeywordDescriptor LODFadeCrossFade = new KeywordDescriptor()
        {
            displayName = ShaderKeywordStrings.LOD_FADE_CROSSFADE,
            referenceName = ShaderKeywordStrings.LOD_FADE_CROSSFADE,
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,

            // Note: SpeedTree shaders used to have their own PS-based Crossfade,
            //       as well as a VS-based smooth LOD transition effect.
            //       These shaders need the LOD_FADE_CROSSFADE keyword in the VS
            //       to skip the VS-based effect.
            scope = KeywordScope.Global
        };*/


        public static readonly KeywordDescriptor CastingPunctualLightShadow = new KeywordDescriptor()
        {
            displayName = "Casting Punctual Light Shadow",
            referenceName = "_CASTING_PUNCTUAL_LIGHT_SHADOW",
            type = KeywordType.Boolean,
            definition = KeywordDefinition.MultiCompile,
            scope = KeywordScope.Global,
            stages = KeywordShaderStage.Vertex,
        };

    }
    #endregion




}

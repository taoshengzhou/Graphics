using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ShaderGraph;
using UnityEngine.Rendering;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.ShaderGraph.Legacy;

namespace UnityEditor.Rendering.BuiltIn.ShaderGraph
{
    sealed class BuiltInLitSubTarget : SubTarget<BuiltInTarget>, ILegacyTarget
    {
        static readonly GUID kSourceCodeGuid = new GUID("8c2d5b55aa47443878a55a05f4294270"); // BuiltInLitSubTarget.cs

        [SerializeField]
        WorkflowMode m_WorkflowMode = WorkflowMode.Metallic;

        [SerializeField]
        NormalDropOffSpace m_NormalDropOffSpace = NormalDropOffSpace.Tangent;

        public BuiltInLitSubTarget()
        {
            displayName = "Lit";
        }

        public WorkflowMode workflowMode
        {
            get => m_WorkflowMode;
            set => m_WorkflowMode = value;
        }

        public NormalDropOffSpace normalDropOffSpace
        {
            get => m_NormalDropOffSpace;
            set => m_NormalDropOffSpace = value;
        }

        public override bool IsActive() => true;

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);
            //context.AddCustomEditorForRenderPipeline("ShaderGraph.PBRMasterGUI", typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)); // TODO: This should be owned by URP

            // Process SubShaders
            SubShaderDescriptor[] litSubShaders = { SubShaders.LitComputeDOTS, SubShaders.LitGLES };
            SubShaderDescriptor[] complexLitSubShaders = { SubShaders.ComplexLitComputeDOTS, SubShaders.LitGLESForwardOnly};

            SubShaderDescriptor[] subShaders = litSubShaders;
            //SubShaderDescriptor[] subShaders = { SubShaders.LitGLES };
            for (int i = 0; i < subShaders.Length; i++)
            {
                // Update Render State
                subShaders[i].renderType = target.renderType;
                subShaders[i].renderQueue = target.renderQueue;

                context.AddSubShader(subShaders[i]);
            }
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            var descs = context.blocks.Select(x => x.descriptor);
            // Surface Type & Blend Mode
            // These must be set per SubTarget as Sprite SubTargets override them
            context.AddField(BuiltInFields.SurfaceOpaque,       target.surfaceType == SurfaceType.Opaque);
            context.AddField(BuiltInFields.SurfaceTransparent,  target.surfaceType != SurfaceType.Opaque);
            context.AddField(BuiltInFields.BlendAdd,            target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Additive);
            context.AddField(Fields.BlendAlpha,                 target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Alpha);
            context.AddField(BuiltInFields.BlendMultiply,       target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Multiply);
            context.AddField(BuiltInFields.BlendPremultiply,    target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Premultiply);

            // Lit
            context.AddField(BuiltInFields.NormalDropOffOS,     normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddField(BuiltInFields.NormalDropOffTS,     normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddField(BuiltInFields.NormalDropOffWS,     normalDropOffSpace == NormalDropOffSpace.World);
            context.AddField(BuiltInFields.SpecularSetup,       workflowMode == WorkflowMode.Specular);
            context.AddField(BuiltInFields.Normal,              descs.Contains(BlockFields.SurfaceDescription.NormalOS) ||
                descs.Contains(BlockFields.SurfaceDescription.NormalTS) ||
                descs.Contains(BlockFields.SurfaceDescription.NormalWS));
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            context.AddBlock(BlockFields.SurfaceDescription.Smoothness);
            context.AddBlock(BlockFields.SurfaceDescription.NormalOS,           normalDropOffSpace == NormalDropOffSpace.Object);
            context.AddBlock(BlockFields.SurfaceDescription.NormalTS,           normalDropOffSpace == NormalDropOffSpace.Tangent);
            context.AddBlock(BlockFields.SurfaceDescription.NormalWS,           normalDropOffSpace == NormalDropOffSpace.World);
            context.AddBlock(BlockFields.SurfaceDescription.Emission);
            context.AddBlock(BlockFields.SurfaceDescription.Occlusion);
            context.AddBlock(BlockFields.SurfaceDescription.Specular,           workflowMode == WorkflowMode.Specular);
            context.AddBlock(BlockFields.SurfaceDescription.Metallic,           workflowMode == WorkflowMode.Metallic);
            context.AddBlock(BlockFields.SurfaceDescription.Alpha,              target.surfaceType == SurfaceType.Transparent || target.alphaClip);
            context.AddBlock(BlockFields.SurfaceDescription.AlphaClipThreshold, target.alphaClip);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
            // Temporarily remove the workflow mode until specular is supported
            //context.AddProperty("Workflow", new EnumField(WorkflowMode.Metallic) { value = workflowMode }, (evt) =>
            //{
            //    if (Equals(workflowMode, evt.newValue))
            //        return;

            //    registerUndo("Change Workflow");
            //    workflowMode = (WorkflowMode)evt.newValue;
            //    onChange();
            //});

            context.AddProperty("Surface", new EnumField(SurfaceType.Opaque) { value = target.surfaceType }, (evt) =>
            {
                if (Equals(target.surfaceType, evt.newValue))
                    return;

                registerUndo("Change Surface");
                target.surfaceType = (SurfaceType)evt.newValue;
                onChange();
            });

            context.AddProperty("Blend", new EnumField(AlphaMode.Alpha) { value = target.alphaMode }, target.surfaceType == SurfaceType.Transparent, (evt) =>
            {
                if (Equals(target.alphaMode, evt.newValue))
                    return;

                registerUndo("Change Blend");
                target.alphaMode = (AlphaMode)evt.newValue;
                onChange();
            });

            context.AddProperty("Alpha Clip", new Toggle() { value = target.alphaClip }, (evt) =>
            {
                if (Equals(target.alphaClip, evt.newValue))
                    return;

                registerUndo("Change Alpha Clip");
                target.alphaClip = evt.newValue;
                onChange();
            });

            context.AddProperty("Two Sided", new Toggle() { value = target.twoSided }, (evt) =>
            {
                if (Equals(target.twoSided, evt.newValue))
                    return;

                registerUndo("Change Two Sided");
                target.twoSided = evt.newValue;
                onChange();
            });

            context.AddProperty("Fragment Normal Space", new EnumField(NormalDropOffSpace.Tangent) { value = normalDropOffSpace }, (evt) =>
            {
                if (Equals(normalDropOffSpace, evt.newValue))
                    return;

                registerUndo("Change Fragment Normal Space");
                normalDropOffSpace = (NormalDropOffSpace)evt.newValue;
                onChange();
            });
        }

        public bool TryUpgradeFromMasterNode(IMasterNode1 masterNode, out Dictionary<BlockFieldDescriptor, int> blockMap)
        {
            blockMap = null;
            if (!(masterNode is PBRMasterNode1 pbrMasterNode))
                return false;

            m_WorkflowMode = (WorkflowMode)pbrMasterNode.m_Model;
            m_NormalDropOffSpace = (NormalDropOffSpace)pbrMasterNode.m_NormalDropOffSpace;

            // Handle mapping of Normal block specifically
            BlockFieldDescriptor normalBlock;
            switch (m_NormalDropOffSpace)
            {
                case NormalDropOffSpace.Object:
                    normalBlock = BlockFields.SurfaceDescription.NormalOS;
                    break;
                case NormalDropOffSpace.World:
                    normalBlock = BlockFields.SurfaceDescription.NormalWS;
                    break;
                default:
                    normalBlock = BlockFields.SurfaceDescription.NormalTS;
                    break;
            }

            // PBRMasterNode adds/removes Metallic/Specular based on settings
            BlockFieldDescriptor specularMetallicBlock;
            int specularMetallicId;
            if (m_WorkflowMode == WorkflowMode.Specular)
            {
                specularMetallicBlock = BlockFields.SurfaceDescription.Specular;
                specularMetallicId = 3;
            }
            else
            {
                specularMetallicBlock = BlockFields.SurfaceDescription.Metallic;
                specularMetallicId = 2;
            }

            // Set blockmap
            blockMap = new Dictionary<BlockFieldDescriptor, int>()
            {
                { BlockFields.VertexDescription.Position, 9 },
                { BlockFields.VertexDescription.Normal, 10 },
                { BlockFields.VertexDescription.Tangent, 11 },
                { BlockFields.SurfaceDescription.BaseColor, 0 },
                { normalBlock, 1 },
                { specularMetallicBlock, specularMetallicId },
                { BlockFields.SurfaceDescription.Emission, 4 },
                { BlockFields.SurfaceDescription.Smoothness, 5 },
                { BlockFields.SurfaceDescription.Occlusion, 6 },
                { BlockFields.SurfaceDescription.Alpha, 7 },
                { BlockFields.SurfaceDescription.AlphaClipThreshold, 8 },
            };

            return true;
        }

        #region SubShader
        static class SubShaders
        {
            // Overloads to do inline PassDescriptor modifications
            // NOTE: param order should match PassDescriptor field order for consistency
            #region PassVariant
            private static PassDescriptor PassVariant(in PassDescriptor source, PragmaCollection pragmas)
            {
                var result = source;
                result.pragmas = pragmas;
                return result;
            }

            private static PassDescriptor PassVariant(in PassDescriptor source, BlockFieldDescriptor[] vertexBlocks, BlockFieldDescriptor[] pixelBlocks, PragmaCollection pragmas, DefineCollection defines)
            {
                var result = source;
                result.validVertexBlocks = vertexBlocks;
                result.validPixelBlocks = pixelBlocks;
                result.pragmas = pragmas;
                result.defines = defines;
                return result;
            }

            #endregion

            // SM 4.5, compute with dots instancing
            public readonly static SubShaderDescriptor LitComputeDOTS = new SubShaderDescriptor()
            {
                //pipelineTag = BuiltInTarget.kPipelineTag,
                customTags = BuiltInTarget.kLitMaterialTypeTag,
                generatesPreview = true,
                passes = new PassCollection
                {
                    { PassVariant(LitPasses.Forward,         CorePragmas.DOTSForward) },
                    { PassVariant(LitPasses.ForwardAdd,      CorePragmas.DOTSForwardAdd) },
                    { PassVariant(LitPasses.Deferred,        CorePragmas.Deferred) },
                    { LitPasses.GBuffer },
                    { PassVariant(CorePasses.ShadowCaster,   CorePragmas.DOTSInstanced) },
                    { PassVariant(CorePasses.DepthOnly,      CorePragmas.DOTSInstanced) },
                    { PassVariant(LitPasses.DepthNormalOnly, CorePragmas.DOTSInstanced) },
                    { PassVariant(LitPasses.Meta,            CorePragmas.DOTSDefault) },
                    { PassVariant(LitPasses._2D,             CorePragmas.DOTSDefault) },
                },
            };

            // Similar to lit, but handles complex material features.
            // Always ForwardOnly and acts as forward fallback in deferred.
            // SM 4.5, compute with dots instancing
            public readonly static SubShaderDescriptor ComplexLitComputeDOTS = new SubShaderDescriptor()
            {
                //pipelineTag = BuiltInTarget.kPipelineTag,
                customTags = BuiltInTarget.kLitMaterialTypeTag,
                generatesPreview = true,
                passes = new PassCollection
                {
                    { PassVariant(LitPasses.ForwardOnly,     CoreBlockMasks.Vertex, LitBlockMasks.FragmentComplexLit, CorePragmas.DOTSForward, LitDefines.ComplexLit) },
                    { PassVariant(CorePasses.ShadowCaster,   CorePragmas.DOTSInstanced) },
                    { PassVariant(CorePasses.DepthOnly,      CorePragmas.DOTSInstanced) },
                    { PassVariant(LitPasses.DepthNormalOnly, CorePragmas.DOTSInstanced) },
                    { PassVariant(LitPasses.Meta,            CorePragmas.DOTSDefault)   },
                    { PassVariant(LitPasses._2D,             CorePragmas.DOTSDefault)   },
                },
            };

            // SM 2.0, GLES
            public readonly static SubShaderDescriptor LitGLES = new SubShaderDescriptor()
            {
                //pipelineTag = BuiltInTarget.kPipelineTag,
                customTags = BuiltInTarget.kLitMaterialTypeTag,
                generatesPreview = true,
                passes = new PassCollection
                {
                    { LitPasses.Forward },
                    { CorePasses.ShadowCaster },
                    { CorePasses.DepthOnly },
                    { LitPasses.DepthNormalOnly },
                    { LitPasses.Meta },
                    { LitPasses._2D },
                },
            };

            // ForwardOnly pass for SM 2.0, GLES
            // Used as complex Lit SM 2.0 fallback for GLES. Drops advanced features and renders materials as Lit.
            public readonly static SubShaderDescriptor LitGLESForwardOnly = new SubShaderDescriptor()
            {
                pipelineTag = BuiltInTarget.kPipelineTag,
                customTags = BuiltInTarget.kLitMaterialTypeTag,
                generatesPreview = true,
                passes = new PassCollection
                {
                    { LitPasses.ForwardOnly },
                    { CorePasses.ShadowCaster },
                    { CorePasses.DepthOnly },
                    { LitPasses.DepthNormalOnly },
                    { LitPasses.Meta },
                    { LitPasses._2D },
                },
            };
        }
        #endregion

        #region Passes
        static class LitPasses
        {
            public static PassDescriptor Forward = new PassDescriptor
            {
                // Definition
                displayName = "BuiltIn Forward",
                referenceName = "SHADERPASS_FORWARD",
                lightMode = "ForwardBase",
                useInPreview = true,

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentLit,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.Forward,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Forward,
                pragmas  = CorePragmas.Forward,     // NOTE: SM 2.0 only GL
                keywords = LitKeywords.Forward,
                includes = LitIncludes.Forward,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };

            public static PassDescriptor ForwardAdd = new PassDescriptor
            {
                // Definition
                displayName = "BuiltIn ForwardAdd",
                referenceName = "SHADERPASS_FORWARD_ADD",
                lightMode = "ForwardAdd",
                useInPreview = true,

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentLit,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.Forward,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.ForwardAdd,
                pragmas  = CorePragmas.Forward,     // NOTE: SM 2.0 only GL
                keywords = LitKeywords.ForwardAdd,
                includes = LitIncludes.ForwardAdd,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };

            public static PassDescriptor ForwardOnly = new PassDescriptor
            {
                // Definition
                displayName = "BuiltIn Forward Only",
                referenceName = "SHADERPASS_FORWARDONLY",
                lightMode = "BuiltInForwardOnly",
                useInPreview = true,

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentLit,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.Forward,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Default,
                pragmas  = CorePragmas.Forward,    // NOTE: SM 2.0 only GL
                keywords = LitKeywords.Forward,
                includes = LitIncludes.Forward,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };

            public static PassDescriptor Deferred = new PassDescriptor
            {
                // Definition
                displayName = "BuiltIn Deferred",
                referenceName = "SHADERPASS_DEFERRED",
                lightMode = "Deferred",
                useInPreview = true,

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentLit,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.Forward,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Default,
                pragmas  = CorePragmas.Deferred,    // NOTE: SM 2.0 only GL
                keywords = LitKeywords.Deferred,
                includes = LitIncludes.Deferred,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };

            // Deferred only in SM4.5, MRT not supported in GLES2
            public static PassDescriptor GBuffer = new PassDescriptor
            {
                // Definition
                displayName = "GBuffer",
                referenceName = "SHADERPASS_GBUFFER",
                lightMode = "BuiltInGBuffer",

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentLit,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.GBuffer,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Default,
                pragmas = CorePragmas.DOTSGBuffer,
                keywords = LitKeywords.GBuffer,
                includes = LitIncludes.GBuffer,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };

            public static PassDescriptor Meta = new PassDescriptor()
            {
                // Definition
                displayName = "Meta",
                referenceName = "SHADERPASS_META",
                lightMode = "Meta",

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentMeta,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.Meta,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Meta,
                pragmas = CorePragmas.Default,
                keywords = LitKeywords.Meta,
                includes = LitIncludes.Meta,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };

            public static readonly PassDescriptor _2D = new PassDescriptor()
            {
                // Definition
                referenceName = "SHADERPASS_2D",
                lightMode = "BuiltIn2D",

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = CoreBlockMasks.FragmentColorAlpha,

                // Fields
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.Default,
                pragmas = CorePragmas.Instanced,
                includes = LitIncludes._2D,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };

            public static readonly PassDescriptor DepthNormalOnly = new PassDescriptor()
            {
                // Definition
                displayName = "DepthNormals",
                referenceName = "SHADERPASS_DEPTHNORMALSONLY",
                lightMode = "DepthNormals",
                useInPreview = false,

                // Template
                passTemplatePath = BuiltInTarget.kTemplatePath,
                sharedTemplateDirectories = BuiltInTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDepthNormals,

                // Fields
                structs = CoreStructCollections.Default,
                requiredFields = LitRequiredFields.DepthNormals,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.DepthNormalsOnly,
                pragmas = CorePragmas.Instanced,
                includes = CoreIncludes.DepthNormalsOnly,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };
        }
        #endregion

        #region PortMasks
        static class LitBlockMasks
        {
            public static readonly BlockFieldDescriptor[] FragmentLit = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Specular,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };

            public static readonly BlockFieldDescriptor[] FragmentComplexLit = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Specular,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                BlockFields.SurfaceDescription.CoatMask,
                BlockFields.SurfaceDescription.CoatSmoothness,
            };

            public static readonly BlockFieldDescriptor[] FragmentMeta = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };

            public static readonly BlockFieldDescriptor[] FragmentDepthNormals = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
            };
        }
        #endregion

        #region RequiredFields
        static class LitRequiredFields
        {
            public static readonly FieldCollection Forward = new FieldCollection()
            {
                StructFields.Attributes.uv1,                            // needed for meta vertex position
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        // needed for vertex lighting
                StructFields.Varyings.viewDirectionWS,
                BuiltInStructFields.Varyings.lightmapUV,
                BuiltInStructFields.Varyings.sh,
                BuiltInStructFields.Varyings.fogFactorAndVertexLight, // fog and vertex lighting, vert input is dependency
                BuiltInStructFields.Varyings.shadowCoord,             // shadow coord, vert input is dependency
            };

            public static readonly FieldCollection GBuffer = new FieldCollection()
            {
                StructFields.Attributes.uv1,                            // needed for meta vertex position
                StructFields.Varyings.positionWS,
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        // needed for vertex lighting
                StructFields.Varyings.viewDirectionWS,
                BuiltInStructFields.Varyings.lightmapUV,
                BuiltInStructFields.Varyings.sh,
                BuiltInStructFields.Varyings.fogFactorAndVertexLight, // fog and vertex lighting, vert input is dependency
                BuiltInStructFields.Varyings.shadowCoord,             // shadow coord, vert input is dependency
            };

            public static readonly FieldCollection DepthNormals = new FieldCollection()
            {
                StructFields.Attributes.uv1,                            // needed for meta vertex position
                StructFields.Varyings.normalWS,
                StructFields.Varyings.tangentWS,                        // needed for vertex lighting
            };

            public static readonly FieldCollection Meta = new FieldCollection()
            {
                StructFields.Attributes.uv1,                            // needed for meta vertex position
                StructFields.Attributes.uv2,                            //needed for meta vertex position
            };
        }
        #endregion

        #region Defines
        static class LitDefines
        {
            public static readonly DefineCollection ComplexLit = new DefineCollection()
            {
            };
        }
        #endregion

        #region Keywords
        static class LitKeywords
        {
            public static readonly KeywordDescriptor GBufferNormalsOct = new KeywordDescriptor()
            {
                displayName = "GBuffer normal octaedron encoding",
                referenceName = "_GBUFFER_NORMALS_OCT",
                type = KeywordType.Boolean,
                definition = KeywordDefinition.MultiCompile,
                scope = KeywordScope.Global,
            };

            public static readonly KeywordDescriptor ScreenSpaceAmbientOcclusion = new KeywordDescriptor()
            {
                displayName = "Screen Space Ambient Occlusion",
                referenceName = "_SCREEN_SPACE_OCCLUSION",
                type = KeywordType.Boolean,
                definition = KeywordDefinition.MultiCompile,
                scope = KeywordScope.Global,
            };

            public static readonly KeywordCollection Forward = new KeywordCollection
            {
                { ScreenSpaceAmbientOcclusion },
                { CoreKeywordDescriptors.Lightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.AdditionalLights },
                { CoreKeywordDescriptors.AdditionalLightShadows },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.ShadowsShadowmask },
            };

            public static readonly KeywordCollection ForwardAdd = new KeywordCollection
            {
                { ScreenSpaceAmbientOcclusion },
                { CoreKeywordDescriptors.Lightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.AdditionalLights },
                { CoreKeywordDescriptors.AdditionalLightShadows },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.ShadowsShadowmask },
            };

            public static readonly KeywordCollection Deferred = new KeywordCollection
            {
                { CoreKeywordDescriptors.Lightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.MixedLightingSubtractive },
                { GBufferNormalsOct },
            };

            public static readonly KeywordCollection GBuffer = new KeywordCollection
            {
                { CoreKeywordDescriptors.Lightmap },
                { CoreKeywordDescriptors.DirectionalLightmapCombined },
                { CoreKeywordDescriptors.MainLightShadows },
                { CoreKeywordDescriptors.ShadowsSoft },
                { CoreKeywordDescriptors.LightmapShadowMixing },
                { CoreKeywordDescriptors.MixedLightingSubtractive },
                { GBufferNormalsOct },
            };

            public static readonly KeywordCollection Meta = new KeywordCollection
            {
                { CoreKeywordDescriptors.SmoothnessChannel },
            };
        }
        #endregion

        #region Includes
        static class LitIncludes
        {
            const string kShadows = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shadows.hlsl";
            const string kMetaInput = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/MetaInput.hlsl";
            const string kForwardPass = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl";
            const string kForwardAddPass = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardAddPass.hlsl";
            const string kDeferredPass = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRDeferredPass.hlsl";
            const string kGBuffer = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/UnityGBuffer.hlsl";
            const string kPBRGBufferPass = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRGBufferPass.hlsl";
            const string kLightingMetaPass = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl";
            const string k2DPass = "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBR2DPass.hlsl";

            public static readonly IncludeCollection Forward = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                //{ kShadows, IncludeLocation.Pregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kForwardPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection ForwardAdd = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kForwardAddPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection Deferred = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kDeferredPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection GBuffer = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { kShadows, IncludeLocation.Pregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kGBuffer, IncludeLocation.Postgraph },
                { kPBRGBufferPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection Meta = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },
                { kMetaInput, IncludeLocation.Pregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kLightingMetaPass, IncludeLocation.Postgraph },
            };

            public static readonly IncludeCollection _2D = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { k2DPass, IncludeLocation.Postgraph },
            };
        }
        #endregion
    }
}

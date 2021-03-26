using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.ShaderGraph;
using UnityEngine.Rendering;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.ShaderGraph.Legacy;

using static UnityEditor.Rendering.Universal.ShaderGraph.SubShaderUtils;
using UnityEditor.Rendering.Universal;

namespace UnityEditor.Rendering.Universal.ShaderGraph
{
    sealed class UniversalUnlitSubTarget : UniversalSubTarget, ILegacyTarget
    {
        static readonly GUID kSourceCodeGuid = new GUID("97c3f7dcb477ec842aa878573640313a"); // UniversalUnlitSubTarget.cs

        public UniversalUnlitSubTarget()
        {
            displayName = "Unlit";
        }

        public override bool IsActive() => true;

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);
            base.Setup(ref context);

            if (!context.HasCustomEditorForRenderPipeline(typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)))
            {
                // TODO use the new one
                context.AddCustomEditorForRenderPipeline("UnityEditor.URPUnlitGUI", typeof(UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)); // TODO: This should be owned by URP
            }

            // Process SubShaders
            context.AddSubShader(SubShaders.Unlit(target.renderType, target.renderQueue));
            context.AddSubShader(SubShaders.UnlitDOTS(target.renderType, target.renderQueue));
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            // copy our target's default settings into the material
            // (technically not necessary since we are always recreating the material from the shader each time,
            // which will pull over the defaults from the shader definition)
            // but if that ever changes, this will ensure the defaults are set
            material.SetFloat(Property.Surface, (float)target.surfaceType);
            material.SetFloat(Property.Blend, (float)target.alphaMode);
            material.SetFloat(Property.AlphaClip, target.alphaClip ? 1.0f : 0.0f);
            material.SetFloat(Property.Cull, (int)target.renderFace);

            // call the full unlit material setup function
            URPUnlitGUI.SetMaterialKeywords(material);
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            // Surface Type & Blend Mode
            // These must be set per SubTarget as Sprite SubTargets override them
            // context.AddField(UniversalFields.SurfaceOpaque,       target.surfaceType == SurfaceType.Opaque);     // TODO: remove?
            // context.AddField(UniversalFields.SurfaceTransparent,  target.surfaceType != SurfaceType.Opaque);
            context.AddField(UniversalFields.BlendAdd,            target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Additive);
            context.AddField(Fields.BlendAlpha,                   target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Alpha);
            context.AddField(UniversalFields.BlendMultiply,       target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Multiply);
            context.AddField(UniversalFields.BlendPremultiply,    target.surfaceType != SurfaceType.Opaque && target.alphaMode == AlphaMode.Premultiply);
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            // TODO: these blocks should only be disabled when "material control" is disabled / they are locked
            context.AddBlock(BlockFields.SurfaceDescription.Alpha);                 //,              target.surfaceType == SurfaceType.Transparent || target.alphaClip);
            context.AddBlock(BlockFields.SurfaceDescription.AlphaClipThreshold);    //, target.alphaClip);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            collector.AddFloatProperty(Property.Surface, (float)target.surfaceType);
            collector.AddFloatProperty(Property.Blend, (float)target.alphaMode);
            collector.AddFloatProperty(Property.AlphaClip, target.alphaClip ? 1.0f : 0.0f);
            collector.AddFloatProperty(Property.SrcBlendSG, 1.0f);    // always set by material inspector (TODO : get src/dst blend and set here?)
            collector.AddFloatProperty(Property.DstBlendSG, 0.0f);    // always set by material inspector
            collector.AddFloatProperty(Property.ZWriteSG, (target.surfaceType == SurfaceType.Opaque) ? 1.0f : 0.0f);
            collector.AddFloatProperty(Property.Cull, (float)target.renderFace);    // render face enum is designed to directly pass as a cull mode
            collector.AddFloatProperty(Property.QueueOffset, 0.0f);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange, Action<String> registerUndo)
        {
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

            context.AddProperty("Render Face", new EnumField(RenderFace.Front) { value = target.renderFace }, (evt) =>
            {
                if (Equals(target.renderFace, evt.newValue))
                    return;

                registerUndo("Change Render Face");
                target.renderFace = (RenderFace)evt.newValue;
                onChange();
            });
        }

        public bool TryUpgradeFromMasterNode(IMasterNode1 masterNode, out Dictionary<BlockFieldDescriptor, int> blockMap)
        {
            blockMap = null;
            if (!(masterNode is UnlitMasterNode1 unlitMasterNode))
                return false;

            // Set blockmap
            blockMap = new Dictionary<BlockFieldDescriptor, int>()
            {
                { BlockFields.VertexDescription.Position, 9 },
                { BlockFields.VertexDescription.Normal, 10 },
                { BlockFields.VertexDescription.Tangent, 11 },
                { BlockFields.SurfaceDescription.BaseColor, 0 },
                { BlockFields.SurfaceDescription.Alpha, 7 },
                { BlockFields.SurfaceDescription.AlphaClipThreshold, 8 },
            };

            return true;
        }

        #region SubShader
        static class SubShaders
        {
            public static SubShaderDescriptor Unlit(string renderType, string renderQueue)
            {
                var result = new SubShaderDescriptor()
                {
                    pipelineTag = UniversalTarget.kPipelineTag,
                    customTags = UniversalTarget.kUnlitMaterialTypeTag,
                    renderType = renderType,
                    renderQueue = renderQueue,
                    generatesPreview = true,
                    passes = new PassCollection
                    {
                        { UnlitPasses.Unlit },
                        { CorePasses.ShadowCaster },
                        { CorePasses.DepthOnly },
                    },
                };
                return result;
            }

            public static SubShaderDescriptor UnlitDOTS(string renderType, string renderQueue)
            {
                var result = new SubShaderDescriptor()
                {
                    pipelineTag = UniversalTarget.kPipelineTag,
                    customTags = UniversalTarget.kUnlitMaterialTypeTag,
                    renderType = renderType,
                    renderQueue = renderQueue,
                    generatesPreview = true,
                    passes = new PassCollection
                    {
                        { PassVariant(UnlitPasses.Unlit, CorePragmas.DOTSForward) },
                        { PassVariant(CorePasses.ShadowCaster, CorePragmas.DOTSInstanced) },
                        { PassVariant(CorePasses.DepthOnly, CorePragmas.DOTSInstanced) },
                    },
                };
                return result;
            }
        }
        #endregion

        #region Pass
        static class UnlitPasses
        {
            public static PassDescriptor Unlit = new PassDescriptor
            {
                // Definition
                displayName = "Pass",
                referenceName = "SHADERPASS_UNLIT",
                useInPreview = true,

                // Template
                passTemplatePath = UniversalTarget.kUberTemplatePath,
                sharedTemplateDirectories = UniversalTarget.kSharedTemplateDirectories,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = CoreBlockMasks.FragmentColorAlpha,

                // Fields
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,

                // Conditional State
                renderStates = CoreRenderStates.UberDefault,
                pragmas = CorePragmas.Forward,
                keywords = UnlitKeywords.Unlit,
                includes = UnlitIncludes.Unlit,

                // Custom Interpolator Support
                customInterpolators = CoreCustomInterpDescriptors.Common
            };
        }
        #endregion

        #region Keywords
        static class UnlitKeywords
        {
            public static KeywordCollection Unlit = new KeywordCollection
            {
                CoreKeywordDescriptors.Lightmap,
                CoreKeywordDescriptors.DirectionalLightmapCombined,
                CoreKeywordDescriptors.SampleGI,
                CoreKeywordDescriptors.AlphaTestOn,
                CoreKeywordDescriptors.SurfaceTypeTransparent,
                CoreKeywordDescriptors.AlphaPremultiplyOn,
            };
        }
        #endregion

        #region Includes
        static class UnlitIncludes
        {
            const string kUnlitPass = "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UnlitPass.hlsl";

            public static IncludeCollection Unlit = new IncludeCollection
            {
                // Pre-graph
                { CoreIncludes.CorePregraph },
                { CoreIncludes.ShaderGraphPregraph },

                // Post-graph
                { CoreIncludes.CorePostgraph },
                { kUnlitPass, IncludeLocation.Postgraph },
            };
        }
        #endregion
    }
}

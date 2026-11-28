using System;
using System.Linq;
using FrogTerrarium.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FrogTerrarium.EditorTools
{
    /// <summary>
    /// Switches the project to the Universal Render Pipeline (URP), the modern way Unity draws:
    /// proper colour, post-processing (glow, depth of field, ambient occlusion) and soft shadows.
    ///
    /// The project keeps no settings files under ProjectSettings except the Unity version, so Unity
    /// makes fresh default settings on every computer and in CI. This code therefore points the
    /// fresh settings at our pipeline asset (Assets/Settings/Rendering) each time the editor
    /// starts and before every build, and also turns on linear colour, which modern lighting needs.
    /// If the pipeline asset cannot be read for any reason it makes a new one from code instead.
    /// </summary>
    [InitializeOnLoad]
    public static class RenderPipelineSetup
    {
        public const string Folder = "Assets/Settings/Rendering";
        public const string PipelinePath = Folder + "/FrogTerrarium URP.asset";
        public const string RendererPath = Folder + "/FrogTerrarium Renderer.asset";
        private const string FallbackPipelinePath = Folder + "/Generated URP.asset";
        private const string FallbackRendererPath = Folder + "/Generated Renderer.asset";
        private const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        static RenderPipelineSetup()
        {
            // The assets may not be imported yet on the very first start; try again once the editor is idle.
            if (!TryAssign()) EditorApplication.delayCall += () => Ensure();
        }

        /// <summary>Makes sure URP is in use with our settings. Called before every build.</summary>
        public static UniversalRenderPipelineAsset Ensure()
        {
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath)
                        ?? AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(FallbackPipelinePath);
            if (asset == null)
            {
                Debug.LogWarning("Frog Terrarium: could not read " + PipelinePath + "; making a URP asset from code.");
                asset = CreateFromCode();
            }
            FixRenderer(asset);
            Assign(asset);
            FixMaterials();
            return asset;
        }

        private static bool TryAssign()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null) return false;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            Assign(asset);
            return true;
        }

        private static void Assign(UniversalRenderPipelineAsset asset)
        {
            if (GraphicsSettings.defaultRenderPipeline != asset) GraphicsSettings.defaultRenderPipeline = asset;
            // Quality levels can each name their own pipeline; make them all use ours (or the default).
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                var level = QualitySettings.GetRenderPipelineAssetAt(i);
                if (level != null && level != asset)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = asset;
                }
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        private static UniversalRenderPipelineAsset CreateFromCode()
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            AssetDatabase.CreateAsset(renderer, FallbackRendererPath);
            var asset = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(asset, FallbackPipelinePath);
            var so = new SerializedObject(asset);
            Set(so, "m_RequireDepthTexture", 1);
            Set(so, "m_RequireOpaqueTexture", 1);
            Set(so, "m_OpaqueDownsampling", 0);
            Set(so, "m_SupportsHDR", 1);
            Set(so, "m_MSAA", 4);
            Set(so, "m_MainLightShadowmapResolution", 2048);
            Set(so, "m_ShadowCascadeCount", 4);
            Set(so, "m_SoftShadowsSupported", 1);
            Set(so, "m_SoftShadowQuality", 2);
            Set(so, "m_ColorGradingMode", 1);
            var distance = so.FindProperty("m_ShadowDistance");
            if (distance != null) distance.floatValue = 60f;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// Gives the renderer its post-processing data (without it there are no effects at all) and
        /// adds screen-space ambient occlusion: soft contact shadows where things meet the ground.
        /// </summary>
        private static void FixRenderer(UniversalRenderPipelineAsset asset)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath)
                           ?? AssetDatabase.LoadAssetAtPath<UniversalRendererData>(FallbackRendererPath);
            if (renderer == null) return;
            bool changed = false;
            if (renderer.postProcessData == null)
            {
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                changed = renderer.postProcessData != null;
                if (!changed) Debug.LogWarning("Frog Terrarium: URP post-processing data not found; effects will be missing.");
            }
            if (!renderer.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"))
                changed |= AddAmbientOcclusion(renderer);
            if (changed)
            {
                EditorUtility.SetDirty(renderer);
                AssetDatabase.SaveAssets();
            }
        }

        private static bool AddAmbientOcclusion(UniversalRendererData renderer)
        {
            try
            {
                // The SSAO feature class is not public in every URP version, so find it by name.
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type == null) { Debug.LogWarning("Frog Terrarium: no SSAO feature in this URP version."); return false; }
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
                feature.name = "Ambient Occlusion";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                var so = new SerializedObject(renderer);
                var map = so.FindProperty("m_RendererFeatureMap");
                if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long id))
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                // Our tank is big in world units, so the shading needs a wider reach than the default.
                var fso = new SerializedObject(feature);
                SetFloat(fso, "m_Settings.Intensity", 1.1f);
                SetFloat(fso, "m_Settings.Radius", 0.25f);
                SetFloat(fso, "m_Settings.DirectLightingStrength", 0.3f);
                SetFloat(fso, "m_Settings.Falloff", 40f);
                fso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(feature);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Frog Terrarium: could not add ambient occlusion: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Makes sure each game material uses the shader it should (see GameMaterials). If a material
        /// lost its shader link, this finds the shader by name and sets it again.
        /// </summary>
        private static void FixMaterials()
        {
            foreach (var name in GameMaterials.Names)
            {
                var mat = Resources.Load<Material>(GameMaterials.Folder + name);
                if (mat == null) continue;
                string expected = GameMaterials.ExpectedShader(name);
                if (mat.shader != null && mat.shader.name == expected) continue;
                var shader = Shader.Find(expected);
                if (shader == null) { Debug.LogError("Frog Terrarium: shader " + expected + " not found for " + name); continue; }
                var colour = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                mat.shader = shader;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
                if (GameMaterials.NeedsInstancing(name)) mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                Debug.Log("Frog Terrarium: material " + name + " now uses " + expected);
            }
            AssetDatabase.SaveAssets();
        }

        private static void Set(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p != null && p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
        }
    }

    /// <summary>Assigns the pipeline as soon as its asset is imported (first start on a new computer).</summary>
    internal sealed class RenderPipelineImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Contains(RenderPipelineSetup.PipelinePath))
                EditorApplication.delayCall += () => RenderPipelineSetup.Ensure();
        }
    }
}

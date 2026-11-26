using System.Collections.Generic;
using System.Linq;
using FrogTerrarium.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrogTerrarium.EditorTools
{
    /// <summary>
    /// Fails the build if the game's materials would draw pink in the player.
    ///
    /// Unity only puts a shader into a build if an asset in the build uses it, and strips
    /// variants it thinks are unused (such as GPU instancing). This hook watches which shader
    /// variants the build actually compiles and, at the end, checks that every material in
    /// Resources/FrogTerrarium got its shader with the instancing variant.
    /// </summary>
    public sealed class ShaderBuildCheck : IPreprocessBuildWithReport, IPreprocessShaders, IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue; // run after any other stripping

        private static readonly HashSet<string> s_Compiled = new HashSet<string>();
        private static readonly HashSet<string> s_CompiledInstanced = new HashSet<string>();

        /// <summary>Problems found by the last build, empty when everything was included.</summary>
        public static List<string> LastProblems { get; } = new List<string>();

        public void OnPreprocessBuild(BuildReport report)
        {
            s_Compiled.Clear();
            s_CompiledInstanced.Clear();
            LastProblems.Clear();
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (data.Count == 0) return;
            s_Compiled.Add(shader.name);
            var instancing = new ShaderKeyword("INSTANCING_ON");
            if (data.Any(d => d.shaderKeywordSet.IsEnabled(instancing))) s_CompiledInstanced.Add(shader.name);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            LastProblems.Clear();
            foreach (var name in GameMaterials.Names)
            {
                var mat = Resources.Load<Material>(GameMaterials.Folder + name);
                if (mat == null) { LastProblems.Add($"Material Resources/{GameMaterials.Folder}{name} is missing."); continue; }
                if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                { LastProblems.Add($"Material {name} has no valid shader."); continue; }
                string shaderName = mat.shader.name;
                if (!s_Compiled.Contains(shaderName))
                    LastProblems.Add($"Shader '{shaderName}' (used by {name}) was not included in the build.");
                else if (mat.enableInstancing && !s_CompiledInstanced.Contains(shaderName))
                    LastProblems.Add($"Shader '{shaderName}' (used by {name}) is missing its GPU instancing variant.");
            }

            if (LastProblems.Count == 0)
                Debug.Log("Shader build check passed: " + string.Join(", ", s_Compiled.Where(n => !n.StartsWith("Hidden/")).OrderBy(n => n)));
            else
                foreach (var p in LastProblems) Debug.LogError("Shader build check FAILED: " + p);
        }
    }
}

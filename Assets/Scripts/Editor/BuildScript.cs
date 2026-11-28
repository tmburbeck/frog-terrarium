using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FrogTerrarium.EditorTools
{
    /// <summary>
    /// Entry point for automated builds (GitHub Actions via GameCI calls
    /// FrogTerrarium.EditorTools.BuildScript.Build). Also usable from the menu.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Frog Terrarium/Build Windows Player")]
        public static void BuildFromMenu()
        {
            Run("Builds/Windows/FrogTerrarium.exe", BuildTarget.StandaloneWindows64, quitWhenDone: false);
        }

        /// <summary>Called by CI. Reads -customBuildPath and -buildTarget from the command line.</summary>
        public static void Build()
        {
            string path = GetArg("-customBuildPath") ?? "Builds/Windows/FrogTerrarium.exe";
            var target = BuildTarget.StandaloneWindows64;
            string targetArg = GetArg("-buildTarget");
            if (!string.IsNullOrEmpty(targetArg) && Enum.TryParse(targetArg, true, out BuildTarget parsed)) target = parsed;
            // GameCI passes the version it worked out (from the git tag for releases); crash reports and
            // the Settings window show it, so a report says exactly which build it came from.
            string version = GetArg("-buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;
            Run(path, target, quitWhenDone: true);
        }

        private static void Run(string path, BuildTarget target, bool quitWhenDone)
        {
            RenderPipelineSetup.Ensure(); // URP, linear colour and the effects (see RenderPipelineSetup)
            SandboxSceneSetup.EnsureScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SandboxSceneSetup.ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors, {summary.totalTime}");
            bool ok = summary.result == BuildResult.Succeeded && ShaderBuildCheck.LastProblems.Count == 0;
            if (!ok && ShaderBuildCheck.LastProblems.Count > 0)
                Debug.LogError("Build rejected: the game would draw pink. " + string.Join(" ", ShaderBuildCheck.LastProblems));
            if (quitWhenDone) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}

using System.IO;
using System.Linq;
using FrogTerrarium.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FrogTerrarium.EditorTools
{
    /// <summary>
    /// Makes sure the Sandbox scene exists and is the first scene in the build.
    /// Runs automatically the first time the project is opened, and is also
    /// available from the menu: Frog Terrarium > Create or Open Sandbox Scene.
    /// </summary>
    [InitializeOnLoad]
    public static class SandboxSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Sandbox.unity";

        static SandboxSceneSetup()
        {
            if (Application.isBatchMode) return; // CI builds call EnsureScene themselves
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ScenePath)) { EnsureInBuildSettings(); return; }
                EnsureScene();
                if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            };
        }

        [MenuItem("Frog Terrarium/Create or Open Sandbox Scene")]
        public static void CreateOrOpen()
        {
            EnsureScene();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Creates Assets/Scenes/Sandbox.unity if missing and adds it to the build list.</summary>
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var active = EditorSceneManager.GetActiveScene();
                // An untitled, unchanged scene has nothing to lose, so replace it; otherwise add alongside.
                bool replaceActive = string.IsNullOrEmpty(active.path) && !active.isDirty;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    replaceActive ? NewSceneMode.Single : NewSceneMode.Additive);
                var go = new GameObject("Sandbox Bootstrap");
                go.AddComponent<SandboxBootstrap>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (!replaceActive) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.Refresh();
                Debug.Log("Frog Terrarium: created " + ScenePath);
            }
            EnsureInBuildSettings();
        }

        private static void EnsureInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Count > 0 && scenes[0].path == ScenePath && scenes[0].enabled) return;
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

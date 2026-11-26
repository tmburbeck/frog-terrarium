using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Loads the placeholder materials from Assets/Resources/FrogTerrarium.
    ///
    /// Why not just make materials in code? A player build only includes shaders that some
    /// asset in the build uses. Materials created at runtime don't count, so the Standard
    /// shader was left out and everything drew pink. Keeping real material assets under
    /// Resources guarantees their shaders (and the GPU-instancing variants) ship in the build.
    /// The build check in Editor/ShaderBuildCheck.cs fails the build if this ever regresses.
    /// </summary>
    public static class GameMaterials
    {
        public const string Folder = "FrogTerrarium/";
        public static readonly string[] Names = { "Frog", "Insect", "Terrain", "Water", "Heater", "HeaterOn", "Mister", "Brush" };

        /// <summary>The shader each material must use. Anything else means it would not draw correctly.</summary>
        public static string ExpectedShader(string name)
        {
            switch (name)
            {
                case "Terrain": return "FrogTerrarium/Terrain";
                case "Water": return "FrogTerrarium/Water";
                default: return "Standard";
            }
        }

        /// <summary>Materials drawn many at once with GPU instancing need that switched on.</summary>
        public static bool NeedsInstancing(string name) => name == "Frog" || name == "Insect" || name == "Brush";

        public static Material Load(string name)
        {
            var mat = Resources.Load<Material>(Folder + name);
            if (mat == null)
            {
                Debug.LogError($"Frog Terrarium: material Resources/{Folder}{name} is missing.");
                return null;
            }
            if (mat.shader == null || !mat.shader.isSupported)
                Debug.LogError($"Frog Terrarium: material {name} uses a shader this machine cannot draw.");
            return mat;
        }
    }
}

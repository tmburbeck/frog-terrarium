using System;
using System.Collections.Generic;
using System.IO;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Saved blueprints on this computer: one .ftbp text file each in the game's "blueprints"
    /// folder. The files are plain text, so they can be emailed or posted, and dropping someone
    /// else's .ftbp file into the folder adds it to the list.
    /// </summary>
    public static class BlueprintFiles
    {
        public const string Extension = ".ftbp";

        public static string Folder => Path.Combine(Application.persistentDataPath, "blueprints");

        /// <summary>The names of the saved blueprints, A to Z.</summary>
        public static List<string> List()
        {
            var names = new List<string>();
            try
            {
                if (!Directory.Exists(Folder)) return names;
                foreach (var f in Directory.GetFiles(Folder, "*" + Extension)) names.Add(Path.GetFileNameWithoutExtension(f));
                names.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e) { Debug.LogWarning("Could not list blueprints: " + e.Message); }
            return names;
        }

        public static string PathOf(string name) => Path.Combine(Folder, Blueprint.CleanName(name) + Extension);

        /// <summary>Saves a blueprint under its name (replacing one with the same name). Returns the file path.</summary>
        public static string Save(Blueprint bp)
        {
            Directory.CreateDirectory(Folder);
            string path = PathOf(bp.Name);
            File.WriteAllText(path, bp.ToText() + Environment.NewLine);
            return path;
        }

        /// <summary>Reads a saved blueprint. Throws <see cref="SaveFormatException"/> or an IO error with a plain message.</summary>
        public static Blueprint Load(string name)
        {
            var bp = Blueprint.FromText(File.ReadAllText(PathOf(name)));
            if (bp.Name == "Untitled build") bp.Name = Blueprint.CleanName(name);
            return bp;
        }

        public static void Delete(string name)
        {
            string path = PathOf(name);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}

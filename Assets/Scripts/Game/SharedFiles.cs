using System;
using System.Collections.Generic;
using System.IO;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>One shared file found on this computer, read once when the list is refreshed.</summary>
    public sealed class SharedFile
    {
        public string Path;
        public ShareKind Kind;
        /// <summary>The name inside the file (or the file name).</summary>
        public string Name;
        public string Author = "";
        public string Fingerprint = "";
        /// <summary>A short line about what is in it ("Rainforest map, Large, 14 parts").</summary>
        public string Details = "";
        /// <summary>Why the file can't be used, or "" if it is fine.</summary>
        public string Problem = "";
        public DateTime Modified;
    }

    /// <summary>
    /// The shared files folder: maps (.ftmap) and species (.ftsp) in the game's "shared" folder,
    /// plus the blueprints (.ftbp) in the "blueprints" folder. Files from friends can be dropped
    /// into either folder and appear in the Files window.
    /// </summary>
    public static class SharedFiles
    {
        public static string Folder => System.IO.Path.Combine(Application.persistentDataPath, "shared");

        /// <summary>The name players sign their maps and species with. Remembered on this computer.</summary>
        public static string Author
        {
            get => PlayerPrefs.GetString("FrogTerrarium.Author", "");
            set { PlayerPrefs.SetString("FrogTerrarium.Author", ShareText.CleanName(value, "")); PlayerPrefs.Save(); }
        }

        /// <summary>Every shared file in both folders, newest first.</summary>
        public static List<SharedFile> List()
        {
            var files = new List<SharedFile>();
            Scan(Folder, "*" + ShareText.ExtensionOf(ShareKind.Map), files);
            Scan(Folder, "*" + ShareText.ExtensionOf(ShareKind.Species), files);
            Scan(Folder, "*" + BlueprintFiles.Extension, files);
            Scan(BlueprintFiles.Folder, "*" + BlueprintFiles.Extension, files);
            files.Sort((a, b) => b.Modified.CompareTo(a.Modified));
            return files;
        }

        private static void Scan(string folder, string pattern, List<SharedFile> into)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var path in Directory.GetFiles(folder, pattern)) into.Add(Read(path));
            }
            catch (Exception e) { Debug.LogWarning("Could not list shared files in " + folder + ": " + e.Message); }
        }

        private static SharedFile Read(string path)
        {
            var f = new SharedFile { Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) };
            try
            {
                f.Modified = File.GetLastWriteTime(path);
                string text = File.ReadAllText(path);
                f.Kind = ShareText.Detect(text);
                switch (f.Kind)
                {
                    case ShareKind.Map:
                    {
                        var m = MapFile.FromText(text);
                        f.Name = m.Name; f.Author = m.Author; f.Fingerprint = m.Fingerprint;
                        f.Details = string.Format("{0} map, {1:0}×{2:0} m, {3} parts, {4} devices", m.Settings.Info.Name,
                            m.HalfSizeX * 2f, m.HalfSizeZ * 2f, m.Parts.Count, m.Devices.Count);
                        break;
                    }
                    case ShareKind.Species:
                    {
                        var s = SpeciesFile.FromText(text);
                        f.Name = s.Name; f.Author = s.Author; f.Fingerprint = s.Fingerprint;
                        f.Details = "A breeding pair of " + Lineage.Describe(s.Type);
                        break;
                    }
                    case ShareKind.Blueprint:
                    {
                        var b = Blueprint.FromText(text);
                        if (b.Name != "Untitled build") f.Name = b.Name;
                        f.Details = "Blueprint, " + b.Parts.Count + " parts";
                        break;
                    }
                    default:
                        f.Problem = "Not a Frog Terrarium file.";
                        break;
                }
            }
            catch (Exception e) { f.Problem = e.Message; }
            return f;
        }

        /// <summary>Writes shared text to a file named after it (adding " 2", " 3"... if that name is taken by different content). Returns the path.</summary>
        public static string Save(ShareKind kind, string name, string text)
        {
            string folder = kind == ShareKind.Blueprint ? BlueprintFiles.Folder : Folder;
            Directory.CreateDirectory(folder);
            string clean = ShareText.CleanName(name, "Untitled");
            string ext = ShareText.ExtensionOf(kind);
            string path = System.IO.Path.Combine(folder, clean + ext);
            for (int n = 2; File.Exists(path) && File.ReadAllText(path).Trim() != text.Trim(); n++)
                path = System.IO.Path.Combine(folder, clean + " " + n + ext);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text + Environment.NewLine);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        public static void Delete(SharedFile f)
        {
            if (File.Exists(f.Path)) File.Delete(f.Path);
        }

        /// <summary>Opens a folder in the computer's file browser (creating it first).</summary>
        public static void OpenFolder(string folder)
        {
            Directory.CreateDirectory(folder);
            Application.OpenURL("file:///" + folder.Replace('\\', '/'));
        }
    }
}

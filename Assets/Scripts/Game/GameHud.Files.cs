using System.Collections.Generic;
using System.IO;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The Files window: share this tank's map or a registered species as a file, paste something a
    /// friend sent, and browse every shared file on this computer (maps, species and blueprints)
    /// to use, copy or delete them.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Whether the Files window is showing.</summary>
        public bool ShowFiles;
        private List<SharedFile> _sharedFiles;
        private Vector2 _filesScroll;
        private string _mapName = "";
        private string _authorName;
        private string _filesMessage = "";
        private string _deleteArmed = "";

        public void OpenFiles(bool open)
        {
            ShowFiles = open;
            if (!open) return;
            _sharedFiles = null;
            _filesMessage = "";
            _deleteArmed = "";
            if (_authorName == null) _authorName = SharedFiles.Author;
            if (_mapName.Length == 0) _mapName = runner.Sim.World.Map.Info.Name + " tank";
        }

        private void DrawFiles(World world, float w, float h)
        {
            const float pw = 780f;
            float ph = Mathf.Min(680f, h - 140f);
            var r = Panel(new Rect((w - pw) * 0.5f, Mathf.Max(54f, (h - ph) * 0.5f - 40f), pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 10f, r.width - 24f, r.height - 20f));
            GUILayout.Label("Files", _headerStyle);
            GUILayout.Label("Share maps, species and blueprints as small text files. Send one to a friend, or paste theirs here.", _smallStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Your name (optional)", _labelStyle, GUILayout.Width(150f));
            string author = GUILayout.TextField(_authorName ?? "", 40, GUILayout.Width(200f));
            if (author != _authorName) { _authorName = author; SharedFiles.Author = author; }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            // Sharing from this tank.
            GUILayout.Label("Share from this tank", _labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Map name", _smallStyle, GUILayout.Width(70f));
            _mapName = GUILayout.TextField(_mapName, 40, GUILayout.Width(220f));
            if (GUILayout.Button("Save map file", GUILayout.Width(120f))) ExportMap(world, false);
            if (GUILayout.Button("Copy map text", GUILayout.Width(120f))) ExportMap(world, true);
            GUILayout.EndHorizontal();
            GUILayout.Label("A map file keeps the ground exactly as you sculpted and painted it, the room, the style, the devices and "
                + "every building part (but no animals). The map code " + world.Map.ToCode() + " only rebuilds the generated ground.", _smallStyle);
            if (world.Registered.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Species", _smallStyle, GUILayout.Width(70f));
                foreach (var reg in world.Registered)
                    if (GUILayout.Button("Share " + reg.Name, GUILayout.MaxWidth(220f))) ExportSpecies(world, reg);
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("Register a new species (Species book) to share it with a breeding pair.", _smallStyle);
            GUILayout.Label("Blueprints are saved from the Build menu and show up here too.", _smallStyle);
            GUILayout.Space(6f);

            // Files on this computer.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shared files on this computer", _labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Paste from clipboard", GUILayout.Width(150f))) PasteShared();
            if (GUILayout.Button("Open folder", GUILayout.Width(100f))) SharedFiles.OpenFolder(SharedFiles.Folder);
            if (GUILayout.Button("Refresh", GUILayout.Width(70f))) _sharedFiles = null;
            GUILayout.EndHorizontal();
            if (_sharedFiles == null) _sharedFiles = SharedFiles.List();
            _filesScroll = GUILayout.BeginScrollView(_filesScroll);
            if (_sharedFiles.Count == 0)
                GUILayout.Label("Nothing yet. Save a map above, or put .ftmap, .ftsp and .ftbp files into " + SharedFiles.Folder, _smallStyle);
            SharedFile toDelete = null;
            foreach (var f in _sharedFiles)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(470f));
                GUILayout.Label(KindWord(f.Kind) + ": " + f.Name + (f.Author.Length > 0 ? "  by " + f.Author : ""), _labelStyle);
                GUILayout.Label(f.Problem.Length > 0 ? "Can't use it: " + f.Problem
                    : f.Details + (f.Fingerprint.Length > 0 ? "   fingerprint " + f.Fingerprint : ""), _smallStyle);
                GUILayout.EndVertical();
                GUI.enabled = f.Problem.Length == 0;
                if (GUILayout.Button(UseWord(f.Kind), GUILayout.Width(90f))) UseShared(world, f);
                if (GUILayout.Button("Copy", GUILayout.Width(56f)))
                {
                    GUIUtility.systemCopyBuffer = File.ReadAllText(f.Path).Trim();
                    _filesMessage = "Copied " + f.Name + ". Paste it into a message to share it.";
                }
                GUI.enabled = true;
                bool armed = _deleteArmed == f.Path;
                if (GUILayout.Button(armed ? "Sure?" : "Delete", GUILayout.Width(60f)))
                {
                    if (armed) toDelete = f;
                    else _deleteArmed = f.Path;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (toDelete != null)
            {
                try { SharedFiles.Delete(toDelete); _filesMessage = "Deleted " + toDelete.Name + "."; }
                catch (System.Exception e) { _filesMessage = "Could not delete it: " + e.Message; }
                _sharedFiles = null;
                _deleteArmed = "";
            }

            if (_filesMessage.Length > 0) GUILayout.Label(_filesMessage, _smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Folder: " + SharedFiles.Folder, _smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(70f))) ShowFiles = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static string KindWord(ShareKind kind)
        {
            switch (kind)
            {
                case ShareKind.Map: return "Map";
                case ShareKind.Species: return "Species";
                case ShareKind.Blueprint: return "Blueprint";
                default: return "File";
            }
        }

        private static string UseWord(ShareKind kind)
        {
            switch (kind)
            {
                case ShareKind.Map: return "Play it";
                case ShareKind.Species: return "Add a pair";
                case ShareKind.Blueprint: return "Place it";
                default: return "Use";
            }
        }

        private void ExportMap(World world, bool toClipboard)
        {
            try
            {
                var map = MapFile.FromWorld(world, _mapName, _authorName);
                string text = map.ToText();
                if (toClipboard)
                {
                    GUIUtility.systemCopyBuffer = text;
                    _filesMessage = "Map text copied (fingerprint " + map.Fingerprint + "). Paste it into a message to share it.";
                }
                else
                {
                    string path = SharedFiles.Save(ShareKind.Map, map.Name, text);
                    _filesMessage = "Saved " + Path.GetFileName(path) + " (fingerprint " + map.Fingerprint + ").";
                    _sharedFiles = null;
                }
            }
            catch (System.Exception e) { _filesMessage = "Could not save the map: " + e.Message; }
        }

        private void ExportSpecies(World world, RegisteredSpecies reg)
        {
            var file = SpeciesFile.FromWorld(world, reg, _authorName, out string why);
            if (file == null) { _filesMessage = why; return; }
            try
            {
                string path = SharedFiles.Save(ShareKind.Species, file.Name, file.ToText());
                _filesMessage = "Saved " + Path.GetFileName(path) + " with a breeding pair (fingerprint " + file.Fingerprint + ").";
                _sharedFiles = null;
            }
            catch (System.Exception e) { _filesMessage = "Could not save the species: " + e.Message; }
        }

        /// <summary>Saves whatever shared text is on the clipboard as a file (a map code opens the New game window instead).</summary>
        private void PasteShared()
        {
            string text = (GUIUtility.systemCopyBuffer ?? "").Trim();
            try
            {
                switch (ShareText.Detect(text))
                {
                    case ShareKind.Map:
                    {
                        var m = MapFile.FromText(text);
                        SharedFiles.Save(ShareKind.Map, m.Name, text);
                        _filesMessage = "Added the map " + m.Name + " (fingerprint " + m.Fingerprint + ").";
                        break;
                    }
                    case ShareKind.Species:
                    {
                        var s = SpeciesFile.FromText(text);
                        SharedFiles.Save(ShareKind.Species, s.Name, text);
                        _filesMessage = "Added the species " + s.Name + ".";
                        break;
                    }
                    case ShareKind.Blueprint:
                    {
                        var b = Blueprint.FromText(text);
                        BlueprintFiles.Save(b);
                        _filesMessage = "Added the blueprint " + b.Name + ".";
                        break;
                    }
                    case ShareKind.MapCode:
                        ShowFiles = false;
                        OpenNewGame(true);
                        _codeText = text;
                        if (MapSettings.TryParse(text, out var settings, out string why)) { _newMap = settings; _seedText = settings.Seed.ToString(); }
                        else _newGameMessage = why;
                        return;
                    default:
                        _filesMessage = "The clipboard has no map, species, blueprint or map code in it.";
                        return;
                }
                _sharedFiles = null;
            }
            catch (System.Exception e) { _filesMessage = e.Message; }
        }

        private void UseShared(World world, SharedFile f)
        {
            try
            {
                string text = File.ReadAllText(f.Path);
                switch (f.Kind)
                {
                    case ShareKind.Map:
                        runner.NewWorld(MapFile.FromText(text), world.Economy.Enabled);
                        tools.LastAction = runner.LastMessage;
                        ShowFiles = false;
                        break;
                    case ShareKind.Species:
                    {
                        var s = SpeciesFile.FromText(text);
                        float x = Orbit != null ? Orbit.target.x : 0f, z = Orbit != null ? Orbit.target.z : 0f;
                        _filesMessage = s.AddTo(world, x, z, out string why)
                            ? "A pair of " + s.Name + " is in the tank" + (world.Economy.Enabled ? " (" + Economy.Format(s.Cost) + ")." : ".")
                            : why;
                        break;
                    }
                    case ShareKind.Blueprint:
                        tools.StartPlacingBlueprint(Blueprint.FromText(text));
                        ShowFiles = false;
                        break;
                }
            }
            catch (System.Exception e) { _filesMessage = e.Message; }
        }
    }
}

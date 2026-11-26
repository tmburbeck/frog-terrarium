using System;
using System.Collections.Generic;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The Build menu (parts, grid or free placement, turning, size and colour, blueprints) and the
    /// inspector for a selected part.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Whether the Build menu is showing.</summary>
        public bool ShowBuild;

        private Vector2 _buildScroll;
        private string _blueprintName = "My build";
        private List<string> _savedBlueprints;
        private float _hue, _saturation, _brightness = 0.5f;
        private string _partMessage = "";
        private readonly System.Random _colourDice = new System.Random();

        /// <summary>A palette of natural and bright colours; the sliders reach any other colour.</summary>
        private static readonly Color32[] Palette =
        {
            new Color32(130, 125, 118, 255), new Color32(80, 78, 74, 255), new Color32(196, 170, 120, 255), new Color32(110, 75, 45, 255),
            new Color32(60, 130, 55, 255), new Color32(150, 190, 70, 255), new Color32(235, 235, 225, 255), new Color32(30, 30, 32, 255),
            new Color32(200, 60, 50, 255), new Color32(235, 160, 40, 255), new Color32(70, 120, 210, 255), new Color32(160, 80, 190, 255),
        };

        public void OpenBuildMenu(bool open)
        {
            ShowBuild = open;
            if (open)
            {
                ShowPopulation = false;
                ShowMoney = false;
                if (!tools.IsPlacing) tools.ChooseBuildPart(tools.BuildKind);
                _savedBlueprints = null; // read the folder again
            }
            else if (tools.IsPlacing) tools.SetTool(Tool.Select);
        }

        private void DrawBuildMenu(World world, float h)
        {
            const float pw = 380f;
            float ph = Mathf.Min(720f, h - 190f);
            var r = Panel(new Rect(12f, 54f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build", _headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(world.Parts.Count + " / " + PartStore.MaxParts + " parts", _smallStyle);
            GUILayout.EndHorizontal();
            _buildScroll = GUILayout.BeginScrollView(_buildScroll);

            // The parts, three to a row.
            var all = Parts.All;
            for (int k = 0; k < all.Length; k += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = k; j < k + 3 && j < all.Length; j++)
                {
                    bool on = tools.CurrentTool == Tool.Build && tools.BuildKind == all[j].Kind;
                    bool open = world.Economy.PartUnlocked(all[j].Kind);
                    string label = !open ? all[j].Name + " (locked)"
                        : world.Economy.Enabled ? all[j].Name + " " + Economy.Format(all[j].Cost) : all[j].Name;
                    GUI.enabled = open;
                    if (GUILayout.Toggle(on, label, GUI.skin.button, GUILayout.Width(110f)) && !on) tools.ChooseBuildPart(all[j].Kind);
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            var info = Parts.Info(tools.BuildKind);
            GUILayout.Label(info.Description + Effects(info)
                + (world.Economy.Enabled ? " Costs " + Economy.Format(Economy.PartCost(tools.BuildKind, tools.BuildScale)) + " at this size." : ""), _smallStyle);
            GUILayout.Space(6f);

            // Placement.
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(tools.GridOn, "Grid", GUI.skin.button, GUILayout.Width(60f)) != tools.GridOn) tools.GridOn = !tools.GridOn;
            if (GUILayout.Toggle(!tools.GridOn, "Free", GUI.skin.button, GUILayout.Width(60f)) == tools.GridOn) tools.GridOn = !tools.GridOn;
            GUILayout.Label("(G)", _smallStyle, GUILayout.Width(28f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("<", GUILayout.Width(32f))) tools.Turn(-1);
            GUILayout.Label(Mathf.RoundToInt(tools.BuildRotation) + "°", _labelStyle, GUILayout.Width(44f));
            if (GUILayout.Button(">", GUILayout.Width(32f))) tools.Turn(1);
            GUILayout.Label("(R)", _smallStyle, GUILayout.Width(28f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Size", _labelStyle, GUILayout.Width(44f));
            tools.BuildScale = Mathf.Round(GUILayout.HorizontalSlider(tools.BuildScale, Parts.MinScale, Parts.MaxScale, GUILayout.Width(200f)) * 20f) / 20f;
            GUILayout.Label(Mathf.RoundToInt(tools.BuildScale * 100f) + "%", _smallStyle, GUILayout.Width(44f));
            if (GUILayout.Button("1:1", GUILayout.Width(36f))) tools.BuildScale = 1f;
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // Colour.
            GUILayout.Label("Colour", _labelStyle);
            var picked = ColourPicker(tools.BuildColour, info);
            tools.BuildColour = picked;
            GUILayout.Space(8f);

            DrawTankSection(world);
            GUILayout.Space(8f);
            DrawBlueprintSection(world);

            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(tools.LastAction)) GUILayout.Label(tools.LastAction, _smallStyle);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(60f))) OpenBuildMenu(false);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>Palette swatches, the natural colour, a dice for a surprise, and hue / richness / brightness sliders.</summary>
        private Color32 ColourPicker(Color32 current, PartInfo natural)
        {
            var result = current;
            GUILayout.BeginHorizontal();
            for (int k = 0; k < Palette.Length; k++)
            {
                if (k == 6) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                if (Swatch(Palette[k], Same(Palette[k], current))) result = Palette[k];
            }
            GUILayout.EndHorizontal();
            // Six more that go with the tank's style set.
            var style = TankLooks.Styles[runner.Sim.World.Style < TankLooks.Styles.Length ? runner.Sim.World.Style : 0];
            GUILayout.BeginHorizontal();
            for (int k = 0; k < 6; k++)
            {
                var c = new Color32(style.Palette[3 * k], style.Palette[3 * k + 1], style.Palette[3 * k + 2], 255);
                if (Swatch(c, Same(c, current))) result = c;
            }
            GUILayout.Label(style.Name, _smallStyle);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Natural", GUILayout.Width(70f))) result = new Color32(natural.R, natural.G, natural.B, 255);
            if (GUILayout.Button("Surprise me", GUILayout.Width(100f)))
                result = Color.HSVToRGB((float)_colourDice.NextDouble(), 0.3f + 0.6f * (float)_colourDice.NextDouble(), 0.35f + 0.6f * (float)_colourDice.NextDouble());
            GUILayout.FlexibleSpace();
            Swatch(result, false);
            GUILayout.EndHorizontal();

            // Keep the sliders in step with the colour unless they are being dragged.
            Color.RGBToHSV(result, out float hue, out float sat, out float val);
            if (!Same(result, (Color32)Color.HSVToRGB(_hue, _saturation, _brightness))) { _hue = hue; _saturation = sat; _brightness = val; }
            float nh = Slider("Hue", _hue, 0f, 1f), ns = Slider("Richness", _saturation, 0f, 1f), nv = Slider("Brightness", _brightness, 0.05f, 1f);
            if (nh != _hue || ns != _saturation || nv != _brightness)
            {
                _hue = nh; _saturation = ns; _brightness = nv;
                result = Color.HSVToRGB(nh, ns, nv);
            }
            return result;
        }

        private float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _smallStyle, GUILayout.Width(70f));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(220f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static bool Same(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) <= 2 && Mathf.Abs(a.g - b.g) <= 2 && Mathf.Abs(a.b - b.b) <= 2;

        /// <summary>A small coloured button. Returns true when clicked.</summary>
        private bool Swatch(Color32 c, bool chosen)
        {
            var rect = GUILayoutUtility.GetRect(36f, 24f, GUILayout.Width(36f), GUILayout.Height(24f));
            bool clicked = GUI.Button(rect, GUIContent.none);
            var old = GUI.color;
            GUI.color = (Color)c;
            float inset = chosen ? 6f : 3f;
            GUI.DrawTexture(new Rect(rect.x + inset, rect.y + inset, rect.width - 2f * inset, rect.height - 2f * inset), Texture2D.whiteTexture);
            GUI.color = old;
            return clicked;
        }

        /// <summary>" Gives cover. Tree frogs perch on it." and so on.</summary>
        private static string Effects(PartInfo info)
        {
            var words = new List<string>();
            if (info.Cover) words.Add("Frogs feel safe under it");
            if (info.Perch) words.Add("tree frogs like to perch on it");
            if (info.Shade > 0f) words.Add("its shade keeps the ground cooler");
            if (info.Damp > 0f) words.Add("it keeps the air damp");
            if (info.Stackable) words.Add("other parts can stand on it");
            if (info.Sockets.Length > 0) words.Add("it snaps to other parts with snap points");
            if (words.Count == 0) return "";
            string s = string.Join(", ", words);
            return " " + char.ToUpperInvariant(s[0]) + s.Substring(1) + ".";
        }

        /// <summary>The tank's size (with the upgrade button) and its style set.</summary>
        private void DrawTankSection(World world)
        {
            var eco = world.Economy;
            GUILayout.Label("Tank", _labelStyle);
            var next = TankLooks.NextSize(world);
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("{0} × {1} m, room for {2} frogs.", 2f * world.HalfSizeX, 2f * world.HalfSizeZ, world.FrogCapacity), _smallStyle, GUILayout.Width(200f));
            if (next != null)
            {
                string label = "Make it " + next.Name.ToLowerInvariant() + (eco.Enabled ? " (" + Economy.Format(next.Cost) + ")" : "");
                GUI.enabled = eco.CanAfford(next.Cost);
                if (GUILayout.Button(label, GUILayout.Width(150f)))
                    tools.LastAction = TankLooks.Upgrade(world, out string why) ? "The tank is now " + next.Size + "." : why;
                GUI.enabled = true;
            }
            else GUILayout.Label("The biggest size.", _smallStyle);
            GUILayout.EndHorizontal();
            if (next != null) GUILayout.Label("A bigger tank keeps everything where it is and adds flat ground around it.", _smallStyle);

            GUILayout.Label("Style: " + TankLooks.Styles[world.Style].Name + ". " + TankLooks.Styles[world.Style].Description, _smallStyle);
            for (int k = 0; k < TankLooks.Styles.Length; k += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = k; j < k + 3 && j < TankLooks.Styles.Length; j++)
                {
                    var st = TankLooks.Styles[j];
                    bool owned = TankLooks.StyleOwned(world, j);
                    string label = owned ? st.Name : st.Name + " " + Economy.Format(st.Cost);
                    bool on = world.Style == j;
                    GUI.enabled = on || owned || eco.CanAfford(st.Cost);
                    if (GUILayout.Toggle(on, label, GUI.skin.button, GUILayout.Width(110f)) && !on)
                        tools.LastAction = TankLooks.UseStyle(world, j, out string why) ? "Switched to the " + st.Name + " style." : why;
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawBlueprintSection(World world)
        {
            GUILayout.Label("Blueprints", _labelStyle);
            GUILayout.Label("Save your build to place it again, turned or somewhere else, or to share it: "
                + "a blueprint is a line of text you can copy and paste, or a .ftbp file.", _smallStyle);
            GUILayout.BeginHorizontal();
            _blueprintName = GUILayout.TextField(_blueprintName, Blueprint.MaxNameLength, GUILayout.Width(200f));
            GUI.enabled = world.Parts.Count > 0;
            if (GUILayout.Button("Save the tank's build", GUILayout.Width(140f))) SaveBlueprint(world, null);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Paste from clipboard", GUILayout.Width(150f)))
            {
                try { tools.StartPlacingBlueprint(Blueprint.FromText(GUIUtility.systemCopyBuffer)); }
                catch (SaveFormatException e) { tools.LastAction = e.Message; }
            }
            if (GUILayout.Button("Open folder", GUILayout.Width(100f)))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(BlueprintFiles.Folder);
                    Application.OpenURL("file:///" + BlueprintFiles.Folder.Replace('\\', '/'));
                }
                catch (Exception e) { tools.LastAction = "Could not open the folder: " + e.Message; }
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(70f))) _savedBlueprints = null;
            GUILayout.EndHorizontal();

            if (_savedBlueprints == null) _savedBlueprints = BlueprintFiles.List();
            if (_savedBlueprints.Count == 0) GUILayout.Label("No saved blueprints yet.", _smallStyle);
            string toDelete = null;
            foreach (var name in _savedBlueprints)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(name, _smallStyle, GUILayout.Width(150f));
                if (GUILayout.Button("Place", GUILayout.Width(52f)))
                {
                    try { tools.StartPlacingBlueprint(BlueprintFiles.Load(name)); }
                    catch (Exception e) { tools.LastAction = "Could not read " + name + ": " + e.Message; }
                }
                if (GUILayout.Button("Copy", GUILayout.Width(48f)))
                {
                    try
                    {
                        GUIUtility.systemCopyBuffer = BlueprintFiles.Load(name).ToText();
                        tools.LastAction = "Copied \"" + name + "\". Paste it anywhere to share it.";
                    }
                    catch (Exception e) { tools.LastAction = "Could not read " + name + ": " + e.Message; }
                }
                if (GUILayout.Button("Delete", GUILayout.Width(56f))) toDelete = name;
                GUILayout.EndHorizontal();
            }
            if (toDelete != null)
            {
                try
                {
                    BlueprintFiles.Delete(toDelete);
                    tools.LastAction = "Deleted the blueprint \"" + toDelete + "\"";
                }
                catch (Exception e) { tools.LastAction = "Could not delete it: " + e.Message; }
                _savedBlueprints = null;
            }
        }

        /// <summary>Saves the whole tank's build, or just the given parts, as a blueprint file and copies its text.</summary>
        private void SaveBlueprint(World world, ICollection<int> ids)
        {
            var bp = Blueprint.FromParts(world.Parts, _blueprintName, ids);
            if (bp.Parts.Count == 0) { tools.LastAction = "There is nothing to save."; return; }
            try
            {
                BlueprintFiles.Save(bp);
                GUIUtility.systemCopyBuffer = bp.ToText();
                tools.LastAction = "Saved \"" + bp.Name + "\" (" + bp.Parts.Count + " parts) and copied it to the clipboard.";
            }
            catch (Exception e) { tools.LastAction = "Could not save the blueprint: " + e.Message; }
            _savedBlueprints = null;
        }

        private void DrawPartInspector(World world, PlacedPart p, float w)
        {
            const float pw = 320f, ph = 420f;
            var r = Panel(new Rect(w - pw - 12f, 12f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            var info = p.Info;
            GUILayout.Label(info.Name, _headerStyle);
            GUILayout.Label(info.Description + Effects(info), _smallStyle);
            var below = world.Parts.Find(p.OnPartId);
            int onTop = world.Parts.StackFrom(p.Id).Count - 1;
            GUILayout.Label((below != null ? "Standing on a " + below.Info.Name.ToLowerInvariant() + "." : "Standing on the ground.")
                + (onTop > 0 ? " " + onTop + (onTop == 1 ? " part stands" : " parts stand") + " on it." : ""), _smallStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Size", _labelStyle, GUILayout.Width(44f));
            float scale = Mathf.Round(GUILayout.HorizontalSlider(p.Scale, Parts.MinScale, Parts.MaxScale, GUILayout.Width(170f)) * 20f) / 20f;
            GUILayout.Label(Mathf.RoundToInt(p.Scale * 100f) + "%", _smallStyle, GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            if (Mathf.Abs(scale - p.Scale) > 1e-4f)
            {
                string why = tools.ResizeSelectedPart(scale);
                _partMessage = why.Length > 0 ? "Can't make it that size: " + why : "";
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Turn", _labelStyle, GUILayout.Width(44f));
            if (GUILayout.Button("<", GUILayout.Width(32f))) _partMessage = Reason("Can't turn it: ", tools.TurnSelectedPart(-1));
            GUILayout.Label(Mathf.RoundToInt(p.Rotation) + "°", _labelStyle, GUILayout.Width(44f));
            if (GUILayout.Button(">", GUILayout.Width(32f))) _partMessage = Reason("Can't turn it: ", tools.TurnSelectedPart(1));
            GUILayout.EndHorizontal();

            GUILayout.Label("Colour", _labelStyle);
            tools.RecolourSelectedPart(ColourPicker(new Color32(p.R, p.G, p.B, 255), info));
            if (!string.IsNullOrEmpty(_partMessage)) GUILayout.Label(_partMessage, _smallStyle);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Move", GUILayout.Width(56f))) { _partMessage = ""; tools.StartMovingPart(); }
            if (GUILayout.Button("Copy", GUILayout.Width(52f))) { _partMessage = ""; tools.CopySelectedPart(); OpenBuildMenu(true); }
            if (GUILayout.Button("Save stack", GUILayout.Width(80f)))
            {
                var ids = new List<int>();
                foreach (var q in world.Parts.StackFrom(p.Id)) ids.Add(q.Id);
                _blueprintName = info.Name + " stack";
                SaveBlueprint(world, ids);
                _partMessage = tools.LastAction;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Delete", GUILayout.Width(60f))) { _partMessage = ""; tools.RemovePart(p.Id); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(60f))) { _partMessage = ""; tools.SelectedPartId = 0; }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static string Reason(string prefix, string why) => string.IsNullOrEmpty(why) ? "" : prefix + why;
    }
}

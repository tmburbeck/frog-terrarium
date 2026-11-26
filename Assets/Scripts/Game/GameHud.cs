using System.Collections.Generic;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The on-screen menus, drawn with Unity's simple immediate-mode GUI (placeholder look, easy
    /// to change): the build bar along the bottom, the alert list at the top, the inspector for
    /// the selected frog or device on the right, and a readout of the conditions under the mouse.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public SimulationRunner runner;
        public ToolController tools;
        public TerrainView terrainView;

        private readonly List<Rect> _panels = new List<Rect>();
        private readonly List<Rect> _panelsLastFrame = new List<Rect>();
        private float _scale = 1f;
        private GUIStyle _panelStyle, _labelStyle, _headerStyle, _smallStyle;

        /// <summary>True if the mouse is over a menu, so clicks there do not sculpt the terrain.</summary>
        public bool IsPointerOverUi(Vector3 mousePosition)
        {
            var p = new Vector2(mousePosition.x / _scale, (Screen.height - mousePosition.y) / _scale);
            foreach (var r in _panelsLastFrame) if (r.Contains(p)) return true;
            return false;
        }

        private void OnGUI()
        {
            if (runner == null || runner.Sim == null || tools == null) return;
            if (Event.current.type == EventType.Layout)
            {
                _panelsLastFrame.Clear();
                _panelsLastFrame.AddRange(_panels);
                _panels.Clear();
            }
            EnsureStyles();
            _scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            float w = Screen.width / _scale, h = Screen.height / _scale;

            var world = runner.Sim.World;
            DrawBuildBar(w, h);
            DrawCursorReadout(world, h);
            DrawLegend(w, h);
            DrawAlerts(world, w);
            var device = world.Devices.Find(tools.SelectedDeviceId);
            if (device != null) DrawDeviceInspector(device, w);
            int frog = tools.SelectedAgentId != 0 ? world.Agents.IndexOf(tools.SelectedAgentId) : -1;
            if (frog >= 0) DrawFrogInspector(world, frog, w);
            else if (Orbit != null) Orbit.following = false;
            GUI.matrix = Matrix4x4.identity;
        }

        private Rect Panel(Rect r)
        {
            if (Event.current.type == EventType.Layout || Event.current.type == EventType.Repaint) _panels.Add(r);
            GUI.Box(r, GUIContent.none, _panelStyle);
            return r;
        }

        private void DrawBuildBar(float w, float h)
        {
            const float barW = 1060f, barH = 96f;
            var r = Panel(new Rect((w - barW) * 0.5f, h - barH - 8f, barW, barH));
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 12f));

            GUILayout.BeginHorizontal();
            ToolButton("Select", Tool.Select);
            GUILayout.Space(8f);
            ToolButton("Raise", Tool.Raise);
            ToolButton("Lower", Tool.Lower);
            ToolButton("Smooth", Tool.Smooth);
            GUILayout.Space(8f);
            PaintButton("Soil", GroundLayer.Soil);
            PaintButton("Moss", GroundLayer.Moss);
            PaintButton("Sand", GroundLayer.Sand);
            PaintButton("Rock", GroundLayer.Rock);
            GUILayout.Space(8f);
            ToolButton("+ Heater", Tool.PlaceHeater);
            ToolButton("+ Mister", Tool.PlaceMister);
            ToolButton("+ Feeder", Tool.PlaceFeeder);
            GUILayout.Space(8f);
            ToolButton("+ Frog", Tool.AddFrog);
            ToolButton("+ 10 Crickets", Tool.AddCrickets);
            GUILayout.Space(8f);
            ToolButton("Remove", Tool.Remove);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Brush size", _smallStyle, GUILayout.Width(64f));
            tools.BrushRadius = GUILayout.HorizontalSlider(tools.BrushRadius, ToolController.MinBrushRadius, ToolController.MaxBrushRadius, GUILayout.Width(110f));
            GUILayout.Label("Strength", _smallStyle, GUILayout.Width(56f));
            tools.BrushStrength = GUILayout.HorizontalSlider(tools.BrushStrength, 0.05f, 1f, GUILayout.Width(110f));
            GUILayout.Space(10f);
            GUI.enabled = tools.History != null && tools.History.UndoCount > 0;
            if (GUILayout.Button("Undo", GUILayout.Width(56f))) tools.Undo();
            GUI.enabled = tools.History != null && tools.History.RedoCount > 0;
            if (GUILayout.Button("Redo", GUILayout.Width(56f))) tools.Redo();
            GUI.enabled = true;
            GUILayout.Space(10f);
            GUILayout.Label("Show", _smallStyle, GUILayout.Width(36f));
            ViewButton("Ground", TerrainViewMode.Normal);
            ViewButton("Heat", TerrainViewMode.Temperature);
            ViewButton("Humidity", TerrainViewMode.Humidity);
            GUILayout.EndHorizontal();

            GUILayout.Label(Hint(), _smallStyle);
            GUILayout.EndArea();
        }

        private string Hint()
        {
            switch (tools.CurrentTool)
            {
                case Tool.Raise: return "Hold left mouse to raise ground.  [ ] brush size,  Ctrl+Z undo.";
                case Tool.Lower: return "Hold left mouse to dig. Dig below the water line to make a pool.";
                case Tool.Smooth: return "Hold left mouse to smooth bumps.";
                case Tool.Paint: return "Hold left mouse to paint " + tools.PaintLayer + ". Moss keeps air moist; sand and rock dry it out.";
                case Tool.PlaceHeater: return "Click to place a heater. It warms the air around it, but dries it a little.";
                case Tool.PlaceMister: return "Click to place a mister. It sprays on a timer and raises humidity.";
                case Tool.PlaceFeeder: return "Click to place a feeder. It releases live crickets on a timer until its supply runs out.";
                case Tool.AddFrog: return "Click to add a White's tree frog.";
                case Tool.AddCrickets: return "Click to release 10 house crickets.";
                case Tool.Remove: return "Click a device to remove it.";
                default: return "Click a frog or device to inspect it. Right drag orbits, middle drag or WASD pans, scroll zooms.";
            }
        }

        private void ToolButton(string label, Tool tool)
        {
            bool on = tools.CurrentTool == tool;
            if (GUILayout.Toggle(on, label, GUI.skin.button, GUILayout.MinWidth(54f)) && !on) tools.SetTool(tool);
        }

        private void PaintButton(string label, GroundLayer layer)
        {
            bool on = tools.CurrentTool == Tool.Paint && tools.PaintLayer == layer;
            if (GUILayout.Toggle(on, label, GUI.skin.button, GUILayout.MinWidth(48f)) && !on)
            {
                tools.SetTool(Tool.Paint);
                tools.PaintLayer = layer;
            }
        }

        private void ViewButton(string label, TerrainViewMode mode)
        {
            if (terrainView == null) return;
            bool on = terrainView.mode == mode;
            if (GUILayout.Toggle(on, label, GUI.skin.button, GUILayout.MinWidth(60f)) && !on) terrainView.mode = mode;
        }

        private void DrawCursorReadout(World world, float h)
        {
            if (!tools.HasCursor) return;
            var p = tools.CursorPoint;
            var env = world.Environment;
            string text = string.Format("{0:0.0} °C   {1:0}% humidity   {2}", env.TemperatureAt(p.x, p.z), env.HumidityAt(p.x, p.z),
                world.Terrain.IsWater(p.x, p.z) ? string.Format("water {0:0.00} m deep", world.Terrain.WaterDepthAt(p.x, p.z))
                                                : world.Terrain.LayerAt(p.x, p.z).ToString());
            var r = new Rect(12f, h - 40f, 380f, 28f);
            GUI.Box(r, GUIContent.none, _panelStyle);
            GUI.Label(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, r.height - 8f), text, _labelStyle);
        }

        private void DrawLegend(float w, float h)
        {
            if (terrainView == null || terrainView.mode == TerrainViewMode.Normal) return;
            bool heat = terrainView.mode == TerrainViewMode.Temperature;
            var r = new Rect(12f, h - 80f, 380f, 34f);
            GUI.Box(r, GUIContent.none, _panelStyle);
            const int steps = 20;
            float barX = r.x + 8f, barW = 220f;
            for (int i = 0; i < steps; i++)
            {
                float k = i / (steps - 1f);
                var c = heat ? TerrainView.TemperatureColour(Mathf.Lerp(15f, 35f, k)) : TerrainView.HumidityColour(Mathf.Lerp(20f, 100f, k));
                c.a = 1f;
                var old = GUI.color;
                GUI.color = c;
                GUI.DrawTexture(new Rect(barX + i * barW / steps, r.y + 8f, barW / steps + 1f, 18f), Texture2D.whiteTexture);
                GUI.color = old;
            }
            GUI.Label(new Rect(barX + barW + 8f, r.y + 7f, 150f, 22f), heat ? "15 °C  to  35 °C" : "20%  to  100%", _labelStyle);
        }

        private void DrawDeviceInspector(Device d, float w)
        {
            const float pw = 280f;
            float ph = d.Kind == DeviceKind.Heater ? 180f : d.Kind == DeviceKind.Mister ? 210f : 250f;
            var r = Panel(new Rect(w - pw - 12f, 12f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.Label(d.DisplayName, _headerStyle);

            switch (d.Kind)
            {
                case DeviceKind.Heater:
                    GUILayout.Label(!d.Enabled ? "Switched off" : d.Active ? "Heating" : "Warm enough, resting", _labelStyle);
                    d.Setting = Stepper("Thermostat", d.Setting, 0.5f, "{0:0.0} °C");
                    GUILayout.Label(string.Format("Never heats above {0:0} °C.", DeviceStore.HeaterMaxTemperature), _smallStyle);
                    break;
                case DeviceKind.Mister:
                    GUILayout.Label(!d.Enabled ? "Switched off" : d.Active ? "Spraying" : string.Format("Next spray in {0:0} s", d.Timer), _labelStyle);
                    d.Setting = Stepper("Spray every", d.Setting, 5f, "{0:0} s");
                    d.Setting2 = Stepper("Spray for", d.Setting2, 1f, "{0:0} s");
                    break;
                case DeviceKind.Feeder:
                    GUILayout.Label(!d.Enabled ? "Switched off" : d.Supply < 1f ? "Empty" : string.Format("Next release in {0:0} s", d.Timer), _labelStyle);
                    Bar("Supply", d.Supply / DeviceStore.FeederCapacity, false, string.Format("{0:0} / {1}", d.Supply, DeviceStore.FeederCapacity));
                    d.Setting = Stepper("Release every", d.Setting, 10f, "{0:0} s");
                    d.Setting2 = Stepper("Crickets each", d.Setting2, 1f, "{0:0}");
                    if (GUILayout.Button("Refill to full")) d.Supply = DeviceStore.FeederCapacity;
                    break;
            }
            DeviceStore.ClampSettings(d);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            d.Enabled = GUILayout.Toggle(d.Enabled, d.Enabled ? " On" : " Off", GUILayout.Width(70f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(80f))) tools.RemoveDevice(d.Id);
            if (GUILayout.Button("Close", GUILayout.Width(60f))) tools.SelectedDeviceId = 0;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private OrbitCamera Orbit
        {
            get
            {
                var cam = Camera.main;
                return cam != null ? cam.GetComponent<OrbitCamera>() : null;
            }
        }

        private void DrawAlerts(World world, float w)
        {
            var alerts = world.Alerts.All;
            if (alerts.Count == 0) return;
            const float aw = 520f;
            int shown = Mathf.Min(alerts.Count, 5);
            var r = Panel(new Rect((w - aw) * 0.5f, 10f, aw, 14f + shown * 44f));
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 10f));
            for (int k = 0; k < shown; k++)
            {
                var al = alerts[k];
                GUILayout.BeginHorizontal();
                var old = GUI.color;
                GUI.color = al.Level == AlertLevel.Critical ? new Color(1f, 0.45f, 0.4f)
                          : al.Level == AlertLevel.Warning ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 0.9f, 1f);
                GUILayout.BeginVertical();
                GUILayout.Label(al.Text, _labelStyle);
                GUI.color = old;
                if (!string.IsNullOrEmpty(al.Fix)) GUILayout.Label(al.Fix, _smallStyle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Show", GUILayout.Width(52f)) && Orbit != null)
                {
                    Orbit.following = false;
                    Orbit.Focus(new Vector3(al.X, world.Terrain.HeightAt(al.X, al.Z), al.Z));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private void DrawFrogInspector(World world, int i, float w)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.Frog;
            int id = a.Id[i];
            const float pw = 300f, ph = 400f;
            var r = Panel(new Rect(w - pw - 12f, 12f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.Label(SpeciesCatalog.FrogName(id), _headerStyle);
            GUILayout.Label(sp.Name + " (" + sp.LatinName + "), " + Mathf.FloorToInt(a.Age[i] / 60f) + " min old", _smallStyle);
            GUILayout.Label(Activity(world, i), _labelStyle);
            GUILayout.Space(4f);
            Bar("Hunger", a.Hunger[i], true, null);
            Bar("Thirst", a.Thirst[i], true, null);
            Bar("Tiredness", a.Fatigue[i], true, null);
            Bar("Comfort", a.Comfort[i], false, null);
            float t = world.Environment.TemperatureAt(a.PosX[i], a.PosZ[i]);
            float h = world.Environment.HumidityAt(a.PosX[i], a.PosZ[i]);
            GUILayout.Label(string.Format("Here: {0:0.0} °C, {1:0}% humidity", t, h), _smallStyle);
            GUILayout.Label(string.Format("Likes {0:0}–{1:0} °C and {2:0}–{3:0}% humidity", sp.MinTemperature, sp.MaxTemperature, sp.MinHumidity, sp.MaxHumidity), _smallStyle);

            var brain = runner.Brain;
            if (brain != null)
            {
                // What the utility brain is weighing up right now (higher wins).
                var ctx = brain.ContextFor(world, i);
                var sb = new System.Text.StringBuilder("Thinking: ");
                foreach (var g in brain.FrogPicker.Goals)
                    sb.Append(GoalWord(g)).Append(' ').Append(brain.FrogPicker.Score(ctx, g).ToString("0.00")).Append("   ");
                GUILayout.Label(sb.ToString(), _smallStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            var orbit = Orbit;
            if (orbit != null)
            {
                orbit.following = GUILayout.Toggle(orbit.following, " Follow", GUILayout.Width(80f));
                if (orbit.following) orbit.followPoint = new Vector3(a.PosX[i], a.PosY[i], a.PosZ[i]);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Rehome", GUILayout.Width(80f))) tools.RehomeFrog(id);
            if (GUILayout.Button("Close", GUILayout.Width(60f))) tools.SelectedAgentId = 0;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static string GoalWord(Goal g)
        {
            switch (g)
            {
                case Goal.Eat: return "eat";
                case Goal.Drink: return "drink";
                case Goal.Sleep: return "sleep";
                case Goal.SeekComfort: return "comfort";
                case Goal.Wander: return "explore";
                default: return g.ToString().ToLowerInvariant();
            }
        }

        /// <summary>A plain-words description of what the frog is doing.</summary>
        public static string Activity(World world, int i)
        {
            var a = world.Agents;
            switch (a.Goal[i])
            {
                case Goal.Eat: return a.TargetId[i] > 0 ? "Stalking a cricket" : "Looking for insects to eat";
                case Goal.Drink: return world.Terrain.IsWater(a.PosX[i], a.PosZ[i]) ? "Soaking in the water (frogs drink through their skin)" : "Heading for water";
                case Goal.Sleep: return "Sleeping";
                case Goal.SeekComfort: return "Looking for a more comfortable spot";
                case Goal.Wander: return "Exploring";
                default: return "Resting";
            }
        }

        /// <summary>A labelled bar. For needs, full means bad (red); for comfort and supply, full means good (green).</summary>
        private void Bar(string label, float value, bool fullIsBad, string text)
        {
            value = Mathf.Clamp01(value);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(84f));
            var rect = GUILayoutUtility.GetRect(150f, 16f, GUILayout.ExpandWidth(true));
            rect.y += 4f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            float good = fullIsBad ? 1f - value : value;
            GUI.color = Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.3f, 0.8f, 0.3f), good);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * value, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
            GUILayout.Label(text ?? Mathf.RoundToInt(value * 100f) + "%", _smallStyle, GUILayout.Width(70f));
            GUILayout.EndHorizontal();
        }

        private float Stepper(string label, float value, float step, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(100f));
            if (GUILayout.Button("-", GUILayout.Width(28f))) value -= step;
            GUILayout.Label(string.Format(format, value), _labelStyle, GUILayout.Width(70f));
            if (GUILayout.Button("+", GUILayout.Width(28f))) value += step;
            GUILayout.EndHorizontal();
            return value;
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null) return;
            _panelStyle = new GUIStyle(GUI.skin.box);
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _labelStyle.normal.textColor = Color.white;
            _headerStyle = new GUIStyle(_labelStyle) { fontSize = 17, fontStyle = FontStyle.Bold };
            _smallStyle = new GUIStyle(_labelStyle) { fontSize = 12 };
            _smallStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        }
    }
}

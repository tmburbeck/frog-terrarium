using System.Collections.Generic;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The on-screen menus, drawn with Unity's simple immediate-mode GUI (placeholder look, easy
    /// to change): the build bar along the bottom, the inspector for the selected device on the
    /// right, and a readout of the conditions under the mouse.
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
            var device = world.Devices.Find(tools.SelectedDeviceId);
            if (device != null) DrawDeviceInspector(device, w);
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
            const float barW = 900f, barH = 96f;
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
                case Tool.Remove: return "Click a device to remove it.";
                default: return "Click a device to inspect it. Right drag orbits, middle drag or WASD pans, scroll zooms.";
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
            float ph = d.Kind == DeviceKind.Mister ? 210f : 180f;
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

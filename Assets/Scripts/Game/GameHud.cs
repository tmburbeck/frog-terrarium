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
    public sealed partial class GameHud : MonoBehaviour
    {
        public SimulationRunner runner;
        public ToolController tools;
        public TerrainView terrainView;
        public PerformanceBenchmark benchmark;
        public DebugOverlay overlay;

        /// <summary>Whether the population panel (opened with the Frogs button) is showing.</summary>
        public bool ShowPopulation;
        /// <summary>Whether the species book (catalogue of species, hybrids and mutations) is showing.</summary>
        public bool ShowSpeciesBook;
        private Vector2 _bookScroll;
        /// <summary>The names typed into the species book for types waiting to be registered.</summary>
        private readonly Dictionary<int, string> _pendingNames = new Dictionary<int, string>();

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
            HandleHealthKey();
            DrawBuildBar(w, h);
            DrawCursorReadout(world, h);
            DrawLegend(w, h);
            DrawAlerts(world, w);
            DrawMoneyBar(world);
            if (ShowHealth) DrawHealthPanel(world, h);
            else if (ShowMoney) DrawMoneyPanel(world, h);
            else if (ShowBuild) DrawBuildMenu(world, h);
            else if (ShowPopulation) DrawPopulation(world, h);
            if (ShowSpeciesBook) DrawSpeciesBook(world, w, h);
            var device = world.Devices.Find(tools.SelectedDeviceId);
            if (device != null) DrawDeviceInspector(device, w);
            int frog = tools.SelectedAgentId != 0 ? world.Agents.IndexOf(tools.SelectedAgentId) : -1;
            if (frog >= 0) DrawFrogInspector(world, frog, w);
            else if (Orbit != null) Orbit.following = false;
            var part = world.Parts.Find(tools.SelectedPartId);
            if (part != null && device == null && frog < 0 && tools.CurrentTool != Tool.MovePart) DrawPartInspector(world, part, w);
            DrawScenario(world, w, h);
            if (ShowFiles) DrawFiles(world, w, h);
            if (ShowNewGame) DrawNewGame(w, h);
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
            const float barW = 1400f, barH = 96f;
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
            ToolButton("+ Controller", Tool.PlaceController);
            GUILayout.Space(8f);
            ToolButton("+ Frog", Tool.AddFrog);
            // Which species "+ Frog" adds; click to switch to the next one.
            var eco = runner.Sim.World.Economy;
            string speciesLabel = SpeciesCatalog.FrogOf(tools.FrogSpecies).ShortName
                + (!eco.SpeciesUnlocked(tools.FrogSpecies) ? " (locked)" : eco.Enabled ? " " + Economy.Format(Economy.FrogCost(tools.FrogSpecies)) : "");
            if (GUILayout.Button(speciesLabel, GUILayout.MinWidth(86f))) tools.NextFrogSpecies();
            ToolButton("+ 10 Crickets", Tool.AddCrickets);
            GUILayout.Space(8f);
            ToolButton("Remove", Tool.Remove);
            GUILayout.Space(8f);
            bool build = GUILayout.Toggle(ShowBuild, "Build", GUI.skin.button, GUILayout.MinWidth(64f));
            if (build != ShowBuild) OpenBuildMenu(build);
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
            GUILayout.Space(10f);
            // Buttons for the F-key shortcuts, because many laptops use F keys for volume,
            // brightness and so on unless you also hold Fn.
            if (GUILayout.Button("Save", GUILayout.Width(48f))) runner.QuickSave();
            if (GUILayout.Button("Load", GUILayout.Width(48f))) runner.QuickLoad();
            bool newGame = GUILayout.Toggle(ShowNewGame, "New", GUI.skin.button, GUILayout.Width(48f));
            if (newGame != ShowNewGame) OpenNewGame(newGame);
            bool files = GUILayout.Toggle(ShowFiles, "Files", GUI.skin.button, GUILayout.Width(48f));
            if (files != ShowFiles) OpenFiles(files);
            if (overlay != null && GUILayout.Button("Stats", GUILayout.Width(48f))) overlay.visible = !overlay.visible;
            bool population = GUILayout.Toggle(ShowPopulation, "Frogs", GUI.skin.button, GUILayout.Width(56f));
            if (population != ShowPopulation)
            {
                ShowPopulation = population;
                if (population && ShowBuild) OpenBuildMenu(false);
                if (population) { ShowMoney = false; ShowHealth = false; }
            }
            bool health = GUILayout.Toggle(ShowHealth, "Health", GUI.skin.button, GUILayout.Width(60f));
            if (health != ShowHealth) OpenHealth(health);
            ShowSpeciesBook = GUILayout.Toggle(ShowSpeciesBook, "Species", GUI.skin.button, GUILayout.Width(64f));
            if (benchmark != null)
            {
                GUI.enabled = !benchmark.Running;
                if (GUILayout.Button("Speed test", GUILayout.Width(80f))) benchmark.Begin();
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();

            // The result of the last command (or why it was refused) for a few seconds, then the tool's hint.
            bool recent = !string.IsNullOrEmpty(tools.LastAction) && Time.unscaledTime - tools.LastActionTime < 5f;
            GUILayout.Label(recent ? tools.LastAction : Hint(), _smallStyle);
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
                case Tool.PlaceController: return "Click to place a smart controller. It sets every device on Smart to suit the frogs near it.";
                case Tool.AddFrog:
                {
                    var sp = SpeciesCatalog.FrogOf(tools.FrogSpecies);
                    return string.Format("Click to add a {0}. Females and males alternate, so two clicks make a pair. Likes {1:0}–{2:0} °C, {3:0}–{4:0}% humidity.",
                        sp.Name.ToLowerInvariant(), sp.MinTemperature, sp.MaxTemperature, sp.MinHumidity, sp.MaxHumidity);
                }
                case Tool.AddCrickets: return "Click to release 10 house crickets.";
                case Tool.Remove: return "Click a device or building part to remove it.";
                case Tool.Build:
                    return (tools.Ghost != null && !tools.GhostValid ? "Can't build here: " + tools.GhostWhy + "  " : "Click to place. ")
                        + "R turns it (Shift+R back), G switches grid and free placement. Parts snap together and stack on slabs and platforms.";
                case Tool.MovePart:
                    return (tools.Ghost != null && !tools.GhostValid ? "Can't move it here: " + tools.GhostWhy + "  " : "Click where it should go. ")
                        + "R turns it. Anything standing on it comes along. Esc cancels.";
                case Tool.PlaceBlueprint:
                    return (tools.BlueprintGhost.Count > 0 && !tools.GhostValid ? "Doesn't fit here: " + tools.GhostWhy + "  " : "Click to place the blueprint. ")
                        + "R turns it, G switches grid. Esc stops.";
                default: return "Click a frog, device or building part to inspect it. Right drag orbits, middle drag or WASD pans, scroll zooms.";
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
            const float pw = 320f;
            float ph = d.Kind == DeviceKind.Heater ? 220f : d.Kind == DeviceKind.Mister ? 250f : d.Kind == DeviceKind.Controller ? 230f : 310f;
            var world = runner.Sim.World;
            var r = Panel(new Rect(w - pw - 12f, 12f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.Label(d.DisplayName, _headerStyle);

            switch (d.Kind)
            {
                case DeviceKind.Heater:
                    GUILayout.Label(!d.Enabled ? "Switched off" : d.Active ? "Heating" : "Warm enough, resting", _labelStyle);
                    DrawModeRow(world, d);
                    if (d.Mode == DeviceMode.Smart) GUILayout.Label(SmartNote(world, string.Format("Thermostat {0:0.0} °C", d.Setting)), _smallStyle);
                    else d.Setting = Stepper("Thermostat", d.Setting, 0.5f, "{0:0.0} °C");
                    GUILayout.Label(string.Format("Never heats above {0:0} °C.", DeviceStore.HeaterMaxTemperature), _smallStyle);
                    break;
                case DeviceKind.Mister:
                    if (d.Mode == DeviceMode.Basic)
                        GUILayout.Label(!d.Enabled ? "Switched off" : d.Active ? "Spraying" : string.Format("Next spray in {0:0} s", d.Timer), _labelStyle);
                    else
                        GUILayout.Label(!d.Enabled ? "Switched off" : string.Format("{0}: air here {1:0}%", d.Active ? "Spraying" : "Damp enough, resting",
                            EnvironmentSystem.AverageHumidity(world.Environment, d.X, d.Z, DeviceStore.SensorRadius)), _labelStyle);
                    DrawModeRow(world, d);
                    if (d.Mode == DeviceMode.Basic)
                    {
                        d.Setting = Stepper("Spray every", d.Setting, 5f, "{0:0} s");
                        d.Setting2 = Stepper("Spray for", d.Setting2, 1f, "{0:0} s");
                    }
                    else if (d.Mode == DeviceMode.Sensor) d.Target = Stepper("Keep air at", d.Target, 1f, "{0:0}%");
                    else GUILayout.Label(SmartNote(world, string.Format("Keeps the air at {0:0}%", d.Target)), _smallStyle);
                    break;
                case DeviceKind.Feeder:
                    GUILayout.Label(!d.Enabled ? "Switched off" : d.Supply < 1f ? "Empty"
                        : d.Mode == DeviceMode.Basic ? string.Format("Next release in {0:0} s", d.Timer)
                        : string.Format("Checks the food in {0:0} s", d.Timer), _labelStyle);
                    Bar("Supply", d.Supply / DeviceStore.FeederCapacity, false, string.Format("{0:0} / {1}", d.Supply, DeviceStore.FeederCapacity));
                    DrawModeRow(world, d);
                    d.Setting = Stepper(d.Mode == DeviceMode.Basic ? "Release every" : "Check every", d.Setting, 10f, "{0:0} s");
                    d.Setting2 = Stepper("Crickets each", d.Setting2, 1f, "{0:0}");
                    if (d.Mode == DeviceMode.Sensor) d.Target = Stepper("Keep per frog", d.Target, 0.5f, "{0:0.0}");
                    else if (d.Mode == DeviceMode.Smart) GUILayout.Label(SmartNote(world, string.Format("Keeps {0:0.0} crickets per frog", d.Target)), _smallStyle);
                    if (d.Mode != DeviceMode.Basic)
                        GUILayout.Label("Refills itself when low" + (world.Economy.Enabled ? " (" + Economy.Format(Economy.FeederRefillCost) + " each time)." : "."), _smallStyle);
                    if (GUILayout.Button(world.Economy.Enabled ? "Refill to full (" + Economy.Format(Economy.FeederRefillCost) + ")" : "Refill to full")) tools.RefillFeeder(d);
                    break;
                case DeviceKind.Controller:
                    DrawControllerInspector(world);
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
            // Centred, but never over the money bar at the top left.
            var r = Panel(new Rect(Mathf.Max((w - aw) * 0.5f, MoneyBarWidth + 20f), 10f, aw, 14f + shown * 44f));
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
                if (al.Cause == AlertSystem.EscapedCause)
                {
                    if (GUILayout.Button("Bring back", GUILayout.Width(86f))) tools.BringBackEscapedFrogs();
                }
                else if (al.Cause == AlertSystem.TankFullCause)
                {
                    if (GUILayout.Button("Frogs", GUILayout.Width(60f))) { ShowPopulation = true; if (ShowBuild) OpenBuildMenu(false); }
                }
                else if (al.Cause == AlertSystem.ReadyToRegisterCause)
                {
                    if (GUILayout.Button("Name it", GUILayout.Width(70f))) ShowSpeciesBook = true;
                }
                else if (GUILayout.Button("Show", GUILayout.Width(52f)) && Orbit != null)
                {
                    Orbit.following = false;
                    Orbit.Focus(new Vector3(al.X, world.Terrain.HeightAt(al.X, al.Z), al.Z));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        /// <summary>
        /// The population panel: how many frogs of each species and age there are, and the
        /// controls for keeping numbers down (breeding switch, frog limit, collect eggs, rehome).
        /// </summary>
        private void DrawPopulation(World world, float h)
        {
            var a = world.Agents;
            int n = SpeciesCatalog.Frogs.Length;
            var counts = new int[n, 4]; // young in water, growing, adult, elder
            int eggs = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) continue;
                var st = a.Stage[i];
                if (st == LifeStage.Egg) eggs++;
                int col = st <= LifeStage.Tadpole ? 0 : st < LifeStage.Adult ? 1 : st == LifeStage.Adult ? 2 : 3;
                counts[a.Species[i], col]++;
            }

            const float pw = 400f, ph = 470f;
            var r = Panel(new Rect(12f, Mathf.Max(12f, (h - ph) * 0.5f - 60f), pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.Label("Frogs in the tank", _headerStyle);
            for (int s = 0; s < n; s++)
            {
                int total = counts[s, 0] + counts[s, 1] + counts[s, 2] + counts[s, 3];
                if (total == 0) continue;
                GUILayout.Label(string.Format("{0}: {1}", SpeciesCatalog.Frogs[s].Name, total), _labelStyle);
                GUILayout.Label(string.Format("   {0} eggs or tadpoles, {1} growing up, {2} adults, {3} elders",
                    counts[s, 0], counts[s, 1], counts[s, 2], counts[s, 3]), _smallStyle);
            }
            int hybrids = 0, mutants = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) continue;
                if (a.Genes[i].IsHybrid) hybrids++;
                if (a.Genes[i].Visible != 0) mutants++;
            }
            if (hybrids + mutants > 0)
                GUILayout.Label(string.Format("   Of these, {0} hybrids and {1} with mutations (counted with their main species).", hybrids, mutants), _smallStyle);
            int frogs = world.FrogCount();
            GUILayout.Label(string.Format("Total {0} frogs. Breeding stops at {1} (the tank can support {2}).",
                frogs, world.FrogLimit, world.FrogCapacity), _smallStyle);
            GUILayout.Space(6f);

            world.BreedingEnabled = GUILayout.Toggle(world.BreedingEnabled, " Let frogs breed");
            bool lab = world.Economy.Has(Research.GeneticsLab);
            GUI.enabled = lab || world.MutationBoost;
            world.MutationBoost = GUILayout.Toggle(world.MutationBoost, lab ? " Mutation boost (25 times more mutations)" : " Mutation boost (needs Genetics lab research)");
            GUI.enabled = true;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Frog limit", _labelStyle, GUILayout.Width(100f));
            int cap = world.FrogCapacity;
            int limit = world.BreedingLimit > 0 && world.BreedingLimit < cap ? world.BreedingLimit : 0;
            if (GUILayout.Button("-", GUILayout.Width(28f))) limit = limit == 0 ? cap - 1 : limit - 1;
            GUILayout.Label(limit == 0 ? "Tank max (" + cap + ")" : limit.ToString(), _labelStyle, GUILayout.Width(110f));
            if (GUILayout.Button("+", GUILayout.Width(28f)) && limit != 0) limit = limit + 1 >= cap ? 0 : limit + 1;
            world.BreedingLimit = Mathf.Max(0, limit == 0 ? 0 : Mathf.Max(2, limit));
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            GUI.enabled = eggs > 0;
            if (GUILayout.Button("Collect eggs (" + eggs + ")", GUILayout.Width(150f))) tools.CollectEggs();
            GUI.enabled = frogs > world.FrogLimit;
            if (GUILayout.Button("Rehome extras (" + Mathf.Max(0, frogs - world.FrogLimit) + ")", GUILayout.Width(170f))) tools.RehomeExtras();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Rehoming gives away the oldest frogs first, but always keeps a breeding pair of each species. "
                + "You can also rehome one frog from its inspector.", _smallStyle);
            GUILayout.Label(string.Format("So far: {0} frogs rehomed, {1} eggs collected.", world.Rehomed, world.EggsCollected), _smallStyle);
            if (!string.IsNullOrEmpty(tools.LastAction)) GUILayout.Label(tools.LastAction, _smallStyle);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(60f))) ShowPopulation = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>
        /// The species book: every real species (and how many are in the tank), the hybrids and
        /// mutations found so far, the new species the player has registered, and any new type that
        /// has bred true and is waiting for a name.
        /// </summary>
        private void DrawSpeciesBook(World world, float w, float h)
        {
            var a = world.Agents;
            const float pw = 560f;
            float ph = Mathf.Min(640f, h - 140f);
            var r = Panel(new Rect((w - pw) * 0.5f, Mathf.Max(12f, (h - ph) * 0.5f - 40f), pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.Label("Species book", _headerStyle);
            _bookScroll = GUILayout.BeginScrollView(_bookScroll);

            var ready = world.TypesReadyToRegister();
            if (ready.Count > 0)
            {
                GUILayout.Label("Ready to name", _labelStyle);
                GUILayout.Label("These new kinds of frog have bred true for " + Lineage.GenerationsToRegister
                    + " generations. Give one a name to register it as a new species.", _smallStyle);
                foreach (int type in ready)
                {
                    GUILayout.Label("• " + Lineage.Describe(type) + " (" + world.CountOfType(type) + " in the tank)", _smallStyle);
                    if (!_pendingNames.TryGetValue(type, out string name)) name = SuggestName(type);
                    GUILayout.BeginHorizontal();
                    name = GUILayout.TextField(name, World.MaxSpeciesNameLength, GUILayout.Width(300f));
                    _pendingNames[type] = name;
                    if (GUILayout.Button("Register", GUILayout.Width(80f)))
                    {
                        tools.LastAction = world.Register(type, name)
                            ? "Registered " + name.Trim() + "!"
                            : "That name is empty or already taken. Try another.";
                    }
                    GUILayout.EndHorizontal();
                }
                if (!string.IsNullOrEmpty(tools.LastAction)) GUILayout.Label(tools.LastAction, _smallStyle);
                GUILayout.Space(8f);
            }

            if (world.Registered.Count > 0)
            {
                GUILayout.Label("Your new species", _labelStyle);
                foreach (var reg in world.Registered)
                    GUILayout.Label(string.Format("• {0}: {1}. {2} in the tank.", reg.Name, Lineage.Describe(reg.Type), world.CountOfType(reg.Type)), _smallStyle);
                GUILayout.Space(8f);
            }

            GUILayout.Label("Real species", _labelStyle);
            var counts = new int[SpeciesCatalog.Frogs.Length];
            for (int i = 0; i < a.Count; i++) if (a.Kind[i] == AgentKind.Frog) counts[a.Species[i]]++;
            foreach (var sp in SpeciesCatalog.Frogs)
            {
                GUILayout.Label(string.Format("• {0} ({1}), genus {2}: {3} in the tank. Likes {4:0}–{5:0} °C, {6:0}–{7:0}% humidity.",
                    sp.Name, sp.LatinName, sp.Genus, counts[sp.Id], sp.MinTemperature, sp.MaxTemperature, sp.MinHumidity, sp.MaxHumidity), _smallStyle);
            }
            GUILayout.Label("Species in the same genus can make hybrids: White's with White-lipped, red-eyed with Morelet's, "
                + "fire-bellied with yellow-bellied. A female only pairs with another species when none of her own kind is about.", _smallStyle);
            GUILayout.Space(8f);

            GUILayout.Label("Hybrids found", _labelStyle);
            bool anyHybrid = false;
            for (int x = 0; x < SpeciesCatalog.Frogs.Length; x++)
                for (int y = x + 1; y < SpeciesCatalog.Frogs.Length; y++)
                {
                    if ((world.DiscoveredHybrids & (1UL << (x * 8 + y))) == 0UL) continue;
                    anyHybrid = true;
                    GUILayout.Label("• " + SpeciesCatalog.Frogs[x].ShortName + " × " + SpeciesCatalog.Frogs[y].ShortName, _smallStyle);
                }
            if (!anyHybrid) GUILayout.Label("None yet. Put a female with a male of a related species, and no male of her own kind.", _smallStyle);
            GUILayout.Space(8f);

            GUILayout.Label("Mutations", _labelStyle);
            GUILayout.Label("About 1 egg in 100 has a new mutation; more if the mother is stressed or the parents are hybrids. "
                + "Dominant ones show with one copy; recessive ones need a copy from each parent.", _smallStyle);
            for (int m = 0; m < Mutations.Count; m++)
            {
                var info = Mutations.All[m];
                bool found = (world.DiscoveredMutations & (1 << m)) != 0;
                GUILayout.Label(found
                    ? string.Format("• {0} ({1}): {2}", info.Name, info.Dominant ? "dominant" : "recessive", info.Effect)
                    : "• ??? (not found yet)", _smallStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(60f))) ShowSpeciesBook = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        /// <summary>A starting suggestion for a new species' name, such as "Toothy blue White's".</summary>
        private static string SuggestName(int type)
        {
            var main = SpeciesCatalog.FrogOf(Lineage.SpeciesOfType(type));
            ushort muts = Lineage.MutationsOfType(type);
            var words = new List<string>();
            if (Mutations.Shows(muts, Mutation.Teeth)) words.Add("Toothy");
            if (Mutations.Shows(muts, Mutation.Claws)) words.Add("Clawed");
            if (Mutations.Shows(muts, Mutation.GiantEyes)) words.Add("Goggle-eyed");
            if (Mutations.Shows(muts, Mutation.LongLegs)) words.Add("Leggy");
            if (Mutations.Shows(muts, Mutation.StubbyLegs)) words.Add("Stubby");
            if (Mutations.Shows(muts, Mutation.LoudCall)) words.Add("Loud");
            if (Mutations.Shows(muts, Mutation.Albino)) words.Add("ghost");
            else if (Mutations.Shows(muts, Mutation.Blue)) words.Add("blue");
            else if (Mutations.Shows(muts, Mutation.Melanistic)) words.Add("black");
            string baseName = Lineage.QuartersOfType(type) > 0
                ? main.ShortName + "-" + SpeciesCatalog.FrogOf(Lineage.OtherOfType(type)).ShortName.ToLowerInvariant() + " frog"
                : main.ShortName + " frog";
            if (words.Count > 0) words[0] = char.ToUpperInvariant(words[0][0]) + words[0].Substring(1);
            words.Add(baseName);
            string name = string.Join(" ", words);
            return name.Length > World.MaxSpeciesNameLength ? name.Substring(0, World.MaxSpeciesNameLength) : name;
        }

        private void DrawFrogInspector(World world, int i, float w)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.FrogOf(a, i);
            var genes = a.Genes[i];
            var stage = a.Stage[i];
            int id = a.Id[i];
            bool young = stage <= LifeStage.Tadpole;
            const float pw = 320f;
            float ph = (young ? 360f : 640f) + (world.Economy.Enabled ? 30f + 26f * MatchingContracts(world, i) : 0f);
            var r = Panel(new Rect(w - pw - 12f, 12f, pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));
            GUILayout.Label(SpeciesCatalog.FrogName(id), _headerStyle);
            var registered = world.RegisteredFor(i);
            if (registered != null) GUILayout.Label(registered.Name + " (a new species you registered)", _smallStyle);
            GUILayout.Label(genes.IsHybrid
                ? string.Format("{0} ({1} × {2}, {3:0}% {4})", sp.Name, sp.LatinName, SpeciesCatalog.FrogOf(genes.HybridSpecies).LatinName,
                    genes.HybridShare * 100f, SpeciesCatalog.FrogOf(genes.HybridSpecies).ShortName)
                : string.Format("{0} ({1})", sp.Name, sp.LatinName), _smallStyle);
            GUILayout.Label(string.Format("{0} {1}, {2} min old", a.Sex[i] == Sex.Female ? "Female" : "Male",
                SpeciesCatalog.StageName(stage), Mathf.FloorToInt(a.Age[i] / 60f)), _smallStyle);
            GUILayout.Label(Activity(world, i), _labelStyle);
            if (a.Growth[i] < sp.AdultAt)
                Bar("Growing", a.Growth[i] / sp.AdultAt, false, Mathf.RoundToInt(100f * a.Growth[i] / sp.AdultAt) + "% grown");
            GUILayout.Space(4f);

            if (young)
            {
                GUILayout.Label(stage == LifeStage.Egg
                    ? "Eggs need to stay in water. They hatch faster in warm water."
                    : "Tadpoles live in the water and graze. They leave it as froglets.", _smallStyle);
            }
            else
            {
                Bar("Hunger", a.Hunger[i], true, null);
                Bar("Thirst", a.Thirst[i], true, null);
                Bar("Tiredness", a.Fatigue[i], true, null);
                Bar("Comfort", a.Comfort[i], false, null);
                Bar("Stress", a.Stress[i], true, null);
                if (sp.Sociability > 0f) Bar("Lonely", a.Social[i], true, null);
                if (a.HasHome[i]) GUILayout.Label("Holds a territory and chases off rival males.", _smallStyle);
                else if (a.Retreat[i] > 0f) GUILayout.Label("Just lost a squabble over territory.", _smallStyle);
                if (a.Stress[i] >= NeedsSystem.WarnStress)
                    GUILayout.Label("Stressed: " + AlertSystem.ProblemWords(world, i) + ". If this goes on it will try to escape.", _smallStyle);
                float t = world.Environment.TemperatureAt(a.PosX[i], a.PosZ[i]);
                float h = world.Environment.HumidityAt(a.PosX[i], a.PosZ[i]);
                GUILayout.Label(string.Format("Here: {0:0.0} °C, {1:0}% humidity", t, h), _smallStyle);
                GUILayout.Label(string.Format("Likes {0:0}–{1:0} °C and {2:0}–{3:0}% humidity",
                    sp.MinTemperature + genes.HeatShift, sp.MaxTemperature + genes.HeatShift,
                    sp.MinHumidity + genes.DampShift, sp.MaxHumidity + genes.DampShift), _smallStyle);
            }

            GUILayout.Label("Genes: " + genes.Describe(), _smallStyle);
            ushort muts = genes.Visible;
            for (int m = 0; m < Mutations.Count; m++)
                if ((muts & (1 << m)) != 0)
                    GUILayout.Label("Mutation: " + Mutations.All[m].Name + " (" + Mutations.All[m].Effect + ")", _smallStyle);
            string hidden = Mutations.Hidden(genes.Alleles);
            if (hidden.Length > 0) GUILayout.Label("Carries a hidden copy of: " + hidden + " (could show up in its young)", _smallStyle);
            if (Lineage.IsNewType(Lineage.TypeOf(a, i)) && genes.Generation > 0)
                GUILayout.Label(string.Format("Its family has bred true for {0} generation{1}.", genes.Generation, genes.Generation == 1 ? "" : "s"), _smallStyle);
            GUILayout.Label(FamilyLine(world, i), _smallStyle);
            if (a.MateId[i] != 0)
                GUILayout.Label("Carrying eggs (father: " + SpeciesCatalog.FrogName(a.MateId[i])
                    + (a.MateSpecies[i] != a.Species[i] ? ", a " + SpeciesCatalog.FrogOf(a.MateSpecies[i]).Name.ToLowerInvariant() + ": they will be hybrids" : "") + ")", _smallStyle);

            var brain = runner.Brain;
            if (brain != null && !young)
            {
                // What the utility brain is weighing up right now (higher wins).
                var ctx = brain.ContextFor(world, i);
                var sb = new System.Text.StringBuilder("Thinking: ");
                foreach (var g in brain.FrogPicker.Goals)
                {
                    float score = brain.FrogPicker.Score(ctx, g);
                    if (score > 0f) sb.Append(GoalWord(g)).Append(' ').Append(score.ToString("0.00")).Append("   ");
                }
                GUILayout.Label(sb.ToString(), _smallStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            int mother = a.MotherId[i] != 0 ? a.IndexOf(a.MotherId[i]) : -1;
            int father = a.FatherId[i] != 0 ? a.IndexOf(a.FatherId[i]) : -1;
            GUI.enabled = mother >= 0;
            if (GUILayout.Button("Mother", GUILayout.Width(64f))) tools.SelectedAgentId = a.MotherId[i];
            GUI.enabled = father >= 0;
            if (GUILayout.Button("Father", GUILayout.Width(64f))) tools.SelectedAgentId = a.FatherId[i];
            GUI.enabled = true;
            GUILayout.EndHorizontal();
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
            if (world.Economy.Enabled) DrawFrogSales(world, i, id);
            GUILayout.EndArea();
        }

        /// <summary>"Mother Basil, father Pip. 4 young: 2 eggs, 2 juveniles", in plain words.</summary>
        private static string FamilyLine(World world, int i)
        {
            var a = world.Agents;
            string parents = a.MotherId[i] == 0 && a.FatherId[i] == 0
                ? "Parents unknown (came from outside the tank)."
                : "Mother " + SpeciesCatalog.FrogName(a.MotherId[i]) + (a.IndexOf(a.MotherId[i]) < 0 ? " (gone)" : "")
                  + ", father " + SpeciesCatalog.FrogName(a.FatherId[i]) + (a.IndexOf(a.FatherId[i]) < 0 ? " (gone)" : "") + ".";
            int id = a.Id[i], eggs = 0, tadpoles = 0, older = 0;
            for (int k = 0; k < a.Count; k++)
            {
                if (a.Kind[k] != AgentKind.Frog || (a.MotherId[k] != id && a.FatherId[k] != id)) continue;
                if (a.Stage[k] == LifeStage.Egg) eggs++;
                else if (a.Stage[k] == LifeStage.Tadpole) tadpoles++;
                else older++;
            }
            int total = eggs + tadpoles + older;
            if (total == 0) return parents;
            var parts = new List<string>();
            if (eggs > 0) parts.Add(eggs + (eggs == 1 ? " egg" : " eggs"));
            if (tadpoles > 0) parts.Add(tadpoles + (tadpoles == 1 ? " tadpole" : " tadpoles"));
            if (older > 0) parts.Add(older + (older == 1 ? " froglet or older" : " froglets or older"));
            return parents + " Young in the tank: " + string.Join(", ", parts) + ".";
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
                case Goal.LayEggs: return "lay eggs";
                case Goal.Disperse: return "find space";
                case Goal.Escape: return "escape";
                case Goal.Socialise: return "find company";
                case Goal.Defend: return "defend";
                case Goal.Retreat: return "back off";
                case Goal.Flee: return "keep away";
                default: return g.ToString().ToLowerInvariant();
            }
        }

        /// <summary>A plain-words description of what the frog is doing.</summary>
        public static string Activity(World world, int i)
        {
            var a = world.Agents;
            if (a.Kind[i] == AgentKind.Frog && a.Stage[i] == LifeStage.Egg) return "Developing in the water";
            if (a.Kind[i] == AgentKind.Frog && a.Stage[i] == LifeStage.Tadpole) return "Swimming about and grazing";
            switch (a.Goal[i])
            {
                case Goal.Eat: return a.TargetId[i] > 0 ? "Stalking a cricket" : "Looking for insects to eat";
                case Goal.Drink: return world.Terrain.IsWater(a.PosX[i], a.PosZ[i]) ? "Soaking in the water (frogs drink through their skin)" : "Heading for water";
                case Goal.Sleep: return "Sleeping";
                case Goal.SeekComfort: return "Looking for a more comfortable spot";
                case Goal.Wander: return "Exploring";
                case Goal.LayEggs: return world.Terrain.IsWater(a.PosX[i], a.PosZ[i]) ? "Laying eggs" : "Heading for water to lay eggs";
                case Goal.Disperse: return "Moving away from the crowd to find space of its own";
                case Goal.Escape: return "Trying to climb out of the tank!";
                case Goal.Socialise: return "Lonely: going to find others of its kind";
                case Goal.Defend: return "Chasing a rival out of its territory";
                case Goal.Retreat: return "Backing off after losing a squabble";
                case Goal.Flee: return "Keeping clear of a big frog";
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

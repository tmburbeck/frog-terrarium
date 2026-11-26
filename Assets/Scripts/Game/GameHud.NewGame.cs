using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The New game window: pick a biome, type a seed (any word or number), set the tank size and
    /// the hills, water and plants sliders, and choose money mode or the sandbox. A small map
    /// preview updates as you change things. Every map has a short map code that can be copied
    /// and pasted to make exactly the same map on another computer.
    ///
    /// Also the Sandbox rules list in the Money menu, which switches parts of the simulation off.
    /// </summary>
    public sealed partial class GameHud
    {
        /// <summary>Whether the New game window is showing.</summary>
        public bool ShowNewGame;

        private MapSettings _newMap = MapSettings.Classic(12345);
        private string _seedText = "12345";
        private string _codeText = "";
        private string _newGameMessage = "";
        private bool _newMoney = true;
        private int _newFrogs = 8;
        private SandboxRules _newRules;
        private Texture2D _preview;
        private string _previewCode = "";

        public void OpenNewGame(bool open)
        {
            ShowNewGame = open;
            if (!open) return;
            var world = runner.Sim.World;
            _newMap = world.Map;
            _seedText = _newMap.Seed.ToString();
            _codeText = _newMap.ToCode();
            _newMoney = world.Economy.Enabled;
            _newRules = world.Rules;
            _newGameMessage = "";
        }

        private void DrawNewGame(float w, float h)
        {
            const float pw = 760f, ph = 600f;
            var r = Panel(new Rect((w - pw) * 0.5f, Mathf.Max(54f, (h - ph) * 0.5f - 40f), pw, ph));
            GUILayout.BeginArea(new Rect(r.x + 12f, r.y + 10f, r.width - 24f, r.height - 20f));
            GUILayout.Label("New game", _headerStyle);
            if (DrawNewGameTabs())
            {
                DrawScenarioPicker();
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Starting a scenario replaces this tank. Save first if you want to keep it.", _smallStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(80f))) ShowNewGame = false;
                if (GUILayout.Button("Start", GUILayout.Width(100f))) StartPickedScenario();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.BeginHorizontal();

            // Left: the choices.
            GUILayout.BeginVertical(GUILayout.Width(440f));
            GUILayout.Label("Biome", _labelStyle);
            int perRow = 3;
            for (int k = 0; k < Biomes.All.Length; k += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = k; j < k + perRow && j < Biomes.All.Length; j++)
                {
                    var b = Biomes.All[j];
                    if (GUILayout.Toggle(_newMap.Biome == b.Id, b.Name, GUI.skin.button, GUILayout.Width(140f)) && _newMap.Biome != b.Id)
                        _newMap.Biome = b.Id;
                }
                GUILayout.EndHorizontal();
            }
            var info = _newMap.Info;
            GUILayout.Label(info.Description, _smallStyle);
            GUILayout.Label(string.Format("Room {0:0} °C and {1:0}% humidity. Starts with {2}.", info.RoomTemperature, info.RoomHumidity,
                StartingFrogsText(info)), _smallStyle);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed", _labelStyle, GUILayout.Width(60f));
            string seed = GUILayout.TextField(_seedText, 40, GUILayout.Width(220f));
            if (seed != _seedText)
            {
                _seedText = seed;
                _newMap.Seed = MapSettings.SeedFromText(seed);
            }
            if (GUILayout.Button("Random", GUILayout.Width(80f)))
            {
                _newMap.Seed = (ulong)Random.Range(1, int.MaxValue);
                _seedText = _newMap.Seed.ToString();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Any word or number. The same seed and settings always make the same map.", _smallStyle);
            GUILayout.Space(4f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tank size", _labelStyle, GUILayout.Width(80f));
            for (int k = 0; k < TankLooks.Sizes.Length; k++)
            {
                var size = TankLooks.Sizes[k];
                string label = size.Name + " " + (size.HalfX * 2f).ToString("0") + "×" + (size.HalfZ * 2f).ToString("0") + " m";
                if (GUILayout.Toggle(_newMap.Size == k, label, GUI.skin.button, GUILayout.Width(118f))) _newMap.Size = (byte)k;
            }
            GUILayout.EndHorizontal();
            _newMap.Hills = Slider("Hills", _newMap.Hills);
            _newMap.Water = Slider("Water", _newMap.Water);
            _newMap.Plants = Slider("Plants", _newMap.Plants);
            _newMap = _newMap.Clamped();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Frogs", _labelStyle, GUILayout.Width(80f));
            _newFrogs = Mathf.RoundToInt(GUILayout.HorizontalSlider(_newFrogs, 2f, 16f, GUILayout.Width(250f)));
            GUILayout.Label(_newFrogs + " (" + _newFrogs / 2 + " pairs)", _smallStyle, GUILayout.Width(90f));
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_newMoney, "Money mode", GUI.skin.button, GUILayout.Width(140f))) _newMoney = true;
            if (GUILayout.Toggle(!_newMoney, "Sandbox", GUI.skin.button, GUILayout.Width(140f))) _newMoney = false;
            GUILayout.EndHorizontal();
            GUILayout.Label(_newMoney
                ? "Start with " + Economy.Format(Economy.StartingMoney) + ", earn from visitors and buyers, and research new things."
                : "Everything is free and unlocked. You can switch parts of the simulation off below or later in the Money menu.", _smallStyle);
            if (!_newMoney) _newRules = DrawRuleSwitches(_newRules);
            GUILayout.EndVertical();

            // Right: the preview and the map code.
            GUILayout.BeginVertical(GUILayout.Width(280f));
            GUILayout.Label("Preview", _labelStyle);
            UpdatePreview();
            var pr = GUILayoutUtility.GetRect(280f, 280f * 0.62f, GUILayout.Width(280f));
            if (_preview != null) GUI.DrawTexture(pr, _preview, ScaleMode.ScaleToFit);
            GUILayout.Label("Front of the tank at the bottom. Blue is water.", _smallStyle);
            GUILayout.Space(8f);
            GUILayout.Label("Map code", _labelStyle);
            GUILayout.Label("Share this code and anyone can make the same map. Paste one here to use it.", _smallStyle);
            _codeText = GUILayout.TextField(_codeText, 80, GUILayout.Width(276f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use code", GUILayout.Width(88f)))
            {
                if (MapSettings.TryParse(_codeText, out var parsed, out string why))
                {
                    _newMap = parsed;
                    _seedText = parsed.Seed.ToString();
                    _newGameMessage = "Map code read: " + parsed.Info.Name + ".";
                }
                else _newGameMessage = why;
            }
            if (GUILayout.Button("Copy", GUILayout.Width(60f)))
            {
                _codeText = _newMap.ToCode();
                GUIUtility.systemCopyBuffer = _codeText;
                _newGameMessage = "Map code copied.";
            }
            if (GUILayout.Button("This tank's", GUILayout.Width(110f)))
            {
                _codeText = runner.Sim.World.Map.ToCode();
                GUIUtility.systemCopyBuffer = _codeText;
                _newGameMessage = "The map code of the tank you are playing, copied.";
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_newGameMessage)) GUILayout.Label(_newGameMessage, _smallStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Starting a new game replaces this tank. Save first if you want to keep it.", _smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(80f))) ShowNewGame = false;
            if (GUILayout.Button("Start", GUILayout.Width(100f)))
            {
                runner.NewWorld(_newMap, _newMoney, _newFrogs, _newMoney ? SandboxRules.None : _newRules);
                tools.LastAction = runner.LastMessage + " (map code " + _newMap.ToCode() + ")";
                ShowNewGame = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private float Slider(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(80f));
            value = GUILayout.HorizontalSlider(value, MapSettings.MinSlider, MapSettings.MaxSlider, GUILayout.Width(250f));
            GUILayout.Label(value < 0.5f ? "a little" : value < 1.5f ? (Mathf.Abs(value - 1f) < 0.03f ? "normal" : "about normal") : "lots",
                _smallStyle, GUILayout.Width(90f));
            GUILayout.EndHorizontal();
            return value;
        }

        private static string StartingFrogsText(BiomeInfo info)
        {
            var names = new System.Text.StringBuilder();
            for (int k = 0; k < info.StartingSpecies.Length; k++)
            {
                if (k > 0) names.Append(k == info.StartingSpecies.Length - 1 ? " and " : ", ");
                names.Append(SpeciesCatalog.FrogOf(info.StartingSpecies[k]).Name.ToLowerInvariant()).Append('s');
            }
            return names.ToString();
        }

        /// <summary>
        /// Draws a top-down picture of the map the current settings make, coloured like the biome's
        /// style set. Only remade when the settings change.
        /// </summary>
        private void UpdatePreview()
        {
            string code = _newMap.ToCode();
            if (_preview != null && code == _previewCode) return;
            _previewCode = code;
            // Only the ground is made (no devices, animals or warming up), so dragging a slider stays smooth.
            var size = _newMap.TankSize;
            var world = new World(_newMap.Seed, size.HalfX, size.HalfZ);
            MapGenerator.Generate(world, _newMap);
            var t = world.Terrain;
            if (_preview == null || _preview.width != t.PointsX || _preview.height != t.PointsZ)
            {
                if (_preview != null) Destroy(_preview);
                _preview = new Texture2D(t.PointsX, t.PointsZ, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            }
            var style = TankLooks.Styles[_newMap.Info.Style];
            var pixels = new Color32[t.PointsX * t.PointsZ];
            for (int pz = 0; pz < t.PointsZ; pz++)
                for (int px = 0; px < t.PointsX; px++)
                {
                    int i = t.Index(px, pz);
                    float height = t.Height[i];
                    Color c;
                    if (height < t.WaterLevel)
                    {
                        float depth = Mathf.Clamp01((t.WaterLevel - height) / 0.5f);
                        c = Color.Lerp(new Color(0.35f, 0.6f, 0.75f), new Color(0.1f, 0.25f, 0.45f), depth);
                    }
                    else
                    {
                        int l = (int)t.Layer[i] * 3;
                        c = new Color32(style.Ground[l], style.Ground[l + 1], style.Ground[l + 2], 255);
                        // Higher ground is drawn lighter, so hills show.
                        c = Color.Lerp(c * 0.7f, Color.Lerp(c, Color.white, 0.25f), Mathf.Clamp01((height - 0.6f) / 2f));
                        c.a = 1f;
                    }
                    pixels[i] = c;
                }
            _preview.SetPixels32(pixels);
            _preview.Apply();
        }

        /// <summary>A switch for each part of the simulation the sandbox can turn off. Returns the new switches.</summary>
        private SandboxRules DrawRuleSwitches(SandboxRules rules)
        {
            foreach (var info in Sandbox.All)
            {
                bool on = (rules & info.Rule) == 0;
                bool now = GUILayout.Toggle(on, " " + info.System + (on ? "" : ": off. " + info.WhenOff));
                if (now != on) rules = now ? rules & ~info.Rule : rules | info.Rule;
            }
            return rules;
        }

        /// <summary>The Sandbox rules list in the Money menu.</summary>
        private void DrawSandboxRules(World world)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Sandbox rules", _labelStyle);
            if (world.InScenario)
            {
                GUILayout.Label("The scenario sets the rules until it is won or lost.", _smallStyle);
                GUI.enabled = false;
                DrawRuleSwitches(world.Rules);
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label("Untick a system to switch it off. Money mode always runs everything.", _smallStyle);
                world.Rules = DrawRuleSwitches(world.Rules);
            }
            GUILayout.Space(6f);
            GUILayout.Label("This tank's map code: " + world.Map.ToCode(), _smallStyle);
        }
    }
}

using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    public enum Tool
    {
        Select,
        Raise,
        Lower,
        Smooth,
        Paint,
        PlaceHeater,
        PlaceMister,
        PlaceFeeder,
        AddFrog,
        AddCrickets,
        Remove,
    }

    /// <summary>
    /// Turns mouse and keyboard input into player commands: sculpting, painting, placing and
    /// selecting devices, undo and redo. This is the one place where Unity code changes the
    /// simulation, and it only does so between simulation ticks, on the main thread.
    ///
    /// Left mouse uses the current tool. Right and middle mouse stay with the camera.
    /// Ctrl+Z undo, Ctrl+Y redo, [ and ] change brush size, Esc goes back to Select.
    /// </summary>
    public sealed class ToolController : MonoBehaviour
    {
        public SimulationRunner runner;
        public GameHud hud;
        public DeviceView deviceView;

        public Tool CurrentTool = Tool.Select;
        public GroundLayer PaintLayer = GroundLayer.Moss;
        public float BrushRadius = 1.2f;
        /// <summary>0..1. Raise and lower move at up to 1.5 m per second at full strength.</summary>
        public float BrushStrength = 0.5f;

        public const float MinBrushRadius = 0.3f, MaxBrushRadius = 4f;
        public const float SelectDistance = 0.7f;

        /// <summary>Which species the "+ Frog" tool adds (index into SpeciesCatalog.Frogs).</summary>
        public byte FrogSpecies;
        /// <summary>The "+ Frog" tool alternates female and male, so two clicks make a pair.</summary>
        private Sex _nextSex = Sex.Female;

        public int SelectedDeviceId { get; set; }
        /// <summary>Id of the frog shown in the inspector (0 = none).</summary>
        public int SelectedAgentId { get; set; }
        public bool HasCursor { get; private set; }
        public Vector3 CursorPoint { get; private set; }
        public TerrainHistory History { get; private set; }
        public string LastAction { get; private set; } = "";

        private World _world;
        private bool _stroking;

        public bool IsBrush(Tool t) => t == Tool.Raise || t == Tool.Lower || t == Tool.Smooth || t == Tool.Paint;

        public void SetTool(Tool tool)
        {
            EndStroke();
            CurrentTool = tool;
        }

        private void Update()
        {
            if (runner == null || runner.Sim == null) return;
            var world = runner.Sim.World;
            if (world != _world)
            {
                _world = world;
                History = new TerrainHistory(world.Terrain);
                _stroking = false;
                SelectedDeviceId = 0;
                SelectedAgentId = 0;
            }
            if (world.Devices.Find(SelectedDeviceId) == null) SelectedDeviceId = 0;
            if (SelectedAgentId != 0 && world.Agents.IndexOf(SelectedAgentId) < 0) SelectedAgentId = 0; // eaten, or rehomed

#if ENABLE_LEGACY_INPUT_MANAGER
            HandleKeys();
            UpdateCursor(world);

            bool overUi = hud != null && hud.IsPointerOverUi(Input.mousePosition);
            if (Input.GetMouseButtonDown(0) && !overUi && HasCursor) Press(world);
            if (_stroking)
            {
                if (Input.GetMouseButton(0)) Stroke(world, Time.unscaledDeltaTime);
                else EndStroke();
            }
#endif
        }

        private void LateUpdate()
        {
            if (deviceView == null || _world == null) return;
            if (HasCursor && IsBrush(CurrentTool))
                deviceView.DrawRing(_world.Terrain, CursorPoint.x, CursorPoint.z, BrushRadius);
            int sel = SelectedAgentId != 0 ? _world.Agents.IndexOf(SelectedAgentId) : -1;
            if (sel >= 0) deviceView.DrawRing(_world.Terrain, _world.Agents.PosX[sel], _world.Agents.PosZ[sel], 0.3f);
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleKeys()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                        || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (ctrl && Input.GetKeyDown(KeyCode.Z)) { if (shift) Redo(); else Undo(); }
            if (ctrl && Input.GetKeyDown(KeyCode.Y)) Redo();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) BrushRadius = Mathf.Max(MinBrushRadius, BrushRadius / 1.2f);
            if (Input.GetKeyDown(KeyCode.RightBracket)) BrushRadius = Mathf.Min(MaxBrushRadius, BrushRadius * 1.2f);
            if (Input.GetKeyDown(KeyCode.Escape)) { SetTool(Tool.Select); SelectedDeviceId = 0; SelectedAgentId = 0; }
            if (Input.GetKeyDown(KeyCode.Delete) && SelectedDeviceId != 0) RemoveDevice(SelectedDeviceId);
        }

        private void UpdateCursor(World world)
        {
            HasCursor = false;
            var cam = Camera.main;
            if (cam == null) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (world.Terrain.Raycast(ray.origin.x, ray.origin.y, ray.origin.z, ray.direction.x, ray.direction.y, ray.direction.z,
                                      200f, out float x, out float y, out float z))
            {
                HasCursor = true;
                CursorPoint = new Vector3(x, y, z);
            }
        }
#endif

        private void Press(World world)
        {
            float x = CursorPoint.x, z = CursorPoint.z;
            switch (CurrentTool)
            {
                case Tool.Select:
                {
                    // Frogs first: they are what you usually want to click.
                    int frog = NearestFrog(world, x, z, 0.6f);
                    var d = frog < 0 ? world.Devices.Nearest(x, z, SelectDistance) : null;
                    SelectedAgentId = frog >= 0 ? world.Agents.Id[frog] : 0;
                    SelectedDeviceId = d != null ? d.Id : 0;
                    break;
                }
                case Tool.AddFrog:
                {
                    if (world.Terrain.IsWater(x, z) || world.Agents.Count >= World.MaxAgents) break;
                    int i = world.AddFrog(x, z, FrogSpecies, _nextSex);
                    SelectedAgentId = world.Agents.Id[i];
                    SelectedDeviceId = 0;
                    LastAction = "Added " + SpeciesCatalog.FrogName(SelectedAgentId) + ", a " + (_nextSex == Sex.Female ? "female " : "male ")
                               + SpeciesCatalog.FrogOf(FrogSpecies).Name.ToLowerInvariant();
                    _nextSex = _nextSex == Sex.Female ? Sex.Male : Sex.Female;
                    break;
                }
                case Tool.AddCrickets:
                {
                    var rng = world.Random.Get("player");
                    int added = 0;
                    for (int k = 0; k < 10 && world.Agents.Count < World.MaxAgents; k++)
                    {
                        world.FindDryGround(rng, x - 0.6f, x + 0.6f, z - 0.6f, z + 0.6f, out float cx, out float cz);
                        int i = world.AddInsect(cx, cz, rng);
                        world.Agents.Age[i] = SpeciesCatalog.Insect.AdultAge;
                        added++;
                    }
                    LastAction = "Released " + added + " crickets";
                    break;
                }
                case Tool.PlaceHeater:
                case Tool.PlaceMister:
                case Tool.PlaceFeeder:
                {
                    var kind = CurrentTool == Tool.PlaceHeater ? DeviceKind.Heater
                             : CurrentTool == Tool.PlaceMister ? DeviceKind.Mister : DeviceKind.Feeder;
                    var d = world.Devices.Add(kind, x, z);
                    SelectedDeviceId = d.Id;
                    SelectedAgentId = 0;
                    LastAction = "Placed " + d.DisplayName;
                    SetTool(Tool.Select);
                    break;
                }
                case Tool.Remove:
                {
                    var d = world.Devices.Nearest(x, z, SelectDistance);
                    if (d != null) RemoveDevice(d.Id);
                    break;
                }
                default:
                    if (IsBrush(CurrentTool))
                    {
                        History.BeginStroke();
                        _stroking = true;
                        Stroke(world, Time.unscaledDeltaTime);
                    }
                    break;
            }
        }

        private void Stroke(World world, float dt)
        {
            if (!HasCursor) return;
            BrushMode mode;
            float strength;
            switch (CurrentTool)
            {
                case Tool.Raise: mode = BrushMode.Raise; strength = 1.5f * BrushStrength; break;
                case Tool.Lower: mode = BrushMode.Lower; strength = 1.5f * BrushStrength; break;
                case Tool.Smooth: mode = BrushMode.Smooth; strength = 8f * BrushStrength; break;
                case Tool.Paint: mode = BrushMode.Paint; strength = 1f; break;
                default: return;
            }
            // Cap the step so a stalled frame cannot dig a crater in one go.
            TerrainBrush.Apply(world.Terrain, mode, CursorPoint.x, CursorPoint.z, BrushRadius, strength, Mathf.Min(dt, 0.05f), PaintLayer);
        }

        private void EndStroke()
        {
            if (!_stroking) return;
            _stroking = false;
            History?.EndStroke();
        }

        public void Undo()
        {
            EndStroke();
            if (History != null && History.Undo()) LastAction = "Undo";
        }

        public void Redo()
        {
            EndStroke();
            if (History != null && History.Redo()) LastAction = "Redo";
        }

        private static int NearestFrog(World world, float x, float z, float maxDistance)
        {
            var a = world.Agents;
            int best = -1;
            float bestD2 = maxDistance * maxDistance;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) continue;
                float dx = a.PosX[i] - x, dz = a.PosZ[i] - z, d2 = dx * dx + dz * dz;
                if (!a.IsLandFrog(i)) d2 *= 2f; // grown frogs win over eggs and tadpoles under the same click
                if (d2 < bestD2) { bestD2 = d2; best = i; }
            }
            return best;
        }

        /// <summary>Takes a frog out of the tank (the inspector's Rehome button).</summary>
        public void RehomeFrog(int id)
        {
            if (_world == null) return;
            int i = _world.Agents.IndexOf(id);
            if (i < 0) return;
            LastAction = "Rehomed " + SpeciesCatalog.FrogName(id);
            _world.Agents.RemoveAt(i);
            if (SelectedAgentId == id) SelectedAgentId = 0;
        }

        /// <summary>Switches the "+ Frog" tool to the next species.</summary>
        public void NextFrogSpecies()
        {
            FrogSpecies = (byte)((FrogSpecies + 1) % SpeciesCatalog.Frogs.Length);
            SetTool(Tool.AddFrog);
        }

        /// <summary>Puts every escaped frog back in the tank (the escape alert's Bring back button).</summary>
        public void BringBackEscapedFrogs()
        {
            if (_world == null) return;
            int n = _world.BringBackEscapedFrogs();
            LastAction = n == 1 ? "Brought 1 frog back" : "Brought " + n + " frogs back";
        }

        public void RemoveDevice(int id)
        {
            var d = _world?.Devices.Find(id);
            if (d == null) return;
            LastAction = "Removed " + d.DisplayName;
            _world.Devices.Remove(id);
            if (SelectedDeviceId == id) SelectedDeviceId = 0;
        }
    }
}

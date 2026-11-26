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

        public int SelectedDeviceId { get; set; }
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
            }
            if (world.Devices.Find(SelectedDeviceId) == null) SelectedDeviceId = 0;

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
            if (deviceView != null && HasCursor && IsBrush(CurrentTool) && _world != null)
                deviceView.DrawRing(_world.Terrain, CursorPoint.x, CursorPoint.z, BrushRadius);
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
            if (Input.GetKeyDown(KeyCode.Escape)) { SetTool(Tool.Select); SelectedDeviceId = 0; }
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
                    var d = world.Devices.Nearest(x, z, SelectDistance);
                    SelectedDeviceId = d != null ? d.Id : 0;
                    break;
                }
                case Tool.PlaceHeater:
                case Tool.PlaceMister:
                {
                    var kind = CurrentTool == Tool.PlaceHeater ? DeviceKind.Heater : DeviceKind.Mister;
                    var d = world.Devices.Add(kind, x, z);
                    SelectedDeviceId = d.Id;
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

using System;
using System.Collections.Generic;
using System.IO;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Bridges Unity and the simulation. Each frame it advances the fixed-tick
    /// simulation, then draws every agent with GPU instancing. It only reads
    /// simulation data for drawing; it never changes it.
    ///
    /// Keys (changeable in the Settings window): F5 quick-save, F9 quick-load, P pause, 1/2/3 speed.
    /// </summary>
    public sealed class SimulationRunner : MonoBehaviour
    {
        [Tooltip("World seed. The same seed always gives the same starting tank.")]
        public ulong seed = 12345;
        public int startingFrogs = 8;
        public int startingInsects = 80;

        public SimulationLoop Sim { get; private set; }
        /// <summary>How many times the tank has been saved since the game started (the tutorial watches it).</summary>
        public int Saves { get; private set; }
        public string QuickSavePath => Path.Combine(Application.persistentDataPath, "saves", "quicksave.ftsv");
        public string LastMessage { get; private set; } = "";

        private Mesh _insectMesh;
        private Material _frogMaterial, _insectMaterial;
        private readonly Matrix4x4[] _batch = new Matrix4x4[1023];

        private void Awake()
        {
            CreateVisuals();
            NewWorld(seed);
        }

        public void NewWorld(ulong worldSeed)
        {
            var world = World.CreateStarterTank(worldSeed, startingFrogs, startingInsects);
            world.Economy.StartMoneyMode(); // new games keep track of money; the Money menu can switch to the sandbox
            Sim = SimulationFactory.CreateDefault(world);
        }

        /// <summary>
        /// Starts a new game on a generated map (see the New game window). Money mode starts with the
        /// usual money; the sandbox has everything free and the chosen sandbox switches.
        /// </summary>
        public void NewWorld(MapSettings map, bool moneyMode, int frogs, SandboxRules rules)
        {
            var world = World.CreateTank(map, frogs, startingInsects);
            if (moneyMode) world.Economy.StartMoneyMode();
            world.Rules = rules;
            float scale = Sim != null ? Sim.TimeScale : 1f;
            Sim = SimulationFactory.CreateDefault(world);
            Sim.TimeScale = scale == 0f ? 1f : scale;
            LastMessage = "New " + map.Info.Name.ToLowerInvariant() + " tank";
        }

        /// <summary>Starts a new game on a shared map file: exactly its ground, devices and parts.</summary>
        public void NewWorld(MapFile map, bool moneyMode)
        {
            var world = map.CreateWorld(startingFrogs, startingInsects);
            if (moneyMode) world.Economy.StartMoneyMode();
            float scale = Sim != null ? Sim.TimeScale : 1f;
            Sim = SimulationFactory.CreateDefault(world);
            Sim.TimeScale = scale == 0f ? 1f : scale;
            LastMessage = "New game on the map " + map.Name;
        }

        /// <summary>Starts a scenario as a new game (see the Scenarios tab of the New game window).</summary>
        public void StartScenario(ScenarioInfo info)
        {
            var world = Scenarios.Start(info);
            Sim = SimulationFactory.CreateDefault(world);
            Sim.TimeScale = 1f;
            LastMessage = "Scenario: " + info.Name;
        }

        public void QuickSave()
        {
            try
            {
                SaveSystem.WriteFile(QuickSavePath, Sim.World);
                LastMessage = "Saved to " + QuickSavePath;
                Saves++;
            }
            catch (Exception e)
            {
                LastMessage = "Save failed: " + e.Message;
                Debug.LogException(e);
            }
            Debug.Log(LastMessage);
        }

        public void QuickLoad()
        {
            try
            {
                var world = SaveSystem.ReadFile(QuickSavePath);
                float scale = Sim != null ? Sim.TimeScale : 1f;
                Sim = SimulationFactory.CreateDefault(world);
                Sim.TimeScale = scale;
                LastMessage = "Loaded " + world.Agents.Count + " agents";
            }
            catch (Exception e)
            {
                LastMessage = "Load failed: " + e.Message;
                Debug.LogWarning(LastMessage);
            }
        }

        private void Update()
        {
            HandleKeys();
            UpdateFocus();
            Sim.Advance(Time.deltaTime);
        }

        private void LateUpdate()
        {
            // Headless runs (automated tests, servers) have no GPU to draw with.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Frogs?.Draw(Sim.World, Time.time, Time.deltaTime * Sim.TimeScale);
            DrawInsects();
        }

        /// <summary>
        /// Tells the simulation where the camera is looking, so animals far out of view can think
        /// less often. Zoomed out over the whole tank, everything counts as in view.
        /// </summary>
        private void UpdateFocus()
        {
            var cam = Camera.main;
            var orbit = cam != null ? cam.GetComponent<OrbitCamera>() : null;
            var world = Sim.World;
            world.HasFocus = orbit != null;
            if (orbit == null) return;
            world.FocusX = orbit.target.x;
            world.FocusZ = orbit.target.z;
            world.FocusRadius = Mathf.Max(4f, orbit.distance * 0.7f);
        }

        private void HandleKeys()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            // The keys are the player's own (Settings window); none act while typing in a text box.
            if (GameSettings.Pressed(GameAction.QuickSave)) QuickSave();
            if (GameSettings.Pressed(GameAction.QuickLoad)) QuickLoad();
            if (GameSettings.Pressed(GameAction.Pause)) Sim.TimeScale = Sim.TimeScale == 0f ? 1f : 0f;
            if (GameSettings.Pressed(GameAction.Speed1)) Sim.TimeScale = 1f;
            if (GameSettings.Pressed(GameAction.Speed2)) Sim.TimeScale = 2f;
            if (GameSettings.Pressed(GameAction.Speed3)) Sim.TimeScale = 4f;
#endif
        }

        private void DrawInsects()
        {
            if (_insectMaterial == null || _insectMesh == null) return;
            var a = Sim.World.Agents;
            var rp = new RenderParams(_insectMaterial);
            const float size = 0.06f;
            var scale = new Vector3(size, size * 0.6f, size * 1.3f);
            int n = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Insect) continue;
                var pos = new Vector3(a.PosX[i], a.PosY[i] + scale.y * 0.5f, a.PosZ[i]);
                _batch[n++] = Matrix4x4.TRS(pos, Facing(a, i), scale);
                if (n == _batch.Length) { Graphics.RenderMeshInstanced(rp, _insectMesh, 0, _batch, n); n = 0; }
            }
            if (n > 0) Graphics.RenderMeshInstanced(rp, _insectMesh, 0, _batch, n);
        }

        /// <summary>The frogs, eggs and tadpoles on screen (shapes, poses and colours: see FrogRenderer).</summary>
        public FrogRenderer Frogs { get; private set; }

        private static Quaternion Facing(AgentStore a, int i)
        {
            var facing = new Vector3(a.VelX[i], 0f, a.VelZ[i]);
            return facing.sqrMagnitude > 1e-12f ? Quaternion.LookRotation(facing) : Quaternion.identity;
        }

        /// <summary>The brain system, used by the inspector to show what a frog is thinking.</summary>
        public BrainSystem Brain
        {
            get
            {
                foreach (var e in Sim.Systems) if (e.System is BrainSystem b) return b;
                return null;
            }
        }

        /// <summary>Frogs built from code-made shapes (FrogRenderer); insects are still dark specks.</summary>
        private void CreateVisuals()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _insectMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
            _frogMaterial = GameMaterials.Load("Frog");
            _insectMaterial = GameMaterials.Load("Insect");
            Frogs = new FrogRenderer(_frogMaterial);
        }

        public bool VisualsReady => Frogs != null && Frogs.Ready && _insectMaterial != null && _insectMesh != null;
    }
}

using System;
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
    /// Keys: F5 quick-save, F9 quick-load, P pause, 1/2/3 speed.
    /// </summary>
    public sealed class SimulationRunner : MonoBehaviour
    {
        [Tooltip("World seed. The same seed always gives the same starting tank.")]
        public ulong seed = 12345;
        public int startingFrogs = 8;
        public int startingInsects = 80;

        public SimulationLoop Sim { get; private set; }
        public string QuickSavePath => Path.Combine(Application.persistentDataPath, "saves", "quicksave.ftsv");
        public string LastMessage { get; private set; } = "";

        private Mesh _frogMesh, _insectMesh;
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
            Sim = SimulationFactory.CreateDefault(world);
        }

        public void QuickSave()
        {
            try
            {
                SaveSystem.WriteFile(QuickSavePath, Sim.World);
                LastMessage = "Saved to " + QuickSavePath;
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
            Sim.Advance(Time.deltaTime);
        }

        private void LateUpdate()
        {
            // Headless runs (automated tests, servers) have no GPU to draw with.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Draw(AgentKind.Frog, _frogMesh, _frogMaterial, 0.25f);
            Draw(AgentKind.Insect, _insectMesh, _insectMaterial, 0.06f);
        }

        private void HandleKeys()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F5)) QuickSave();
            if (Input.GetKeyDown(KeyCode.F9)) QuickLoad();
            if (Input.GetKeyDown(KeyCode.P)) Sim.TimeScale = Sim.TimeScale == 0f ? 1f : 0f;
            if (Input.GetKeyDown(KeyCode.Alpha1)) Sim.TimeScale = 1f;
            if (Input.GetKeyDown(KeyCode.Alpha2)) Sim.TimeScale = 2f;
            if (Input.GetKeyDown(KeyCode.Alpha3)) Sim.TimeScale = 4f;
#endif
        }

        private void Draw(AgentKind kind, Mesh mesh, Material material, float size)
        {
            if (material == null || mesh == null) return;
            var a = Sim.World.Agents;
            var rp = new RenderParams(material);
            int n = 0;
            float time = Time.time;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != kind) continue;
                var facing = new Vector3(a.VelX[i], 0f, a.VelZ[i]);
                float speed = facing.magnitude;
                var rot = speed > 1e-6f ? Quaternion.LookRotation(facing) : Quaternion.identity;
                var scale = new Vector3(size, size * 0.6f, size * 1.3f);
                float lift = 0f;
                if (kind == AgentKind.Frog)
                {
                    // Placeholder animation: frogs bob along in hops when moving fast, and flatten out asleep.
                    if (speed > 0.5f) lift = Mathf.Abs(Mathf.Sin(time * 9f + a.Id[i])) * 0.12f;
                    if (a.Goal[i] == Goal.Sleep) scale.y *= 0.7f;
                }
                var pos = new Vector3(a.PosX[i], a.PosY[i] + scale.y * 0.5f + lift, a.PosZ[i]);
                _batch[n++] = Matrix4x4.TRS(pos, rot, scale);
                if (n == _batch.Length)
                {
                    Graphics.RenderMeshInstanced(rp, mesh, 0, _batch, n);
                    n = 0;
                }
            }
            if (n > 0) Graphics.RenderMeshInstanced(rp, mesh, 0, _batch, n);
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

        /// <summary>Placeholder art: green blobs for frogs, dark specks for insects.</summary>
        private void CreateVisuals()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _frogMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            _insectMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            Destroy(cube);
            _frogMaterial = GameMaterials.Load("Frog");
            _insectMaterial = GameMaterials.Load("Insect");
        }

        public bool VisualsReady => _frogMaterial != null && _insectMaterial != null && _frogMesh != null && _insectMesh != null;
    }
}

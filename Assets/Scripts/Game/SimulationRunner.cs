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
        // Frogs are drawn in groups that share a colour: one group per species, shade and age, plus eggs and tadpoles.
        private const int Shades = 3;
        private const int EggGroup = 0, TadpoleGroup = 1, FirstSpeciesGroup = 2;
        private MaterialPropertyBlock[] _groupColours;
        private int[] _groupOf = new int[0];
        private int[] _groupCount;

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
            DrawFrogs();
            DrawInsects();
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

        /// <summary>
        /// Draws frogs at every life stage. Size comes from the species, how grown the frog is and its
        /// size gene; colour from the species, its colour gene (three shades) and age (elders go greyer).
        /// Placeholder look until real models arrive: eggs are pale beads, tadpoles dark commas.
        /// </summary>
        private void DrawFrogs()
        {
            if (_frogMaterial == null || _frogMesh == null) return;
            var a = Sim.World.Agents;
            if (_groupColours == null) CreateGroupColours();
            if (_groupOf.Length < a.Count) _groupOf = new int[a.Capacity];
            System.Array.Clear(_groupCount, 0, _groupCount.Length);
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) { _groupOf[i] = -1; continue; }
                int g = GroupOf(a, i);
                _groupOf[i] = g;
                _groupCount[g]++;
            }

            float time = Time.time;
            for (int g = 0; g < _groupColours.Length; g++)
            {
                if (_groupCount[g] == 0) continue;
                var rp = new RenderParams(_frogMaterial) { matProps = _groupColours[g] };
                int n = 0;
                for (int i = 0; i < a.Count; i++)
                {
                    if (_groupOf[i] != g) continue;
                    _batch[n++] = FrogMatrix(a, i, time);
                    if (n == _batch.Length) { Graphics.RenderMeshInstanced(rp, _frogMesh, 0, _batch, n); n = 0; }
                }
                if (n > 0) Graphics.RenderMeshInstanced(rp, _frogMesh, 0, _batch, n);
            }
        }

        private static int GroupOf(AgentStore a, int i)
        {
            if (a.Stage[i] == LifeStage.Egg) return EggGroup;
            if (a.Stage[i] == LifeStage.Tadpole) return TadpoleGroup;
            float hue = a.Genes[i].Hue;
            int shade = hue < -0.33f ? 0 : hue > 0.33f ? 2 : 1;
            int old = a.Stage[i] == LifeStage.Elder ? 1 : 0;
            int species = a.Species[i] < SpeciesCatalog.Frogs.Length ? a.Species[i] : 0;
            return FirstSpeciesGroup + (species * 2 + old) * Shades + shade;
        }

        private void CreateGroupColours()
        {
            int groups = FirstSpeciesGroup + SpeciesCatalog.Frogs.Length * 2 * Shades;
            _groupColours = new MaterialPropertyBlock[groups];
            _groupCount = new int[groups];
            for (int g = 0; g < groups; g++) _groupColours[g] = new MaterialPropertyBlock();
            _groupColours[EggGroup].SetColor("_Color", new Color(0.86f, 0.86f, 0.74f));
            _groupColours[TadpoleGroup].SetColor("_Color", new Color(0.18f, 0.16f, 0.11f));
            for (int sp = 0; sp < SpeciesCatalog.Frogs.Length; sp++)
            {
                var s = SpeciesCatalog.Frogs[sp];
                var baseColour = new Color(s.ColourR, s.ColourG, s.ColourB);
                for (int old = 0; old < 2; old++)
                    for (int shade = 0; shade < Shades; shade++)
                    {
                        // Shade 0 darker and duller, 2 lighter and brighter.
                        float k = 0.78f + 0.2f * shade;
                        var c = new Color(Mathf.Clamp01(baseColour.r * k), Mathf.Clamp01(baseColour.g * k), Mathf.Clamp01(baseColour.b * k));
                        if (old == 1) c = Color.Lerp(c, new Color(0.45f, 0.44f, 0.38f), 0.35f);
                        _groupColours[FirstSpeciesGroup + (sp * 2 + old) * Shades + shade].SetColor("_Color", c);
                    }
            }
        }

        private Matrix4x4 FrogMatrix(AgentStore a, int i, float time)
        {
            var sp = SpeciesCatalog.FrogOf(a.Species[i]);
            float size = sp.AdultSize * sp.SizeFraction(a.Growth[i]) * a.Genes[i].Size;
            Vector3 scale;
            float lift = 0f;
            var stage = a.Stage[i];
            if (stage == LifeStage.Egg) scale = new Vector3(size, size, size);
            else if (stage == LifeStage.Tadpole) scale = new Vector3(size * 0.7f, size * 0.5f, size * 1.6f);
            else
            {
                scale = new Vector3(size, size * 0.6f, size * 1.3f);
                float speed = Mathf.Sqrt(a.VelX[i] * a.VelX[i] + a.VelZ[i] * a.VelZ[i]);
                // Placeholder animation: frogs bob along in hops when moving fast, flatten out asleep,
                // look plumper when carrying eggs, and scrabble up the glass when trying to escape.
                if (speed > 0.5f) lift = Mathf.Abs(Mathf.Sin(time * 9f + a.Id[i])) * 0.12f;
                if (a.Goal[i] == Goal.Sleep) scale.y *= 0.7f;
                if (a.MateId[i] != 0) { scale.x *= 1.15f; scale.y *= 1.1f; }
                if (a.Goal[i] == Goal.Escape && speed < 0.01f) lift = 0.08f + Mathf.Abs(Mathf.Sin(time * 5f + a.Id[i])) * 0.1f;
            }
            var pos = new Vector3(a.PosX[i], a.PosY[i] + scale.y * 0.5f + lift, a.PosZ[i]);
            return Matrix4x4.TRS(pos, Facing(a, i), scale);
        }

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

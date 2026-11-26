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
        // Frogs and their body parts are drawn in batches that share one colour. Colours are rounded a
        // little so that frogs that look alike share a batch.
        private readonly Dictionary<int, ColourBatch> _batches = new Dictionary<int, ColourBatch>();

        private sealed class ColourBatch
        {
            public MaterialPropertyBlock Colour;
            public Mesh Mesh;
            public readonly List<Matrix4x4> Items = new List<Matrix4x4>();
        }

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
            UpdateFocus();
            Sim.Advance(Time.deltaTime);
        }

        private void LateUpdate()
        {
            // Headless runs (automated tests, servers) have no GPU to draw with.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            DrawFrogs();
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
        /// Draws frogs at every life stage, built from simple shapes until real models arrive (see the
        /// plan: one rigged model that blends the same things). Size comes from the species (blended for
        /// hybrids), how grown the frog is and its size gene. Body colour comes from the species, its
        /// colour gene and age (elders go greyer), and the colour mutations (albino, blue, black).
        /// Froglets and older get eyes and back legs, and the mutations that show get their own parts:
        /// teeth, claws, giant eyes, long or stubby legs and a big throat sac for loud callers.
        /// Eggs are pale beads, tadpoles dark commas.
        /// </summary>
        private void DrawFrogs()
        {
            if (_frogMaterial == null || _frogMesh == null) return;
            var a = Sim.World.Agents;
            foreach (var b in _batches.Values) b.Items.Clear();
            float time = Time.time;
            var eggColour = new Color(0.86f, 0.86f, 0.74f);
            var tadpoleColour = new Color(0.18f, 0.16f, 0.11f);
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) continue;
                var body = FrogMatrix(a, i, time);
                var stage = a.Stage[i];
                if (stage == LifeStage.Egg) { Add(_frogMesh, eggColour, body); continue; }
                if (stage == LifeStage.Tadpole) { Add(_frogMesh, tadpoleColour, body); continue; }
                var sp = SpeciesCatalog.FrogOf(a, i);
                ushort muts = a.Genes[i].Visible;
                Add(_frogMesh, BodyColour(sp, a.Genes[i].Hue, muts, stage == LifeStage.Elder), body);
                DrawParts(a, i, sp, muts, body);
            }
            foreach (var b in _batches.Values)
            {
                if (b.Items.Count == 0) continue;
                var rp = new RenderParams(_frogMaterial) { matProps = b.Colour };
                int n = 0;
                foreach (var m in b.Items)
                {
                    _batch[n++] = m;
                    if (n == _batch.Length) { Graphics.RenderMeshInstanced(rp, b.Mesh, 0, _batch, n); n = 0; }
                }
                if (n > 0) Graphics.RenderMeshInstanced(rp, b.Mesh, 0, _batch, n);
            }
        }

        /// <summary>
        /// The parts that make a blob look like a frog, in the body's own space (the body is a unit
        /// sphere stretched to the frog's shape): eyes on top at the front, folded back legs at the
        /// sides, and the mutation parts.
        /// </summary>
        private void DrawParts(AgentStore a, int i, FrogSpecies sp, ushort muts, Matrix4x4 body)
        {
            bool albino = Mutations.Shows(muts, Mutation.Albino);
            var eye = albino ? new Color(0.95f, 0.35f, 0.4f) : new Color(sp.EyeR, sp.EyeG, sp.EyeB);
            float eyeSize = Mutations.Shows(muts, Mutation.GiantEyes) ? 0.42f : 0.26f;
            for (int side = -1; side <= 1; side += 2)
                Add(_frogMesh, eye, body * Matrix4x4.TRS(new Vector3(0.27f * side, 0.3f, 0.28f), Quaternion.identity, new Vector3(eyeSize, eyeSize * 1.4f, eyeSize)));

            // Back legs: longer and thinner with Long legs, short and fat with Stubby legs.
            float legLength = Mutations.Shows(muts, Mutation.LongLegs) ? 0.75f : Mutations.Shows(muts, Mutation.StubbyLegs) ? 0.32f : 0.5f;
            float legWidth = Mutations.Shows(muts, Mutation.StubbyLegs) ? 0.3f : 0.22f;
            var legColour = BodyColour(sp, a.Genes[i].Hue - 0.3f, muts, a.Stage[i] == LifeStage.Elder);
            for (int side = -1; side <= 1; side += 2)
                Add(_frogMesh, legColour, body * Matrix4x4.TRS(new Vector3(0.42f * side, -0.25f, -0.25f), Quaternion.Euler(0f, 25f * side, 0f), new Vector3(legWidth, legWidth * 1.2f, legLength)));

            if (Mutations.Shows(muts, Mutation.Teeth))
            {
                // Two goofy front teeth poking out over the bottom lip.
                var white = new Color(0.97f, 0.96f, 0.9f);
                for (int side = -1; side <= 1; side += 2)
                    Add(_insectMesh, white, body * Matrix4x4.TRS(new Vector3(0.07f * side, -0.12f, 0.46f), Quaternion.identity, new Vector3(0.09f, 0.2f, 0.06f)));
            }
            if (Mutations.Shows(muts, Mutation.Claws))
            {
                // Dark hooked claws on all four feet.
                var claw = new Color(0.12f, 0.1f, 0.08f);
                for (int side = -1; side <= 1; side += 2)
                    for (int end = -1; end <= 1; end += 2)
                        Add(_insectMesh, claw, body * Matrix4x4.TRS(new Vector3(0.45f * side, -0.45f, 0.42f * end), Quaternion.Euler(30f, 0f, 0f), new Vector3(0.12f, 0.06f, 0.2f)));
            }
            if (Mutations.Shows(muts, Mutation.LoudCall) && a.Sex[i] == Sex.Male)
            {
                // A big pale throat sac that swells while he calls.
                float pulse = 0.3f + 0.08f * Mathf.Sin(Time.time * 6f + a.Id[i]);
                Add(_frogMesh, new Color(0.95f, 0.9f, 0.75f), body * Matrix4x4.TRS(new Vector3(0f, -0.3f, 0.38f), Quaternion.identity, new Vector3(pulse, pulse, pulse)));
            }
        }

        /// <summary>A frog's skin colour.</summary>
        private static Color BodyColour(FrogSpecies sp, float hue, ushort muts, bool elder)
        {
            Color c;
            if (Mutations.Shows(muts, Mutation.Albino)) c = new Color(0.96f, 0.92f, 0.86f);
            else if (Mutations.Shows(muts, Mutation.Blue)) c = new Color(0.27f, 0.56f, 0.92f);
            else c = new Color(sp.ColourR, sp.ColourG, sp.ColourB);
            if (Mutations.Shows(muts, Mutation.Melanistic) && !Mutations.Shows(muts, Mutation.Albino)) c *= 0.28f;
            // The colour gene: darker and duller at -1, lighter and brighter at +1.
            float k = 0.88f + 0.12f * Mathf.Clamp(hue, -1f, 1f);
            c = new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k));
            if (elder) c = Color.Lerp(c, new Color(0.45f, 0.44f, 0.38f), 0.35f);
            return c;
        }

        private void Add(Mesh mesh, Color c, Matrix4x4 m)
        {
            // Round the colour to 32 steps a channel so look-alike frogs share a batch.
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 31f), 0, 31);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 31f), 0, 31);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 31f), 0, 31);
            int key = (r | (g << 5) | (b << 10)) * 2 + (mesh == _frogMesh ? 0 : 1);
            if (!_batches.TryGetValue(key, out var batch))
            {
                batch = new ColourBatch { Colour = new MaterialPropertyBlock(), Mesh = mesh };
                batch.Colour.SetColor("_Color", new Color(r / 31f, g / 31f, b / 31f));
                _batches[key] = batch;
            }
            batch.Items.Add(m);
        }

        private Matrix4x4 FrogMatrix(AgentStore a, int i, float time)
        {
            var sp = SpeciesCatalog.FrogOf(a, i);
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

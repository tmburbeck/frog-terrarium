using System.Collections.Generic;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Draws the building parts with placeholder shapes made from Unity's built-in cube, sphere and
    /// cylinder, each in the colour the player picked. Also draws the "ghost" of a part or blueprint
    /// being placed: tinted green where it fits and red where it does not. Reads the simulation;
    /// never changes it.
    /// </summary>
    public sealed class PartView : MonoBehaviour
    {
        public SimulationRunner runner;
        public ToolController tools;

        private Mesh _cube, _cylinder, _sphere;
        private Material _material;
        private readonly Matrix4x4[] _batch = new Matrix4x4[1023];
        private readonly Dictionary<long, Batch> _batches = new Dictionary<long, Batch>();

        private sealed class Batch
        {
            public Mesh Mesh;
            public MaterialPropertyBlock Colour;
            public readonly List<Matrix4x4> Items = new List<Matrix4x4>();
        }

        /// <summary>How many shapes were drawn last frame (for tests).</summary>
        public int ShapesDrawn { get; private set; }

        private void Start()
        {
            _cube = DeviceView.PrimitiveMesh(PrimitiveType.Cube);
            _cylinder = DeviceView.PrimitiveMesh(PrimitiveType.Cylinder);
            _sphere = DeviceView.PrimitiveMesh(PrimitiveType.Sphere);
            _material = GameMaterials.Load("Frog");
        }

        private void LateUpdate()
        {
            if (runner == null || runner.Sim == null || _material == null || _cube == null) return;
            var world = runner.Sim.World;
            foreach (var b in _batches.Values) b.Items.Clear();
            ShapesDrawn = 0;

            int moving = tools != null && tools.CurrentTool == Tool.MovePart ? tools.SelectedPartId : 0;
            foreach (var p in world.Parts.All)
            {
                var colour = new Color32(p.R, p.G, p.B, 255);
                if (p.Id == moving) colour = Color32.Lerp(colour, new Color32(255, 255, 255, 255), 0.5f); // lifted
                AddPart(p, world.Parts.BaseHeight(p, world.Terrain), colour);
            }

            if (tools != null)
            {
                var tint = tools.GhostValid ? new Color32(90, 230, 110, 255) : new Color32(240, 70, 60, 255);
                if (tools.Ghost != null)
                {
                    var g = tools.Ghost;
                    AddPart(g, world.Parts.BaseHeight(g, world.Terrain) + 0.01f, Color32.Lerp(new Color32(g.R, g.G, g.B, 255), tint, 0.55f));
                }
                for (int k = 0; k < tools.BlueprintGhost.Count; k++)
                {
                    var g = tools.BlueprintGhost[k];
                    AddPart(g, tools.BlueprintGhostHeights[k] + 0.01f, Color32.Lerp(new Color32(g.R, g.G, g.B, 255), tint, 0.55f));
                }
            }

            // Headless runs (automated tests) have no GPU to draw with, but still count the shapes.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            foreach (var b in _batches.Values)
            {
                if (b.Items.Count == 0) continue;
                var rp = new RenderParams(_material) { matProps = b.Colour };
                int n = 0;
                foreach (var m in b.Items)
                {
                    _batch[n++] = m;
                    if (n == _batch.Length) { Graphics.RenderMeshInstanced(rp, b.Mesh, 0, _batch, n); n = 0; }
                }
                if (n > 0) Graphics.RenderMeshInstanced(rp, b.Mesh, 0, _batch, n);
            }
        }

        /// <summary>Adds the shapes that make up one part, standing with its bottom at height y.</summary>
        private void AddPart(PlacedPart p, float y, Color32 c)
        {
            float sx = p.SizeX, sy = p.SizeY, sz = p.SizeZ;
            var root = Matrix4x4.TRS(new Vector3(p.X, y, p.Z), Quaternion.Euler(0f, p.Rotation, 0f), Vector3.one);
            var dark = Color32.Lerp(c, new Color32(0, 0, 0, 255), 0.55f);
            switch (p.Kind)
            {
                case PartKind.Rock:
                    Add(_sphere, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.identity, new Vector3(sx, sy, sz));
                    Add(_sphere, dark, root, new Vector3(sx * 0.18f, sy * 0.62f, -sz * 0.1f), Quaternion.Euler(0f, 30f, 0f), new Vector3(sx * 0.55f, sy * 0.6f, sz * 0.5f));
                    break;
                case PartKind.Log:
                    Add(_cylinder, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(sx, sz * 0.5f, sy));
                    break;
                case PartKind.Branch:
                {
                    // A thin limb leaning from the ground at the back up to the top at the front.
                    float rise = sy - sx, length = Mathf.Sqrt(sz * sz + rise * rise);
                    float tilt = Mathf.Atan2(sz, rise) * Mathf.Rad2Deg;
                    Add(_cylinder, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.Euler(tilt, 0f, 0f), new Vector3(sx, length * 0.5f, sx));
                    Add(_cylinder, c, root, new Vector3(0f, sy * 0.62f, sz * 0.12f), Quaternion.Euler(tilt - 50f, 35f, 0f), new Vector3(sx * 0.6f, length * 0.18f, sx * 0.6f));
                    break;
                }
                case PartKind.Hide:
                    Add(_sphere, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.identity, new Vector3(sx, sy, sz));
                    Add(_cube, c, root, new Vector3(0f, sy * 0.25f, 0f), Quaternion.identity, new Vector3(sx * 0.95f, sy * 0.5f, sz * 0.95f));
                    Add(_cube, new Color32(15, 12, 10, 255), root, new Vector3(0f, sy * 0.22f, sz * 0.45f), Quaternion.identity, new Vector3(sx * 0.4f, sy * 0.44f, sz * 0.12f));
                    break;
                case PartKind.Plant:
                    Add(_cylinder, dark, root, new Vector3(0f, sy * 0.2f, 0f), Quaternion.identity, new Vector3(sx * 0.1f, sy * 0.2f, sz * 0.1f));
                    Add(_sphere, c, root, new Vector3(0f, sy * 0.62f, 0f), Quaternion.identity, new Vector3(sx, sy * 0.72f, sz));
                    Add(_sphere, Color32.Lerp(c, new Color32(255, 255, 255, 255), 0.15f), root, new Vector3(sx * 0.15f, sy * 0.85f, sz * 0.1f), Quaternion.identity, new Vector3(sx * 0.6f, sy * 0.35f, sz * 0.6f));
                    break;
                case PartKind.Fern:
                    for (int k = 0; k < 5; k++)
                    {
                        // Five fronds fanning out from the middle.
                        var turn = Quaternion.Euler(0f, k * 72f, 0f);
                        var at = turn * new Vector3(0f, sy * 0.45f, sz * 0.22f);
                        Add(_sphere, k % 2 == 0 ? c : Color32.Lerp(c, dark, 0.3f), root, at, turn * Quaternion.Euler(-35f, 0f, 0f), new Vector3(sx * 0.3f, sy * 0.25f, sz * 0.6f));
                    }
                    break;
                case PartKind.MossPad:
                    Add(_cylinder, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.identity, new Vector3(sx, sy * 0.5f, sz));
                    break;
                case PartKind.Pillar:
                    Add(_cylinder, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.identity, new Vector3(sx, sy * 0.5f, sz));
                    break;
                case PartKind.Ramp:
                {
                    float length = Mathf.Sqrt(sz * sz + sy * sy);
                    float tilt = Mathf.Atan2(sy, sz) * Mathf.Rad2Deg;
                    Add(_cube, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.Euler(-tilt, 0f, 0f), new Vector3(sx, 0.04f, length));
                    break;
                }
                default: // slab, wall, platform: plain boxes
                    Add(_cube, c, root, new Vector3(0f, sy * 0.5f, 0f), Quaternion.identity, new Vector3(sx, sy, sz));
                    break;
            }
        }

        private void ClearUnused()
        {
            var empty = new List<long>();
            foreach (var kv in _batches) if (kv.Value.Items.Count == 0) empty.Add(kv.Key);
            foreach (long k in empty) _batches.Remove(k);
        }

        private void Add(Mesh mesh, Color32 c, Matrix4x4 root, Vector3 at, Quaternion turn, Vector3 size)
        {
            long key = ((long)c.r << 24) | ((long)c.g << 16) | ((long)c.b << 8) | (mesh == _cube ? 0L : mesh == _sphere ? 1L : 2L);
            if (!_batches.TryGetValue(key, out var batch))
            {
                if (_batches.Count > 2000) ClearUnused(); // lots of colours tried with the sliders
                batch = new Batch { Mesh = mesh, Colour = new MaterialPropertyBlock() };
                batch.Colour.SetColor("_Color", (Color)c);
                _batches[key] = batch;
            }
            batch.Items.Add(root * Matrix4x4.TRS(at, turn, size));
            ShapesDrawn++;
        }
    }
}

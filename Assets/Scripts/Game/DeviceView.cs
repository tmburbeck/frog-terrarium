using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Draws the placed devices with placeholder shapes: a heater is a low box that glows orange
    /// while heating; a mister is a pole with a nozzle that puffs a mist cloud while spraying;
    /// a feeder is a wooden box with a lid, which turns dark when empty; a smart controller is a
    /// small grey box with a light on top that glows while it is switched on.
    /// Also draws the ring around the selected device. Reads the simulation; never changes it.
    /// </summary>
    public sealed class DeviceView : MonoBehaviour
    {
        public SimulationRunner runner;
        public ToolController tools;

        private Mesh _cube, _cylinder, _sphere;
        private Material _heater, _heaterOn, _mister, _mist, _ring, _feeder;
        private readonly Matrix4x4[] _ringBatch = new Matrix4x4[48];

        private void Start()
        {
            _cube = PrimitiveMesh(PrimitiveType.Cube);
            _cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            _sphere = PrimitiveMesh(PrimitiveType.Sphere);
            _heater = GameMaterials.Load("Heater");
            _heaterOn = GameMaterials.Load("HeaterOn");
            _mister = GameMaterials.Load("Mister");
            _mist = GameMaterials.Load("Water");
            _ring = GameMaterials.Load("Brush");
            _feeder = GameMaterials.Load("Feeder");
        }

        internal static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return mesh;
        }

        private void LateUpdate()
        {
            if (runner == null || runner.Sim == null || _cube == null) return;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var world = runner.Sim.World;
            foreach (var d in world.Devices.All)
            {
                float y = world.Terrain.HeightAt(d.X, d.Z);
                var basePos = new Vector3(d.X, y, d.Z);
                switch (d.Kind)
                {
                    case DeviceKind.Heater:
                        Draw(_cube, Matrix4x4.TRS(basePos + Vector3.up * 0.1f, Quaternion.identity, new Vector3(0.7f, 0.2f, 0.45f)),
                             d.Active ? _heaterOn : _heater);
                        break;
                    case DeviceKind.Mister:
                        Draw(_cylinder, Matrix4x4.TRS(basePos + Vector3.up * 0.6f, Quaternion.identity, new Vector3(0.06f, 0.6f, 0.06f)), _mister);
                        Draw(_sphere, Matrix4x4.TRS(basePos + Vector3.up * 1.22f, Quaternion.identity, Vector3.one * 0.16f), _mister);
                        if (d.Active)
                        {
                            float puff = 0.9f + 0.15f * Mathf.Sin(Time.time * 6f);
                            Draw(_sphere, Matrix4x4.TRS(basePos + Vector3.up * 1.0f, Quaternion.identity, new Vector3(1.6f, 0.7f, 1.6f) * puff), _mist);
                        }
                        break;
                    case DeviceKind.Feeder:
                        Draw(_cube, Matrix4x4.TRS(basePos + Vector3.up * 0.2f, Quaternion.identity, new Vector3(0.5f, 0.4f, 0.5f)),
                             d.Supply >= 1f ? _feeder : _heater);
                        Draw(_cylinder, Matrix4x4.TRS(basePos + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.35f, 0.05f, 0.35f)), _mister);
                        break;
                    case DeviceKind.Controller:
                        Draw(_cube, Matrix4x4.TRS(basePos + Vector3.up * 0.15f, Quaternion.identity, new Vector3(0.4f, 0.3f, 0.3f)), _mister);
                        Draw(_cylinder, Matrix4x4.TRS(basePos + Vector3.up * 0.45f, Quaternion.identity, new Vector3(0.03f, 0.15f, 0.03f)), _mister);
                        Draw(_sphere, Matrix4x4.TRS(basePos + Vector3.up * 0.62f, Quaternion.identity, Vector3.one * 0.1f), d.Enabled ? _heaterOn : _heater);
                        break;
                }
            }

            if (tools != null)
            {
                var selected = world.Devices.Find(tools.SelectedDeviceId);
                if (selected != null) DrawRing(world.Terrain, selected.X, selected.Z, 0.6f);
            }
        }

        private void Draw(Mesh mesh, Matrix4x4 matrix, Material material)
        {
            if (material != null) Graphics.DrawMesh(mesh, matrix, material, 0);
        }

        /// <summary>A circle of small markers sitting on the ground, used for selection and brush outlines.</summary>
        public void DrawRing(TerrainGrid terrain, float x, float z, float radius)
        {
            if (_ring == null || _cube == null) return;
            int n = _ringBatch.Length;
            float size = Mathf.Clamp(radius * 0.06f, 0.03f, 0.12f);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float px = x + Mathf.Cos(a) * radius, pz = z + Mathf.Sin(a) * radius;
                float py = MovementSystem.SurfaceHeight(terrain, px, pz) + size * 0.5f;
                _ringBatch[i] = Matrix4x4.TRS(new Vector3(px, py, pz), Quaternion.identity, Vector3.one * size);
            }
            Graphics.RenderMeshInstanced(new RenderParams(_ring), _cube, 0, _ringBatch, n);
        }
    }
}

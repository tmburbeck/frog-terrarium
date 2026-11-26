using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>What the ground colours show.</summary>
    public enum TerrainViewMode
    {
        /// <summary>Painted ground layers (soil, moss, sand, rock), darker when wet.</summary>
        Normal,
        /// <summary>Blue (cold) to red (hot).</summary>
        Temperature,
        /// <summary>Tan (dry) to blue (humid).</summary>
        Humidity,
    }

    /// <summary>
    /// Draws the simulation's terrain as one mesh per chunk, plus the soil sides of the tank and
    /// the water surface. Only chunks whose version changed are rebuilt, so sculpting stays fast.
    /// Reads the simulation; never changes it.
    /// </summary>
    public sealed class TerrainView : MonoBehaviour
    {
        public SimulationRunner runner;
        public TerrainViewMode mode = TerrainViewMode.Normal;

        /// <summary>How often the temperature and humidity colours refresh, in seconds.</summary>
        public float fieldRefreshInterval = 0.5f;

        public static readonly Color SoilColour = new Color(0.33f, 0.24f, 0.16f);
        public static readonly Color MossColour = new Color(0.25f, 0.40f, 0.14f);
        public static readonly Color SandColour = new Color(0.74f, 0.66f, 0.48f);
        public static readonly Color RockColour = new Color(0.46f, 0.45f, 0.43f);

        private World _world;
        private TerrainGrid _builtTerrain;
        private int _drawnStyle = -1;
        // The ground colours of the tank's style set (see TankLooks.Styles).
        private Color _soil = SoilColour, _moss = MossColour, _sand = SandColour, _rock = RockColour;
        private Material _terrainMaterial, _waterMaterial;
        private MeshFilter[] _chunkFilters;
        private int[] _drawnVersion;
        private TerrainViewMode _drawnMode;
        private float _fieldTimer;
        private MeshFilter _sides;
        private int _sidesVersion = -1;
        private Transform _water;

        private Vector3[] _verts;
        private Vector3[] _normals;
        private Color[] _colours;

        public int ChunkCount => _chunkFilters != null ? _chunkFilters.Length : 0;

        private void Start()
        {
            _terrainMaterial = GameMaterials.Load("Terrain");
            _waterMaterial = GameMaterials.Load("Water");
            BuildForWorld();
        }

        private void LateUpdate()
        {
            if (runner == null || runner.Sim == null) return;
            // A new tank, a loaded save or a bigger tank: start again.
            if (runner.Sim.World != _world || runner.Sim.World.Terrain != _builtTerrain) BuildForWorld();

            var t = _world.Terrain;
            bool modeChanged = mode != _drawnMode;
            if (_world.Style != _drawnStyle)
            {
                ApplyStyle();
                modeChanged = true; // redraw every chunk in the new colours
                _sidesVersion = -1;
            }
            bool refreshFields = false;
            if (mode != TerrainViewMode.Normal)
            {
                _fieldTimer -= Time.unscaledDeltaTime;
                if (_fieldTimer <= 0f) { _fieldTimer = fieldRefreshInterval; refreshFields = true; }
            }
            for (int c = 0; c < _chunkFilters.Length; c++)
            {
                if (modeChanged || refreshFields || _drawnVersion[c] != t.ChunkVersion[c])
                {
                    BuildChunk(c);
                    _drawnVersion[c] = t.ChunkVersion[c];
                }
            }
            _drawnMode = mode;
            if (_sidesVersion != t.Version) { BuildSides(); _sidesVersion = t.Version; }
            _water.position = new Vector3(0f, t.WaterLevel, 0f);
        }

        /// <summary>Throws away all meshes and makes new ones (new tank or a loaded save).</summary>
        private void BuildForWorld()
        {
            if (runner == null || runner.Sim == null) return;
            _world = runner.Sim.World;
            _builtTerrain = _world.Terrain;
            ApplyStyle();
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            var t = _world.Terrain;
            _chunkFilters = new MeshFilter[t.ChunksX * t.ChunksZ];
            _drawnVersion = new int[_chunkFilters.Length];
            int maxVerts = (TerrainGrid.ChunkCells + 1) * (TerrainGrid.ChunkCells + 1);
            _verts = new Vector3[maxVerts];
            _normals = new Vector3[maxVerts];
            _colours = new Color[maxVerts];
            for (int c = 0; c < _chunkFilters.Length; c++)
            {
                _chunkFilters[c] = MakeChild("Chunk " + c, _terrainMaterial, true);
                BuildChunk(c);
                _drawnVersion[c] = t.ChunkVersion[c];
            }
            _drawnMode = mode;

            _sides = MakeChild("Tank Sides", _terrainMaterial, false);
            BuildSides();
            _sidesVersion = t.Version;

            var waterFilter = MakeChild("Water", _waterMaterial, false);
            waterFilter.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waterFilter.sharedMesh = MakeQuad(t.HalfSizeX, t.HalfSizeZ);
            _water = waterFilter.transform;
            _water.position = new Vector3(0f, t.WaterLevel, 0f);
        }

        /// <summary>Takes the ground and backdrop colours from the tank's style set.</summary>
        private void ApplyStyle()
        {
            int style = _world.Style < TankLooks.Styles.Length ? _world.Style : 0;
            var s = TankLooks.Styles[style];
            Color C(int k) => new Color(s.Ground[k] / 255f, s.Ground[k + 1] / 255f, s.Ground[k + 2] / 255f);
            _soil = C(0); _moss = C(3); _sand = C(6); _rock = C(9);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(s.BackR / 255f, s.BackG / 255f, s.BackB / 255f);
            _drawnStyle = _world.Style;
        }

        private MeshFilter MakeChild(string name, Material material, bool receiveShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = receiveShadows;
            filter.sharedMesh = new Mesh { name = name };
            filter.sharedMesh.MarkDynamic();
            return filter;
        }

        private void BuildChunk(int c)
        {
            var t = _world.Terrain;
            int cx = c % t.ChunksX, cz = c / t.ChunksX;
            int px0 = cx * TerrainGrid.ChunkCells, pz0 = cz * TerrainGrid.ChunkCells;
            int w = Mathf.Min(TerrainGrid.ChunkCells, t.CellsX - px0) + 1;
            int h = Mathf.Min(TerrainGrid.ChunkCells, t.CellsZ - pz0) + 1;
            int n = w * h;

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    int px = px0 + x, pz = pz0 + z;
                    int v = z * w + x;
                    float height = t.Height[t.Index(px, pz)];
                    _verts[v] = new Vector3(t.PointX(px), height, t.PointZ(pz));
                    _normals[v] = NormalAt(t, px, pz);
                    _colours[v] = ColourAt(t, px, pz, height);
                }
            }

            var mesh = _chunkFilters[c].sharedMesh;
            if (mesh.vertexCount != n)
            {
                mesh.Clear();
                mesh.SetVertices(_verts, 0, n);
                mesh.SetNormals(_normals, 0, n);
                mesh.SetColors(_colours, 0, n);
                mesh.SetTriangles(GridTriangles(w, h), 0);
            }
            else
            {
                mesh.SetVertices(_verts, 0, n);
                mesh.SetNormals(_normals, 0, n);
                mesh.SetColors(_colours, 0, n);
            }
            mesh.RecalculateBounds();
        }

        private static Vector3 NormalAt(TerrainGrid t, int px, int pz)
        {
            float l = t.Height[t.Index(Mathf.Max(px - 1, 0), pz)];
            float r = t.Height[t.Index(Mathf.Min(px + 1, t.CellsX), pz)];
            float d = t.Height[t.Index(px, Mathf.Max(pz - 1, 0))];
            float u = t.Height[t.Index(px, Mathf.Min(pz + 1, t.CellsZ))];
            return new Vector3(l - r, 2f * t.CellSize, d - u).normalized;
        }

        private Color ColourAt(TerrainGrid t, int px, int pz, float height)
        {
            float x = t.PointX(px), z = t.PointZ(pz);
            switch (mode)
            {
                case TerrainViewMode.Temperature:
                    return TemperatureColour(_world.Environment.TemperatureAt(x, z));
                case TerrainViewMode.Humidity:
                    return HumidityColour(_world.Environment.HumidityAt(x, z));
            }

            Color c;
            switch (t.Layer[t.Index(px, pz)])
            {
                case GroundLayer.Moss: c = _moss; break;
                case GroundLayer.Sand: c = _sand; break;
                case GroundLayer.Rock: c = _rock; break;
                default: c = _soil; break;
            }
            // Ground near or under the water line looks wet: darker and shinier.
            float wet = Mathf.Clamp01(1f - (height - t.WaterLevel) / 0.25f);
            c = Color.Lerp(c, c * 0.55f, wet);
            c.a = wet;
            return c;
        }

        /// <summary>15 °C or colder is blue, 25 °C green, 35 °C or hotter red.</summary>
        public static Color TemperatureColour(float celsius)
        {
            float k = Mathf.InverseLerp(15f, 35f, celsius);
            Color c = k < 0.5f
                ? Color.Lerp(new Color(0.15f, 0.3f, 0.85f), new Color(0.2f, 0.75f, 0.3f), k * 2f)
                : Color.Lerp(new Color(0.2f, 0.75f, 0.3f), new Color(0.9f, 0.2f, 0.1f), k * 2f - 1f);
            c.a = 0f;
            return c;
        }

        /// <summary>20% or drier is tan, 100% deep blue.</summary>
        public static Color HumidityColour(float percent)
        {
            float k = Mathf.InverseLerp(20f, 100f, percent);
            Color c = k < 0.5f
                ? Color.Lerp(new Color(0.8f, 0.68f, 0.42f), new Color(0.3f, 0.7f, 0.65f), k * 2f)
                : Color.Lerp(new Color(0.3f, 0.7f, 0.65f), new Color(0.1f, 0.25f, 0.8f), k * 2f - 1f);
            c.a = 0f;
            return c;
        }

        /// <summary>Four walls of soil from the ground surface down to the tank bottom.</summary>
        private void BuildSides()
        {
            var t = _world.Terrain;
            int perimeter = 2 * (t.CellsX + t.CellsZ);
            var verts = new Vector3[perimeter * 4];
            var normals = new Vector3[verts.Length];
            var colours = new Color[verts.Length];
            var tris = new int[perimeter * 6];
            int q = 0;
            Color side = _soil * 0.7f;
            side.a = 0f;

            void Quad(int pxA, int pzA, int pxB, int pzB, Vector3 normal)
            {
                var a = new Vector3(t.PointX(pxA), t.Height[t.Index(pxA, pzA)], t.PointZ(pzA));
                var b = new Vector3(t.PointX(pxB), t.Height[t.Index(pxB, pzB)], t.PointZ(pzB));
                int v = q * 4;
                verts[v] = new Vector3(a.x, 0f, a.z);
                verts[v + 1] = a;
                verts[v + 2] = b;
                verts[v + 3] = new Vector3(b.x, 0f, b.z);
                for (int k = 0; k < 4; k++) { normals[v + k] = normal; colours[v + k] = side; }
                int i = q * 6;
                // Wind the triangles so each wall faces outwards.
                bool flip = Vector3.Dot(Vector3.Cross(a - verts[v], b - verts[v]), normal) < 0f;
                tris[i] = v; tris[i + 1] = flip ? v + 2 : v + 1; tris[i + 2] = flip ? v + 1 : v + 2;
                tris[i + 3] = v; tris[i + 4] = flip ? v + 3 : v + 2; tris[i + 5] = flip ? v + 2 : v + 3;
                q++;
            }

            for (int px = 0; px < t.CellsX; px++)
            {
                Quad(px, 0, px + 1, 0, Vector3.back);
                Quad(px, t.CellsZ, px + 1, t.CellsZ, Vector3.forward);
            }
            for (int pz = 0; pz < t.CellsZ; pz++)
            {
                Quad(0, pz, 0, pz + 1, Vector3.left);
                Quad(t.CellsX, pz, t.CellsX, pz + 1, Vector3.right);
            }

            var mesh = _sides.sharedMesh;
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.colors = colours;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        private static int[] GridTriangles(int w, int h)
        {
            var tris = new int[(w - 1) * (h - 1) * 6];
            int i = 0;
            for (int z = 0; z < h - 1; z++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    int v = z * w + x;
                    tris[i++] = v; tris[i++] = v + w; tris[i++] = v + 1;
                    tris[i++] = v + 1; tris[i++] = v + w; tris[i++] = v + w + 1;
                }
            }
            return tris;
        }

        private static Mesh MakeQuad(float halfX, float halfZ)
        {
            var mesh = new Mesh { name = "Water Surface" };
            mesh.vertices = new[]
            {
                new Vector3(-halfX, 0f, -halfZ), new Vector3(-halfX, 0f, halfZ),
                new Vector3(halfX, 0f, halfZ), new Vector3(halfX, 0f, -halfZ),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

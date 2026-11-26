using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>What the ground is covered with at one terrain point. Painted by the player.</summary>
    public enum GroundLayer : byte
    {
        Soil = 0,
        Moss = 1,
        Sand = 2,
        Rock = 3,
    }

    /// <summary>
    /// The tank floor as a heightmap: a grid of points, each with a height in metres and a
    /// painted ground layer. Anything lower than WaterLevel is under water, so digging a hole
    /// below the water line makes a pool, and raising ground out of a pool drains it.
    ///
    /// The grid is split into square chunks so the renderer only rebuilds the parts that
    /// changed: every edit bumps ChunkVersion for the chunks it touched.
    /// </summary>
    public sealed class TerrainGrid
    {
        public const float DefaultCellSize = 0.25f;
        public const int ChunkCells = 16;
        public const float MinHeight = 0f;
        public const float MaxHeight = 4f;
        public const float DefaultBaseHeight = 1f;
        public const float DefaultWaterLevel = 0.6f;

        public TerrainGrid(float halfSizeX, float halfSizeZ, float cellSize = DefaultCellSize, float baseHeight = DefaultBaseHeight)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            HalfSizeX = halfSizeX;
            HalfSizeZ = halfSizeZ;
            CellSize = cellSize;
            CellsX = Math.Max(1, (int)Math.Round(2f * halfSizeX / cellSize));
            CellsZ = Math.Max(1, (int)Math.Round(2f * halfSizeZ / cellSize));
            Height = new float[PointsX * PointsZ];
            Layer = new GroundLayer[PointsX * PointsZ];
            for (int i = 0; i < Height.Length; i++) Height[i] = baseHeight;
            ChunksX = (CellsX + ChunkCells - 1) / ChunkCells;
            ChunksZ = (CellsZ + ChunkCells - 1) / ChunkCells;
            ChunkVersion = new int[ChunksX * ChunksZ];
        }

        public float HalfSizeX { get; }
        public float HalfSizeZ { get; }
        public float CellSize { get; }
        public int CellsX { get; }
        public int CellsZ { get; }
        public int PointsX => CellsX + 1;
        public int PointsZ => CellsZ + 1;
        public float MinX => -HalfSizeX;
        public float MinZ => -HalfSizeZ;

        /// <summary>Height of every grid point, row by row: index = z * PointsX + x.</summary>
        public readonly float[] Height;
        /// <summary>Painted ground layer of every grid point, same indexing as Height.</summary>
        public readonly GroundLayer[] Layer;

        /// <summary>Everything lower than this is under water.</summary>
        public float WaterLevel { get; set; } = DefaultWaterLevel;

        public int ChunksX { get; }
        public int ChunksZ { get; }
        /// <summary>Goes up by one every time a chunk changes. The renderer compares it with what it last drew.</summary>
        public readonly int[] ChunkVersion;
        /// <summary>Goes up by one on every edit anywhere. Systems that cache terrain-derived data watch it.</summary>
        public int Version { get; private set; }

        public int Index(int px, int pz) => pz * PointsX + px;
        public float PointX(int px) => MinX + px * CellSize;
        public float PointZ(int pz) => MinZ + pz * CellSize;

        public bool Contains(float x, float z) => x >= -HalfSizeX && x <= HalfSizeX && z >= -HalfSizeZ && z <= HalfSizeZ;

        /// <summary>Ground height at any position, smoothly blended between grid points.</summary>
        public float HeightAt(float x, float z)
        {
            float gx = Clamp((x - MinX) / CellSize, 0f, CellsX);
            float gz = Clamp((z - MinZ) / CellSize, 0f, CellsZ);
            int x0 = Math.Min((int)gx, CellsX - 1);
            int z0 = Math.Min((int)gz, CellsZ - 1);
            float tx = gx - x0, tz = gz - z0;
            float h00 = Height[Index(x0, z0)], h10 = Height[Index(x0 + 1, z0)];
            float h01 = Height[Index(x0, z0 + 1)], h11 = Height[Index(x0 + 1, z0 + 1)];
            float a = h00 + (h10 - h00) * tx;
            float b = h01 + (h11 - h01) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>How deep the water is here (0 on dry ground).</summary>
        public float WaterDepthAt(float x, float z)
        {
            float d = WaterLevel - HeightAt(x, z);
            return d > 0f ? d : 0f;
        }

        public bool IsWater(float x, float z) => WaterDepthAt(x, z) > 0.02f;

        /// <summary>Ground layer of the grid point nearest to a position.</summary>
        public GroundLayer LayerAt(float x, float z)
        {
            int px = (int)Math.Round(Clamp((x - MinX) / CellSize, 0f, CellsX));
            int pz = (int)Math.Round(Clamp((z - MinZ) / CellSize, 0f, CellsZ));
            return Layer[Index(px, pz)];
        }

        /// <summary>
        /// Finds where a ray (for example from the mouse) first hits the ground. Used by the
        /// sculpting tools. Returns false if the ray misses the tank.
        /// </summary>
        public bool Raycast(float ox, float oy, float oz, float dx, float dy, float dz, float maxDistance,
                            out float hitX, out float hitY, out float hitZ)
        {
            hitX = hitY = hitZ = 0f;
            float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6f) return false;
            dx /= len; dy /= len; dz /= len;

            float step = CellSize * 0.25f;
            float prevT = 0f;
            bool prevInside = false;
            for (float t = 0f; t <= maxDistance; t += step)
            {
                float x = ox + dx * t, y = oy + dy * t, z = oz + dz * t;
                bool inside = Contains(x, z);
                if (inside && y <= HeightAt(x, z))
                {
                    if (!prevInside) { hitX = x; hitY = HeightAt(x, z); hitZ = z; return true; }
                    // Narrow down between the last point above ground and this one.
                    float lo = prevT, hi = t;
                    for (int i = 0; i < 12; i++)
                    {
                        float mid = 0.5f * (lo + hi);
                        float mx = ox + dx * mid, my = oy + dy * mid, mz = oz + dz * mid;
                        if (my <= HeightAt(mx, mz)) hi = mid; else lo = mid;
                    }
                    hitX = ox + dx * hi; hitZ = oz + dz * hi; hitY = HeightAt(hitX, hitZ);
                    return true;
                }
                prevInside = inside;
                prevT = t;
            }
            return false;
        }

        /// <summary>Records that the grid points in this rectangle changed, so their chunks get redrawn.</summary>
        public void MarkChanged(int minPx, int minPz, int maxPx, int maxPz)
        {
            Version++;
            // A point on a chunk border belongs to both chunks.
            int c0x = Math.Max(0, (minPx - 1) / ChunkCells), c1x = Math.Min(ChunksX - 1, maxPx / ChunkCells);
            int c0z = Math.Max(0, (minPz - 1) / ChunkCells), c1z = Math.Min(ChunksZ - 1, maxPz / ChunkCells);
            for (int cz = c0z; cz <= c1z; cz++)
                for (int cx = c0x; cx <= c1x; cx++)
                    ChunkVersion[cz * ChunksX + cx]++;
        }

        public void MarkAllChanged() => MarkChanged(0, 0, CellsX, CellsZ);

        /// <summary>Copies heights and layers from another grid of the same size (used by undo and load).</summary>
        public void CopyFrom(float[] heights, GroundLayer[] layers)
        {
            if (heights.Length != Height.Length || layers.Length != Layer.Length)
                throw new ArgumentException("Terrain size does not match.");
            Array.Copy(heights, Height, Height.Length);
            Array.Copy(layers, Layer, Layer.Length);
            MarkAllChanged();
        }

        internal static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}

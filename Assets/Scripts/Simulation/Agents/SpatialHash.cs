using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Answers "who is near this point?" quickly. The tank is divided into 1 m squares and every
    /// agent is filed under the square it stands in, so a search only looks at a few squares
    /// instead of every agent. Rebuilt from scratch each brain tick, which takes well under a
    /// millisecond for hundreds of agents and never allocates memory after the first build.
    /// </summary>
    public sealed class SpatialHash
    {
        public const float DefaultCellSize = 1f;

        private int[] _cellStart = new int[0];
        private int[] _cellCount = new int[0];
        private int[] _items = new int[0];
        private float _minX, _minZ, _cellSize;
        private int _cellsX, _cellsZ;

        public float CellSize => _cellSize;
        public int ItemCount { get; private set; }

        /// <summary>Files every agent under its square. Indices are valid until agents are added or removed.</summary>
        public void Build(AgentStore agents, float halfSizeX, float halfSizeZ, float cellSize = DefaultCellSize)
        {
            _cellSize = cellSize;
            _minX = -halfSizeX;
            _minZ = -halfSizeZ;
            _cellsX = Math.Max(1, (int)Math.Ceiling(2f * halfSizeX / cellSize));
            _cellsZ = Math.Max(1, (int)Math.Ceiling(2f * halfSizeZ / cellSize));
            int cells = _cellsX * _cellsZ;
            if (_cellStart.Length != cells + 1)
            {
                _cellStart = new int[cells + 1];
                _cellCount = new int[cells];
            }
            if (_items.Length < agents.Count) _items = new int[Math.Max(agents.Count, _items.Length * 2)];

            Array.Clear(_cellCount, 0, cells);
            for (int i = 0; i < agents.Count; i++) _cellCount[CellOf(agents.PosX[i], agents.PosZ[i])]++;
            _cellStart[0] = 0;
            for (int c = 0; c < cells; c++) _cellStart[c + 1] = _cellStart[c] + _cellCount[c];
            Array.Clear(_cellCount, 0, cells);
            for (int i = 0; i < agents.Count; i++)
            {
                int c = CellOf(agents.PosX[i], agents.PosZ[i]);
                _items[_cellStart[c] + _cellCount[c]++] = i;
            }
            ItemCount = agents.Count;
        }

        private int CellOf(float x, float z)
        {
            int cx = (int)((x - _minX) / _cellSize);
            int cz = (int)((z - _minZ) / _cellSize);
            cx = cx < 0 ? 0 : (cx >= _cellsX ? _cellsX - 1 : cx);
            cz = cz < 0 ? 0 : (cz >= _cellsZ ? _cellsZ - 1 : cz);
            return cz * _cellsX + cx;
        }

        /// <summary>
        /// The nearest agent of the given kind within radius, or -1. Agents for which skip
        /// returns true are ignored (for example ones already eaten this tick).
        /// </summary>
        public int FindNearest(AgentStore agents, float x, float z, float radius, AgentKind kind, int excludeIndex = -1,
                               bool[] skip = null)
        {
            int best = -1;
            float bestD2 = radius * radius;
            if (!CellBounds(x, z, radius, out int c0x, out int c1x, out int c0z, out int c1z)) return -1;
            {
                for (int cz = c0z; cz <= c1z; cz++)
                {
                    for (int cx = c0x; cx <= c1x; cx++)
                    {
                        int c = cz * _cellsX + cx;
                        for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                        {
                            int i = _items[k];
                            if (i == excludeIndex || agents.Kind[i] != kind) continue;
                            if (skip != null && skip[i]) continue;
                            float dx = agents.PosX[i] - x, dz = agents.PosZ[i] - z;
                            float d2 = dx * dx + dz * dz;
                            if (d2 < bestD2) { bestD2 = d2; best = i; }
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>How many agents of a kind are within radius (excluding one index).</summary>
        public int CountWithin(AgentStore agents, float x, float z, float radius, AgentKind kind, int excludeIndex = -1)
        {
            int n = 0;
            float r2 = radius * radius;
            if (!CellBounds(x, z, radius, out int c0x, out int c1x, out int c0z, out int c1z)) return 0;
            {
                for (int cz = c0z; cz <= c1z; cz++)
                {
                    for (int cx = c0x; cx <= c1x; cx++)
                    {
                        int c = cz * _cellsX + cx;
                        for (int k = _cellStart[c], end = _cellStart[c + 1]; k < end; k++)
                        {
                            int i = _items[k];
                            if (i == excludeIndex || agents.Kind[i] != kind) continue;
                            float dx = agents.PosX[i] - x, dz = agents.PosZ[i] - z;
                            if (dx * dx + dz * dz <= r2) n++;
                        }
                    }
                }
            }
            return n;
        }

        private bool CellBounds(float x, float z, float radius, out int c0x, out int c1x, out int c0z, out int c1z)
        {
            c0x = c1x = c0z = c1z = 0;
            if (ItemCount == 0) return false;
            c0x = (int)Math.Floor((x - radius - _minX) / _cellSize); c1x = (int)Math.Floor((x + radius - _minX) / _cellSize);
            c0z = (int)Math.Floor((z - radius - _minZ) / _cellSize); c1z = (int)Math.Floor((z + radius - _minZ) / _cellSize);
            if (c0x < 0) c0x = 0;
            if (c0z < 0) c0z = 0;
            if (c1x >= _cellsX) c1x = _cellsX - 1;
            if (c1z >= _cellsZ) c1z = _cellsZ - 1;
            return c0x <= c1x && c0z <= c1z;
        }
    }
}

using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    public enum BrushMode : byte
    {
        Raise,
        Lower,
        Smooth,
        Paint,
    }

    /// <summary>
    /// The sculpting and painting tools. A brush is a soft circle: full strength in the middle,
    /// fading to nothing at the edge. Each call applies one frame's worth of the brush, so holding
    /// the mouse button down keeps raising (or lowering, smoothing, painting).
    /// </summary>
    public static class TerrainBrush
    {
        /// <summary>
        /// Applies a brush at (x, z). Strength is metres per second for raise and lower, and
        /// 0..1 blending per second for smooth. Returns true if anything changed.
        /// </summary>
        public static bool Apply(TerrainGrid t, BrushMode mode, float x, float z, float radius, float strength, float dt,
                                 GroundLayer paintLayer = GroundLayer.Soil)
        {
            if (radius <= 0f || dt <= 0f) return false;
            int minPx = Math.Max(0, (int)Math.Floor((x - radius - t.MinX) / t.CellSize));
            int maxPx = Math.Min(t.CellsX, (int)Math.Ceiling((x + radius - t.MinX) / t.CellSize));
            int minPz = Math.Max(0, (int)Math.Floor((z - radius - t.MinZ) / t.CellSize));
            int maxPz = Math.Min(t.CellsZ, (int)Math.Ceiling((z + radius - t.MinZ) / t.CellSize));
            if (minPx > maxPx || minPz > maxPz) return false;

            float[] smoothed = mode == BrushMode.Smooth ? Neighbourhood(t, minPx, minPz, maxPx, maxPz) : null;
            int w = maxPx - minPx + 1;
            bool changed = false;
            for (int pz = minPz; pz <= maxPz; pz++)
            {
                for (int px = minPx; px <= maxPx; px++)
                {
                    float f = Falloff(t.PointX(px) - x, t.PointZ(pz) - z, radius);
                    if (f <= 0f) continue;
                    int i = t.Index(px, pz);
                    switch (mode)
                    {
                        case BrushMode.Raise:
                        case BrushMode.Lower:
                        {
                            float sign = mode == BrushMode.Raise ? 1f : -1f;
                            float h = TerrainGrid.Clamp(t.Height[i] + sign * strength * f * dt, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
                            if (h != t.Height[i]) { t.Height[i] = h; changed = true; }
                            break;
                        }
                        case BrushMode.Smooth:
                        {
                            float target = smoothed[(pz - minPz) * w + (px - minPx)];
                            float k = TerrainGrid.Clamp(strength * f * dt, 0f, 1f);
                            float h = t.Height[i] + (target - t.Height[i]) * k;
                            if (Math.Abs(h - t.Height[i]) > 1e-6f) { t.Height[i] = h; changed = true; }
                            break;
                        }
                        case BrushMode.Paint:
                            // Paint has a firm edge so painted patches look deliberate.
                            if (f > 0.25f && t.Layer[i] != paintLayer) { t.Layer[i] = paintLayer; changed = true; }
                            break;
                    }
                }
            }
            if (changed) t.MarkChanged(minPx, minPz, maxPx, maxPz);
            return changed;
        }

        /// <summary>Smooth bump: 1 at the centre, 0 at the radius and beyond.</summary>
        public static float Falloff(float dx, float dz, float radius)
        {
            float d2 = (dx * dx + dz * dz) / (radius * radius);
            if (d2 >= 1f) return 0f;
            float s = 1f - d2;
            return s * s;
        }

        /// <summary>Average height of each point and its eight neighbours, read before any point changes.</summary>
        private static float[] Neighbourhood(TerrainGrid t, int minPx, int minPz, int maxPx, int maxPz)
        {
            int w = maxPx - minPx + 1, h = maxPz - minPz + 1;
            var result = new float[w * h];
            for (int pz = minPz; pz <= maxPz; pz++)
            {
                for (int px = minPx; px <= maxPx; px++)
                {
                    float sum = 0f; int n = 0;
                    for (int oz = -1; oz <= 1; oz++)
                    {
                        int qz = pz + oz;
                        if (qz < 0 || qz > t.CellsZ) continue;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int qx = px + ox;
                            if (qx < 0 || qx > t.CellsX) continue;
                            sum += t.Height[t.Index(qx, qz)]; n++;
                        }
                    }
                    result[(pz - minPz) * w + (px - minPx)] = sum / n;
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Undo and redo for terrain strokes. Call BeginStroke when the mouse button goes down and
    /// EndStroke when it comes up; the whole drag then undoes in one step. Keeps the last 50 strokes.
    /// </summary>
    public sealed class TerrainHistory
    {
        public const int MaxSteps = 50;

        private readonly TerrainGrid _terrain;
        private readonly LinkedList<Snapshot> _undo = new LinkedList<Snapshot>();
        private readonly Stack<Snapshot> _redo = new Stack<Snapshot>();
        private Snapshot _strokeStart;

        public TerrainHistory(TerrainGrid terrain)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        /// <summary>The ground this history belongs to.</summary>
        public TerrainGrid Terrain => _terrain;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;
        public bool InStroke => _strokeStart != null;

        public void BeginStroke()
        {
            _strokeStart = Snapshot.Take(_terrain);
        }

        /// <summary>Finishes the stroke. Strokes that changed nothing are not recorded.</summary>
        public void EndStroke()
        {
            if (_strokeStart == null) return;
            var before = _strokeStart;
            _strokeStart = null;
            if (before.SameAs(_terrain)) return;
            _undo.AddLast(before);
            if (_undo.Count > MaxSteps) _undo.RemoveFirst();
            _redo.Clear();
        }

        public bool Undo()
        {
            EndStroke();
            if (_undo.Count == 0) return false;
            var snap = _undo.Last.Value;
            _undo.RemoveLast();
            _redo.Push(Snapshot.Take(_terrain));
            snap.Restore(_terrain);
            return true;
        }

        public bool Redo()
        {
            EndStroke();
            if (_redo.Count == 0) return false;
            _undo.AddLast(Snapshot.Take(_terrain));
            _redo.Pop().Restore(_terrain);
            return true;
        }

        private sealed class Snapshot
        {
            private float[] _height;
            private GroundLayer[] _layer;

            public static Snapshot Take(TerrainGrid t) =>
                new Snapshot { _height = (float[])t.Height.Clone(), _layer = (GroundLayer[])t.Layer.Clone() };

            public void Restore(TerrainGrid t) => t.CopyFrom(_height, _layer);

            public bool SameAs(TerrainGrid t)
            {
                for (int i = 0; i < _height.Length; i++)
                    if (_height[i] != t.Height[i] || _layer[i] != t.Layer[i]) return false;
                return true;
            }
        }
    }
}

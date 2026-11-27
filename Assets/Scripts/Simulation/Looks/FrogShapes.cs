using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>A point or direction in a mesh (kept here so the shapes can be built and tested without Unity).</summary>
    public struct V3
    {
        public float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, float k) => new V3(a.X * k, a.Y * k, a.Z * k);
        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public V3 Normalised { get { float l = Length; return l > 1e-9f ? this * (1f / l) : new V3(0f, 1f, 0f); } }
        public static V3 Cross(V3 a, V3 b) => new V3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Dot(V3 a, V3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    /// <summary>A triangle mesh: corner positions, smooth normals and three corner numbers per triangle.</summary>
    public sealed class MeshData
    {
        public V3[] Positions;
        public V3[] Normals;
        public int[] Triangles;

        public int TriangleCount => Triangles.Length / 3;

        public void Bounds(out V3 min, out V3 max)
        {
            min = new V3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new V3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var p in Positions)
            {
                min = new V3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new V3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
        }
    }

    /// <summary>
    /// The frog's body parts, made from numbers rather than downloaded models, so there are no
    /// licences to track and every part can later be reshaped by genes. Each part is built once
    /// and drawn many times (GPU instancing); the game poses them every frame (see FrogPose).
    ///
    /// Sizes are in "frog units": the body is about 1 wide, 0.6 tall and 1.3 long once the game
    /// stretches it, matching the old placeholder sphere so nothing else had to move.
    /// </summary>
    public static class FrogShapes
    {
        /// <summary>
        /// The body: a sphere pushed into a frog shape. Broad flat head at the front (+Z), a waist,
        /// wide hips, a little hump over the hips (the sacral hump real frogs have) and a flat belly.
        /// It fits in a 1 x 1 x 1 box centred on the origin.
        /// </summary>
        public static MeshData Body(int rings = 18, int segments = 28)
        {
            return Sphere(rings, segments, u =>
            {
                float x = u.X * 0.5f, y = u.Y * 0.5f, z = u.Z * 0.5f;
                float zf = u.Z; // -1 at the back, +1 at the snout
                // Wider at the hips, a little narrower at the waist, broad again across the head.
                float width = 1f + 0.10f * Bump(zf, -0.4f, 0.32f) - 0.07f * Bump(zf, 0.15f, 0.22f) + 0.05f * Bump(zf, 0.5f, 0.25f);
                x *= width;
                // A flatter head and a blunt snout.
                if (zf > 0.25f) y *= 1f - 0.22f * Smooth((zf - 0.25f) / 0.75f);
                if (zf > 0.7f) z -= 0.05f * Smooth((zf - 0.7f) / 0.3f);
                // The hump over the hips.
                if (u.Y > 0f) y += 0.07f * Bump(zf, -0.45f, 0.22f) * u.Y;
                // A flat belly to sit on.
                if (u.Y < -0.3f) y = -0.15f + (y + 0.15f) * 0.4f;
                return new V3(x, y, z);
            });
        }

        /// <summary>
        /// The pale underside: the lower part of the body, pushed out a hair so it draws on top. It
        /// takes the belly colour (white, red for fire-bellied toads, yellow for yellow-bellied).
        /// </summary>
        public static MeshData Belly(MeshData body)
        {
            const float below = -0.11f, outward = 1.012f;
            var keep = new List<int>();
            var t = body.Triangles;
            for (int k = 0; k < t.Length; k += 3)
                if (body.Positions[t[k]].Y < below && body.Positions[t[k + 1]].Y < below && body.Positions[t[k + 2]].Y < below)
                {
                    keep.Add(t[k]); keep.Add(t[k + 1]); keep.Add(t[k + 2]);
                }
            var map = new Dictionary<int, int>();
            var pos = new List<V3>();
            var nrm = new List<V3>();
            var tri = new int[keep.Count];
            for (int k = 0; k < keep.Count; k++)
            {
                int old = keep[k];
                if (!map.TryGetValue(old, out int n))
                {
                    n = pos.Count;
                    map[old] = n;
                    pos.Add(body.Positions[old] * outward);
                    nrm.Add(body.Normals[old]);
                }
                tri[k] = n;
            }
            return new MeshData { Positions = pos.ToArray(), Normals = nrm.ToArray(), Triangles = tri };
        }

        /// <summary>A round ball of diameter 1 (eyes, pupils, eggs, the throat sac, toe pads).</summary>
        public static MeshData Ball(int rings = 10, int segments = 14) => Sphere(rings, segments, u => u * 0.5f);

        /// <summary>
        /// One leg bone (thigh, shin, upper arm, forearm, tongue): a rounded tube along +Z from 0 to
        /// 1, 1 thick at the start and <paramref name="taper"/> thick at the end.
        /// </summary>
        public static MeshData Limb(float taper = 0.7f, int segments = 10, int rings = 6)
        {
            return Sphere(rings + 4, segments, u =>
            {
                // A sphere stretched into a capsule: round ends, an even tube in between.
                float z = (u.Z + 1f) * 0.5f; // 0..1 along the bone
                float r = 0.5f * (1f + (taper - 1f) * z);
                float m = (float)Math.Sqrt(Math.Max(0f, 1f - u.Z * u.Z));
                float tube = Math.Min(1f, m * 2.2f);
                float k = m > 1e-5f ? tube / m * r : 0f;
                return new V3(u.X * k, u.Y * k, z);
            });
        }

        /// <summary>
        /// A webbed foot: a flat palm with four splayed toes ending in round pads (big pads are the
        /// sticky toes of tree frogs; the game shrinks them for toads). Lies flat, toes towards +Z,
        /// fits in a 1 x 0.2 x 1 box with the heel at the origin.
        /// </summary>
        public static MeshData Foot(int toes = 4, float pads = 1f)
        {
            var parts = new List<(MeshData mesh, V3 offset, float yaw, V3 scale)>();
            parts.Add((Ball(6, 10), new V3(0f, 0f, 0.25f), 0f, new V3(0.45f, 0.12f, 0.55f)));
            var limb = Limb(0.6f, 6, 3);
            var pad = Ball(5, 8);
            for (int k = 0; k < toes; k++)
            {
                float yaw = -0.6f + 1.2f * k / Math.Max(1, toes - 1);
                float len = 0.75f - 0.12f * Math.Abs(k - (toes - 1) * 0.5f);
                parts.Add((limb, new V3(0f, 0f, 0.2f), yaw, new V3(0.12f, 0.08f, len)));
                var tip = new V3((float)Math.Sin(yaw) * (len + 0.2f), 0f, (float)Math.Cos(yaw) * (len + 0.2f) + 0.0f);
                parts.Add((pad, tip, 0f, new V3(0.17f * pads, 0.1f * pads, 0.17f * pads)));
            }
            return Combine(parts);
        }

        /// <summary>
        /// A tadpole's tail fin: a flat blade along -Z from 0 to -1, tallest near the body and pointed
        /// at the tip. The game bends it in three pieces to make it swish.
        /// </summary>
        public static MeshData TailPiece() => Sphere(8, 12, u =>
        {
            float z = (u.Z - 1f) * 0.5f; // 0 at the body end, -1 at the tip
            float h = 0.5f * (1f + z * 0.55f);
            return new V3(u.X * 0.08f, u.Y * h, z);
        });

        /// <summary>A UV sphere with every point moved by <paramref name="shape"/> (given the unit direction), with smooth normals.</summary>
        public static MeshData Sphere(int rings, int segments, Func<V3, V3> shape)
        {
            if (rings < 2 || segments < 3) throw new ArgumentOutOfRangeException(nameof(rings));
            var pos = new List<V3> { shape(new V3(0f, 1f, 0f)) };
            for (int r = 1; r < rings; r++)
            {
                double th = Math.PI * r / rings;
                for (int s = 0; s < segments; s++)
                {
                    double ph = 2 * Math.PI * s / segments;
                    var u = new V3((float)(Math.Sin(th) * Math.Sin(ph)), (float)Math.Cos(th), (float)(Math.Sin(th) * Math.Cos(ph)));
                    pos.Add(shape(u));
                }
            }
            pos.Add(shape(new V3(0f, -1f, 0f)));
            int bottom = pos.Count - 1;
            var tri = new List<int>();
            int Ring(int r, int s) => 1 + (r - 1) * segments + ((s % segments) + segments) % segments;
            for (int s = 0; s < segments; s++) { tri.Add(0); tri.Add(Ring(1, s)); tri.Add(Ring(1, s + 1)); }
            for (int r = 1; r < rings - 1; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = Ring(r, s), b = Ring(r, s + 1), c = Ring(r + 1, s), d = Ring(r + 1, s + 1);
                    tri.Add(a); tri.Add(c); tri.Add(b);
                    tri.Add(b); tri.Add(c); tri.Add(d);
                }
            for (int s = 0; s < segments; s++) { tri.Add(bottom); tri.Add(Ring(rings - 1, s + 1)); tri.Add(Ring(rings - 1, s)); }
            var mesh = new MeshData { Positions = pos.ToArray(), Triangles = tri.ToArray() };
            mesh.Normals = SmoothNormals(mesh.Positions, mesh.Triangles);
            return mesh;
        }

        /// <summary>Normals made by averaging the faces around each corner (bigger faces count for more).</summary>
        public static V3[] SmoothNormals(V3[] pos, int[] tri)
        {
            var n = new V3[pos.Length];
            for (int k = 0; k < tri.Length; k += 3)
            {
                var face = V3.Cross(pos[tri[k + 1]] - pos[tri[k]], pos[tri[k + 2]] - pos[tri[k]]);
                n[tri[k]] += face; n[tri[k + 1]] += face; n[tri[k + 2]] += face;
            }
            for (int k = 0; k < n.Length; k++) n[k] = n[k].Normalised;
            return n;
        }

        /// <summary>Puts several shapes into one mesh, each scaled, turned about the vertical (radians) and moved.</summary>
        public static MeshData Combine(List<(MeshData mesh, V3 offset, float yaw, V3 scale)> parts)
        {
            var pos = new List<V3>();
            var nrm = new List<V3>();
            var tri = new List<int>();
            foreach (var (mesh, offset, yaw, scale) in parts)
            {
                float c = (float)Math.Cos(yaw), s = (float)Math.Sin(yaw);
                int start = pos.Count;
                for (int k = 0; k < mesh.Positions.Length; k++)
                {
                    var p = mesh.Positions[k];
                    p = new V3(p.X * scale.X, p.Y * scale.Y, p.Z * scale.Z);
                    pos.Add(new V3(p.X * c + p.Z * s, p.Y, -p.X * s + p.Z * c) + offset);
                    // Normals under a stretch: divide by the scale, then turn.
                    var q = mesh.Normals[k];
                    q = new V3(q.X / scale.X, q.Y / scale.Y, q.Z / scale.Z).Normalised;
                    nrm.Add(new V3(q.X * c + q.Z * s, q.Y, -q.X * s + q.Z * c));
                }
                foreach (int t in mesh.Triangles) tri.Add(start + t);
            }
            return new MeshData { Positions = pos.ToArray(), Normals = nrm.ToArray(), Triangles = tri.ToArray() };
        }

        /// <summary>1 at the centre, falling smoothly to about 0 at width away.</summary>
        private static float Bump(float v, float centre, float width)
        {
            float d = (v - centre) / width;
            return (float)Math.Exp(-d * d * 2.0);
        }

        private static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }
}

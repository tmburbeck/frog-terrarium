using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// The decoration and building parts a player can place in the tank. Never reorder: saves and
    /// blueprints store the number. Add new parts to the end.
    /// </summary>
    public enum PartKind : byte
    {
        Rock = 0,
        Slab = 1,
        Log = 2,
        Branch = 3,
        Hide = 4,
        Plant = 5,
        Fern = 6,
        MossPad = 7,
        Wall = 8,
        Pillar = 9,
        Platform = 10,
        Ramp = 11,
        // Frog playground pieces (Phase 5). Frogs ride them on their own; see Playground.
        Slide = 12,
        SpiralStairs = 13,
        AirTunnel = 14,
        Elevator = 15,
        Teleporter = 16,
    }

    /// <summary>
    /// A snap point on a part, in the part's own space (before rotation and scale): where it is and
    /// which way it faces. Two parts snap together when a socket of one meets a socket of the other
    /// facing the opposite way.
    /// </summary>
    public struct Socket
    {
        public float X, Y, Z;
        /// <summary>Facing, as a direction on the ground (0,1 = forward, 1,0 = right).</summary>
        public float DirX, DirZ;

        public Socket(float x, float y, float z, float dirX, float dirZ)
        {
            X = x; Y = y; Z = z; DirX = dirX; DirZ = dirZ;
        }
    }

    /// <summary>What a kind of part is and does.</summary>
    public sealed class PartInfo
    {
        public PartKind Kind;
        public string Name;
        /// <summary>Plain words for the build panel.</summary>
        public string Description;
        /// <summary>Size in metres at scale 1: width (X), height (Y) and depth (Z).</summary>
        public float SizeX, SizeY, SizeZ;
        /// <summary>Default colour.</summary>
        public byte R, G, B;
        /// <summary>
        /// 0..1. How much shade it gives the ground under and around it: shaded ground stays up to
        /// 1.5 °C cooler than the room.
        /// </summary>
        public float Shade;
        /// <summary>Humidity points per second it adds around it (living plants and moss breathe out water).</summary>
        public float Damp;
        /// <summary>Frogs feel safe under or next to it: stress fades faster and comfort is a little higher.</summary>
        public bool Cover;
        /// <summary>Tree frogs like to sit up on it.</summary>
        public bool Perch;
        /// <summary>Other parts can be stacked on top of it.</summary>
        public bool Stackable;
        /// <summary>Snap points on its sides.</summary>
        public Socket[] Sockets;
        /// <summary>What it costs at normal size in money mode. Bigger parts cost more (by area).</summary>
        public float Cost = 10f;
    }

    /// <summary>One part placed in the tank. Saved.</summary>
    public sealed class PlacedPart
    {
        public int Id;
        public PartKind Kind;
        /// <summary>Centre of the part on the ground, in metres.</summary>
        public float X, Z;
        /// <summary>Turn around the vertical axis, in degrees (0 to 360).</summary>
        public float Rotation;
        /// <summary>Size compared to normal (0.5 to 2).</summary>
        public float Scale = 1f;
        /// <summary>The player's colour for it.</summary>
        public byte R, G, B;
        /// <summary>The part it sits on top of (0 = it stands on the ground).</summary>
        public int OnPartId;

        public PartInfo Info => Parts.Info(Kind);
        public float SizeX => Info.SizeX * Scale;
        public float SizeY => Info.SizeY * Scale;
        public float SizeZ => Info.SizeZ * Scale;

        public PlacedPart Clone() => (PlacedPart)MemberwiseClone();
    }

    /// <summary>
    /// The catalogue of parts and the geometry rules for placing them: snapping, stacking and
    /// whether a spot is allowed. No Unity code, so it is tested with the rest of the simulation.
    /// </summary>
    public static class Parts
    {
        public const float MinScale = 0.5f, MaxScale = 2f;
        /// <summary>Grid spacing for grid placement, in metres.</summary>
        public const float GridStep = 0.25f;
        /// <summary>Rotation step for grid placement, in degrees.</summary>
        public const float GridAngle = 45f;
        /// <summary>A socket this close to a matching socket snaps to it, in metres.</summary>
        public const float SnapRadius = 0.35f;
        /// <summary>Parts may touch: overlaps smaller than this are allowed, in metres.</summary>
        public const float Touch = 0.03f;

        private static Socket[] Sides(float x, float z) => new[]
        {
            new Socket(x, 0f, 0f, 1f, 0f), new Socket(-x, 0f, 0f, -1f, 0f),
            new Socket(0f, 0f, z, 0f, 1f), new Socket(0f, 0f, -z, 0f, -1f),
        };

        private static Socket[] Ends(float z) => new[] { new Socket(0f, 0f, z, 0f, 1f), new Socket(0f, 0f, -z, 0f, -1f) };

        public static readonly PartInfo[] All =
        {
            new PartInfo { Kind = PartKind.Rock, Cost = 8f, Name = "Rock", Description = "A rounded stone. Gives a little shade.",
                SizeX = 0.6f, SizeY = 0.4f, SizeZ = 0.5f, R = 120, G = 116, B = 108, Shade = 0.3f, Sockets = new Socket[0] },
            new PartInfo { Kind = PartKind.Slab, Cost = 10f, Name = "Stone slab", Description = "A flat stone. Stack things on it or line them up.",
                SizeX = 1f, SizeY = 0.15f, SizeZ = 1f, R = 140, G = 134, B = 122, Shade = 0.5f, Stackable = true, Sockets = Sides(0.5f, 0.5f) },
            new PartInfo { Kind = PartKind.Log, Cost = 12f, Name = "Log", Description = "A fallen log. Frogs hide beside it; tree frogs sit on it.",
                SizeX = 0.35f, SizeY = 0.35f, SizeZ = 1.4f, R = 110, G = 80, B = 52, Shade = 0.4f, Cover = true, Perch = true, Sockets = Ends(0.7f) },
            new PartInfo { Kind = PartKind.Branch, Cost = 6f, Name = "Branch", Description = "A raised climbing branch. Tree frogs love a perch.",
                SizeX = 0.12f, SizeY = 0.9f, SizeZ = 1.6f, R = 128, G = 96, B = 64, Shade = 0.1f, Perch = true, Sockets = Ends(0.8f) },
            new PartInfo { Kind = PartKind.Hide, Cost = 15f, Name = "Hide", Description = "A little cave. Frogs calm down faster inside.",
                SizeX = 0.6f, SizeY = 0.35f, SizeZ = 0.6f, R = 150, G = 110, B = 80, Shade = 0.9f, Cover = true, Stackable = true, Sockets = Sides(0.3f, 0.3f) },
            new PartInfo { Kind = PartKind.Plant, Cost = 12f, Name = "Leafy plant", Description = "A broad-leaved plant. Shade, damp air, cover and a perch.",
                SizeX = 0.7f, SizeY = 0.8f, SizeZ = 0.7f, R = 60, G = 130, B = 55, Shade = 0.6f, Damp = 3f, Cover = true, Perch = true, Sockets = new Socket[0] },
            new PartInfo { Kind = PartKind.Fern, Cost = 8f, Name = "Fern", Description = "A low feathery fern. Damp air and cover.",
                SizeX = 0.6f, SizeY = 0.4f, SizeZ = 0.6f, R = 70, G = 150, B = 70, Shade = 0.4f, Damp = 2f, Cover = true, Sockets = new Socket[0] },
            new PartInfo { Kind = PartKind.MossPad, Cost = 5f, Name = "Moss pad", Description = "A cushion of live moss. Keeps the air around it damp.",
                SizeX = 0.8f, SizeY = 0.08f, SizeZ = 0.8f, R = 80, G = 120, B = 50, Damp = 2.5f, Sockets = Sides(0.4f, 0.4f) },
            new PartInfo { Kind = PartKind.Wall, Cost = 10f, Name = "Wall", Description = "A stone wall section. Snaps end to end and to pillars.",
                SizeX = 1f, SizeY = 0.6f, SizeZ = 0.15f, R = 150, G = 140, B = 125, Shade = 0.3f, Sockets = new[] { new Socket(0.5f, 0f, 0f, 1f, 0f), new Socket(-0.5f, 0f, 0f, -1f, 0f) } },
            new PartInfo { Kind = PartKind.Pillar, Cost = 6f, Name = "Pillar", Description = "A post. Walls and platforms snap to its sides.",
                SizeX = 0.2f, SizeY = 0.9f, SizeZ = 0.2f, R = 130, G = 120, B = 110, Stackable = true, Sockets = Sides(0.1f, 0.1f) },
            new PartInfo { Kind = PartKind.Platform, Cost = 14f, Name = "Platform", Description = "A wooden deck. Stack it on pillars or hides to build up.",
                SizeX = 1f, SizeY = 0.08f, SizeZ = 1f, R = 150, G = 115, B = 75, Shade = 0.7f, Cover = true, Stackable = true, Sockets = Sides(0.5f, 0.5f) },
            new PartInfo { Kind = PartKind.Ramp, Cost = 8f, Name = "Ramp", Description = "A sloping board from the ground up to a platform.",
                SizeX = 0.5f, SizeY = 0.35f, SizeZ = 1f, R = 150, G = 115, B = 75, Shade = 0.2f, Perch = true, Sockets = Ends(0.5f) },
            new PartInfo { Kind = PartKind.Slide, Cost = 30f, Name = "Slide", Description = "Playground. Frogs climb the ladder and slide down the front. Point it at a pool for a splash.",
                SizeX = 0.5f, SizeY = 1f, SizeZ = 2f, R = 225, G = 90, B = 60, Shade = 0.2f, Sockets = Ends(1f) },
            new PartInfo { Kind = PartKind.SpiralStairs, Cost = 35f, Name = "Spiral staircase", Description = "Playground. Frogs climb round and round to the top, look about, then leap off the front.",
                SizeX = 0.9f, SizeY = 1.4f, SizeZ = 0.9f, R = 160, G = 140, B = 120, Shade = 0.3f, Cover = true, Sockets = Ends(0.45f) },
            new PartInfo { Kind = PartKind.AirTunnel, Cost = 45f, Name = "Air tunnel", Description = "Playground. A fan sucks frogs in at the back and whooshes them out the front, tumbling. Uses a little power per ride.",
                SizeX = 0.45f, SizeY = 0.45f, SizeZ = 2.4f, R = 90, G = 170, B = 200, Shade = 0.3f, Sockets = Ends(1.2f) },
            new PartInfo { Kind = PartKind.Elevator, Cost = 60f, Name = "Elevator", Description = "Playground. A little lift that goes up to a lookout ledge and back down on its own. Holds three frogs, who may squabble over space. Uses power while frogs ride.",
                SizeX = 0.7f, SizeY = 1.6f, SizeZ = 0.7f, R = 200, G = 170, B = 70, Shade = 0.4f, Cover = true, Sockets = Ends(0.35f) },
            new PartInfo { Kind = PartKind.Teleporter, Cost = 80f, Name = "Teleporter pad", Description = "Playground. Place two: a frog that steps on one pops out of the other, anywhere in the tank. Pads pair up in the order you place them. Uses power per trip.",
                SizeX = 0.7f, SizeY = 0.1f, SizeZ = 0.7f, R = 150, G = 90, B = 220, Sockets = Ends(0.35f) },
        };

        public static PartInfo Info(PartKind kind) => All[(int)kind < All.Length ? (int)kind : 0];

        /// <summary>Turns a point in a part's own space into tank space (scale, then rotate, then move).</summary>
        public static void ToWorld(PlacedPart p, float lx, float lz, out float wx, out float wz)
        {
            double a = p.Rotation * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            lx *= p.Scale; lz *= p.Scale;
            // Clockwise when seen from above, like a compass: 90° turns forward (+Z) to the right (+X).
            wx = p.X + lx * c + lz * s;
            wz = p.Z - lx * s + lz * c;
        }

        /// <summary>Turns a direction in a part's own space into tank space.</summary>
        public static void DirToWorld(float rotation, float dx, float dz, out float wx, out float wz)
        {
            double a = rotation * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            wx = dx * c + dz * s;
            wz = -dx * s + dz * c;
        }

        /// <summary>The four corners of a part's footprint on the ground, in tank space.</summary>
        public static void Corners(PlacedPart p, float[] xs, float[] zs)
        {
            float hx = 0.5f * p.Info.SizeX, hz = 0.5f * p.Info.SizeZ;
            ToWorld(p, -hx, -hz, out xs[0], out zs[0]);
            ToWorld(p, hx, -hz, out xs[1], out zs[1]);
            ToWorld(p, hx, hz, out xs[2], out zs[2]);
            ToWorld(p, -hx, hz, out xs[3], out zs[3]);
        }

        /// <summary>True if a point on the ground is inside a part's footprint (grown by margin metres).</summary>
        public static bool Contains(PlacedPart p, float x, float z, float margin = 0f)
        {
            float dx = x - p.X, dz = z - p.Z;
            double a = -p.Rotation * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float lx = dx * c + dz * s, lz = -dx * s + dz * c;
            return Math.Abs(lx) <= 0.5f * p.SizeX + margin && Math.Abs(lz) <= 0.5f * p.SizeZ + margin;
        }

        private static readonly float[] Ax = new float[4], Az = new float[4], Bx = new float[4], Bz = new float[4];

        /// <summary>
        /// True if two parts' footprints overlap by more than a touch (separating axis test on the two
        /// rotated rectangles).
        /// </summary>
        public static bool FootprintsOverlap(PlacedPart a, PlacedPart b)
        {
            lock (Ax)
            {
                Corners(a, Ax, Az);
                Corners(b, Bx, Bz);
                return !Separated(Ax, Az, Bx, Bz) && !Separated(Bx, Bz, Ax, Az);
            }
        }

        private static bool Separated(float[] px, float[] pz, float[] qx, float[] qz)
        {
            for (int e = 0; e < 2; e++)
            {
                float nx = -(pz[e + 1] - pz[e]), nz = px[e + 1] - px[e];
                float len = (float)Math.Sqrt(nx * nx + nz * nz);
                if (len < 1e-6f) continue;
                nx /= len; nz /= len;
                float pMin = float.MaxValue, pMax = float.MinValue, qMin = float.MaxValue, qMax = float.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    float d = px[k] * nx + pz[k] * nz;
                    if (d < pMin) pMin = d;
                    if (d > pMax) pMax = d;
                    float f = qx[k] * nx + qz[k] * nz;
                    if (f < qMin) qMin = f;
                    if (f > qMax) qMax = f;
                }
                if (pMax - Touch <= qMin || qMax - Touch <= pMin) return true;
            }
            return false;
        }

        /// <summary>Rounds a spot and turn to the build grid.</summary>
        public static void ToGrid(ref float x, ref float z, ref float rotation)
        {
            x = (float)Math.Round(x / GridStep) * GridStep;
            z = (float)Math.Round(z / GridStep) * GridStep;
            rotation = NormaliseAngle((float)Math.Round(rotation / GridAngle) * GridAngle);
        }

        public static float NormaliseAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees < 0f) degrees += 360f;
            return degrees;
        }

        public static float ClampScale(float s) => s < MinScale ? MinScale : (s > MaxScale ? MaxScale : s);
    }
}

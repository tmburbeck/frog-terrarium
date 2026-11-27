using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>One point on a ride, in the piece's own space (before turning and sizing).</summary>
    public struct RidePoint
    {
        public float X, Y, Z;
        /// <summary>How fast the frog travels from the previous point to this one, in metres per second. Infinity = in no time at all (a teleport).</summary>
        public float Speed;
        /// <summary>Seconds the frog sits here after arriving (at the top of a slide, say).</summary>
        public float Wait;

        public RidePoint(float x, float y, float z, float speed, float wait = 0f)
        {
            X = x; Y = y; Z = z; Speed = speed; Wait = wait;
        }
    }

    /// <summary>How a frog rides a piece. Some traits change it (see <see cref="Playground.StyleFor"/>).</summary>
    public enum RideStyle : byte
    {
        Normal,
        /// <summary>Long legs: bounds up the ladder or the stairs in a couple of leaps.</summary>
        BigJump,
        /// <summary>Sticky toe pads (tree frogs) or claws: climbs back up the slide the wrong way for another go.</summary>
        Sticky,
    }

    /// <summary>What a playground piece does: the route a frog takes on it and how much fun that is.</summary>
    public sealed class PlayInfo
    {
        public PartKind Kind;
        /// <summary>How many frogs can ride it at once. Others wait their turn at the entry.</summary>
        public int Capacity;
        /// <summary>How much boredom one ride takes away (0 to 1).</summary>
        public float Relief;
        /// <summary>Uses power while a frog rides it (money mode), and whirrs: shy frogs nearby dislike the noise.</summary>
        public bool Powered;
        /// <summary>The route, from the entry (first point, on the piece's base) to where the frog lands.</summary>
        public RidePoint[] Route;
        /// <summary>The route for a big jumper, or null to use <see cref="Route"/>.</summary>
        public RidePoint[] JumpRoute;
        /// <summary>The route for a sticky-footed frog, or null to use <see cref="Route"/>.</summary>
        public RidePoint[] StickyRoute;
        /// <summary>For teleporter pads: the route on the partner pad, after the jump.</summary>
        public RidePoint[] ExitRoute;
        /// <summary>A lift whose car runs on its own timetable (see <see cref="Playground.ElevatorPhase"/>).</summary>
        public bool Elevator;
        /// <summary>Words for the frog inspector ("sliding down the slide").</summary>
        public string Doing;

        public RidePoint[] RouteFor(RideStyle style) =>
            style == RideStyle.BigJump && JumpRoute != null ? JumpRoute
            : style == RideStyle.Sticky && StickyRoute != null ? StickyRoute
            : Route;
    }

    /// <summary>
    /// The frog playground (Phase 5): slides, spiral staircases, air tunnels, elevators and
    /// teleporter pads that frogs ride by themselves when they are bored. Pieces are ordinary
    /// building parts, so they snap, stack, save and travel in blueprints like any other part.
    ///
    /// How it works:
    /// * While a tank has at least one playground piece, every land frog slowly gets bored
    ///   (<see cref="AgentStore.Play"/>): faster for young and bold frogs, slower for elders.
    /// * A bored frog may pick the Play goal (see BrainSystem). It walks to a piece's entry and, if
    ///   there is room, starts a ride (<see cref="StartRide"/>).
    /// * During a ride the frog follows the piece's route (<see cref="Advance"/>, called by
    ///   MovementSystem 30 times a second) instead of walking. Its brain rests until it lands.
    /// * Traits change the ride: long-legged frogs bound up in big leaps, and frogs with sticky toe
    ///   pads (tree frogs) or claws may climb back up a slide the wrong way for another go.
    /// * Landing takes boredom away and leaves a short glow of fun that adds comfort. If the frog
    ///   lands at the entry of another piece and is still a little bored, it goes straight on: pieces
    ///   snapped end to end become chains (tunnel into slide into pool).
    ///
    /// No upkeep: pieces never break or need refilling. Powered pieces (tunnels, elevators and
    /// teleporters) use a little power while a frog rides, and their whirring bothers shy frogs close by.
    /// </summary>
    public static class Playground
    {
        /// <summary>Boredom per second for an average adult (about four minutes from 0 to 1).</summary>
        public const float BoredomPerSecond = 1f / 240f;
        /// <summary>A frog thinks about playing once its boredom passes this.</summary>
        public const float PlayAbove = 0.35f;
        /// <summary>How quickly the glow of fun fades, per second.</summary>
        public const float FunFade = 1f / 90f;
        /// <summary>Comfort added right after a ride, fading with the fun.</summary>
        public const float FunComfort = 0.06f;
        /// <summary>Frogs only go to pieces within this distance, in metres.</summary>
        public const float SearchRadius = 12f;
        /// <summary>A frog this close to a piece's entry can get on, in metres.</summary>
        public const float BoardDistance = 0.3f;
        /// <summary>A frog landing this close to another piece's entry (times the piece's size) carries straight on.</summary>
        public const float ChainRadius = 0.75f;
        /// <summary>Speed of the little hop up onto a raised piece and the drop off a raised exit, in m/s.</summary>
        public const float HopSpeed = 2f;
        /// <summary>Frogs below this boldness are shy: they skip pieces that are busy and dislike noisy ones.</summary>
        public const float ShyBelow = 0.35f;
        /// <summary>Power used while a frog rides a powered piece, in money per second.</summary>
        public const float RidePower = 0.02f;
        /// <summary>Shy frogs this close to a powered piece in use (metres) lose some comfort to the noise.</summary>
        public const float NoiseRadius = 1.5f;
        public const float NoisePenalty = 0.1f;
        /// <summary>Stress every rider gets when a third frog squeezes into a lift.</summary>
        public const float SquabbleStress = 0.02f;

        /// <summary>The elevator's timetable, in seconds: doors open at the bottom, going up, at the top, coming down.</summary>
        public const float LiftBottom = 3f, LiftUp = 3.5f, LiftTop = 4f, LiftDown = 3.5f;
        public const float LiftCycle = LiftBottom + LiftUp + LiftTop + LiftDown;
        /// <summary>How high the car rises, at normal size, in metres.</summary>
        public const float LiftRise = 1.4f;

        private static readonly RidePoint Instantly = new RidePoint(0f, 0f, 0f, float.PositiveInfinity);

        public static readonly PlayInfo[] All =
        {
            new PlayInfo
            {
                Kind = PartKind.Slide, Capacity = 2, Relief = 0.3f, Doing = "Sliding down the slide",
                Route = new[]
                {
                    new RidePoint(0f, 0f, -1f, 0f),
                    new RidePoint(0f, 1f, -0.9f, 0.35f),          // up the ladder
                    new RidePoint(0f, 1f, -0.7f, 0.3f, 1f),       // a look round at the top
                    new RidePoint(0f, 0.15f, 0.85f, 1.8f),        // wheee
                    new RidePoint(0f, 0f, 1.25f, 1.6f),           // shot off the end
                },
                JumpRoute = new[]
                {
                    new RidePoint(0f, 0f, -1f, 0f),
                    new RidePoint(0f, 1.25f, -0.95f, 3f),         // one big bound...
                    new RidePoint(0f, 1f, -0.7f, 2f, 0.6f),       // ...and onto the top
                    new RidePoint(0f, 0.15f, 0.85f, 1.8f),
                    new RidePoint(0f, 0f, 1.25f, 1.6f),
                },
                StickyRoute = new[]
                {
                    new RidePoint(0f, 0f, -1f, 0f),
                    new RidePoint(0f, 1f, -0.9f, 0.35f),
                    new RidePoint(0f, 1f, -0.7f, 0.3f, 1f),
                    new RidePoint(0f, 0.15f, 0.85f, 1.8f, 0.4f),
                    new RidePoint(0f, 1f, -0.7f, 0.3f, 0.6f),     // sticky toes: back up the chute the wrong way
                    new RidePoint(0f, 0.15f, 0.85f, 1.8f),        // and again!
                    new RidePoint(0f, 0f, 1.25f, 1.6f),
                },
            },
            new PlayInfo
            {
                Kind = PartKind.SpiralStairs, Capacity = 2, Relief = 0.25f, Doing = "Climbing the spiral staircase",
                Route = Spiral(),
                JumpRoute = new[]
                {
                    new RidePoint(0f, 0f, -0.45f, 0f),
                    new RidePoint(0f, 0.8f, -0.2f, 3f, 0.3f),     // never mind the steps
                    new RidePoint(0f, 1.4f, 0.3f, 3f, 1.5f),
                    new RidePoint(0f, 1.55f, 0.55f, 2f),
                    new RidePoint(0f, 0.9f, 0.95f, 2.5f),
                    new RidePoint(0f, 0f, 1.15f, 2.5f),
                },
            },
            new PlayInfo
            {
                Kind = PartKind.AirTunnel, Capacity = 1, Relief = 0.35f, Powered = true, Doing = "Whooshing through the air tunnel",
                Route = new[]
                {
                    new RidePoint(0f, 0f, -1.2f, 0f),
                    new RidePoint(0f, 0.1f, -1f, 0.6f),
                    new RidePoint(0f, 0.12f, -0.9f, 0.5f, 0.4f),  // the fan takes hold
                    new RidePoint(0f, 0.12f, 1.2f, 5f),           // whoosh
                    new RidePoint(0f, 0.35f, 1.45f, 2.5f),        // tumbling out
                    new RidePoint(0f, 0f, 1.8f, 2f),
                },
            },
            new PlayInfo
            {
                // The route only gives the entry and the landing: the car carries the frogs (see Advance).
                Kind = PartKind.Elevator, Capacity = 3, Relief = 0.3f, Powered = true, Elevator = true, Doing = "Riding the elevator",
                Route = new[] { new RidePoint(0f, 0f, -0.35f, 0f), new RidePoint(0f, 0f, 0.6f, HopSpeed) },
            },
            new PlayInfo
            {
                Kind = PartKind.Teleporter, Capacity = 1, Relief = 0.35f, Powered = true, Doing = "Teleporting",
                Route = new[]
                {
                    new RidePoint(0f, 0f, -0.35f, 0f),
                    new RidePoint(0f, 0.1f, 0f, 0.6f, 1.2f),      // charging up...
                },
                ExitRoute = new[]
                {
                    new RidePoint(0f, 0.1f, 0f, float.PositiveInfinity, 0.6f), // ...pop! out of the partner pad
                    new RidePoint(0f, 0.3f, 0.4f, 1.5f),
                    new RidePoint(0f, 0f, 0.75f, 1.5f),
                },
            },
        };

        /// <summary>One and a half turns round the post, slowly, then a leap off the front.</summary>
        private static RidePoint[] Spiral()
        {
            var route = new List<RidePoint> { new RidePoint(0f, 0f, -0.45f, 0f) };
            const int steps = 12;
            const float radius = 0.3f, top = 1.4f;
            for (int k = 0; k <= steps; k++)
            {
                // Clockwise from the back (180°) through one and a half turns to the front.
                double angle = (180.0 + 540.0 * k / steps) * Math.PI / 180.0;
                float y = 0.05f + (top - 0.05f) * k / steps;
                route.Add(new RidePoint(radius * (float)Math.Sin(angle), y, radius * (float)Math.Cos(angle), 0.35f, k == steps ? 1.5f : 0f));
            }
            route.Add(new RidePoint(0f, top + 0.15f, 0.55f, 2f));   // leap
            route.Add(new RidePoint(0f, 0.9f, 0.95f, 2.5f));
            route.Add(new RidePoint(0f, 0f, 1.15f, 2.5f));          // land
            return route.ToArray();
        }

        public static bool IsPiece(PartKind kind) => InfoOf(kind) != null;

        /// <summary>The playground details of a kind of part, or null if it is not a playground piece.</summary>
        public static PlayInfo InfoOf(PartKind kind)
        {
            foreach (var info in All) if (info.Kind == kind) return info;
            return null;
        }

        /// <summary>Where a frog gets on a piece, on the ground.</summary>
        public static void Entry(PlacedPart p, out float x, out float z)
        {
            var first = InfoOf(p.Kind).Route[0];
            Parts.ToWorld(p, first.X, first.Z, out x, out z);
        }

        /// <summary>Where a frog lands after riding a piece, on the ground (for a teleporter pad: by its partner).</summary>
        public static void Landing(PartStore parts, PlacedPart p, out float x, out float z)
        {
            var info = InfoOf(p.Kind);
            var exitPart = p;
            var route = info.Route;
            if (info.ExitRoute != null)
            {
                exitPart = PartnerOf(parts, p) ?? p;
                route = info.ExitRoute;
            }
            var last = route[route.Length - 1];
            Parts.ToWorld(exitPart, last.X, last.Z, out x, out z);
        }

        /// <summary>
        /// The other pad of a teleporter pair, or null. Pads pair up in the order they were placed:
        /// first with second, third with fourth, and so on.
        /// </summary>
        public static PlacedPart PartnerOf(PartStore parts, PlacedPart pad)
        {
            int k = 0;
            PlacedPart previous = null;
            var list = parts.PlayPieces;
            for (int n = 0; n < list.Count; n++)
            {
                var q = list[n];
                if (q.Kind != PartKind.Teleporter) continue;
                if (k % 2 == 1 && q == pad) return previous;
                if (k % 2 == 0 && q == pad)
                {
                    for (int m = n + 1; m < list.Count; m++) if (list[m].Kind == PartKind.Teleporter) return list[m];
                    return null;
                }
                previous = q;
                k++;
            }
            return null;
        }

        /// <summary>Teleporter pads without a partner (0 or 1).</summary>
        public static int UnpairedTeleporters(PartStore parts)
        {
            int n = 0;
            foreach (var p in parts.PlayPieces) if (p.Kind == PartKind.Teleporter) n++;
            return n % 2;
        }

        /// <summary>True if frogs can use this piece right now (a teleporter pad needs a partner).</summary>
        public static bool Usable(PartStore parts, PlacedPart p) => p.Kind != PartKind.Teleporter || PartnerOf(parts, p) != null;

        // ------------------------------------------------------------------ elevator

        /// <summary>Where an elevator's car is in its timetable, 0 to <see cref="LiftCycle"/> seconds. Each lift keeps its own time.</summary>
        public static float ElevatorPhase(World world, PlacedPart p)
        {
            double t = world.SimTime + p.Id * 2.3;
            return (float)(t - Math.Floor(t / LiftCycle) * LiftCycle);
        }

        /// <summary>True while the car is at the bottom with its doors open (with a moment to spare before it leaves).</summary>
        public static bool DoorsOpen(float phase) => phase < LiftBottom - 0.4f;

        /// <summary>How high the car floor is above the elevator's base, in metres.</summary>
        public static float CarHeight(float phase, float scale)
        {
            float rise = LiftRise * scale, f;
            if (phase < LiftBottom) f = 0f;
            else if (phase < LiftBottom + LiftUp) f = Smooth((phase - LiftBottom) / LiftUp);
            else if (phase < LiftBottom + LiftUp + LiftTop) f = 1f;
            else f = 1f - Smooth((phase - LiftBottom - LiftUp - LiftTop) / LiftDown);
            return rise * f;
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        // ------------------------------------------------------------------ riding

        /// <summary>How many frogs are riding a piece right now.</summary>
        public static int RidersOn(AgentStore a, int partId)
        {
            int n = 0;
            for (int i = 0; i < a.Count; i++) if (a.RidePart[i] == partId) n++;
            return n;
        }

        /// <summary>True if a piece has room for another rider.</summary>
        public static bool HasRoom(AgentStore a, PlacedPart p) => RidersOn(a, p.Id) < InfoOf(p.Kind).Capacity;

        /// <summary>How fast a frog gets bored, per second. Young and bold frogs get bored sooner.</summary>
        public static float BoredomRate(AgentStore a, int i)
        {
            float stage;
            switch (a.Stage[i])
            {
                case LifeStage.Froglet: stage = 1.4f; break;
                case LifeStage.Juvenile: stage = 1.6f; break;
                case LifeStage.Elder: stage = 0.6f; break;
                default: stage = 1f; break;
            }
            return BoredomPerSecond * stage * (0.7f + 0.6f * a.Genes[i].Boldness);
        }

        /// <summary>True for frogs with long legs: they bound up instead of climbing.</summary>
        public static bool IsBigJumper(AgentStore a, int i) => Mutations.Shows(a.Genes[i].Visible, Mutation.LongLegs);

        /// <summary>True for frogs with sticky toe pads (tree frogs) or claws: they can climb a slide the wrong way.</summary>
        public static bool HasStickyFeet(AgentStore a, int i) =>
            SpeciesCatalog.FrogOf(a, i).LikesPerches || Mutations.Shows(a.Genes[i].Visible, Mutation.Claws);

        /// <summary>
        /// How this frog rides this piece. Big jumpers always leap. A sticky-footed frog has its own
        /// habit for each slide: about half of them like to climb back up for another go.
        /// </summary>
        public static RideStyle StyleFor(AgentStore a, int i, PlacedPart p)
        {
            if (IsBigJumper(a, i)) return RideStyle.BigJump;
            if (HasStickyFeet(a, i) && BrainSystem.Favourite(a.Id[i], p.Id) < 1.5f) return RideStyle.Sticky;
            return RideStyle.Normal;
        }

        /// <summary>Puts a frog on a piece. It follows the route from its next movement step.</summary>
        public static void StartRide(World world, int i, PlacedPart p)
        {
            var a = world.Agents;
            if (InfoOf(p.Kind).Elevator)
            {
                // A squeeze: a third frog in the car makes everyone a bit cross.
                int aboard = RidersOn(a, p.Id);
                if (aboard >= 2)
                {
                    for (int j = 0; j < a.Count; j++)
                        if (a.RidePart[j] == p.Id) a.Stress[j] = Math.Min(1f, a.Stress[j] + SquabbleStress);
                    a.Stress[i] = Math.Min(1f, a.Stress[i] + SquabbleStress);
                    world.PlaySquabbles++;
                }
            }
            a.RidePart[i] = p.Id;
            a.RideTime[i] = 0f;
            a.TargetId[i] = p.Id;
            if (a.Goal[i] != Goal.Play)
            {
                a.Goal[i] = Goal.Play;
                a.GoalTime[i] = 0f;
            }
            a.VelX[i] = 0f; a.VelZ[i] = 0f;
        }

        /// <summary>
        /// Moves a riding frog along its piece's route. Called by MovementSystem instead of normal
        /// walking. Ends the ride when the frog lands (or straight away if the piece was taken away).
        /// </summary>
        public static void Advance(World world, int i, float dt, RidePath path)
        {
            var a = world.Agents;
            var p = world.Parts.Find(a.RidePart[i]);
            if (p == null || !IsPiece(p.Kind) || !Usable(world.Parts, p))
            {
                // The piece was removed or changed under the frog: it hops down where it is.
                StopRiding(world, i);
                return;
            }
            a.RideTime[i] += dt;
            var info = InfoOf(p.Kind);
            if (info.Elevator)
            {
                RideElevator(world, i, p);
                return;
            }
            path.Build(world, p, StyleFor(a, i, p));
            if (a.RideTime[i] >= path.Duration)
            {
                path.Sample(path.Duration, out float lx, out _, out float lz, out _, out _, out _);
                a.PosX[i] = Clamp(lx, -world.HalfSizeX, world.HalfSizeX);
                a.PosZ[i] = Clamp(lz, -world.HalfSizeZ, world.HalfSizeZ);
                Land(world, i, p);
                return;
            }
            path.Sample(a.RideTime[i], out float x, out float y, out float z, out float dx, out float dz, out float speed);
            a.PosX[i] = Clamp(x, -world.HalfSizeX, world.HalfSizeX);
            a.PosZ[i] = Clamp(z, -world.HalfSizeZ, world.HalfSizeZ);
            a.PosY[i] = y;
            // The velocity only points the frog the right way for drawing; MovementSystem leaves riders alone.
            float s = speed > 0.01f ? speed : 0.01f;
            a.VelX[i] = dx * s;
            a.VelZ[i] = dz * s;
        }

        /// <summary>
        /// A frog in the elevator stands in the car, wherever the car is on its timetable. It gets off
        /// once it has been up and the doors open at the bottom again.
        /// </summary>
        private static void RideElevator(World world, int i, PlacedPart p)
        {
            var a = world.Agents;
            float phase = ElevatorPhase(world, p);
            if (a.RideTime[i] > LiftCycle * 0.5f && DoorsOpen(phase))
            {
                Landing(world.Parts, p, out float lx, out float lz);
                a.PosX[i] = Clamp(lx, -world.HalfSizeX, world.HalfSizeX);
                a.PosZ[i] = Clamp(lz, -world.HalfSizeZ, world.HalfSizeZ);
                Land(world, i, p);
                return;
            }
            // Each frog has its own spot in the car.
            float f = BrainSystem.Favourite(a.Id[i], p.Id) / 3f;
            float ox = (f - 0.5f) * 0.3f, oz = ((f * 7f) % 1f - 0.5f) * 0.3f;
            Parts.ToWorld(p, ox, oz, out float x, out float z);
            a.PosX[i] = x;
            a.PosZ[i] = z;
            a.PosY[i] = world.Parts.BaseHeight(p, world.Terrain) + CarHeight(phase, p.Scale) + 0.05f;
            Parts.DirToWorld(p.Rotation, 0f, 1f, out float fx, out float fz);
            a.VelX[i] = fx * 0.01f;
            a.VelZ[i] = fz * 0.01f;
        }

        /// <summary>The end of a ride: less bored, a glow of fun, and maybe straight on to the next piece.</summary>
        private static void Land(World world, int i, PlacedPart p)
        {
            var a = world.Agents;
            var info = InfoOf(p.Kind);
            a.Play[i] = Math.Max(0f, a.Play[i] - info.Relief);
            a.Fun[i] = 1f;
            world.PlayRides[(int)p.Kind]++;
            StopRiding(world, i);
            if (a.Play[i] <= 0f) return;

            // Chains: landing at the entry of another piece carries straight on.
            float reach = ChainRadius * p.Scale;
            PlacedPart next = null;
            float best = reach * reach;
            foreach (var q in world.Parts.PlayPieces)
            {
                if (q.Id == p.Id || !Usable(world.Parts, q)) continue;
                if (InfoOf(q.Kind).Elevator && !DoorsOpen(ElevatorPhase(world, q))) continue;
                Entry(q, out float ex, out float ez);
                float d2 = (ex - a.PosX[i]) * (ex - a.PosX[i]) + (ez - a.PosZ[i]) * (ez - a.PosZ[i]);
                if (d2 < best && HasRoom(a, q)) { best = d2; next = q; }
            }
            if (next != null)
            {
                StartRide(world, i, next);
                world.PlayChains++;
            }
        }

        /// <summary>Takes a frog off its piece and puts it back on the ground.</summary>
        public static void StopRiding(World world, int i)
        {
            var a = world.Agents;
            a.RidePart[i] = 0;
            a.RideTime[i] = 0f;
            a.TargetId[i] = 0;
            a.VelX[i] = 0f; a.VelZ[i] = 0f;
            a.PosY[i] = MovementSystem.SurfaceHeight(world.Terrain, a.PosX[i], a.PosZ[i]);
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }

    /// <summary>
    /// A ride route turned into tank space for one placed piece: a hop up from the ground to the
    /// piece's base, the piece's own route, and a drop to the ground at the end if the piece stands
    /// on something. A teleporter's route carries on at its partner pad. Reused between frogs, so
    /// riding makes no garbage. (Elevators are not routes: their car carries the frogs.)
    /// </summary>
    public sealed class RidePath
    {
        private const int Max = 40;
        private readonly float[] _x = new float[Max], _y = new float[Max], _z = new float[Max];
        /// <summary>Seconds to travel to each point from the previous one, and to wait there.</summary>
        private readonly float[] _move = new float[Max], _wait = new float[Max];
        private int _count;

        /// <summary>How long the whole ride takes, in seconds.</summary>
        public float Duration { get; private set; }
        public int Count => _count;

        public void Build(World world, PlacedPart p, RideStyle style = RideStyle.Normal)
        {
            var info = Playground.InfoOf(p.Kind);
            var route = info.RouteFor(style);
            _count = 0;
            Duration = 0f;
            Parts.ToWorld(p, route[0].X, route[0].Z, out float ex, out float ez);
            Add(ex, MovementSystem.SurfaceHeight(world.Terrain, ex, ez), ez, 0f, 0f);
            AddRoute(world, p, route);
            if (info.ExitRoute != null)
            {
                var partner = Playground.PartnerOf(world.Parts, p);
                if (partner != null) AddRoute(world, partner, info.ExitRoute);
            }
            float lx = _x[_count - 1], lz = _z[_count - 1];
            Add(lx, MovementSystem.SurfaceHeight(world.Terrain, lx, lz), lz, Playground.HopSpeed, 0f);
        }

        private void AddRoute(World world, PlacedPart p, RidePoint[] route)
        {
            float baseY = world.Parts.BaseHeight(p, world.Terrain);
            foreach (var pt in route)
            {
                Parts.ToWorld(p, pt.X, pt.Z, out float wx, out float wz);
                Add(wx, baseY + pt.Y * p.Scale, wz, pt.Speed > 0f ? pt.Speed : Playground.HopSpeed, pt.Wait);
            }
        }

        private void Add(float x, float y, float z, float speed, float wait)
        {
            if (_count == Max) return;
            int k = _count++;
            _x[k] = x; _y[k] = y; _z[k] = z;
            float move = 0f;
            if (k > 0 && !float.IsPositiveInfinity(speed))
            {
                float dx = x - _x[k - 1], dy = y - _y[k - 1], dz = z - _z[k - 1];
                move = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / speed;
            }
            _move[k] = move;
            _wait[k] = wait;
            Duration += move + wait;
        }

        /// <summary>Where the frog is at a time into the ride, which way it is heading on the ground and how fast.</summary>
        public void Sample(float t, out float x, out float y, out float z, out float dirX, out float dirZ, out float speed)
        {
            dirX = 0f; dirZ = 1f; speed = 0f;
            for (int k = 1; k < _count; k++)
            {
                float hx = _x[k] - _x[k - 1], hz = _z[k] - _z[k - 1];
                float flat = (float)Math.Sqrt(hx * hx + hz * hz);
                if (_move[k] > 0f && flat > 1e-4f) { dirX = hx / flat; dirZ = hz / flat; }
                if (t < _move[k])
                {
                    float f = t / _move[k];
                    x = _x[k - 1] + (_x[k] - _x[k - 1]) * f;
                    y = _y[k - 1] + (_y[k] - _y[k - 1]) * f;
                    z = _z[k - 1] + (_z[k] - _z[k - 1]) * f;
                    float dy = _y[k] - _y[k - 1];
                    speed = (float)Math.Sqrt(flat * flat + dy * dy) / _move[k];
                    return;
                }
                t -= _move[k];
                if (t < _wait[k])
                {
                    x = _x[k]; y = _y[k]; z = _z[k];
                    return;
                }
                t -= _wait[k];
            }
            x = _x[_count - 1]; y = _y[_count - 1]; z = _z[_count - 1];
        }

        /// <summary>A point of the path (for tests and drawing).</summary>
        public void Point(int k, out float x, out float y, out float z)
        {
            x = _x[k]; y = _y[k]; z = _z[k];
        }
    }
}

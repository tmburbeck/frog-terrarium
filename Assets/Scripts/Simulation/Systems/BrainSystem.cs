using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// The agents' decision making, 10 times per second. For every agent:
    /// 1. the utility picker chooses a goal (eat, drink, sleep, find a comfier spot, wander, flee);
    /// 2. that goal's behaviour tree decides what to do this tick, usually by setting a velocity
    ///    that MovementSystem then follows.
    ///
    /// Frogs are sit-and-wait hunters: they hop toward insects they can sense, then creep the last
    /// stretch slowly, because crickets only notice a frog that moves fast. Crickets wander and run
    /// from fast-moving frogs.
    ///
    /// Females carrying eggs head for water to lay them. Juveniles move away from crowded spots to
    /// find room of their own. A frog whose stress reaches breaking point heads for the nearest tank
    /// wall and tries to climb out; if its stress falls again before it makes it, it gives up.
    /// Tadpoles swim about in the water; eggs just sit there.
    /// </summary>
    public sealed class BrainSystem : ISimSystem
    {
        public const int Rate = 10;
        /// <summary>A frog moving slower than this does not scare crickets.</summary>
        public const float StealthSpeed = 0.4f;
        /// <summary>Frogs creep within this distance of their prey.</summary>
        public const float CreepDistance = 1.4f;
        public const float StrikeSuccess = 0.75f;
        /// <summary>Seconds a frog spends at the wall before each attempt to climb out.</summary>
        public const float ClimbSeconds = 8f;
        /// <summary>Chance that each climbing attempt succeeds.</summary>
        public const float ClimbSuccess = 0.35f;
        /// <summary>An escaping frog gives up once its stress has fallen to this.</summary>
        public const float CalmDownStress = 0.8f;

        public string Name => "Brain";
        public int TicksPerSecond => Rate;

        public SpatialHash Hash { get; } = new SpatialHash();
        public UtilityGoalPicker FrogPicker { get; }
        public UtilityGoalPicker InsectPicker { get; }
        public UtilityGoalPicker TadpolePicker { get; }

        private readonly Dictionary<Goal, BtNode> _frogTrees = new Dictionary<Goal, BtNode>();
        private readonly Dictionary<Goal, BtNode> _insectTrees = new Dictionary<Goal, BtNode>();
        private readonly Dictionary<Goal, BtNode> _tadpoleTrees = new Dictionary<Goal, BtNode>();
        private readonly AgentContext _ctx = new AgentContext();
        private readonly Dictionary<int, int> _indexOfId = new Dictionary<int, int>();
        private readonly List<float> _waterX = new List<float>(), _waterZ = new List<float>();
        private int _waterTerrainVersion = -1;
        private World _waterWorld;
        private bool[] _removed = new bool[0];

        /// <summary>Insects eaten since the simulation started (handy for tests and the overlay).</summary>
        public int InsectsEaten { get; private set; }
        /// <summary>Frogs that climbed out since the simulation started.</summary>
        public int Escapes { get; private set; }

        public BrainSystem()
        {
            _ctx.IndexOfId = id => _indexOfId.TryGetValue(id, out int idx) ? idx : -1;
            _ctx.WaterPointsX = _waterX;
            _ctx.WaterPointsZ = _waterZ;

            FrogPicker = new UtilityGoalPicker()
                .Add(Goal.Eat, ScoreEat)
                .Add(Goal.Drink, ScoreDrink)
                .Add(Goal.Sleep, ScoreSleep)
                .Add(Goal.SeekComfort, ScoreComfort)
                .Add(Goal.LayEggs, ScoreLayEggs)
                .Add(Goal.Disperse, ScoreDisperse)
                .Add(Goal.Escape, ScoreEscape)
                .Add(Goal.Wander, c => 0.15f);
            InsectPicker = new UtilityGoalPicker { MinCommitSeconds = 0.5f }
                .Add(Goal.Flee, ScoreFlee)
                .Add(Goal.Wander, c => 0.2f);
            TadpolePicker = new UtilityGoalPicker()
                .Add(Goal.Wander, c => 0.2f);
            BuildTrees();
        }

        /// <summary>Prepares the context for one agent outside a tick (used by the inspector to show scores).</summary>
        public AgentContext ContextFor(World world, int index)
        {
            _ctx.World = world;
            _ctx.Agents = world.Agents;
            _ctx.Index = index;
            _ctx.Dt = 1f / Rate;
            _ctx.Hash = Hash;
            if (_removed.Length < world.Agents.Count) _removed = new bool[world.Agents.Capacity];
            _ctx.Removed = _removed;
            return _ctx;
        }

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            Hash.Build(a, world.HalfSizeX, world.HalfSizeZ);
            _indexOfId.Clear();
            for (int i = 0; i < a.Count; i++) _indexOfId[a.Id[i]] = i;
            RefreshWaterPoints(world);
            if (_removed.Length < a.Capacity) _removed = new bool[a.Capacity];
            Array.Clear(_removed, 0, a.Count);

            _ctx.World = world;
            _ctx.Agents = a;
            _ctx.Dt = dt;
            _ctx.Random = world.Random.Get(Name);
            _ctx.Hash = Hash;
            _ctx.Removed = _removed;

            int count = a.Count;
            for (int i = 0; i < count; i++)
            {
                if (_removed[i]) continue;
                _ctx.Index = i;
                if (a.Cooldown[i] > 0f) a.Cooldown[i] -= dt;
                if (a.Timer[i] > 0f) a.Timer[i] -= dt;

                if (a.Kind[i] == AgentKind.Frog && a.Stage[i] == LifeStage.Egg)
                {
                    a.Goal[i] = Goal.Idle;
                    a.VelX[i] = 0f; a.VelZ[i] = 0f;
                    continue;
                }
                var picker = PickerFor(a, i);
                var goal = picker.Pick(_ctx);
                if (goal != a.Goal[i])
                {
                    a.Goal[i] = goal;
                    a.GoalTime[i] = 0f;
                    a.TargetId[i] = 0;
                    a.Timer[i] = 0f;
                }
                a.GoalTime[i] += dt;
                var trees = picker == FrogPicker ? _frogTrees : picker == TadpolePicker ? _tadpoleTrees : _insectTrees;
                if (trees.TryGetValue(goal, out var tree)) tree.Tick(_ctx);
                else Stop(a, i);
            }

            for (int i = count - 1; i >= 0; i--)
                if (_removed[i]) a.RemoveAt(i);
        }

        /// <summary>The goal picker that applies to this agent (frog, tadpole or insect).</summary>
        public UtilityGoalPicker PickerFor(AgentStore a, int i)
        {
            if (a.Kind[i] == AgentKind.Insect) return InsectPicker;
            return a.Stage[i] <= LifeStage.Tadpole ? TadpolePicker : FrogPicker;
        }

        private void RefreshWaterPoints(World world)
        {
            if (world == _waterWorld && world.Terrain.Version == _waterTerrainVersion) return;
            _waterWorld = world;
            _waterTerrainVersion = world.Terrain.Version;
            _waterX.Clear();
            _waterZ.Clear();
            var t = world.Terrain;
            // Sample every other terrain point: plenty for finding the nearest pool. Prefer spots a
            // little way in from the shore, so a frog that stops just short still ends up in the water.
            for (int pass = 0; pass < 2 && _waterX.Count == 0; pass++)
            {
                float minDepth = pass == 0 ? 0.1f : 0.02f;
                for (int pz = 0; pz <= t.CellsZ; pz += 2)
                    for (int px = 0; px <= t.CellsX; px += 2)
                    {
                        float x = t.PointX(px), z = t.PointZ(pz);
                        if (t.WaterDepthAt(x, z) > minDepth) { _waterX.Add(x); _waterZ.Add(z); }
                    }
            }
        }

        // ---------------------------------------------------------------- scores

        private static float Curve(float need, float threshold, float weight)
        {
            if (need < threshold) return 0f;
            return weight * (float)Math.Pow(need, 1.3);
        }

        private static float ScoreEat(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            float s = Curve(a.Hunger[i], 0.2f, 1.1f);
            // Keep eating until properly full, not just until the urge drops.
            if (a.Goal[i] == Goal.Eat && a.Hunger[i] > 0.1f && s < 0.4f) s = 0.4f;
            return s;
        }

        private static float ScoreDrink(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            float s = Curve(a.Thirst[i], 0.2f, 1.2f);
            if (a.Goal[i] == Goal.Drink && a.Thirst[i] > 0.05f && s < 0.4f) s = 0.4f;
            return s;
        }

        private static float ScoreSleep(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            float f = a.Fatigue[i];
            if (a.Goal[i] == Goal.Sleep && f > 0.02f) return 0.6f; // stay asleep unless something is urgent
            return f < 0.3f ? 0f : 1.3f * f * f;
        }

        private static float ScoreComfort(AgentContext c)
        {
            float comfort = c.Agents.Comfort[c.Index];
            return comfort >= 0.85f ? 0f : (1f - comfort) * 0.9f;
        }

        private static float ScoreLayEggs(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            return a.MateId[i] != 0 ? 0.7f : 0f;
        }

        private static float ScoreEscape(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (a.Stress[i] >= 1f) return 2f;
            if (a.Goal[i] == Goal.Escape && a.Stress[i] > CalmDownStress) return 2f;
            return 0f;
        }

        private static float ScoreDisperse(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (a.Stage[i] != LifeStage.Juvenile) return 0f;
            if (a.Goal[i] == Goal.Disperse && a.Timer[i] > 0f) return 0.3f; // keep going to the spot it picked
            int near = c.Hash.CountWithin(a, a.PosX[i], a.PosZ[i], 1.5f, AgentKind.Frog, i, SpatialHash.LandFrogs);
            return near >= 2 ? 0.3f : 0f;
        }

        private static float ScoreFlee(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (a.Goal[i] == Goal.Flee && a.GoalTime[i] < 1.5f) return 0.8f;
            return ThreateningFrog(c) >= 0 ? 1f : 0f;
        }

        /// <summary>Index of a fast-moving frog close enough to scare this insect, or -1.</summary>
        private static int ThreateningFrog(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            var sp = SpeciesCatalog.Insect;
            int f = c.Hash.FindNearest(a, a.PosX[i], a.PosZ[i], sp.FleeRadius, AgentKind.Frog, -1, null, SpatialHash.LandFrogs);
            if (f < 0) return -1;
            float v2 = a.VelX[f] * a.VelX[f] + a.VelZ[f] * a.VelZ[f];
            return v2 > StealthSpeed * StealthSpeed ? f : -1;
        }

        // ---------------------------------------------------------------- trees

        private void BuildTrees()
        {
            _frogTrees[Goal.Eat] = BtNode.Selector("Eat",
                BtNode.Sequence("Hunt",
                    BtNode.Condition("Has prey", AcquirePrey),
                    BtNode.Selector("Catch",
                        BtNode.Sequence("Strike",
                            BtNode.Condition("In tongue reach", InTongueReach),
                            BtNode.Action("Flick tongue", Strike)),
                        BtNode.Action("Stalk", Stalk))),
                BtNode.Action("Search for insects", c => Wander(c, Sp(c).TravelSpeed * Small(c), 0.1f)));

            _frogTrees[Goal.Drink] = BtNode.Selector("Drink",
                BtNode.Sequence("Soak",
                    BtNode.Condition("In water", InWater),
                    BtNode.Action("Sit and soak", SitStill)),
                BtNode.Sequence("Go to water",
                    BtNode.Condition("Knows where water is", FindWater),
                    BtNode.Action("Hop to water", c => GoToTarget(c, Sp(c).TravelSpeed * Small(c), 0.02f))),
                BtNode.Action("Search for water", c => Wander(c, Sp(c).TravelSpeed * Small(c), 0.1f)));

            _frogTrees[Goal.LayEggs] = BtNode.Selector("Lay eggs",
                BtNode.Sequence("Lay",
                    BtNode.Condition("In water", InWater),
                    BtNode.Action("Lay eggs here", SitStill)),
                BtNode.Sequence("Go to water",
                    BtNode.Condition("Knows where water is", FindWater),
                    BtNode.Action("Hop to water", c => GoToTarget(c, Sp(c).TravelSpeed, 0.02f))),
                BtNode.Action("Search for water", c => Wander(c, Sp(c).TravelSpeed, 0.1f)));

            _frogTrees[Goal.Sleep] = BtNode.Action("Sleep", SitStill);

            _frogTrees[Goal.SeekComfort] = BtNode.Selector("Seek comfort",
                BtNode.Sequence("Move somewhere better",
                    BtNode.Condition("Found a comfier spot", FindComfierSpot),
                    BtNode.Action("Go there", c => GoToTarget(c, Sp(c).TravelSpeed * Small(c), 0.3f))),
                BtNode.Action("Sit tight", SitStill));

            _frogTrees[Goal.Disperse] = BtNode.Selector("Find own space",
                BtNode.Sequence("Move somewhere roomier",
                    BtNode.Condition("Found a roomier spot", FindRoomierSpot),
                    BtNode.Action("Go there", c => GoToTarget(c, Sp(c).TravelSpeed * Small(c), 0.3f))),
                BtNode.Action("Wander off", c => Wander(c, Sp(c).WanderSpeed * Small(c), 0.1f)));

            _frogTrees[Goal.Escape] = BtNode.Action("Climb out", TryToEscape);

            _frogTrees[Goal.Wander] = BtNode.Action("Explore", c => Wander(c, Sp(c).WanderSpeed * Small(c), 0.45f));

            _tadpoleTrees[Goal.Wander] = BtNode.Action("Swim", Swim);

            _insectTrees[Goal.Flee] = BtNode.Action("Run away", Flee);
            _insectTrees[Goal.Wander] = BtNode.Action("Wander", c => Wander(c, SpeciesCatalog.Insect.WanderSpeed, 0.4f));
        }

        /// <summary>The species of the agent being thought about.</summary>
        private static FrogSpecies Sp(AgentContext c) => SpeciesCatalog.FrogOf(c.Agents.Species[c.Index]);

        /// <summary>Small young frogs move more slowly: half speed as a froglet, full speed when grown.</summary>
        private static float Small(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            return 0.5f + 0.5f * Sp(c).SizeFraction(a.Growth[i]);
        }

        private static bool InWater(AgentContext c) => c.World.Terrain.IsWater(c.Agents.PosX[c.Index], c.Agents.PosZ[c.Index]);

        private static BtStatus SitStill(AgentContext c)
        {
            Stop(c.Agents, c.Index);
            return BtStatus.Running;
        }

        /// <summary>
        /// Heads for the nearest tank wall and tries to climb out: after a few seconds at the wall,
        /// each attempt may succeed. A frog that gets out is remembered in World.Escaped.
        /// </summary>
        private BtStatus TryToEscape(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            NearestWallPoint(c.World, a.PosX[i], a.PosZ[i], out float wx, out float wz);
            float dx = wx - a.PosX[i], dz = wz - a.PosZ[i];
            if (dx * dx + dz * dz > 0.3f * 0.3f)
            {
                MoveToward(a, i, wx, wz, Sp(c).TravelSpeed * Small(c));
                a.Timer[i] = ClimbSeconds;
                return BtStatus.Running;
            }
            Face(a, i, wx, wz); // standing at the wall, trying to climb
            if (a.Timer[i] > 0f) return BtStatus.Running;
            if (c.Random.Chance(ClimbSuccess))
            {
                c.World.Escape(i, AlertSystem.ProblemWords(c.World, i));
                c.Removed[i] = true;
                Escapes++;
                return BtStatus.Success;
            }
            a.Timer[i] = ClimbSeconds * 0.5f; // slipped back down; try again
            return BtStatus.Running;
        }

        /// <summary>The closest point just inside the tank wall.</summary>
        public static void NearestWallPoint(World world, float x, float z, out float wx, out float wz)
        {
            const float inset = 0.1f;
            float toLeft = x + world.HalfSizeX, toRight = world.HalfSizeX - x;
            float toBack = z + world.HalfSizeZ, toFront = world.HalfSizeZ - z;
            float best = toLeft; wx = -world.HalfSizeX + inset; wz = z;
            if (toRight < best) { best = toRight; wx = world.HalfSizeX - inset; wz = z; }
            if (toBack < best) { best = toBack; wx = x; wz = -world.HalfSizeZ + inset; }
            if (toFront < best) { wx = x; wz = world.HalfSizeZ - inset; }
        }

        /// <summary>Picks a nearby dry spot with as few other frogs around as possible.</summary>
        private static bool FindRoomierSpot(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (a.Timer[i] > 0f && a.TargetId[i] == -1) return true; // still heading to the spot found earlier
            var world = c.World;
            int here = c.Hash.CountWithin(a, a.PosX[i], a.PosZ[i], 1.5f, AgentKind.Frog, i, SpatialHash.LandFrogs);
            int best = here;
            bool found = false;
            float start = c.Random.Range(0f, 2f * (float)Math.PI);
            for (int k = 0; k < 10; k++)
            {
                float ang = start + k * (float)Math.PI / 5f, r = 3f + (k % 3);
                float x = a.PosX[i] + (float)Math.Cos(ang) * r, z = a.PosZ[i] + (float)Math.Sin(ang) * r;
                if (!world.Terrain.Contains(x, z) || world.Terrain.IsWater(x, z)) continue;
                int n = c.Hash.CountWithin(a, x, z, 1.5f, AgentKind.Frog, i, SpatialHash.LandFrogs);
                if (n < best) { best = n; found = true; a.TargetX[i] = x; a.TargetZ[i] = z; }
            }
            a.TargetId[i] = found ? -1 : 0;
            a.Timer[i] = found ? 10f : 3f;
            return found;
        }

        /// <summary>Tadpoles swim to random nearby spots that are still in the water.</summary>
        private static BtStatus Swim(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            var t = c.World.Terrain;
            float dx = a.TargetX[i] - a.PosX[i], dz = a.TargetZ[i] - a.PosZ[i];
            if (a.Timer[i] <= 0f || dx * dx + dz * dz < 0.01f)
            {
                var rng = c.Random;
                a.TargetX[i] = a.PosX[i];
                a.TargetZ[i] = a.PosZ[i];
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    float ang = rng.Range(0f, 2f * (float)Math.PI), dist = rng.Range(0.3f, 1.2f);
                    float x = a.PosX[i] + (float)Math.Cos(ang) * dist, z = a.PosZ[i] + (float)Math.Sin(ang) * dist;
                    if (t.Contains(x, z) && t.IsWater(x, z)) { a.TargetX[i] = x; a.TargetZ[i] = z; break; }
                }
                a.Timer[i] = rng.Range(1.5f, 4f);
            }
            dx = a.TargetX[i] - a.PosX[i]; dz = a.TargetZ[i] - a.PosZ[i];
            if (dx * dx + dz * dz < 0.01f) Stop(a, i);
            else MoveToward(a, i, a.TargetX[i], a.TargetZ[i], 0.25f);
            return BtStatus.Running;
        }

        private static bool AcquirePrey(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            var sp = Sp(c);
            if (a.TargetId[i] != 0)
            {
                int t = c.IndexOfId(a.TargetId[i]);
                if (t >= 0 && !c.Removed[t] && DistanceSq(a, i, t) < sp.SenseRadius * sp.SenseRadius * 1.7f) return true;
                a.TargetId[i] = 0;
            }
            int prey = c.Hash.FindNearest(a, a.PosX[i], a.PosZ[i], sp.SenseRadius, AgentKind.Insect, i, c.Removed);
            if (prey < 0) return false;
            a.TargetId[i] = a.Id[prey];
            return true;
        }

        private static bool InTongueReach(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            int t = c.IndexOfId(a.TargetId[i]);
            float reach = Sp(c).TongueReach;
            return t >= 0 && a.Cooldown[i] <= 0f && DistanceSq(a, i, t) <= reach * reach;
        }

        private BtStatus Strike(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            int t = c.IndexOfId(a.TargetId[i]);
            Stop(a, i);
            a.Cooldown[i] = Sp(c).StrikeCooldown;
            if (t < 0) return BtStatus.Failure;
            Face(a, i, a.PosX[t], a.PosZ[t]);
            if (!c.Random.Chance(StrikeSuccess)) return BtStatus.Running; // missed; try again after the cooldown
            c.Removed[t] = true;
            a.Hunger[i] = Math.Max(0f, a.Hunger[i] - Sp(c).MealSize / NeedsSystem.AppetiteFor(a.Stage[i]));
            a.TargetId[i] = 0;
            InsectsEaten++;
            return BtStatus.Success;
        }

        private static BtStatus Stalk(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            int t = c.IndexOfId(a.TargetId[i]);
            if (t < 0) return BtStatus.Failure;
            float d = (float)Math.Sqrt(DistanceSq(a, i, t));
            var sp = Sp(c);
            // Hop closer while far away, then creep slowly so the cricket does not notice.
            float speed = d > CreepDistance ? sp.HuntSpeed * Small(c) : StealthSpeed * 0.75f;
            if (a.Cooldown[i] > 0f && d <= sp.TongueReach) speed = 0f;
            MoveToward(a, i, a.PosX[t], a.PosZ[t], speed);
            return BtStatus.Running;
        }

        private static bool FindWater(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (c.WaterPointsX.Count == 0) return false;
            if (a.Timer[i] > 0f) return true; // keep the water spot picked recently
            float best = float.MaxValue;
            for (int k = 0; k < c.WaterPointsX.Count; k++)
            {
                float dx = c.WaterPointsX[k] - a.PosX[i], dz = c.WaterPointsZ[k] - a.PosZ[i];
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; a.TargetX[i] = c.WaterPointsX[k]; a.TargetZ[i] = c.WaterPointsZ[k]; }
            }
            a.Timer[i] = 5f;
            return true;
        }

        private static bool FindComfierSpot(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (a.Timer[i] > 0f && a.TargetId[i] == -1) return true; // still heading to the spot found earlier
            var world = c.World;
            var sp = Sp(c);
            var genes = a.Genes[i];
            // Count crowding as well as climate, or every frog heads for the same perfect spot and they all end up crammed together.
            float here = ComfortAt(world, sp, genes, a.PosX[i], a.PosZ[i]) - CrowdingAt(c, a.PosX[i], a.PosZ[i]);
            float bestScore = here + 0.05f;
            bool found = false;
            for (int ring = 1; ring <= 3; ring++)
            {
                float r = 1.5f * ring;
                for (int k = 0; k < 12; k++)
                {
                    float ang = (k + 0.5f * ring) * (float)Math.PI / 6f;
                    float x = a.PosX[i] + (float)Math.Cos(ang) * r, z = a.PosZ[i] + (float)Math.Sin(ang) * r;
                    if (!world.Terrain.Contains(x, z) || world.Terrain.IsWater(x, z)) continue;
                    float score = ComfortAt(world, sp, genes, x, z) - CrowdingAt(c, x, z) - 0.01f * r; // prefer closer spots
                    if (score > bestScore) { bestScore = score; found = true; a.TargetX[i] = x; a.TargetZ[i] = z; }
                }
            }
            a.TargetId[i] = found ? -1 : 0;
            a.Timer[i] = found ? 8f : 3f;
            return found;
        }

        /// <summary>How much crowding would cost this frog's comfort at a spot (see NeedsSystem).</summary>
        private static float CrowdingAt(AgentContext c, float x, float z)
        {
            int n = c.Hash.CountWithin(c.Agents, x, z, NeedsSystem.CrowdRadius, AgentKind.Frog, c.Index, SpatialHash.LandFrogs);
            return n > 2 ? NeedsSystem.CrowdPenalty * (n - 2) : 0f;
        }

        public static float ComfortAt(World world, FrogSpecies sp, float x, float z) =>
            sp.ComfortFor(world.Environment.TemperatureAt(x, z), world.Environment.HumidityAt(x, z));

        public static float ComfortAt(World world, FrogSpecies sp, in Genome genes, float x, float z) =>
            sp.ComfortFor(world.Environment.TemperatureAt(x, z), world.Environment.HumidityAt(x, z), genes);

        private static BtStatus GoToTarget(AgentContext c, float speed, float arriveDistance)
        {
            var a = c.Agents; int i = c.Index;
            float dx = a.TargetX[i] - a.PosX[i], dz = a.TargetZ[i] - a.PosZ[i];
            if (dx * dx + dz * dz <= arriveDistance * arriveDistance)
            {
                Stop(a, i);
                a.Timer[i] = 0f;
                return BtStatus.Success;
            }
            MoveToward(a, i, a.TargetX[i], a.TargetZ[i], speed);
            return BtStatus.Running;
        }

        /// <summary>Walks to random nearby spots, pausing now and then.</summary>
        private static BtStatus Wander(AgentContext c, float speed, float pauseChance)
        {
            var a = c.Agents; int i = c.Index;
            var t = c.World.Terrain;
            float dx = a.TargetX[i] - a.PosX[i], dz = a.TargetZ[i] - a.PosZ[i];
            bool arrived = dx * dx + dz * dz < 0.04f;
            bool moving = a.VelX[i] * a.VelX[i] + a.VelZ[i] * a.VelZ[i] > 1e-4f;
            if (a.Timer[i] <= 0f || (arrived && moving))
            {
                var rng = c.Random;
                if (rng.Chance(pauseChance))
                {
                    a.TargetX[i] = a.PosX[i];
                    a.TargetZ[i] = a.PosZ[i];
                }
                else
                {
                    for (int attempt = 0; attempt < 4; attempt++)
                    {
                        float ang = rng.Range(0f, 2f * (float)Math.PI), dist = rng.Range(0.8f, 3f);
                        float x = a.PosX[i] + (float)Math.Cos(ang) * dist, z = a.PosZ[i] + (float)Math.Sin(ang) * dist;
                        x = TerrainGrid.Clamp(x, -t.HalfSizeX + 0.2f, t.HalfSizeX - 0.2f);
                        z = TerrainGrid.Clamp(z, -t.HalfSizeZ + 0.2f, t.HalfSizeZ - 0.2f);
                        a.TargetX[i] = x;
                        a.TargetZ[i] = z;
                        if (!t.IsWater(x, z)) break;
                    }
                }
                a.Timer[i] = rng.Range(2f, 6f);
            }
            dx = a.TargetX[i] - a.PosX[i]; dz = a.TargetZ[i] - a.PosZ[i];
            if (dx * dx + dz * dz < 0.04f) Stop(a, i);
            else MoveToward(a, i, a.TargetX[i], a.TargetZ[i], speed);
            return BtStatus.Running;
        }

        private static BtStatus Flee(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            var sp = SpeciesCatalog.Insect;
            int f = c.Hash.FindNearest(a, a.PosX[i], a.PosZ[i], sp.FleeRadius * 2f, AgentKind.Frog, -1, null, SpatialHash.LandFrogs);
            if (f < 0) { Stop(a, i); return BtStatus.Failure; }
            float dx = a.PosX[i] - a.PosX[f], dz = a.PosZ[i] - a.PosZ[f];
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f) { dx = 1f; dz = 0f; len = 1f; }
            // Run away with a little zig-zag, which also scatters a group of crickets.
            float jitter = c.Random.Range(-0.6f, 0.6f);
            float ux = dx / len, uz = dz / len;
            float rx = ux - uz * jitter, rz = uz + ux * jitter;
            float rl = (float)Math.Sqrt(rx * rx + rz * rz);
            a.VelX[i] = rx / rl * sp.FleeSpeed;
            a.VelZ[i] = rz / rl * sp.FleeSpeed;
            a.TargetX[i] = a.PosX[i] + a.VelX[i];
            a.TargetZ[i] = a.PosZ[i] + a.VelZ[i];
            return BtStatus.Running;
        }

        // ---------------------------------------------------------------- helpers

        private static float DistanceSq(AgentStore a, int i, int j)
        {
            float dx = a.PosX[j] - a.PosX[i], dz = a.PosZ[j] - a.PosZ[i];
            return dx * dx + dz * dz;
        }

        private static void MoveToward(AgentStore a, int i, float x, float z, float speed)
        {
            float dx = x - a.PosX[i], dz = z - a.PosZ[i];
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f || speed <= 0f) { Stop(a, i); return; }
            // Do not overshoot: arrive within one movement tick.
            float maxSpeed = len * MovementSystem.Rate;
            if (speed > maxSpeed) speed = maxSpeed;
            a.VelX[i] = dx / len * speed;
            a.VelZ[i] = dz / len * speed;
        }

        /// <summary>Stands still. A tiny velocity is kept so the agent still faces the same way.</summary>
        private static void Stop(AgentStore a, int i)
        {
            float len = (float)Math.Sqrt(a.VelX[i] * a.VelX[i] + a.VelZ[i] * a.VelZ[i]);
            if (len < 1e-6f) return;
            a.VelX[i] = a.VelX[i] / len * 1e-4f;
            a.VelZ[i] = a.VelZ[i] / len * 1e-4f;
        }

        private static void Face(AgentStore a, int i, float x, float z)
        {
            float dx = x - a.PosX[i], dz = z - a.PosZ[i];
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-6f) return;
            a.VelX[i] = dx / len * 1e-4f;
            a.VelZ[i] = dz / len * 1e-4f;
        }
    }
}

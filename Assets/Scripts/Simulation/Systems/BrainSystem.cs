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
    ///
    /// Social life: frogs of sociable species that feel lonely go and find their own kind. Adult
    /// males of territorial species potter about inside their own patch and chase off rival males
    /// of their species; the two wrestle, and the loser backs off for a few seconds. Froglets and
    /// juveniles keep clear of big frogs of other species, which might eat them.
    ///
    /// To save time in a big tank, agents far from where the player is looking only think on one
    /// tick in three (they keep doing what they were doing in between).
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
        /// <summary>A lonely frog looks for its own kind within this distance (metres).</summary>
        public const float CompanySearchRadius = 8f;
        /// <summary>A territory owner notices a rival within this distance of himself.</summary>
        public const float RivalSightRadius = 3f;
        /// <summary>Seconds a frog that lost a dispute keeps away.</summary>
        public const float RetreatSeconds = 6f;
        /// <summary>Stress a frog gains from losing a dispute.</summary>
        public const float DefeatStress = 0.1f;
        /// <summary>Young frogs keep away from big frogs of other species within this distance.</summary>
        public const float BigFrogFear = 1f;
        /// <summary>Far from where the player is looking, agents think on only one brain tick in this many.</summary>
        public const int FarThinkEvery = 3;

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
        /// <summary>Territorial disputes since the simulation started.</summary>
        public int Disputes { get; private set; }
        /// <summary>Disputes the intruder won, taking over the territory.</summary>
        public int Takeovers { get; private set; }
        /// <summary>Agents that skipped thinking on the last tick because they were far from the player's view.</summary>
        public int FarAgents { get; private set; }

        private int _tick;
        private readonly Func<AgentStore, int, bool> _rival, _company, _bigOtherFrog;
        private byte _wantSpecies;
        private float _wantBiggerThan;

        public BrainSystem()
        {
            _ctx.IndexOfId = id => _indexOfId.TryGetValue(id, out int idx) ? idx : -1;
            _ctx.WaterPointsX = _waterX;
            _ctx.WaterPointsZ = _waterZ;
            _rival = (s, j) => s.Stage[j] == LifeStage.Adult && s.Sex[j] == Sex.Male && s.Species[j] == _wantSpecies && s.Retreat[j] <= 0f;
            _company = (s, j) => s.Stage[j] >= LifeStage.Froglet && s.Species[j] == _wantSpecies;
            _bigOtherFrog = (s, j) => s.Stage[j] >= LifeStage.Adult && s.Species[j] != _wantSpecies && BodySize(s, j) > _wantBiggerThan;

            FrogPicker = new UtilityGoalPicker()
                .Add(Goal.Eat, ScoreEat)
                .Add(Goal.Drink, ScoreDrink)
                .Add(Goal.Sleep, ScoreSleep)
                .Add(Goal.SeekComfort, ScoreComfort)
                .Add(Goal.LayEggs, ScoreLayEggs)
                .Add(Goal.Disperse, ScoreDisperse)
                .Add(Goal.Escape, ScoreEscape)
                .Add(Goal.Socialise, ScoreSocialise)
                .Add(Goal.Defend, ScoreDefend)
                .Add(Goal.Retreat, ScoreRetreat)
                .Add(Goal.Flee, ScoreFrogFlee)
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

            _tick++;
            bool lod = world.HasFocus;
            float fx = world.FocusX, fz = world.FocusZ, fr2 = world.FocusRadius * world.FocusRadius;
            int far = 0;

            int count = a.Count;
            for (int i = 0; i < count; i++)
            {
                if (_removed[i]) continue;
                _ctx.Index = i;
                if (a.Cooldown[i] > 0f) a.Cooldown[i] -= dt;
                if (a.Timer[i] > 0f) a.Timer[i] -= dt;
                if (a.Retreat[i] > 0f) a.Retreat[i] -= dt;

                if (lod && (_tick + a.Id[i]) % FarThinkEvery != 0)
                {
                    float dx = a.PosX[i] - fx, dz = a.PosZ[i] - fz;
                    if (dx * dx + dz * dz > fr2)
                    {
                        // Out of view: keep doing the same thing and think again on a later tick.
                        far++;
                        a.GoalTime[i] += dt;
                        StopIfArriving(a, i, dt);
                        continue;
                    }
                }

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
            FarAgents = far;
        }

        /// <summary>
        /// For an agent that is skipping a think: stops it at its target instead of walking past,
        /// since nothing will steer it until it next thinks.
        /// </summary>
        private static void StopIfArriving(AgentStore a, int i, float dt)
        {
            float v2 = a.VelX[i] * a.VelX[i] + a.VelZ[i] * a.VelZ[i];
            if (v2 < 1e-6f) return;
            float dx = a.TargetX[i] - a.PosX[i], dz = a.TargetZ[i] - a.PosZ[i];
            float step = (float)Math.Sqrt(v2) * dt;
            if (dx * dx + dz * dz <= step * step) Stop(a, i);
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

        private static float ScoreSocialise(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            float s = a.Social[i];
            if (a.Goal[i] == Goal.Socialise && s > 0.2f) return 0.25f; // keep going until properly settled in company
            return s > 0.5f ? 0.25f + 0.4f * (s - 0.5f) : 0f;
        }

        private float ScoreDefend(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (!a.HasHome[i] || a.Retreat[i] > 0f) return 0f;
            return FindRival(c) >= 0 ? 0.9f : 0f;
        }

        private static float ScoreRetreat(AgentContext c) => c.Agents.Retreat[c.Index] > 0f ? 1.5f : 0f;

        private float ScoreFrogFlee(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            if (a.Stage[i] != LifeStage.Froglet && a.Stage[i] != LifeStage.Juvenile) return 0f;
            if (a.Goal[i] == Goal.Flee && a.GoalTime[i] < 1.5f) return 0.8f;
            return BigFrogNearby(c, BigFrogFear) >= 0 ? 1f : 0f;
        }

        /// <summary>
        /// A rival male of this frog's species inside his territory and close enough to notice, or -1.
        /// Keeps chasing the rival he already picked while it is still in the territory.
        /// </summary>
        private int FindRival(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            float radius = Sp(c).TerritoryRadius;
            if (a.Goal[i] == Goal.Defend && a.TargetId[i] > 0)
            {
                int t = c.IndexOfId(a.TargetId[i]);
                if (t >= 0 && !c.Removed[t] && a.Retreat[t] <= 0f && InTerritory(a, i, t, radius + 0.3f)) return t;
            }
            _wantSpecies = a.Species[i];
            int r = c.Hash.FindNearest(a, a.PosX[i], a.PosZ[i], RivalSightRadius, AgentKind.Frog, i, c.Removed, _rival);
            return r >= 0 && InTerritory(a, i, r, radius) ? r : -1;
        }

        private static bool InTerritory(AgentStore a, int owner, int other, float radius)
        {
            float dx = a.PosX[other] - a.HomeX[owner], dz = a.PosZ[other] - a.HomeZ[owner];
            return dx * dx + dz * dz < radius * radius;
        }

        /// <summary>A grown frog of another species, bigger than this young frog, within the distance, or -1.</summary>
        private int BigFrogNearby(AgentContext c, float distance)
        {
            var a = c.Agents; int i = c.Index;
            _wantSpecies = a.Species[i];
            _wantBiggerThan = BodySize(a, i) * 1.3f;
            return c.Hash.FindNearest(a, a.PosX[i], a.PosZ[i], distance, AgentKind.Frog, i, c.Removed, _bigOtherFrog);
        }

        /// <summary>A frog's body length in metres, from its species, growth and size gene.</summary>
        public static float BodySize(AgentStore a, int i)
        {
            var sp = SpeciesCatalog.FrogOf(a, i);
            return sp.AdultSize * sp.SizeFraction(a.Growth[i]) * a.Genes[i].Size;
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

            _frogTrees[Goal.Socialise] = BtNode.Selector("Find company",
                BtNode.Sequence("Join others",
                    BtNode.Condition("Knows where others are", FindCompany),
                    BtNode.Action("Hop over", c => GoToTarget(c, Sp(c).TravelSpeed * Small(c), 0.8f))),
                BtNode.Action("Look around", c => Wander(c, Sp(c).TravelSpeed * Small(c), 0.1f)));

            _frogTrees[Goal.Defend] = BtNode.Selector("Defend territory",
                BtNode.Sequence("Chase off rival",
                    BtNode.Condition("Rival in territory", c => { int r = FindRival(c); c.Agents.TargetId[c.Index] = r >= 0 ? c.Agents.Id[r] : 0; return r >= 0; }),
                    BtNode.Selector("Confront",
                        BtNode.Sequence("Wrestle",
                            BtNode.Condition("Face to face", RivalInReach),
                            BtNode.Action("Push", Contest)),
                        BtNode.Action("Charge", ChargeRival))),
                BtNode.Action("Stand guard", SitStill));

            _frogTrees[Goal.Retreat] = BtNode.Selector("Back off",
                BtNode.Sequence("Move away",
                    BtNode.Condition("Still close", c => DistanceToTargetSq(c) > 0.04f),
                    BtNode.Action("Hop away", c => GoToTarget(c, Sp(c).TravelSpeed * Small(c), 0.2f))),
                BtNode.Action("Keep low", SitStill));

            _frogTrees[Goal.Flee] = BtNode.Action("Keep clear of big frog", FrogFlee);

            _frogTrees[Goal.Wander] = BtNode.Action("Explore", c => Wander(c, Sp(c).WanderSpeed * Small(c), 0.45f, stayHome: true));

            _tadpoleTrees[Goal.Wander] = BtNode.Action("Swim", Swim);

            _insectTrees[Goal.Flee] = BtNode.Action("Run away", Flee);
            _insectTrees[Goal.Wander] = BtNode.Action("Wander", c => Wander(c, SpeciesCatalog.Insect.WanderSpeed, 0.4f));
        }

        /// <summary>The species of the agent being thought about.</summary>
        private static FrogSpecies Sp(AgentContext c) => SpeciesCatalog.FrogOf(c.Agents, c.Index);

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
            if (c.Random.Chance(ClimbSuccess * Sp(c).ClimbSkill)) // claws make it much easier
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

        /// <summary>Picks the nearest frog of the same species as somewhere to go.</summary>
        private bool FindCompany(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            _wantSpecies = a.Species[i];
            int f = c.Hash.FindNearest(a, a.PosX[i], a.PosZ[i], CompanySearchRadius, AgentKind.Frog, i, c.Removed, _company);
            if (f < 0) return false;
            a.TargetX[i] = a.PosX[f];
            a.TargetZ[i] = a.PosZ[f];
            return true;
        }

        private static bool RivalInReach(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            int t = c.IndexOfId(a.TargetId[i]);
            return t >= 0 && DistanceSq(a, i, t) <= 0.3f * 0.3f;
        }

        private static BtStatus ChargeRival(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            int t = c.IndexOfId(a.TargetId[i]);
            if (t < 0) return BtStatus.Failure;
            MoveToward(a, i, a.PosX[t], a.PosZ[t], Sp(c).HuntSpeed);
            return BtStatus.Running;
        }

        /// <summary>
        /// The owner and the intruder wrestle. Bigger, bolder frogs usually win, and the owner has
        /// an edge on his own patch, but anything can happen. The loser backs off, a bit stressed;
        /// if the owner loses, the winner takes over the territory.
        /// </summary>
        private BtStatus Contest(AgentContext c)
        {
            var a = c.Agents; int owner = c.Index;
            int rival = c.IndexOfId(a.TargetId[owner]);
            if (rival < 0) return BtStatus.Failure;
            var rng = c.Random;
            float ownerStrength = Strength(a, owner) * 1.25f * rng.Range(0.75f, 1.25f);
            float rivalStrength = Strength(a, rival) * rng.Range(0.75f, 1.25f);
            Disputes++;
            float hx = a.HomeX[owner], hz = a.HomeZ[owner];
            if (ownerStrength >= rivalStrength)
            {
                Defeat(c, rival, hx, hz);
            }
            else
            {
                Takeovers++;
                a.HasHome[rival] = true;
                a.HomeX[rival] = hx; a.HomeZ[rival] = hz;
                a.HasHome[owner] = false;
                Defeat(c, owner, hx, hz);
            }
            Stop(a, owner);
            a.TargetId[owner] = 0;
            return BtStatus.Success;
        }

        /// <summary>How hard a frog pushes in a dispute: size and boldness, plus teeth and claws.</summary>
        private static float Strength(AgentStore a, int i) =>
            a.Genes[i].Size * (0.6f + a.Genes[i].Boldness) * (1f + SpeciesCatalog.FrogOf(a, i).FightBonus);

        /// <summary>Sends the loser of a dispute away from the territory for a few seconds.</summary>
        private void Defeat(AgentContext c, int loser, float hx, float hz)
        {
            var a = c.Agents;
            var sp = SpeciesCatalog.FrogOf(a, loser);
            a.Retreat[loser] = RetreatSeconds;
            a.Stress[loser] = Math.Min(1f, a.Stress[loser] + DefeatStress);
            float dx = a.PosX[loser] - hx, dz = a.PosZ[loser] - hz;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-3f) { float ang = c.Random.Range(0f, 2f * (float)Math.PI); dx = (float)Math.Cos(ang); dz = (float)Math.Sin(ang); len = 1f; }
            float d = sp.TerritoryRadius + 0.8f;
            var t = c.World.Terrain;
            a.TargetX[loser] = TerrainGrid.Clamp(hx + dx / len * d, -t.HalfSizeX + 0.2f, t.HalfSizeX - 0.2f);
            a.TargetZ[loser] = TerrainGrid.Clamp(hz + dz / len * d, -t.HalfSizeZ + 0.2f, t.HalfSizeZ - 0.2f);
            // Switch straight to backing off, keeping the spot picked above.
            a.Goal[loser] = Goal.Retreat;
            a.GoalTime[loser] = 0f;
            a.TargetId[loser] = 0;
            a.Timer[loser] = 0f;
        }

        private static float DistanceToTargetSq(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            float dx = a.TargetX[i] - a.PosX[i], dz = a.TargetZ[i] - a.PosZ[i];
            return dx * dx + dz * dz;
        }

        /// <summary>A young frog hops away from a big frog of another species.</summary>
        private BtStatus FrogFlee(AgentContext c)
        {
            var a = c.Agents; int i = c.Index;
            int f = BigFrogNearby(c, BigFrogFear * 2f);
            if (f < 0) { Stop(a, i); return BtStatus.Failure; }
            float dx = a.PosX[i] - a.PosX[f], dz = a.PosZ[i] - a.PosZ[f];
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f) { dx = 1f; dz = 0f; len = 1f; }
            var t = c.World.Terrain;
            float x = TerrainGrid.Clamp(a.PosX[i] + dx / len, -t.HalfSizeX + 0.2f, t.HalfSizeX - 0.2f);
            float z = TerrainGrid.Clamp(a.PosZ[i] + dz / len, -t.HalfSizeZ + 0.2f, t.HalfSizeZ - 0.2f);
            a.TargetX[i] = x; a.TargetZ[i] = z;
            MoveToward(a, i, x, z, Sp(c).TravelSpeed * Small(c));
            return BtStatus.Running;
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
        private static BtStatus Wander(AgentContext c, float speed, float pauseChance, bool stayHome = false)
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
                    // A frog with a territory potters about inside it.
                    bool home = stayHome && a.HasHome[i];
                    float cx = home ? a.HomeX[i] : a.PosX[i], cz = home ? a.HomeZ[i] : a.PosZ[i];
                    float minDist = home ? 0f : 0.8f, maxDist = home ? Sp(c).TerritoryRadius : 3f;
                    for (int attempt = 0; attempt < 4; attempt++)
                    {
                        float ang = rng.Range(0f, 2f * (float)Math.PI), dist = rng.Range(minDist, maxDist);
                        float x = cx + (float)Math.Cos(ang) * dist, z = cz + (float)Math.Sin(ang) * dist;
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

using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>What a scenario goal asks for. Never renumber: saves store the number.</summary>
    public enum GoalKind : byte
    {
        /// <summary>Have at least this many frogs out of the water (froglets and older).</summary>
        Frogs = 0,
        /// <summary>Have grown frogs of at least this many different species.</summary>
        Species = 1,
        /// <summary>Have at least this much money in the bank.</summary>
        Money = 2,
        /// <summary>Reach this reputation (0 to 100).</summary>
        Reputation = 3,
        /// <summary>Welcome this many visitors in total.</summary>
        Visitors = 4,
        /// <summary>Find this many different hybrids.</summary>
        Hybrids = 5,
        /// <summary>Find this many different mutations.</summary>
        Mutations = 6,
        /// <summary>Register this many new species.</summary>
        Registered = 7,
        /// <summary>Place this many building and decoration parts.</summary>
        Decorations = 8,
        /// <summary>Keep the tank health score at or above Amount for Minutes in a row.</summary>
        HealthFor = 9,
        /// <summary>Keep the tank going for this many minutes.</summary>
        Survive = 10,
    }

    /// <summary>One thing the player must do to win a scenario.</summary>
    public sealed class ScenarioGoal
    {
        public GoalKind Kind;
        public float Amount;
        /// <summary>For <see cref="GoalKind.HealthFor"/>: how long the score must hold, in minutes.</summary>
        public float Minutes;

        /// <summary>The goal in words, for the tracker ("Have 12 frogs").</summary>
        public string Describe()
        {
            int n = (int)Math.Round(Amount);
            switch (Kind)
            {
                case GoalKind.Frogs: return "Have " + n + " frogs";
                case GoalKind.Species: return "Keep " + n + " different species";
                case GoalKind.Money: return "Save up " + Economy.Format(Amount);
                case GoalKind.Reputation: return "Reach reputation " + n;
                case GoalKind.Visitors: return "Welcome " + n + " visitors";
                case GoalKind.Hybrids: return n == 1 ? "Breed a hybrid" : "Breed " + n + " different hybrids";
                case GoalKind.Mutations: return n == 1 ? "Find a mutation" : "Find " + n + " different mutations";
                case GoalKind.Registered: return n == 1 ? "Register a new species" : "Register " + n + " new species";
                case GoalKind.Decorations: return "Place " + n + " building parts";
                case GoalKind.HealthFor: return "Keep tank health at " + n + " or more for " + Minutes.ToString("0") + " minutes in a row";
                case GoalKind.Survive: return "Keep the tank going for " + n + " minutes";
                default: return Kind.ToString();
            }
        }
    }

    /// <summary>A ready-made challenge: a map, a starting setup, goals to reach and ways to lose.</summary>
    public sealed class ScenarioInfo
    {
        public byte Id;
        public string Name;
        public string Description;
        public MapSettings Map;
        public int Frogs = 8;
        public int Insects = 80;
        /// <summary>Money mode, starting with <see cref="StartMoney"/>. Off means the sandbox with <see cref="Rules"/>.</summary>
        public bool MoneyMode = true;
        public float StartMoney = Economy.StartingMoney;
        public SandboxRules Rules;
        /// <summary>Simulated minutes to reach every goal (0 = no limit).</summary>
        public float TimeLimitMinutes;
        /// <summary>The scenario is lost once more frogs than this escape (-1 = escapes don't lose it).</summary>
        public int MaxEscapes = -1;
        public ScenarioGoal[] Goals;
        /// <summary>Every device starts switched off.</summary>
        public bool DevicesOff;
        /// <summary>The frogs start hungry, thirsty, uncomfortable and stressed.</summary>
        public bool StartStressed;
        /// <summary>Starts with the mutation boost on (new mutations 25 times as common).</summary>
        public bool MutationBoost;
        /// <summary>A tip shown under the goals, or "".</summary>
        public string Hint = "";
    }

    public enum ScenarioStatus : byte
    {
        None = 0,
        Playing = 1,
        Won = 2,
        Lost = 3,
    }

    /// <summary>How the scenario being played is going. Saved from version 12.</summary>
    public sealed class ScenarioState
    {
        public byte Id;
        public ScenarioStatus Status = ScenarioStatus.Playing;
        /// <summary>Simulation time when it started, and when it was won or lost.</summary>
        public double StartTime, EndTime;
        /// <summary>Frogs that escaped since it started.</summary>
        public int Escapes;
        /// <summary>One bit per goal that has been reached (goals stay reached).</summary>
        public uint Done;
        /// <summary>For each goal, seconds its condition has held in a row (used by HealthFor).</summary>
        public float[] Held = new float[0];
        /// <summary>Why it was lost, or "" (not saved; worked out again from the state).</summary>
        public string Reason = "";

        public ScenarioInfo Info => Scenarios.Find(Id);
        public bool IsDone(int goal) => (Done & (1u << goal)) != 0;
        public bool Active => Status == ScenarioStatus.Playing;
    }

    /// <summary>The built-in scenarios and the rules for winning and losing them.</summary>
    public static class Scenarios
    {
        /// <summary>The most goals a scenario can have (they are stored as bits).</summary>
        public const int MaxGoals = 16;

        private static ScenarioGoal G(GoalKind kind, float amount, float minutes = 0f) =>
            new ScenarioGoal { Kind = kind, Amount = amount, Minutes = minutes };

        /// <summary>Every scenario, by id. Never reuse an id: saves store it.</summary>
        public static readonly ScenarioInfo[] All =
        {
            new ScenarioInfo
            {
                Id = 1, Name = "First steps",
                Description = "A gentle start in a classic tank. Grow your little colony and put some money aside.",
                Map = MapSettings.For(Biome.Classic, 1001), Frogs = 6, TimeLimitMinutes = 40f,
                Hint = "Sell frogs from the frog inspector, and fill buyers' orders in the Money menu.",
                Goals = new[] { G(GoalKind.Frogs, 12), G(GoalKind.Money, 450), G(GoalKind.Decorations, 5) },
            },
            new ScenarioInfo
            {
                Id = 2, Name = "Desert rescue",
                Description = "A pet shop left its frogs in a hot, dry desert tank with every device switched off. Make them comfortable fast, and don't let more than two get away.",
                Map = MapSettings.For(Biome.Desert, 2002), Frogs = 8, TimeLimitMinutes = 25f, MaxEscapes = 2, StartMoney = 400f,
                DevicesOff = true, StartStressed = true,
                Hint = "Click a device to switch it on. More misters and moss help in a dry room.",
                Goals = new[] { G(GoalKind.HealthFor, 85, 5f) },
            },
            new ScenarioInfo
            {
                Id = 3, Name = "Hybrid hunter",
                Description = "Two pairs of tree frog species share a rainforest. Crossbreed them and look out for something strange.",
                Map = MapSettings.For(Biome.Rainforest, 3003), Frogs = 8, TimeLimitMinutes = 75f, StartMoney = 500f,
                MutationBoost = true,
                Hint = "A female only pairs with another species when no male of her own kind is nearby. Selling or rehoming some males helps.",
                Goals = new[] { G(GoalKind.Hybrids, 1), G(GoalKind.Mutations, 1), G(GoalKind.Frogs, 14) },
            },
            new ScenarioInfo
            {
                Id = 4, Name = "Island zoo",
                Description = "Turn a tank of little islands into a show that brings the crowds in.",
                Map = MapSettings.For(Biome.Islands, 4004), Frogs = 8, TimeLimitMinutes = 50f, StartMoney = 600f,
                Hint = "Visitors love many species, decorations and colours. Keep the feeder full.",
                Goals = new[] { G(GoalKind.Visitors, 100), G(GoalKind.Reputation, 35), G(GoalKind.Species, 3) },
            },
            new ScenarioInfo
            {
                Id = 5, Name = "Volcano garden",
                Description = "Dress up the black slopes of a volcano with plants and rocks, and fill it with life. Money is no object here.",
                Map = MapSettings.For(Biome.Volcanic, 5005), Frogs = 8, MoneyMode = false, TimeLimitMinutes = 30f,
                Rules = SandboxRules.NoDirt,
                Goals = new[] { G(GoalKind.Decorations, 20), G(GoalKind.Species, 4), G(GoalKind.Frogs, 16) },
            },
            new ScenarioInfo
            {
                Id = 6, Name = "A species of your own",
                Description = "No time limit. Breed a new kind of frog that breeds true and give it a name.",
                Map = MapSettings.For(Biome.Riverbank, 6006), Frogs = 8, StartMoney = 500f,
                Goals = new[] { G(GoalKind.Registered, 1) },
            },
        };

        /// <summary>The scenario with this id, or null.</summary>
        public static ScenarioInfo Find(byte id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }

        /// <summary>A new game for a scenario: its map, frogs and money, with the scenario started.</summary>
        public static World Start(ScenarioInfo info)
        {
            var world = World.CreateTank(info.Map, info.Frogs, info.Insects);
            if (info.MoneyMode)
            {
                world.Economy.StartMoneyMode();
                world.Economy.Money = info.StartMoney;
            }
            world.Rules = info.Rules;
            world.MutationBoost = info.MutationBoost;
            if (info.DevicesOff) foreach (var d in world.Devices.All) d.Enabled = false;
            if (info.StartStressed)
            {
                var a = world.Agents;
                for (int i = 0; i < a.Count; i++)
                {
                    if (!a.IsLandFrog(i)) continue;
                    a.Comfort[i] = 0.3f; a.Stress[i] = 0.45f; a.Hunger[i] = 0.7f; a.Thirst[i] = 0.7f;
                }
            }
            world.Scenario = new ScenarioState
            {
                Id = info.Id,
                StartTime = world.SimTime,
                Held = new float[info.Goals.Length],
            };
            return world;
        }

        /// <summary>Simulated minutes since the scenario started (up to when it ended).</summary>
        public static double MinutesPlayed(World world)
        {
            var s = world.Scenario;
            if (s == null) return 0;
            double end = s.Active ? world.SimTime : s.EndTime;
            return Math.Max(0, end - s.StartTime) / 60.0;
        }

        /// <summary>Minutes left before time runs out, or -1 when there is no limit.</summary>
        public static double MinutesLeft(World world)
        {
            var s = world.Scenario;
            var info = s?.Info;
            if (info == null || info.TimeLimitMinutes <= 0f) return -1;
            return Math.Max(0, info.TimeLimitMinutes - MinutesPlayed(world));
        }

        /// <summary>
        /// How far along a goal is right now, in the goal's own units (frogs, dollars, minutes held).
        /// The goal is reached when this is at least its target (see <see cref="Target"/>).
        /// </summary>
        public static float Progress(World world, int goal)
        {
            var s = world.Scenario;
            var info = s?.Info;
            if (info == null || goal < 0 || goal >= info.Goals.Length) return 0f;
            var g = info.Goals[goal];
            var eco = world.Economy;
            switch (g.Kind)
            {
                case GoalKind.Frogs: return LandFrogs(world);
                case GoalKind.Species: return SpeciesCount(world);
                case GoalKind.Money: return (float)eco.Money;
                case GoalKind.Reputation: return eco.Reputation;
                case GoalKind.Visitors: return eco.Visitors;
                case GoalKind.Hybrids: return Bits(world.DiscoveredHybrids);
                case GoalKind.Mutations: return Bits(world.DiscoveredMutations);
                case GoalKind.Registered: return world.Registered.Count;
                case GoalKind.Decorations: return world.Parts.Count;
                case GoalKind.HealthFor: return goal < s.Held.Length ? s.Held[goal] / 60f : 0f;
                case GoalKind.Survive: return (float)MinutesPlayed(world);
                default: return 0f;
            }
        }

        /// <summary>The number <see cref="Progress"/> must reach.</summary>
        public static float Target(ScenarioGoal g) => g.Kind == GoalKind.HealthFor ? g.Minutes : g.Amount;

        /// <summary>Grown frogs (froglets and older) of how many different species are in the tank.</summary>
        public static int SpeciesCount(World world)
        {
            var a = world.Agents;
            var seen = new HashSet<byte>();
            for (int i = 0; i < a.Count; i++) if (a.IsLandFrog(i)) seen.Add(a.Species[i]);
            return seen.Count;
        }

        private static int LandFrogs(World world)
        {
            int n = 0;
            for (int i = 0; i < world.Agents.Count; i++) if (world.Agents.IsLandFrog(i)) n++;
            return n;
        }

        private static int Bits(ulong v)
        {
            int n = 0;
            while (v != 0) { v &= v - 1; n++; }
            return n;
        }

        /// <summary>
        /// Checks the goals and the ways to lose, once a second (see <see cref="ScenarioSystem"/>).
        /// A goal stays reached once reached. The scenario is won when every goal is reached, and
        /// lost when time runs out, too many frogs escape, or no frogs are left.
        /// </summary>
        public static void Update(World world, float dt)
        {
            var s = world.Scenario;
            var info = s?.Info;
            if (info == null || !s.Active) return;
            if (s.Held.Length != info.Goals.Length) Array.Resize(ref s.Held, info.Goals.Length);

            int healthScore = -1;
            for (int k = 0; k < info.Goals.Length; k++)
            {
                if (s.IsDone(k)) continue;
                var g = info.Goals[k];
                if (g.Kind == GoalKind.HealthFor)
                {
                    if (healthScore < 0) healthScore = TankHealth.Compute(world).Score;
                    s.Held[k] = healthScore >= g.Amount ? s.Held[k] + dt : 0f;
                }
                if (Progress(world, k) >= Target(g) - 1e-4f) s.Done |= 1u << k;
            }

            uint all = info.Goals.Length >= 32 ? uint.MaxValue : (1u << info.Goals.Length) - 1u;
            if ((s.Done & all) == all)
            {
                End(world, ScenarioStatus.Won);
                return;
            }
            string why = LoseReason(world);
            if (why.Length > 0) End(world, ScenarioStatus.Lost);
        }

        /// <summary>Why the scenario is lost right now, or "" if it is not.</summary>
        public static string LoseReason(World world)
        {
            var s = world.Scenario;
            var info = s?.Info;
            if (info == null) return "";
            if (info.MaxEscapes >= 0 && s.Escapes > info.MaxEscapes)
                return s.Escapes + " frogs escaped (no more than " + info.MaxEscapes + " allowed).";
            if (world.FrogCount() == 0) return "No frogs are left in the tank.";
            if (info.TimeLimitMinutes > 0f && MinutesPlayed(world) >= info.TimeLimitMinutes) return "Time ran out.";
            return "";
        }

        private static void End(World world, ScenarioStatus status)
        {
            var s = world.Scenario;
            s.Status = status;
            s.EndTime = world.SimTime;
            s.Reason = status == ScenarioStatus.Lost ? LoseReason(world) : "";
            world.Announce(status == ScenarioStatus.Won
                ? "Scenario complete: " + s.Info.Name + "! " + Stars(world) + " stars."
                : "Scenario lost: " + s.Reason);
        }

        /// <summary>
        /// Stars for a won scenario: 3 if it took at most half the time limit, 2 if at most four
        /// fifths, otherwise 1. A scenario with no limit always gives 3. 0 if not won.
        /// </summary>
        public static int Stars(World world)
        {
            var s = world.Scenario;
            var info = s?.Info;
            if (info == null || s.Status != ScenarioStatus.Won) return 0;
            if (info.TimeLimitMinutes <= 0f) return 3;
            double share = MinutesPlayed(world) / info.TimeLimitMinutes;
            return share <= 0.5 ? 3 : share <= 0.8 ? 2 : 1;
        }
    }

    /// <summary>Checks the scenario's goals once a second. Does nothing outside a scenario.</summary>
    public sealed class ScenarioSystem : ISimSystem
    {
        public string Name => "Scenario";
        public int TicksPerSecond => 1;
        public void Tick(World world, float dt) => Scenarios.Update(world, dt);
    }
}

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// The rare mutations a frog can be born with. Each one shows (teeth, claws, odd colours) and
    /// changes how the frog lives. Never reorder: saves store two bits per mutation in this order.
    /// Add new mutations to the end (up to 16).
    /// </summary>
    public enum Mutation : byte
    {
        Teeth = 0,
        Claws = 1,
        GiantEyes = 2,
        LongLegs = 3,
        LoudCall = 4,
        Albino = 5,
        Blue = 6,
        Melanistic = 7,
        StubbyLegs = 8,
    }

    /// <summary>What a mutation is called, whether one copy is enough to show it, and what it does.</summary>
    public sealed class MutationInfo
    {
        public Mutation Kind;
        public string Name;
        /// <summary>A short, plain description of what it does, for the inspector and catalogue.</summary>
        public string Effect;
        /// <summary>
        /// Dominant mutations show with one copy (from one parent). Recessive ones only show with two
        /// copies (one from each parent), so they can hide in a family for generations, then pop up.
        /// </summary>
        public bool Dominant;
    }

    /// <summary>
    /// The list of mutations and the rules for passing them on. Each frog carries 0, 1 or 2 copies
    /// of each mutation (one from its mother, one from its father), like real genes.
    ///
    /// New mutations are rare: about 1 egg in 100 gets one. Stressed mothers and hybrid parents make
    /// them more likely, so players can chase them on purpose (Phase 3 research upgrades will add
    /// more ways). The sandbox "mutation boost" switch makes them 25 times more common for testing.
    /// </summary>
    public static class Mutations
    {
        public const int Count = 9;

        /// <summary>Chance that an egg from a calm, pure-bred mother gets a brand-new mutation.</summary>
        public const float BaseChancePerEgg = 0.01f;
        /// <summary>How many times more likely mutations are with the sandbox boost switched on.</summary>
        public const float BoostFactor = 25f;

        public static readonly MutationInfo[] All =
        {
            new MutationInfo { Kind = Mutation.Teeth, Name = "Teeth", Dominant = true,
                Effect = "bites crickets in half, so each meal fills it up more; tougher in a fight" },
            new MutationInfo { Kind = Mutation.Claws, Name = "Claws", Dominant = true,
                Effect = "a better climber: a bit quicker, tougher in a fight, and much better at climbing out of the tank" },
            new MutationInfo { Kind = Mutation.GiantEyes, Name = "Giant eyes", Dominant = true,
                Effect = "spots insects from much further away, but gets sleepy sooner" },
            new MutationInfo { Kind = Mutation.LongLegs, Name = "Long legs", Dominant = true,
                Effect = "fast, but gets hungry sooner" },
            new MutationInfo { Kind = Mutation.LoudCall, Name = "Loud call", Dominant = true,
                Effect = "males are heard from across a much bigger tank; also claims a bigger territory" },
            new MutationInfo { Kind = Mutation.Albino, Name = "Albino", Dominant = false,
                Effect = "pale white with pink eyes; poor eyesight and dislikes heat" },
            new MutationInfo { Kind = Mutation.Blue, Name = "Blue", Dominant = false,
                Effect = "sky-blue skin (a real colour form of tree frogs); no other effect, but collectors love it" },
            new MutationInfo { Kind = Mutation.Melanistic, Name = "Black", Dominant = false,
                Effect = "very dark skin that soaks up warmth, so it likes the tank a little cooler" },
            new MutationInfo { Kind = Mutation.StubbyLegs, Name = "Stubby legs", Dominant = false,
                Effect = "slow and waddly, but needs less food" },
        };

        public static MutationInfo Info(Mutation m) => All[(int)m];

        /// <summary>Copies (0, 1 or 2) of a mutation in a set of alleles.</summary>
        public static int Copies(uint alleles, Mutation m) => (int)((alleles >> (2 * (int)m)) & 3u);

        /// <summary>The alleles with the number of copies of one mutation changed.</summary>
        public static uint WithCopies(uint alleles, Mutation m, int copies)
        {
            int shift = 2 * (int)m;
            if (copies < 0) copies = 0;
            if (copies > 2) copies = 2;
            return (alleles & ~(3u << shift)) | ((uint)copies << shift);
        }

        /// <summary>One bit per mutation that shows (for dominant ones, one copy is enough; for recessive ones, two).</summary>
        public static ushort Visible(uint alleles)
        {
            if (alleles == 0u) return 0;
            ushort mask = 0;
            for (int m = 0; m < Count; m++)
            {
                int copies = (int)((alleles >> (2 * m)) & 3u);
                if (copies >= (All[m].Dominant ? 1 : 2)) mask |= (ushort)(1 << m);
            }
            return mask;
        }

        public static bool Shows(ushort visible, Mutation m) => (visible & (1 << (int)m)) != 0;

        /// <summary>
        /// A child's alleles: one copy (or none) of each mutation from each parent, picked at random
        /// when the parent has only one copy. Draws from the random stream only for mutations a parent
        /// actually carries, so families without mutations use no extra random numbers.
        /// </summary>
        public static uint Inherit(uint mother, uint father, SeededRandom rng)
        {
            if ((mother | father) == 0u) return 0u;
            uint child = 0u;
            for (int m = 0; m < Count; m++)
            {
                int c = Pass(Copies(mother, (Mutation)m), rng) + Pass(Copies(father, (Mutation)m), rng);
                child = WithCopies(child, (Mutation)m, c);
            }
            return child;
        }

        private static int Pass(int copies, SeededRandom rng)
        {
            if (copies <= 0) return 0;
            if (copies >= 2) return 1;
            return rng.Chance(0.5f) ? 1 : 0;
        }

        /// <summary>
        /// The chance that one egg gets a brand-new mutation. Up to four times more likely when the
        /// mother is very stressed, twice as likely when either parent is a hybrid.
        /// </summary>
        public static float ChancePerEgg(float motherStress, bool hybridParents, bool boost)
        {
            float chance = BaseChancePerEgg * (1f + 3f * Clamp01(motherStress));
            if (hybridParents) chance *= 2f;
            if (boost) chance *= BoostFactor;
            return chance > 1f ? 1f : chance;
        }

        /// <summary>Adds one copy of a random mutation (a new mutation always starts as a single copy).</summary>
        public static uint AddRandom(uint alleles, SeededRandom rng, out Mutation picked)
        {
            picked = (Mutation)rng.Range(0, Count);
            int copies = Copies(alleles, picked);
            return WithCopies(alleles, picked, copies + 1);
        }

        /// <summary>The names of the mutations that show, for example "Teeth, Blue". Empty if none.</summary>
        public static string Names(ushort visible)
        {
            if (visible == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int m = 0; m < Count; m++)
            {
                if ((visible & (1 << m)) == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(All[m].Name);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Hidden copies: recessive mutations the frog carries one copy of. It looks normal but can
        /// pass them on. For the inspector, e.g. "Albino".
        /// </summary>
        public static string Hidden(uint alleles)
        {
            var sb = new System.Text.StringBuilder();
            for (int m = 0; m < Count; m++)
            {
                if (All[m].Dominant || Copies(alleles, (Mutation)m) != 1) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(All[m].Name);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Changes a species' numbers for the mutations that show. Used to build the per-frog
        /// species in <see cref="SpeciesCatalog.FrogOf(AgentStore, int)"/>.
        /// </summary>
        internal static void Apply(FrogSpecies sp, ushort visible)
        {
            if (Shows(visible, Mutation.Teeth)) { sp.MealSize *= 1.4f; sp.FightBonus += 0.3f; }
            if (Shows(visible, Mutation.Claws))
            {
                sp.WanderSpeed *= 1.1f; sp.HuntSpeed *= 1.1f; sp.TravelSpeed *= 1.1f;
                sp.FightBonus += 0.3f;
                sp.ClimbSkill *= 2.5f;
            }
            if (Shows(visible, Mutation.GiantEyes)) { sp.SenseRadius *= 1.4f; sp.FatiguePerSecond *= 1.25f; }
            if (Shows(visible, Mutation.LongLegs))
            {
                sp.WanderSpeed *= 1.25f; sp.HuntSpeed *= 1.25f; sp.TravelSpeed *= 1.25f;
                sp.HungerPerSecond *= 1.2f;
            }
            if (Shows(visible, Mutation.LoudCall)) { sp.CallRange *= 1.6f; sp.TerritoryRadius *= 1.3f; }
            if (Shows(visible, Mutation.Albino)) { sp.SenseRadius *= 0.75f; sp.MaxTemperature -= 1.5f; }
            if (Shows(visible, Mutation.Melanistic)) { sp.MinTemperature -= 1.5f; sp.MaxTemperature -= 1.5f; }
            if (Shows(visible, Mutation.StubbyLegs))
            {
                sp.WanderSpeed *= 0.75f; sp.HuntSpeed *= 0.8f; sp.TravelSpeed *= 0.75f;
                sp.HungerPerSecond *= 0.85f;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Rules for hybrids and new species.
    ///
    /// Frogs of two different species in the same genus (for example White's and White-lipped tree
    /// frogs) can pair up, though a female prefers a male of her own kind when one is about. Their
    /// young are hybrids: half one species, half the other, with in-between size, colour and climate
    /// needs. Hybrids are less fertile than pure-bred frogs (first crosses lay about 60% as many eggs),
    /// like many real hybrids. Breeding a hybrid back to a pure-bred frog gives a quarter hybrid, and so on.
    ///
    /// A "type" of frog is its main species, its hybrid mix (to the nearest quarter) and the
    /// mutations that show. When a new type (a hybrid, or one with mutations) has bred true for three
    /// generations in a row, the player can register it as a new species and give it a name.
    /// </summary>
    public static class Lineage
    {
        /// <summary>Generations in a row a type must breed true before it can be registered.</summary>
        public const int GenerationsToRegister = 3;

        /// <summary>
        /// Chance, each time a female with no male of her own species nearby tries to pair up, that she
        /// accepts a male of a related species instead.
        /// </summary>
        public const float CrossPairChance = 0.3f;

        /// <summary>How fertile a frog is because of its hybrid mix: 1 for pure-bred, 0.6 for a half-and-half first cross.</summary>
        public static float HybridFertility(in Genome g) => 1f - 0.8f * g.HybridShare;

        /// <summary>
        /// Works out a child's species mix from its parents'. Each parent passes on half of its own
        /// mix. The child's main species is whichever it has most of (the mother's when it is exactly
        /// half and half). Only two species ever share a genus at the moment; if a third ever appears,
        /// the smallest part is dropped.
        /// </summary>
        public static void ChildAncestry(byte motherSpecies, in Genome mother, byte fatherSpecies, in Genome father,
                                         out byte main, out byte other, out float share)
        {
            // Up to four (species, amount) parts.
            var ids = new byte[4];
            var amounts = new float[4];
            int n = 0;
            void AddPart(byte id, float amount)
            {
                if (amount <= 0f) return;
                for (int k = 0; k < n; k++) if (ids[k] == id) { amounts[k] += amount; return; }
                ids[n] = id; amounts[n] = amount; n++;
            }
            AddPart(motherSpecies, 0.5f * (1f - Share(motherSpecies, mother)));
            if (mother.IsHybrid && mother.HybridSpecies != motherSpecies) AddPart(mother.HybridSpecies, 0.5f * mother.HybridShare);
            AddPart(fatherSpecies, 0.5f * (1f - Share(fatherSpecies, father)));
            if (father.IsHybrid && father.HybridSpecies != fatherSpecies) AddPart(father.HybridSpecies, 0.5f * father.HybridShare);

            // The biggest part wins; parts are added mother first, so she wins a tie.
            int best = 0;
            for (int k = 1; k < n; k++) if (amounts[k] > amounts[best] + 1e-4f) best = k;
            int second = -1;
            for (int k = 0; k < n; k++)
            {
                if (k == best) continue;
                if (second < 0 || amounts[k] > amounts[second] + 1e-4f) second = k;
            }
            main = ids[best];
            if (second < 0)
            {
                other = main;
                share = 0f;
                return;
            }
            other = ids[second];
            share = amounts[second] / (amounts[best] + amounts[second]);
            if (share < 1f / 64f) { other = main; share = 0f; } // so diluted it no longer counts
        }

        private static float Share(byte species, in Genome g) => g.IsHybrid && g.HybridSpecies != species ? g.HybridShare : 0f;

        /// <summary>
        /// A number that is the same for every frog of the same type: main species, the other species
        /// and the mix to the nearest quarter for hybrids, and the mutations that show.
        /// </summary>
        public static int TypeOf(byte species, in Genome g)
        {
            int quarter = g.IsHybrid && g.HybridSpecies != species ? (int)(g.HybridShare * 4f + 0.5f) : 0;
            int other = quarter > 0 ? g.HybridSpecies : species;
            return (species & 0xFF) | ((other & 0xFF) << 8) | (quarter << 16) | (g.Visible << 20);
        }

        public static int TypeOf(AgentStore a, int i) => TypeOf(a.Species[i], a.Genes[i]);

        /// <summary>The main species of a type.</summary>
        public static byte SpeciesOfType(int type) => (byte)(type & 0xFF);
        /// <summary>The other species in a hybrid type (the same as the main one for pure-bred types).</summary>
        public static byte OtherOfType(int type) => (byte)((type >> 8) & 0xFF);
        /// <summary>The hybrid mix of a type, in quarters (0 = pure-bred, 2 = half and half).</summary>
        public static int QuartersOfType(int type) => (type >> 16) & 0xF;
        /// <summary>The mutations that show on frogs of a type.</summary>
        public static ushort MutationsOfType(int type) => (ushort)((type >> 20) & 0x3FF);

        /// <summary>True for a type that is new: a hybrid, or one with mutations. Plain species are already known.</summary>
        public static bool IsNewType(int type) => QuartersOfType(type) > 0 || MutationsOfType(type) != 0;

        /// <summary>
        /// A plain description of a type, for example "Toothy, blue White's tree frog" or
        /// "Fire-bellied × Yellow-bellied hybrid (half and half)".
        /// </summary>
        public static string Describe(int type)
        {
            var main = SpeciesCatalog.FrogOf(SpeciesOfType(type));
            int quarters = QuartersOfType(type);
            string name;
            if (quarters > 0)
            {
                var other = SpeciesCatalog.FrogOf(OtherOfType(type));
                name = main.ShortName + " × " + other.ShortName + " hybrid (" + (quarters >= 2 ? "half and half" : "a quarter " + other.ShortName) + ")";
            }
            else name = main.Name;
            string muts = Mutations.Names(MutationsOfType(type));
            return muts.Length == 0 ? name : name + " with " + muts.ToLowerInvariant();
        }

        /// <summary>
        /// The child's generation count: one more than its parents' when mother, father and child are
        /// all the same type, otherwise 0 (a new type starts here).
        /// </summary>
        public static byte NextGeneration(int childType, byte motherSpecies, in Genome mother, byte fatherSpecies, in Genome father)
        {
            if (TypeOf(motherSpecies, mother) != childType || TypeOf(fatherSpecies, father) != childType) return 0;
            int g = (mother.Generation < father.Generation ? mother.Generation : father.Generation) + 1;
            return (byte)(g > 255 ? 255 : g);
        }
    }

    /// <summary>A new species the player registered and named. Saved.</summary>
    public sealed class RegisteredSpecies
    {
        /// <summary>The type of frog it covers (see <see cref="Lineage.TypeOf(byte, in Genome)"/>).</summary>
        public int Type;
        public string Name;
        /// <summary>Simulated time when it was registered.</summary>
        public double Time;
    }
}

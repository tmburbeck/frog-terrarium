using System.IO;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// A frog's inherited traits. Each young frog gets a mix of its mother's and father's values,
    /// plus a small random wobble, so a family looks alike but no two frogs are identical.
    /// Breeding the frogs you like best slowly shifts the line (for example towards bigger frogs,
    /// or frogs that cope with a cooler tank).
    ///
    /// Every trait has a limit, so no frog can drift to something silly. On top of the seven
    /// blended traits, a frog carries rare mutations (<see cref="Alleles"/>, passed on like real
    /// genes), its hybrid ancestry, and how many generations its family has bred true.
    ///
    /// It is a small struct of plain numbers, so it can sit in an array per agent and be saved
    /// as-is. Add new traits to the end and bump the save version.
    /// </summary>
    public struct Genome
    {
        /// <summary>Body size compared to the species average (0.8 to 1.25).</summary>
        public float Size;
        /// <summary>Colour shift from the species colour: -1 darker and duller, +1 lighter and brighter.</summary>
        public float Hue;
        /// <summary>°C added to the species' comfortable temperature range (-3 to +3).</summary>
        public float HeatShift;
        /// <summary>Humidity points added to the species' comfortable range (-8 to +8).</summary>
        public float DampShift;
        /// <summary>0 shy (stresses quickly) to 1 bold (stays calm longer). 0.5 is average.</summary>
        public float Boldness;
        /// <summary>Lifespan compared to the species average (0.8 to 1.2).</summary>
        public float Longevity;
        /// <summary>Breeding readiness and clutch size compared to average (0.6 to 1.4).</summary>
        public float Fertility;

        public const int TraitCount = 7;

        // Phase 3 (save version 6).
        /// <summary>Two bits per <see cref="Mutation"/>: how many copies (0, 1 or 2) this frog carries.</summary>
        public uint Alleles;
        /// <summary>For a hybrid: the other species in its family tree (index into SpeciesCatalog.Frogs).</summary>
        public byte HybridSpecies;
        /// <summary>
        /// For a hybrid: how much of it is <see cref="HybridSpecies"/>, from 0 (pure-bred) to 0.5
        /// (a first-generation cross, half and half). The rest is the frog's main species.
        /// </summary>
        public float HybridShare;
        /// <summary>
        /// Generations in a row this frog's family has looked the same (same species mix and the
        /// same visible mutations in both parents and the child). 0 for a frog whose parents differed.
        /// A new type that reaches 3 has "bred true" and can be registered as a new species.
        /// </summary>
        public byte Generation;

        /// <summary>True for a hybrid (a frog with some of another species in it).</summary>
        public bool IsHybrid => HybridShare > 0.001f;

        /// <summary>The mutations that show on this frog, one bit each.</summary>
        public ushort Visible => Mutations.Visible(Alleles);

        /// <summary>An exactly average frog: what frogs from old saves get.</summary>
        public static Genome Default => new Genome
        {
            Size = 1f, Hue = 0f, HeatShift = 0f, DampShift = 0f, Boldness = 0.5f, Longevity = 1f, Fertility = 1f,
        };

        /// <summary>A frog from outside the tank (starting frogs, shop frogs): average give or take a little.</summary>
        public static Genome Wild(SeededRandom rng)
        {
            var g = new Genome
            {
                Size = 1f + rng.Range(-0.08f, 0.08f),
                Hue = rng.Range(-0.4f, 0.4f),
                HeatShift = rng.Range(-0.8f, 0.8f),
                DampShift = rng.Range(-2f, 2f),
                Boldness = rng.Range(0.3f, 0.7f),
                Longevity = 1f + rng.Range(-0.06f, 0.06f),
                Fertility = 1f + rng.Range(-0.15f, 0.15f),
            };
            return g.Clamped();
        }

        /// <summary>
        /// A child's genes: for each trait, somewhere between the two parents, plus a small random
        /// wobble. Uses only the random stream it is given, so the same seed gives the same family.
        /// </summary>
        public static Genome Inherit(in Genome mother, in Genome father, SeededRandom rng)
        {
            var g = new Genome
            {
                Size = Mix(mother.Size, father.Size, 0.03f, rng),
                Hue = Mix(mother.Hue, father.Hue, 0.12f, rng),
                HeatShift = Mix(mother.HeatShift, father.HeatShift, 0.3f, rng),
                DampShift = Mix(mother.DampShift, father.DampShift, 0.8f, rng),
                Boldness = Mix(mother.Boldness, father.Boldness, 0.06f, rng),
                Longevity = Mix(mother.Longevity, father.Longevity, 0.02f, rng),
                Fertility = Mix(mother.Fertility, father.Fertility, 0.05f, rng),
            };
            return g.Clamped();
        }

        private static float Mix(float a, float b, float wobble, SeededRandom rng)
        {
            float t = rng.NextFloat();
            return a + (b - a) * t + rng.Range(-wobble, wobble);
        }

        /// <summary>The same genes with every trait kept inside its limits.</summary>
        public Genome Clamped()
        {
            var g = this;
            g.Size = Clamp(g.Size, 0.8f, 1.25f);
            g.Hue = Clamp(g.Hue, -1f, 1f);
            g.HeatShift = Clamp(g.HeatShift, -3f, 3f);
            g.DampShift = Clamp(g.DampShift, -8f, 8f);
            g.Boldness = Clamp(g.Boldness, 0f, 1f);
            g.Longevity = Clamp(g.Longevity, 0.8f, 1.2f);
            g.Fertility = Clamp(g.Fertility, 0.6f, 1.4f);
            return g;
        }

        /// <summary>A short, plain description of anything unusual about these genes, for the inspector.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            void Note(string s) { if (sb.Length > 0) sb.Append(", "); sb.Append(s); }
            if (Size >= 1.08f) Note("big");
            else if (Size <= 0.92f) Note("small");
            if (HeatShift >= 0.75f) Note("likes it warmer");
            else if (HeatShift <= -0.75f) Note("likes it cooler");
            if (DampShift >= 2.5f) Note("likes it damper");
            else if (DampShift <= -2.5f) Note("copes with drier air");
            if (Boldness >= 0.7f) Note("bold");
            else if (Boldness <= 0.3f) Note("shy");
            if (Fertility >= 1.15f) Note("very fertile");
            if (Longevity >= 1.08f) Note("long-lived");
            return sb.Length == 0 ? "average" : sb.ToString();
        }

        /// <summary>Writes the genes in the current save format (version 6: the seven traits, then the Phase 3 extras).</summary>
        internal void Write(BinaryWriter w)
        {
            w.Write(Size); w.Write(Hue); w.Write(HeatShift); w.Write(DampShift);
            w.Write(Boldness); w.Write(Longevity); w.Write(Fertility);
            w.Write(Alleles);
            w.Write(HybridSpecies);
            w.Write(HybridShare);
            w.Write(Generation);
        }

        /// <summary>Reads genes. Saves before version 6 have only the seven traits: no mutations, pure-bred.</summary>
        internal static Genome Read(BinaryReader r, bool phase3)
        {
            var g = Read(r);
            if (!phase3) return g;
            uint alleles = r.ReadUInt32();
            for (int m = Mutations.Count; m < 16; m++)
                if (((alleles >> (2 * m)) & 3u) != 0u) throw new SaveFormatException("Damaged save: unknown mutation.");
            for (int m = 0; m < Mutations.Count; m++)
                if (((alleles >> (2 * m)) & 3u) == 3u) throw new SaveFormatException("Damaged save: bad mutation.");
            g.Alleles = alleles;
            g.HybridSpecies = r.ReadByte();
            if (g.HybridSpecies >= SpeciesCatalog.Frogs.Length) throw new SaveFormatException("Damaged save: unknown hybrid species.");
            float share = r.ReadSingle();
            if (float.IsNaN(share)) throw new SaveFormatException("Damaged save: bad genes.");
            g.HybridShare = share < 0f ? 0f : (share > 0.5f ? 0.5f : share);
            g.Generation = r.ReadByte();
            return g;
        }

        /// <summary>The seven traits, as saved by versions 4 and 5.</summary>
        private static Genome Read(BinaryReader r)
        {
            var g = new Genome
            {
                Size = r.ReadSingle(), Hue = r.ReadSingle(), HeatShift = r.ReadSingle(), DampShift = r.ReadSingle(),
                Boldness = r.ReadSingle(), Longevity = r.ReadSingle(), Fertility = r.ReadSingle(),
            };
            if (float.IsNaN(g.Size) || float.IsNaN(g.Hue) || float.IsNaN(g.HeatShift) || float.IsNaN(g.DampShift) ||
                float.IsNaN(g.Boldness) || float.IsNaN(g.Longevity) || float.IsNaN(g.Fertility))
                throw new SaveFormatException("Damaged save: bad genes.");
            return g.Clamped();
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}

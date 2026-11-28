using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>A colour as three numbers from 0 to 1 (red, green, blue), in the usual sRGB space.</summary>
    public struct Rgb
    {
        public float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        public Rgb Times(float k) => new Rgb(Clamp01(R * k), Clamp01(G * k), Clamp01(B * k));
        internal static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>
    /// Everything the frog skin shader needs to paint one frog: its colours, how much of each kind
    /// of marking it has, how warty and how shiny it is, and a number that makes its pattern its
    /// own. Worked out from the species (blended for hybrids), the colour gene, colour mutations,
    /// age and the frog's id, so the same frog always looks the same, and no two frogs match.
    /// </summary>
    public struct SkinLook
    {
        public Rgb Back, Mark, Belly, Toe, Iris;
        public float Spots, Blotches, Bars, Lip, Warts, Gloss, ToeTint, PupilShape;
        /// <summary>Moves the pattern so each frog's spots and blotches fall in different places (0 to 1000).</summary>
        public float Seed;
        /// <summary>How strongly the markings stand out (the colour gene makes some frogs bolder).</summary>
        public float Contrast;
    }

    public static class FrogSkin
    {
        /// <summary>The look of a frog (or froglet, juvenile, adult or elder).</summary>
        public static SkinLook For(FrogSpecies sp, in Genome genes, int id, bool elder)
        {
            ushort muts = genes.Visible;
            bool albino = Mutations.Shows(muts, Mutation.Albino);
            bool blue = Mutations.Shows(muts, Mutation.Blue);
            bool black = Mutations.Shows(muts, Mutation.Melanistic) && !albino;

            var look = new SkinLook
            {
                Back = new Rgb(sp.ColourR, sp.ColourG, sp.ColourB),
                Mark = new Rgb(sp.MarkR, sp.MarkG, sp.MarkB),
                Belly = new Rgb(sp.BellyR, sp.BellyG, sp.BellyB),
                Toe = new Rgb(sp.ToeR, sp.ToeG, sp.ToeB),
                Iris = new Rgb(sp.EyeR, sp.EyeG, sp.EyeB),
                Spots = sp.Spots, Blotches = sp.Blotches, Bars = sp.Bars, Lip = sp.Lip,
                Warts = sp.Warts, Gloss = sp.Gloss, ToeTint = sp.ToeTint, PupilShape = sp.PupilShape,
                Seed = Seed(id),
            };

            if (albino)
            {
                // No dark colour at all: cream skin, pale yellow markings and pink-red eyes.
                look.Back = new Rgb(0.96f, 0.92f, 0.86f);
                look.Mark = Rgb.Lerp(look.Mark, new Rgb(0.98f, 0.88f, 0.55f), 0.8f);
                look.Belly = new Rgb(1f, 0.95f, 0.92f);
                look.Toe = Rgb.Lerp(look.Toe, new Rgb(1f, 0.8f, 0.75f), 0.6f);
                look.Iris = new Rgb(0.95f, 0.35f, 0.4f);
            }
            else if (blue)
            {
                // Blue frogs are missing their yellow pigment, so green parts turn sky blue.
                look.Back = new Rgb(0.27f, 0.56f, 0.92f);
            }
            if (black)
            {
                look.Back = look.Back.Times(0.28f);
                look.Mark = look.Mark.Times(0.5f);
                look.Belly = look.Belly.Times(0.45f);
                look.Toe = look.Toe.Times(0.5f);
            }

            // The colour gene: darker and duller at -1, lighter and brighter at +1; bolder markings at +1.
            float hue = Math.Max(-1f, Math.Min(1f, genes.Hue));
            look.Back = look.Back.Times(0.88f + 0.12f * hue);
            look.Contrast = 0.8f + 0.2f * hue;

            if (elder)
            {
                // Old frogs fade a little and lose some of their shine.
                var grey = new Rgb(0.45f, 0.44f, 0.38f);
                look.Back = Rgb.Lerp(look.Back, grey, 0.35f);
                look.Belly = Rgb.Lerp(look.Belly, new Rgb(0.7f, 0.68f, 0.62f), 0.3f);
                look.Contrast *= 0.8f;
                look.Gloss *= 0.8f;
            }
            return look;
        }

        /// <summary>A tadpole: dark brown-olive with gold flecks, smooth and wet.</summary>
        public static SkinLook Tadpole(int id) => new SkinLook
        {
            Back = new Rgb(0.2f, 0.18f, 0.12f),
            Mark = new Rgb(0.75f, 0.62f, 0.3f),
            Belly = new Rgb(0.42f, 0.4f, 0.32f),
            Toe = new Rgb(0.2f, 0.18f, 0.12f),
            Iris = new Rgb(0.6f, 0.5f, 0.2f),
            Spots = 0.4f, Gloss = 0.8f, Contrast = 0.8f,
            Seed = Seed(id),
        };

        /// <summary>A number from 0 to 1000 that is the same for a frog every time, and differs from frog to frog.</summary>
        public static float Seed(int id)
        {
            uint h = (uint)id * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h % 100000u) / 100f;
        }
    }
}

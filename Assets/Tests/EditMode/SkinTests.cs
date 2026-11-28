using System;
using System.Collections.Generic;
using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Graphics part 2: the numbers that paint each frog's skin and eyes.</summary>
    public class SkinTests
    {
        private static float Diff(Rgb a, Rgb b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

        private static void AssertInRange(SkinLook l, string name)
        {
            foreach (var c in new[] { l.Back, l.Mark, l.Belly, l.Toe, l.Iris })
            {
                Assert.That(c.R, Is.InRange(0f, 1f), name);
                Assert.That(c.G, Is.InRange(0f, 1f), name);
                Assert.That(c.B, Is.InRange(0f, 1f), name);
            }
            foreach (float v in new[] { l.Spots, l.Blotches, l.Bars, l.Lip, l.Warts, l.Gloss, l.ToeTint, l.PupilShape, l.Contrast })
                Assert.That(v, Is.InRange(0f, 1f), name);
            Assert.That(l.Seed, Is.InRange(0f, 1000f), name);
        }

        [Test]
        public void EverySpecies_HasItsOwnLook()
        {
            var looks = new List<SkinLook>();
            foreach (var sp in SpeciesCatalog.Frogs)
            {
                var l = FrogSkin.For(sp, Genome.Default, 7, false);
                AssertInRange(l, sp.Name);
                looks.Add(l);
            }
            for (int a = 0; a < looks.Count; a++)
                for (int b = a + 1; b < looks.Count; b++)
                {
                    float d = Diff(looks[a].Back, looks[b].Back) + Diff(looks[a].Mark, looks[b].Mark)
                        + Math.Abs(looks[a].Spots - looks[b].Spots) + Math.Abs(looks[a].Blotches - looks[b].Blotches)
                        + Math.Abs(looks[a].Bars - looks[b].Bars) + Math.Abs(looks[a].Lip - looks[b].Lip) + Math.Abs(looks[a].Warts - looks[b].Warts);
                    Assert.Greater(d, 0.15f, SpeciesCatalog.Frogs[a].Name + " and " + SpeciesCatalog.Frogs[b].Name + " look different");
                }
        }

        [Test]
        public void Species_GetTheirRealMarkings()
        {
            var redEyed = FrogSkin.For(SpeciesCatalog.FrogOf(1), Genome.Default, 1, false);
            Assert.Greater(redEyed.Bars, 0.5f, "red-eyed tree frogs have barred flanks");
            Assert.Greater(redEyed.PupilShape, 0.5f, "and upright slit pupils");
            var fireBelly = FrogSkin.For(SpeciesCatalog.FrogOf(2), Genome.Default, 1, false);
            Assert.Greater(fireBelly.Blotches, 0.5f, "fire-bellied toads are blotched");
            Assert.Greater(fireBelly.Warts, 0.5f, "and warty");
            var whiteLipped = FrogSkin.For(SpeciesCatalog.FrogOf(3), Genome.Default, 1, false);
            Assert.Greater(whiteLipped.Lip, 0.5f, "white-lipped tree frogs have a white lip stripe");
            var whites = FrogSkin.For(SpeciesCatalog.Frog, Genome.Default, 1, false);
            Assert.Less(whites.Warts, 0.2f, "White's tree frogs are smooth");
            Assert.Less(whites.PupilShape, 0.5f, "with a wide pupil");
        }

        [Test]
        public void Hybrids_MixTheirParentsPatterns()
        {
            var whites = SpeciesCatalog.Frog;
            var redEyed = SpeciesCatalog.FrogOf(1);
            var g = Genome.Default;
            g.HybridSpecies = 1;
            g.HybridShare = 0.5f;
            var hybrid = FrogSkin.For(SpeciesCatalog.FrogFor(0, g), g, 3, false);
            Assert.Greater(hybrid.Bars, whites.Bars);
            Assert.Less(hybrid.Bars, redEyed.Bars);
            Assert.Greater(hybrid.PupilShape, whites.PupilShape);
            Assert.Less(hybrid.PupilShape, redEyed.PupilShape);
        }

        [Test]
        public void Mutations_ChangeTheColours()
        {
            var sp = SpeciesCatalog.FrogOf(1);
            var plain = FrogSkin.For(sp, Genome.Default, 5, false);
            var g = Genome.Default;
            g.Alleles = Mutations.WithCopies(0u, Mutation.Albino, 2);
            var albino = FrogSkin.For(sp, g, 5, false);
            Assert.Greater(albino.Back.R + albino.Back.G + albino.Back.B, 2.2f, "albinos are pale");
            Assert.Greater(albino.Iris.R, albino.Iris.G, "with pink-red eyes");
            g.Alleles = Mutations.WithCopies(0u, Mutation.Melanistic, 2);
            var black = FrogSkin.For(sp, g, 5, false);
            Assert.Less(black.Back.R + black.Back.G + black.Back.B, (plain.Back.R + plain.Back.G + plain.Back.B) * 0.5f, "melanistic frogs are dark");
            AssertInRange(albino, "albino");
            AssertInRange(black, "black");
        }

        [Test]
        public void EachFrog_HasItsOwnPattern_AndKeepsIt()
        {
            var seeds = new HashSet<float>();
            for (int id = 1; id <= 200; id++)
            {
                float s = FrogSkin.Seed(id);
                Assert.That(s, Is.InRange(0f, 1000f));
                Assert.AreEqual(s, FrogSkin.Seed(id), "the same frog always gets the same pattern");
                seeds.Add(s);
            }
            Assert.Greater(seeds.Count, 195, "frogs almost never share a pattern");
        }

        [Test]
        public void Elders_FadeAndTheColourGeneShows()
        {
            var sp = SpeciesCatalog.FrogOf(1);
            var young = FrogSkin.For(sp, Genome.Default, 9, false);
            var old = FrogSkin.For(sp, Genome.Default, 9, true);
            Assert.Less(old.Gloss, young.Gloss);
            Assert.Less(old.Contrast, young.Contrast);
            var g = Genome.Default;
            g.Hue = 1f;
            var bright = FrogSkin.For(sp, g, 9, false);
            g.Hue = -1f;
            var dull = FrogSkin.For(sp, g, 9, false);
            Assert.Greater(bright.Back.G, dull.Back.G);
            Assert.Greater(bright.Contrast, dull.Contrast);
            AssertInRange(FrogSkin.Tadpole(4), "tadpole");
        }
    }
}

using System;
using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 5, part 4: the frog's shape, its moves and its sounds, all made in code.</summary>
    public class LooksTests
    {
        private static void AssertSound(MeshData m, string name)
        {
            Assert.Greater(m.TriangleCount, 10, name);
            Assert.AreEqual(m.Positions.Length, m.Normals.Length, name);
            foreach (int t in m.Triangles) Assert.That(t, Is.InRange(0, m.Positions.Length - 1), name);
            foreach (var n in m.Normals) Assert.AreEqual(1f, n.Length, 1e-3f, name + " normals are unit length");
            foreach (var p in m.Positions) Assert.IsFalse(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z), name);
            Assert.Less(m.Positions.Length, 65000, name + " fits Unity's 16-bit index meshes");
        }

        /// <summary>Normals point away from the middle on a closed, roughly round shape.</summary>
        private static void AssertOutward(MeshData m, V3 centre, string name)
        {
            int outward = 0;
            for (int k = 0; k < m.Positions.Length; k++)
                if (V3.Dot(m.Normals[k], m.Positions[k] - centre) > 0f) outward++;
            Assert.Greater(outward, m.Positions.Length * 0.95f, name + " faces outwards (so it is not drawn inside out)");
        }

        [Test]
        public void Shapes_AreWellFormed()
        {
            var body = FrogShapes.Body();
            AssertSound(body, "body");
            AssertSound(FrogShapes.Belly(body), "belly");
            AssertSound(FrogShapes.Ball(), "ball");
            AssertSound(FrogShapes.Limb(), "limb");
            AssertSound(FrogShapes.Foot(), "foot");
            AssertSound(FrogShapes.TailPiece(), "tail");
            AssertOutward(body, new V3(0f, 0f, 0f), "body");
            AssertOutward(FrogShapes.Ball(), new V3(0f, 0f, 0f), "ball");
            AssertOutward(FrogShapes.Limb(), new V3(0f, 0f, 0.5f), "limb");
        }

        [Test]
        public void Body_IsFrogShaped()
        {
            var body = FrogShapes.Body();
            body.Bounds(out var min, out var max);
            Assert.That(max.X - min.X, Is.InRange(0.95f, 1.15f), "about 1 wide");
            Assert.That(max.Z - min.Z, Is.InRange(0.9f, 1.05f), "about 1 long before stretching");
            Assert.Less(max.Y - min.Y, 0.9f, "flatter than a ball");
            Assert.Greater(-min.Y, 0.15f);
            Assert.Less(-min.Y, max.Y, "a flat belly: more above the middle than below");

            // Wider across the hips than across the waist.
            float Width(float zFrom, float zTo)
            {
                float w = 0f;
                foreach (var p in body.Positions) if (p.Z > zFrom && p.Z < zTo) w = Math.Max(w, Math.Abs(p.X));
                return w;
            }
            Assert.Greater(Width(-0.25f, -0.15f), Width(0.05f, 0.12f));

            var belly = FrogShapes.Belly(body);
            belly.Bounds(out var bmin, out var bmax);
            Assert.Less(bmax.Y, -0.05f, "the belly is only the underside");
        }

        [Test]
        public void Limb_RunsFromZeroToOneAndTapers()
        {
            var limb = FrogShapes.Limb(0.5f);
            limb.Bounds(out var min, out var max);
            Assert.AreEqual(0f, min.Z, 0.01f);
            Assert.AreEqual(1f, max.Z, 0.01f);
            float start = 0f, end = 0f;
            foreach (var p in limb.Positions)
            {
                if (p.Z > 0.1f && p.Z < 0.2f) start = Math.Max(start, Math.Abs(p.X));
                if (p.Z > 0.8f && p.Z < 0.9f) end = Math.Max(end, Math.Abs(p.X));
            }
            Assert.Less(end, start * 0.8f);
        }

        [Test]
        public void Hop_StartsAndEndsSitting_AndChangesSmoothly()
        {
            var a = FrogMoves.Hop(0f);
            var b = FrogMoves.Hop(1f);
            var sit = FrogMoves.Sitting;
            foreach (var p in new[] { a, b })
            {
                Assert.AreEqual(sit.Lift, p.Lift, 1e-4f);
                Assert.AreEqual(sit.Stretch, p.Stretch, 1e-4f);
                Assert.AreEqual(sit.Reach, p.Reach, 1e-4f);
                Assert.AreEqual(sit.Pitch, p.Pitch, 1e-3f);
            }
            float highest = 0f, stretched = 0f;
            var prev = FrogMoves.Hop(0f);
            for (int k = 1; k <= 400; k++)
            {
                var p = FrogMoves.Hop(k / 400f);
                Assert.Less(Math.Abs(p.Lift - prev.Lift), 0.02f, "no jumps in height at " + k);
                Assert.Less(Math.Abs(p.Stretch - prev.Stretch), 0.05f, "no snapping legs at " + k);
                Assert.Less(Math.Abs(p.Pitch - prev.Pitch), 2f, "no snapping body at " + k);
                Assert.GreaterOrEqual(p.Lift, 0f);
                highest = Math.Max(highest, p.Lift);
                if (p.Lift > 0.3f) stretched = Math.Max(stretched, p.Stretch);
                prev = p;
            }
            Assert.AreEqual(FrogMoves.HopHeight, highest, 0.01f);
            Assert.Greater(stretched, 0.9f, "legs straight out in the air");
            Assert.Less(FrogMoves.Hop(0.2f).Pitch, 0f, "nose up on take-off");
            Assert.Greater(FrogMoves.Hop(0.6f).Pitch, 0f, "nose down to land");
        }

        [Test]
        public void Swim_KicksOutToTheSides()
        {
            Assert.AreEqual(1f, FrogMoves.Swim(0f).Stretch, 1e-4f);
            Assert.AreEqual(1f, FrogMoves.Swim(0.99999f).Stretch, 1e-3f);
            Assert.Greater(FrogMoves.Swim(0.5f).Splay, 0.8f);
            Assert.AreEqual(0f, FrogMoves.Swim(0.5f).Lift);
        }

        [Test]
        public void Breathing_Blinking_AndTheThroat()
        {
            for (float t = 0f; t < 10f; t += 0.05f) Assert.That(FrogMoves.Breath(t, 3), Is.InRange(0.979f, 1.021f));
            int shut = 0, frames = 0;
            for (float t = 0f; t < 30f; t += 0.01f, frames++) if (FrogMoves.EyesOpen(t, 5) < 0.2f) shut++;
            Assert.That(shut, Is.InRange(1, frames / 20), "blinks now and then, eyes open most of the time");
            float FirstBlink(int id) { for (float t = 0f; t < 10f; t += 0.01f) if (FrogMoves.EyesOpen(t, id) < 0.2f) return t; return -1f; }
            Assert.Greater(Math.Abs(FirstBlink(1) - FirstBlink(2)), 0.2f, "frogs do not all blink together");

            Assert.AreEqual(0f, FrogMoves.Throat(-1f, 1f, 3f));
            Assert.AreEqual(0f, FrogMoves.Throat(2f, 1f, 3f));
            float most = 0f;
            for (float t = 0f; t < 1f; t += 0.005f) most = Math.Max(most, FrogMoves.Throat(t, 1f, 3f));
            Assert.Greater(most, 0.9f, "the sac fills while calling");
        }

        [Test]
        public void EverySpecies_HasItsOwnCall()
        {
            var pitches = new float[SpeciesCatalog.Frogs.Length];
            for (int k = 0; k < pitches.Length; k++)
            {
                var sp = SpeciesCatalog.Frogs[k];
                var call = SoundSynth.Call(sp);
                Assert.AreEqual((int)(SoundSynth.CallLength(sp) * SoundSynth.SampleRate), call.Length, sp.Name);
                float peak = 0f;
                foreach (float v in call)
                {
                    Assert.IsFalse(float.IsNaN(v), sp.Name);
                    peak = Math.Max(peak, Math.Abs(v));
                }
                Assert.That(peak, Is.InRange(0.5f, 1f), sp.Name + " is loud enough and never clips");
                pitches[k] = SoundSynth.RoughPitch(call);
                CollectionAssert.AreEqual(call, SoundSynth.Call(sp), sp.Name + " sounds the same every time");
            }
            int redEyed = Array.FindIndex(SpeciesCatalog.Frogs, s => s.ShortName == "Red-eyed");
            Assert.Greater(pitches[redEyed], pitches[0] * 2f, "little red-eyed frogs chirp higher than White's tree frogs croak");
        }

        [Test]
        public void Hybrids_SoundBetweenTheirParents()
        {
            var a = SpeciesCatalog.Frogs[0];
            var b = Array.Find(SpeciesCatalog.Frogs, s => s.Genus == a.Genus && s != a);
            Assert.IsNotNull(b, "another species in the same genus");
            var g = Genome.Default;
            g.HybridSpecies = b.Id;
            g.HybridShare = 0.5f;
            var mix = SpeciesCatalog.FrogFor(a.Id, g);
            Assert.That(mix.CallPitch, Is.InRange(Math.Min(a.CallPitch, b.CallPitch), Math.Max(a.CallPitch, b.CallPitch)));
            Assert.AreNotEqual(a.CallPitch, mix.CallPitch);
        }

        [Test]
        public void EveryOtherSound_IsMade()
        {
            foreach (SoundKind kind in Enum.GetValues(typeof(SoundKind)))
            {
                var s = SoundSynth.Make(kind);
                Assert.Greater(s.Length, SoundSynth.SampleRate / 20, kind.ToString());
                float peak = 0f;
                foreach (float v in s) { Assert.IsFalse(float.IsNaN(v)); peak = Math.Max(peak, Math.Abs(v)); }
                Assert.That(peak, Is.InRange(0.3f, 1f), kind + " is audible and never clips");
            }
            Assert.Greater(SoundSynth.RoughPitch(SoundSynth.Make(SoundKind.Cricket)), 3000f, "crickets chirp high");
        }
    }
}

using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void DifferentSeeds_GiveDifferentSequences()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.NextUInt() == b.NextUInt()) same++;
            Assert.Less(same, 3);
        }

        [Test]
        public void MatchesReferencePcg32()
        {
            // Values from the reference PCG32 demo (seed 42, stream 54). This locks the
            // algorithm: if it fails, every saved map seed would change.
            var r = new SeededRandom(42, 54);
            Assert.AreEqual(0xa15c02b7u, r.NextUInt());
            Assert.AreEqual(2068313097u, r.NextUInt());
            Assert.AreEqual(3122475824u, r.NextUInt());
        }

        [Test]
        public void RestoredState_ContinuesTheSameSequence()
        {
            var a = new SeededRandom(7);
            for (int i = 0; i < 50; i++) a.NextUInt();
            var b = SeededRandom.FromState(a.State, a.Increment);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void Ranges_StayInBounds()
        {
            var r = new SeededRandom(99);
            for (int i = 0; i < 10000; i++)
            {
                float f = r.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                int n = r.Range(-3, 4);
                Assert.That(n, Is.InRange(-3, 3));
            }
        }

        [Test]
        public void RandomService_StreamsAreIndependentOfCreationOrder()
        {
            var s1 = new RandomService(500);
            uint m1 = s1.Get("movement").NextUInt();
            var s2 = new RandomService(500);
            s2.Get("genetics").NextUInt();
            s2.Get("mapgen").NextUInt();
            uint m2 = s2.Get("movement").NextUInt();
            Assert.AreEqual(m1, m2, "Adding a new stream must not change another stream's numbers");
        }

        [Test]
        public void Fnv1a_IsStable()
        {
            // Standard FNV-1a 64 test vectors.
            Assert.AreEqual(14695981039346656037UL, RandomService.Fnv1a64(""));
            Assert.AreEqual(0xaf63dc4c8601ec8cUL, RandomService.Fnv1a64("a"));
        }
    }
}

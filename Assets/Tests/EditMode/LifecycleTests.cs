using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 2: growing up, breeding, families, genes, species and escapes.</summary>
    public class LifecycleTests
    {
        /// <summary>A flat tank at conditions White's tree frogs like, with a pool dug at x = 6.</summary>
        private static World PondWorld(ulong seed = 1)
        {
            var w = new World(seed);
            w.Environment.AmbientTemperature = 26f;
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            TerrainBrush.Apply(w.Terrain, BrushMode.Lower, 6f, 0f, 2.5f, 1f, 1f);
            return w;
        }

        private static void Run(SimulationLoop loop, float seconds)
        {
            int frames = (int)(seconds * 10f);
            for (int i = 0; i < frames; i++) loop.Advance(0.1);
        }

        private static T SystemOf<T>(SimulationLoop loop) where T : class, ISimSystem
        {
            foreach (var e in loop.Systems) if (e.System is T t) return t;
            return null;
        }

        /// <summary>Keeps every land frog fed and watered, so a test is only about what it is testing.</summary>
        private static void Feed(World w)
        {
            var a = w.Agents;
            for (int i = 0; i < a.Count; i++)
                if (a.IsLandFrog(i)) { a.Hunger[i] = 0.05f; a.Thirst[i] = 0.05f; }
        }

        private static int CountStage(World w, LifeStage stage)
        {
            int n = 0;
            for (int i = 0; i < w.Agents.Count; i++)
                if (w.Agents.Kind[i] == AgentKind.Frog && w.Agents.Stage[i] == stage) n++;
            return n;
        }

        [Test]
        public void Egg_HatchesSwimsAsATadpole_ThenLeavesTheWaterAndGrowsUp()
        {
            var w = PondWorld();
            var mum = Genome.Default;
            int egg = w.AddEgg(6f, 0f, 0, Sex.Female, mum, mum);
            int id = w.Agents.Id[egg];
            var sp = SpeciesCatalog.Frog;
            var loop = SimulationFactory.CreateDefault(w);

            Assert.AreEqual(LifeStage.Egg, w.Agents.Stage[egg]);
            Run(loop, sp.EggSeconds + 20f);
            int i = w.Agents.IndexOf(id);
            Assert.AreEqual(LifeStage.Tadpole, w.Agents.Stage[i], "the egg hatches");

            // Tadpoles stay in the water however long they swim about.
            for (int k = 0; k < 60; k++)
            {
                Run(loop, 2f);
                i = w.Agents.IndexOf(id);
                Assert.GreaterOrEqual(i, 0, "the tadpole is still alive");
                if (w.Agents.Stage[i] == LifeStage.Tadpole)
                    Assert.IsTrue(w.Terrain.IsWater(w.Agents.PosX[i], w.Agents.PosZ[i]), "tadpoles never leave the water");
            }

            // Then a froglet that walks out, and on to grown-up, as long as it is looked after.
            for (int k = 0; k < 120 && w.Agents.Stage[w.Agents.IndexOf(id)] < LifeStage.Adult; k++)
            {
                Feed(w);
                Run(loop, 10f);
            }
            i = w.Agents.IndexOf(id);
            Assert.AreEqual(LifeStage.Adult, w.Agents.Stage[i], "it grows up");
            Assert.IsFalse(w.Terrain.IsWater(w.Agents.PosX[i], w.Agents.PosZ[i]) && w.Agents.Goal[i] != Goal.Drink,
                "a grown frog lives on land");
        }

        [Test]
        public void HungryOrUncomfortableYoung_GrowMoreSlowly()
        {
            // QA: growth speed depends on food and comfort.
            var w = PondWorld();
            var g = Genome.Default;
            var sp = SpeciesCatalog.Frog;
            int fed = w.AddEgg(-4f, -2f, 0, Sex.Female, g, g);
            int starved = w.AddEgg(-4f, 2f, 0, Sex.Male, g, g);
            int fedId = w.Agents.Id[fed], starvedId = w.Agents.Id[starved];
            foreach (int i in new[] { fed, starved })
            {
                w.Agents.Growth[i] = sp.JuvenileAt;
                w.Agents.Stage[i] = LifeStage.Juvenile;
            }
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 60; k++)
            {
                Feed(w);
                w.Agents.Hunger[w.Agents.IndexOf(starvedId)] = 1f;
                Run(loop, 1f);
            }
            float fedGrowth = w.Agents.Growth[w.Agents.IndexOf(fedId)] - sp.JuvenileAt;
            float starvedGrowth = w.Agents.Growth[w.Agents.IndexOf(starvedId)] - sp.JuvenileAt;
            Assert.Greater(fedGrowth, 50f, "a fed, comfy juvenile grows at nearly full speed");
            Assert.Less(starvedGrowth, fedGrowth * 0.5f, "a starving one grows far more slowly");
        }

        [Test]
        public void EggsAndTadpoles_DieIfThePoolIsDrained()
        {
            var w = PondWorld();
            var g = Genome.Default;
            for (int k = 0; k < 4; k++) w.AddEgg(6f + 0.1f * k, 0f, 0, Sex.Female, g, g);
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 5f);
            Assert.AreEqual(4, w.Agents.CountOf(AgentKind.Frog));
            w.Terrain.WaterLevel = -10f; // drain the tank
            Run(loop, 60f);
            Assert.AreEqual(0, w.Agents.CountOf(AgentKind.Frog));
        }

        [Test]
        public void HealthyPair_LaysEggsInThePool_AndTheYoungKnowBothParents()
        {
            var w = PondWorld();
            int mum = w.AddFrog(2f, 0f, 0, Sex.Female);
            int dad = w.AddFrog(2.5f, 1f, 0, Sex.Male);
            int mumId = w.Agents.Id[mum], dadId = w.Agents.Id[dad];
            w.Agents.BreedCooldown[mum] = 0f;
            w.Agents.BreedCooldown[dad] = 0f;
            for (int k = 0; k < 60; k++) w.AddInsect(-6f + 0.2f * k, -4f, w.Random.Get("test")); // plenty of food about
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);

            for (int k = 0; k < 600 && life.EggsLaid == 0; k++)
            {
                Feed(w);
                Run(loop, 1f);
            }
            Assert.Greater(life.EggsLaid, 0, "the pair breeds");
            int females = 0, males = 0;
            for (int i = 0; i < w.Agents.Count; i++)
            {
                if (w.Agents.Kind[i] != AgentKind.Frog || w.Agents.Stage[i] != LifeStage.Egg) continue;
                Assert.AreEqual(mumId, w.Agents.MotherId[i]);
                Assert.AreEqual(dadId, w.Agents.FatherId[i]);
                Assert.IsTrue(w.Terrain.IsWater(w.Agents.PosX[i], w.Agents.PosZ[i]), "eggs are laid in water");
                if (w.Agents.Sex[i] == Sex.Female) females++; else males++;
            }
            Assert.LessOrEqual(System.Math.Abs(females - males), 1, "a clutch is half female, half male");
            int m = w.Agents.IndexOf(mumId);
            Assert.AreEqual(0, w.Agents.MateId[m], "she is no longer carrying eggs");
            Assert.Greater(w.Agents.BreedCooldown[m], 0f, "and rests before breeding again");
        }

        [Test]
        public void DifferentSpecies_DoNotBreedTogether()
        {
            var w = PondWorld();
            int mum = w.AddFrog(2f, 0f, 0, Sex.Female);
            int dad = w.AddFrog(2.5f, 1f, 1, Sex.Male);
            w.Agents.BreedCooldown[mum] = 0f;
            w.Agents.BreedCooldown[dad] = 0f;
            for (int k = 0; k < 60; k++) w.AddInsect(-6f + 0.2f * k, -4f, w.Random.Get("test"));
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);
            for (int k = 0; k < 300; k++)
            {
                Feed(w);
                Run(loop, 1f);
            }
            Assert.AreEqual(0, life.EggsLaid, "White's and red-eyed tree frogs are in different genera, so they never pair up");
        }

        [Test]
        public void FullTank_StopsBreeding()
        {
            // A 9 x 5 m tank holds 3 frogs.
            var w = new World(3, 4.5f, 2.5f);
            w.Environment.AmbientTemperature = 26f;
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            TerrainBrush.Apply(w.Terrain, BrushMode.Lower, 3f, 0f, 1.2f, 1f, 1f);
            Assert.AreEqual(3, w.FrogCapacity);
            for (int k = 0; k < 3; k++)
            {
                int i = w.AddFrog(-1f + 0.5f * k, 0f, 0, k % 2 == 0 ? Sex.Female : Sex.Male);
                w.Agents.BreedCooldown[i] = 0f;
            }
            for (int k = 0; k < 30; k++) w.AddInsect(-4f + 0.2f * k, -2f, w.Random.Get("test"));
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);
            for (int k = 0; k < 300; k++)
            {
                Feed(w);
                Run(loop, 1f);
            }
            Assert.AreEqual(0, life.EggsLaid);
        }

        [Test]
        public void Genes_ComeFromBothParents_StayInLimits_AndRepeatWithTheSameSeed()
        {
            var mum = Genome.Default;
            mum.Size = 1.2f; mum.HeatShift = 2f; mum.Boldness = 0.9f;
            var dad = Genome.Default;
            dad.Size = 0.85f; dad.HeatShift = -2f; dad.Boldness = 0.1f;

            var rngA = new SeededRandom(42);
            var rngB = new SeededRandom(42);
            float sumSize = 0f;
            const int n = 400;
            for (int k = 0; k < n; k++)
            {
                var child = Genome.Inherit(mum, dad, rngA);
                var again = Genome.Inherit(mum, dad, rngB);
                Assert.AreEqual(child.Size, again.Size, "same seed, same family");
                Assert.AreEqual(child.HeatShift, again.HeatShift);
                Assert.That(child.Size, Is.InRange(0.8f, 1.25f));
                Assert.That(child.HeatShift, Is.InRange(-2.3f, 2.3f), "between the parents, give or take a little");
                Assert.That(child.Boldness, Is.InRange(0f, 1f));
                sumSize += child.Size;
            }
            Assert.AreEqual((1.2f + 0.85f) / 2f, sumSize / n, 0.03f, "on average, halfway between the parents");
        }

        [Test]
        public void Species_HaveTheirOwnComfortZones()
        {
            var whites = SpeciesCatalog.Frogs[0];
            var redEyed = SpeciesCatalog.Frogs[1];
            var fireBellied = SpeciesCatalog.Frogs[2];
            // Warm, fairly dry air: fine for White's tree frogs, too dry for red-eyed tree frogs.
            Assert.AreEqual(1f, whites.ComfortFor(26f, 55f));
            Assert.Less(redEyed.ComfortFor(26f, 55f), 0.6f);
            // Cool and damp: fire-bellied toads love it, the tree frogs do not.
            Assert.AreEqual(1f, fireBellied.ComfortFor(19f, 70f));
            Assert.Less(redEyed.ComfortFor(19f, 70f), 0.5f);
            // A frog whose genes like it warmer is happier a little above the species range.
            var warm = Genome.Default;
            warm.HeatShift = 2f;
            Assert.Greater(whites.ComfortFor(31.5f, 60f, warm), whites.ComfortFor(31.5f, 60f));
            // Every species has a unique id that matches its place in the catalogue.
            for (int k = 0; k < SpeciesCatalog.Frogs.Length; k++) Assert.AreEqual(k, SpeciesCatalog.Frogs[k].Id);
        }

        [Test]
        public void UnhappyFrog_IsWarnedAbout_ThenEscapes_AndCanBeBroughtBack()
        {
            // QA: escapes have a visible warning period and a recovery path.
            var w = new World(5);
            w.Environment.AmbientTemperature = 12f; // far too cold
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            int frog = w.AddFrog(0f, 0f, 0, Sex.Female);
            int id = w.Agents.Id[frog];
            var loop = SimulationFactory.CreateDefault(w);

            bool warned = false;
            double warnedAt = 0, escapedAt = 0;
            for (int k = 0; k < 1200 && w.Escaped.Count == 0; k++)
            {
                Feed(w);
                Run(loop, 1f);
                if (!warned && w.Alerts.Find("stressed") != null) { warned = true; warnedAt = w.SimTime; }
            }
            escapedAt = w.SimTime;
            Assert.IsTrue(warned, "the player is warned first");
            Assert.AreEqual(1, w.Escaped.Count, "then it climbs out");
            Assert.Greater(escapedAt - warnedAt, 60.0, "with at least a minute between warning and escape");
            Assert.AreEqual(id, w.Escaped[0].Id);
            Assert.AreEqual("too cold", w.Escaped[0].Reason);
            Assert.AreEqual(-1, w.Agents.IndexOf(id));
            Run(loop, 1f);
            Assert.IsNotNull(w.Alerts.Find(AlertSystem.EscapedCause), "the escape is reported");

            Assert.AreEqual(1, w.BringBackEscapedFrogs());
            Assert.AreEqual(0, w.Escaped.Count);
            int back = w.Agents.IndexOf(id);
            Assert.GreaterOrEqual(back, 0, "the same frog comes back");
            Assert.AreEqual(Sex.Female, w.Agents.Sex[back]);
            Assert.Less(w.Agents.Stress[back], 1f);
        }

        [Test]
        public void ComfortableFrogs_NeverTryToEscape()
        {
            var w = PondWorld();
            for (int k = 0; k < 6; k++) w.AddFrog(-6f + 2f * k, -2f);
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 600; k++)
            {
                Feed(w);
                Run(loop, 1f);
                for (int i = 0; i < w.Agents.Count; i++) Assert.AreNotEqual(Goal.Escape, w.Agents.Goal[i]);
            }
            Assert.AreEqual(0, w.Escaped.Count);
        }

        [Test]
        public void StarterTank_LeftAloneForTwoHours_StaysStable()
        {
            // QA: soak test. No player input at all; the feeder runs dry after about 20 minutes, so
            // the frogs live on the tank's own cricket colony. Populations must neither crash nor explode.
            var w = World.CreateStarterTank(12345, 8, 80);
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);
            int minFrogs = int.MaxValue, maxFrogs = 0;
            for (int minute = 1; minute <= 120; minute++)
            {
                Run(loop, 60f);
                int frogs = w.Agents.CountOf(AgentKind.Frog);
                if (minute >= 10) { minFrogs = System.Math.Min(minFrogs, frogs); maxFrogs = System.Math.Max(maxFrogs, frogs); }
                for (int i = 0; i < w.Agents.Count; i++)
                {
                    Assert.IsFalse(float.IsNaN(w.Agents.PosX[i]) || float.IsNaN(w.Agents.Stress[i]), "no broken numbers");
                    if (w.Agents.Kind[i] == AgentKind.Frog && w.Agents.Stage[i] == LifeStage.Tadpole)
                        Assert.IsTrue(w.Terrain.IsWater(w.Agents.PosX[i], w.Agents.PosZ[i]), "tadpoles stay in water");
                }
            }
            Assert.Greater(life.EggsLaid, 10, "frogs keep breeding");
            Assert.GreaterOrEqual(minFrogs, 8, "the population does not crash");
            Assert.LessOrEqual(maxFrogs, w.FrogCapacity, "or overflow the tank");
            Assert.Greater(w.Agents.CountOf(AgentKind.Insect), 20, "the crickets keep going too");
        }
    }
}

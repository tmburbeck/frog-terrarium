using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 3: mutations, hybrids and registering new species.</summary>
    public class GeneticsTests
    {
        private const byte Whites = 0, RedEyed = 1, FireBellied = 2, WhiteLipped = 3, Morelets = 4, YellowBellied = 5;

        /// <summary>A flat tank at conditions White's tree frogs like, with a pool dug at x = 6.</summary>
        private static World PondWorld(ulong seed = 1)
        {
            var w = new World(seed);
            w.Environment.AmbientTemperature = 26f;
            w.Environment.AmbientHumidity = 65f;
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

        private static void Feed(World w)
        {
            var a = w.Agents;
            for (int i = 0; i < a.Count; i++)
                if (a.IsLandFrog(i)) { a.Hunger[i] = 0.05f; a.Thirst[i] = 0.05f; a.Stress[i] = 0f; }
        }

        private static Genome With(Mutation m, int copies)
        {
            var g = Genome.Default;
            g.Alleles = Mutations.WithCopies(0u, m, copies);
            return g;
        }

        // ------------------------------------------------------------------ species

        [Test]
        public void EverySpecies_HasARelativeItCanInterbreedWith()
        {
            var frogs = SpeciesCatalog.Frogs;
            for (int i = 0; i < frogs.Length; i++)
            {
                Assert.AreEqual(i, frogs[i].Id, "species ids match their place in the list");
                bool partner = false;
                for (int j = 0; j < frogs.Length; j++)
                    if (j != i && SpeciesCatalog.CanInterbreed((byte)i, (byte)j)) partner = true;
                Assert.IsTrue(partner, frogs[i].Name + " has a relative to make hybrids with");
            }
            Assert.IsTrue(SpeciesCatalog.CanInterbreed(Whites, WhiteLipped));
            Assert.IsTrue(SpeciesCatalog.CanInterbreed(RedEyed, Morelets));
            Assert.IsTrue(SpeciesCatalog.CanInterbreed(FireBellied, YellowBellied));
            Assert.IsFalse(SpeciesCatalog.CanInterbreed(Whites, RedEyed));
            Assert.IsFalse(SpeciesCatalog.CanInterbreed(Whites, FireBellied));
        }

        // ------------------------------------------------------------------ mutations

        [Test]
        public void DominantMutation_ShowsWithOneCopy_RecessiveNeedsTwo()
        {
            Assert.IsTrue(Mutations.Shows(Mutations.Visible(With(Mutation.Teeth, 1).Alleles), Mutation.Teeth));
            Assert.IsFalse(Mutations.Shows(Mutations.Visible(With(Mutation.Blue, 1).Alleles), Mutation.Blue), "one copy of a recessive mutation is hidden");
            Assert.IsTrue(Mutations.Shows(Mutations.Visible(With(Mutation.Blue, 2).Alleles), Mutation.Blue));
            Assert.AreEqual("Blue", Mutations.Hidden(With(Mutation.Blue, 1).Alleles), "but the inspector can say it carries it");
        }

        [Test]
        public void HiddenRecessive_PopsUpInAboutAQuarterOfTheYoung_OfTwoCarriers()
        {
            // QA GE: Mendelian inheritance. Two carriers of one hidden copy each: 1 in 4 young show it.
            var rng = new SeededRandom(77);
            uint carrier = Mutations.WithCopies(0u, Mutation.Albino, 1);
            int shows = 0, carriers = 0;
            const int n = 4000;
            for (int k = 0; k < n; k++)
            {
                uint child = Mutations.Inherit(carrier, carrier, rng);
                int c = Mutations.Copies(child, Mutation.Albino);
                if (c == 2) shows++;
                if (c == 1) carriers++;
            }
            Assert.AreEqual(0.25, shows / (double)n, 0.03, "a quarter are albino");
            Assert.AreEqual(0.5, carriers / (double)n, 0.03, "half carry one hidden copy");
        }

        [Test]
        public void TwoParentsWithTwoCopies_AlwaysPassItOn_AndParentsWithoutNeverDo()
        {
            var rng = new SeededRandom(5);
            uint blue = Mutations.WithCopies(0u, Mutation.Blue, 2);
            for (int k = 0; k < 200; k++)
            {
                Assert.AreEqual(2, Mutations.Copies(Mutations.Inherit(blue, blue, rng), Mutation.Blue), "breeds true");
                Assert.AreEqual(0u, Mutations.Inherit(0u, 0u, rng), "no mutations come from nowhere during inheritance");
            }
        }

        [Test]
        public void NewMutations_AreRare_MoreCommonUnderStress_AndBoostable()
        {
            float calm = Mutations.ChancePerEgg(0f, false, false);
            Assert.AreEqual(0.01f, calm, 1e-6f, "about 1 egg in 100");
            Assert.Greater(Mutations.ChancePerEgg(1f, false, false), calm * 3.9f, "a very stressed mother: four times as likely");
            Assert.Greater(Mutations.ChancePerEgg(0f, true, false), calm * 1.9f, "hybrid parents: twice as likely");
            Assert.AreEqual(calm * Mutations.BoostFactor, Mutations.ChancePerEgg(0f, false, true), 1e-6f);

            // And it really happens at about that rate when eggs are laid.
            var w = PondWorld(3);
            var g = Genome.Default;
            int mutants = 0;
            const int eggs = 1000;
            for (int k = 0; k < eggs; k++)
            {
                int e = w.AddEgg(6f, 0f, Whites, g, Whites, g, Sex.Female, 0.25f);
                if (w.Agents.Genes[e].Alleles != 0u) mutants++;
                w.Agents.RemoveAt(e);
            }
            Assert.AreEqual(250, mutants, 45);
        }

        [Test]
        public void Mutations_ChangeHowAFrogLives()
        {
            var plain = SpeciesCatalog.FrogFor(Whites, Genome.Default);
            Assert.AreSame(SpeciesCatalog.Frog, plain, "plain frogs use the species as it is");
            var teeth = SpeciesCatalog.FrogFor(Whites, With(Mutation.Teeth, 1));
            Assert.Greater(teeth.MealSize, plain.MealSize, "teeth: each cricket fills it up more");
            Assert.Greater(teeth.FightBonus, 0f, "teeth: tougher in a fight");
            var claws = SpeciesCatalog.FrogFor(Whites, With(Mutation.Claws, 1));
            Assert.Greater(claws.ClimbSkill, plain.ClimbSkill, "claws: better at climbing out");
            var eyes = SpeciesCatalog.FrogFor(Whites, With(Mutation.GiantEyes, 1));
            Assert.Greater(eyes.SenseRadius, plain.SenseRadius, "giant eyes: sees further");
            Assert.Greater(eyes.FatiguePerSecond, plain.FatiguePerSecond, "but gets sleepy sooner");
            var legs = SpeciesCatalog.FrogFor(Whites, With(Mutation.LongLegs, 1));
            Assert.Greater(legs.HuntSpeed, plain.HuntSpeed);
            Assert.Greater(legs.HungerPerSecond, plain.HungerPerSecond);
            var stubby = SpeciesCatalog.FrogFor(Whites, With(Mutation.StubbyLegs, 2));
            Assert.Less(stubby.TravelSpeed, plain.TravelSpeed);
            Assert.Less(stubby.HungerPerSecond, plain.HungerPerSecond);
            var albino = SpeciesCatalog.FrogFor(Whites, With(Mutation.Albino, 2));
            Assert.Less(albino.SenseRadius, plain.SenseRadius);
            Assert.Less(albino.ComfortFor(29.5f, 60f), plain.ComfortFor(29.5f, 60f), "albinos dislike heat");
            var black = SpeciesCatalog.FrogFor(Whites, With(Mutation.Melanistic, 2));
            Assert.Greater(black.ComfortFor(21f, 60f), plain.ComfortFor(21f, 60f), "black frogs cope with a cooler tank");
            var loud = SpeciesCatalog.FrogFor(Whites, With(Mutation.LoudCall, 1));
            Assert.Greater(loud.CallRange, plain.CallRange);
            var blue = SpeciesCatalog.FrogFor(Whites, With(Mutation.Blue, 2));
            Assert.AreEqual(plain.HuntSpeed, blue.HuntSpeed, "blue is only skin deep");
            Assert.AreEqual(plain.MealSize, blue.MealSize);
            Assert.AreEqual(SpeciesCatalog.Frog.MealSize, 0.3f, "the shared species is never changed by a mutant");
        }

        [Test]
        public void MutantFrog_KeepsItsMutationWhenSavedAndLoaded()
        {
            var w = PondWorld();
            int f = w.AddFrog(0f, 0f, Whites, Sex.Female);
            var a = w.Agents;
            var g = a.Genes[f];
            g.Alleles = Mutations.WithCopies(Mutations.WithCopies(0u, Mutation.Teeth, 1), Mutation.Albino, 2);
            g.Generation = 4;
            a.Genes[f] = g;
            var b = SaveSystem.FromBytes(SaveSystem.ToBytes(w)).Agents;
            Assert.AreEqual(g, b.Genes[0]);
            Assert.IsTrue(Mutations.Shows(b.Genes[0].Visible, Mutation.Teeth));
            Assert.IsTrue(Mutations.Shows(b.Genes[0].Visible, Mutation.Albino));
        }

        [Test]
        public void MutantFroglet_IsAnnouncedOnce()
        {
            var w = PondWorld();
            var loop = SimulationFactory.CreateDefault(w);
            var teeth = With(Mutation.Teeth, 2);
            int e = w.AddEgg(6f, 0f, Whites, Sex.Female, teeth, teeth);
            int id = w.Agents.Id[e];
            var sp = SpeciesCatalog.FrogFor(Whites, w.Agents.Genes[e]);
            w.Agents.Growth[e] = sp.FrogletAt - 2f;
            w.Agents.Stage[e] = LifeStage.Tadpole;
            Run(loop, 10f);
            Assert.AreEqual(LifeStage.Froglet, w.Agents.Stage[w.Agents.IndexOf(id)]);
            StringAssert.Contains("teeth", w.News);
            Assert.IsTrue(Mutations.Shows(w.DiscoveredMutations, Mutation.Teeth));
            var news = w.Alerts.Find(AlertSystem.NewsCause);
            Assert.IsNotNull(news, "the news shows in the alerts list");
            Assert.AreEqual(AlertLevel.Info, news.Level);

            Run(loop, AlertSystem.NewsSeconds + 5f);
            Assert.IsNull(w.Alerts.Find(AlertSystem.NewsCause), "and goes away after a while");
        }

        // ------------------------------------------------------------------ hybrids

        [Test]
        public void HybridAncestry_IsWorkedOutFromBothParents()
        {
            var pure = Genome.Default;
            Lineage.ChildAncestry(Whites, pure, WhiteLipped, pure, out byte main, out byte other, out float share);
            Assert.AreEqual(Whites, main, "a half-and-half cross takes its mother's species as its main one");
            Assert.AreEqual(WhiteLipped, other);
            Assert.AreEqual(0.5f, share, 1e-6f);

            var f1 = pure;
            f1.HybridSpecies = WhiteLipped;
            f1.HybridShare = 0.5f;
            Lineage.ChildAncestry(Whites, f1, Whites, pure, out main, out other, out share);
            Assert.AreEqual(Whites, main);
            Assert.AreEqual(0.25f, share, 1e-6f, "back-crossed to White's: a quarter White-lipped");

            Lineage.ChildAncestry(Whites, f1, Whites, f1, out main, out other, out share);
            Assert.AreEqual(0.5f, share, 1e-6f, "two first crosses: still half and half");

            Lineage.ChildAncestry(Whites, f1, WhiteLipped, pure, out main, out other, out share);
            Assert.AreEqual(WhiteLipped, main, "back-crossed to White-lipped: mostly White-lipped now");
            Assert.AreEqual(Whites, other);
            Assert.AreEqual(0.25f, share, 1e-6f);

            Lineage.ChildAncestry(Whites, pure, Whites, pure, out main, out other, out share);
            Assert.AreEqual(0f, share, "pure-bred parents, pure-bred young");
        }

        [Test]
        public void Hybrids_AreInBetweenTheirParents_AndLessFertile()
        {
            var g = Genome.Default;
            g.HybridSpecies = WhiteLipped;
            g.HybridShare = 0.5f;
            var hybrid = SpeciesCatalog.FrogFor(Whites, g);
            var whites = SpeciesCatalog.FrogOf(Whites);
            var lipped = SpeciesCatalog.FrogOf(WhiteLipped);
            Assert.Greater(hybrid.AdultSize, whites.AdultSize);
            Assert.Less(hybrid.AdultSize, lipped.AdultSize);
            Assert.AreEqual(0.5f * (whites.MinTemperature + lipped.MinTemperature), hybrid.MinTemperature, 1e-4f);
            StringAssert.Contains("hybrid", hybrid.Name);
            Assert.AreEqual(Whites, hybrid.Id, "it counts as its main species for company and territory");

            Assert.AreEqual(1f, Lineage.HybridFertility(Genome.Default));
            Assert.AreEqual(0.6f, Lineage.HybridFertility(g), 1e-6f, "a first cross is 60% as fertile");
        }

        [Test]
        public void LonelyFemale_PairsWithARelatedSpecies_AndTheirYoungAreHybrids()
        {
            var w = PondWorld();
            int mum = w.AddFrog(2f, 0f, Whites, Sex.Female);
            int dad = w.AddFrog(2.5f, 1f, WhiteLipped, Sex.Male);
            int mumId = w.Agents.Id[mum];
            w.Agents.BreedCooldown[mum] = 0f;
            w.Agents.BreedCooldown[dad] = 0f;
            for (int k = 0; k < 60; k++) w.AddInsect(-6f + 0.2f * k, -4f, w.Random.Get("test"));
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);
            for (int k = 0; k < 900 && life.EggsLaid == 0; k++)
            {
                Feed(w);
                Run(loop, 1f);
            }
            Assert.Greater(life.EggsLaid, 0, "with no White's male about, she accepts a White-lipped one");
            Assert.Greater(life.CrossPairings, 0);
            var a = w.Agents;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog || a.Stage[i] != LifeStage.Egg) continue;
                Assert.AreEqual(mumId, a.MotherId[i]);
                Assert.AreEqual(Whites, a.Species[i]);
                Assert.IsTrue(a.Genes[i].IsHybrid);
                Assert.AreEqual(WhiteLipped, a.Genes[i].HybridSpecies);
                Assert.AreEqual(0.5f, a.Genes[i].HybridShare, 1e-6f);
            }
        }

        [Test]
        public void Female_PrefersHerOwnSpecies_WhenOneIsAbout()
        {
            var w = PondWorld(9);
            int mum = w.AddFrog(2f, 0f, Whites, Sex.Female);
            int own = w.AddFrog(3f, 1f, Whites, Sex.Male);
            int other = w.AddFrog(2.2f, 0.2f, WhiteLipped, Sex.Male); // nearer
            int ownId = w.Agents.Id[own];
            foreach (int i in new[] { mum, own, other }) w.Agents.BreedCooldown[i] = 0f;
            for (int k = 0; k < 60; k++) w.AddInsect(-6f + 0.2f * k, -4f, w.Random.Get("test"));
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);
            for (int k = 0; k < 600 && life.EggsLaid == 0; k++)
            {
                Feed(w);
                Run(loop, 1f);
            }
            Assert.Greater(life.EggsLaid, 0);
            Assert.AreEqual(0, life.CrossPairings, "she picks the White's male even though the other is closer");
            for (int i = 0; i < w.Agents.Count; i++)
                if (w.Agents.Kind[i] == AgentKind.Frog && w.Agents.Stage[i] == LifeStage.Egg)
                    Assert.AreEqual(ownId, w.Agents.FatherId[i]);
        }

        [Test]
        public void Hybrids_SaveAndLoad_WithTheFatherSpeciesOfCarriedEggs()
        {
            var w = PondWorld();
            var a = w.Agents;
            int mum = w.AddFrog(0f, 0f, FireBellied, Sex.Female);
            int dad = w.AddFrog(1f, 0f, YellowBellied, Sex.Male);
            a.MateId[mum] = a.Id[dad];
            a.MateGenes[mum] = a.Genes[dad];
            a.MateSpecies[mum] = YellowBellied;
            int egg = w.AddEgg(6f, 0f, FireBellied, a.Genes[mum], YellowBellied, a.Genes[dad], Sex.Male, 0f);
            w.MutationBoost = true;
            w.DiscoveredMutations = 5;
            w.DiscoveredHybrids = 1UL << (2 * 8 + 5);
            w.Escape(egg, "testing");

            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            var b = loaded.Agents;
            Assert.AreEqual(YellowBellied, b.MateSpecies[0]);
            Assert.AreEqual(a.Genes[egg], b.Genes[egg]);
            Assert.AreEqual(0.5f, b.Genes[egg].HybridShare, 1e-6f);
            Assert.AreEqual(a.Genes[egg], loaded.Escaped[0].Genes, "an escaped hybrid stays a hybrid");
            Assert.IsTrue(loaded.MutationBoost);
            Assert.AreEqual(5, loaded.DiscoveredMutations);
            Assert.AreEqual(w.DiscoveredHybrids, loaded.DiscoveredHybrids);
        }

        // ------------------------------------------------------------------ new species

        /// <summary>Adds an adult of a given type and generation.</summary>
        private static int AddTyped(World w, float x, byte species, Sex sex, uint alleles, byte other, float share, byte generation)
        {
            int i = w.AddFrog(x, 0f, species, sex);
            var g = w.Agents.Genes[i];
            g.Alleles = alleles;
            g.HybridSpecies = other;
            g.HybridShare = share;
            g.Generation = generation;
            w.Agents.Genes[i] = g;
            return i;
        }

        [Test]
        public void Generations_CountUp_OnlyWhileParentsAndYoungLookTheSame()
        {
            var w = PondWorld();
            uint teeth = Mutations.WithCopies(0u, Mutation.Teeth, 2);
            int mum = AddTyped(w, 0f, Whites, Sex.Female, teeth, 0, 0f, 2);
            int dad = AddTyped(w, 1f, Whites, Sex.Male, teeth, 0, 0f, 4);
            var a = w.Agents;
            int e = w.AddEgg(6f, 0f, Whites, Sex.Female, a.Genes[mum], a.Genes[dad]);
            Assert.AreEqual(3, a.Genes[e].Generation, "one more than the younger line");

            int plainDad = AddTyped(w, 2f, Whites, Sex.Male, 0u, 0, 0f, 9);
            e = w.AddEgg(6f, 0f, Whites, Sex.Female, a.Genes[mum], a.Genes[plainDad]);
            Assert.AreEqual(0, a.Genes[e].Generation, "a cross with a different type starts again at 0");
        }

        [Test]
        public void TypeThatBredTrue_CanBeRegisteredAndNamed_AndKeepsItsName()
        {
            var w = PondWorld();
            uint blueTeeth = Mutations.WithCopies(Mutations.WithCopies(0u, Mutation.Blue, 2), Mutation.Teeth, 2);
            int young = AddTyped(w, 0f, Whites, Sex.Female, blueTeeth, 0, 0f, 2);
            Assert.AreEqual(0, w.TypesReadyToRegister().Count, "two generations are not enough");
            int type = Lineage.TypeOf(w.Agents, young);
            Assert.IsFalse(w.Register(type, "Bitey blue"), "not ready yet");

            AddTyped(w, 1f, Whites, Sex.Male, blueTeeth, 0, 0f, 3);
            AddTyped(w, 2f, Whites, Sex.Male, 0u, 0, 0f, 50); // plain White's: old line, but nothing new
            var ready = w.TypesReadyToRegister();
            Assert.AreEqual(1, ready.Count);
            Assert.AreEqual(type, ready[0]);
            StringAssert.Contains("White's tree frog", Lineage.Describe(type));
            StringAssert.Contains("teeth", Lineage.Describe(type));

            Assert.IsFalse(w.Register(type, "   "), "a name is needed");
            Assert.IsFalse(w.Register(type, "Red-eyed tree frog"), "it cannot take a real species' name");
            Assert.IsTrue(w.Register(type, "Bitey blue"));
            Assert.IsFalse(w.Register(type, "Another name"), "it can only be registered once");
            Assert.AreEqual(0, w.TypesReadyToRegister().Count);
            Assert.AreEqual("Bitey blue", w.KindName(young), "every frog of that type now goes by the new name");
            Assert.AreEqual(2, w.CountOfType(type));

            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.AreEqual(1, loaded.Registered.Count);
            Assert.AreEqual("Bitey blue", loaded.Registered[0].Name);
            Assert.AreEqual(type, loaded.Registered[0].Type);
            Assert.AreEqual("Bitey blue", loaded.KindName(0));
        }

        [Test]
        public void HybridLine_CanBecomeANewSpecies()
        {
            var w = PondWorld();
            int f = AddTyped(w, 0f, FireBellied, Sex.Female, 0u, YellowBellied, 0.5f, 3);
            var ready = w.TypesReadyToRegister();
            Assert.AreEqual(1, ready.Count);
            Assert.AreEqual(2, Lineage.QuartersOfType(ready[0]));
            StringAssert.Contains("half and half", Lineage.Describe(ready[0]));
            Assert.IsTrue(w.Register(ready[0], "Orange-bellied toad"));
            Assert.AreEqual("Orange-bellied toad", w.KindName(f));
        }

        // ------------------------------------------------------------------ a long run

        [Test]
        public void MixedTank_WithBoost_BreedsMutantsAndHybrids_AndStaysStable()
        {
            // Two White's females, one White's male and one White-lipped male, with the mutation boost:
            // after a long while there should be mutants and some hybrids, and nothing explodes.
            var w = PondWorld(21);
            w.MutationBoost = true;
            w.AddFrog(1f, 0f, Whites, Sex.Female);
            w.AddFrog(1.5f, 1f, Whites, Sex.Female);
            w.AddFrog(2f, -1f, Whites, Sex.Male);
            w.AddFrog(0.5f, 1.5f, WhiteLipped, Sex.Male);
            w.Devices.Add(DeviceKind.Feeder, -3f, 0f);
            for (int k = 0; k < 80; k++) w.AddInsect(-6f + 0.15f * k, -3f, w.Random.Get("test"));
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);
            // Collect the eggs now and then so the tank keeps room to breed. Run until plenty of eggs
            // have been laid, so the test does not hang on exactly how many come in a set time.
            for (int k = 0; k < 400 && life.EggsLaid < 40; k++)
            {
                foreach (var d in w.Devices.All) if (d.Kind == DeviceKind.Feeder) d.Supply = DeviceStore.FeederCapacity;
                w.Dirt = 0f; // the keeper cleans the tank too
                Feed(w);
                Run(loop, 30f);
                Assert.LessOrEqual(w.FrogCount(), w.FrogLimit + 3, "the population still levels off");
                if (k % 4 == 3) w.CollectEggs();
            }
            Assert.GreaterOrEqual(life.EggsLaid, 40);
            Assert.Greater(life.NewMutations, 0, "the boost brings out mutations (about 1 egg in 4)");
            Assert.Less(w.Agents.Count, World.MaxAgents);
        }
    }
}

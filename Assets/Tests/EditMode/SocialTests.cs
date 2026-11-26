using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 2, part 2: loneliness, territories, young frogs keeping clear of big ones, population controls and level of detail.</summary>
    public class SocialTests
    {
        /// <summary>A flat tank with even conditions and a pool dug at x = 6.</summary>
        private static World FlatWorld(float temperature, float humidity, ulong seed = 1)
        {
            var w = new World(seed);
            w.Environment.AmbientTemperature = temperature;
            w.Environment.AmbientHumidity = humidity;
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
                if (a.IsLandFrog(i)) { a.Hunger[i] = 0.05f; a.Thirst[i] = 0.05f; a.Fatigue[i] = 0.05f; }
        }

        private static float Distance(World w, int idA, int idB)
        {
            var a = w.Agents;
            int i = a.IndexOf(idA), j = a.IndexOf(idB);
            float dx = a.PosX[i] - a.PosX[j], dz = a.PosZ[i] - a.PosZ[j];
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }

        private const byte Whites = 0, RedEyed = 1, FireBellied = 2;

        [Test]
        public void SociableFrog_AloneGetsLonely_ThenGoesToFindItsOwnKind()
        {
            var w = FlatWorld(21f, 70f); // fire-bellied toads are comfortable here
            int lonerIdx = w.AddFrog(-8f, -4f, FireBellied, Sex.Female);
            int loner = w.Agents.Id[lonerIdx];
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 24; k++) { Feed(w); Run(loop, 10f); }
            Assert.Greater(w.Agents.Social[w.Agents.IndexOf(loner)], 0.5f, "four minutes alone makes a toad lonely");

            // Another toad arrives a few metres away: the lonely one goes to it.
            int friend = w.Agents.Id[w.AddFrog(-3f, -1f, FireBellied, Sex.Male)];
            float closest = float.MaxValue;
            for (int k = 0; k < 40; k++)
            {
                Feed(w);
                Run(loop, 1f);
                closest = System.Math.Min(closest, Distance(w, loner, friend));
            }
            Assert.Less(closest, NeedsSystem.CompanyRadius, "it joins the other toad");
            for (int k = 0; k < 6; k++) { Feed(w); Run(loop, 10f); }
            Assert.Less(w.Agents.Social[w.Agents.IndexOf(loner)], 0.5f, "company makes it feel better");
        }

        [Test]
        public void VeryLonelyFrog_IsLessComfortable_AndThePlayerIsTold()
        {
            var w = FlatWorld(21f, 70f);
            int i = w.AddFrog(0f, 0f, FireBellied, Sex.Female);
            int id = w.Agents.Id[i];
            var loop = SimulationFactory.CreateDefault(w);
            Feed(w);
            Run(loop, 5f);
            float settled = w.Agents.Comfort[w.Agents.IndexOf(id)];
            w.Agents.Social[w.Agents.IndexOf(id)] = 1f;
            Feed(w);
            Run(loop, 20f);
            i = w.Agents.IndexOf(id);
            Assert.Less(w.Agents.Comfort[i], settled - 0.1f);
            Assert.AreEqual("lonely", AlertSystem.ComfortProblem(w, i));
        }

        [Test]
        public void AdultMale_ClaimsATerritory_ButFemalesAndFireBelliedToadsDoNot()
        {
            var w = FlatWorld(26f, 62f); // White's tree frogs are comfortable here
            int male = w.Agents.Id[w.AddFrog(-4f, 0f, Whites, Sex.Male)];
            int female = w.Agents.Id[w.AddFrog(-4f, 3f, Whites, Sex.Female)];
            int toad = w.Agents.Id[w.AddFrog(0f, -3f, FireBellied, Sex.Male)];
            var loop = SimulationFactory.CreateDefault(w);
            Feed(w);
            Run(loop, 5f);
            var a = w.Agents;
            Assert.IsTrue(a.HasHome[a.IndexOf(male)]);
            Assert.IsFalse(a.HasHome[a.IndexOf(female)]);
            Assert.IsFalse(a.HasHome[a.IndexOf(toad)], "fire-bellied toads are not territorial");

            // A male with a territory wanders about inside it.
            int m = a.IndexOf(male);
            float hx = a.HomeX[m], hz = a.HomeZ[m];
            float radius = SpeciesCatalog.Frog.TerritoryRadius;
            for (int k = 0; k < 60; k++)
            {
                Feed(w);
                Run(loop, 1f);
                m = a.IndexOf(male);
                if (a.Goal[m] != Goal.Wander) continue;
                float dx = a.PosX[m] - hx, dz = a.PosZ[m] - hz;
                Assert.Less(dx * dx + dz * dz, (radius + 0.5f) * (radius + 0.5f), "it stays near home while exploring");
            }
        }

        [Test]
        public void RivalMale_InATerritory_IsChasedAndOneOfThemBacksOff()
        {
            var w = FlatWorld(26f, 62f);
            int owner = w.Agents.Id[w.AddFrog(-4f, 0f, Whites, Sex.Male)];
            var loop = SimulationFactory.CreateDefault(w);
            var brain = SystemOf<BrainSystem>(loop);
            Feed(w);
            Run(loop, 3f);
            var a = w.Agents;
            int o = a.IndexOf(owner);
            Assert.IsTrue(a.HasHome[o]);

            int rival = a.Id[w.AddFrog(a.HomeX[o] + 0.6f, a.HomeZ[o], Whites, Sex.Male)];
            for (int k = 0; k < 300 && brain.Disputes == 0; k++) { Feed(w); Run(loop, 0.1f); }
            Assert.Greater(brain.Disputes, 0, "the owner confronts the rival");

            int oi = a.IndexOf(owner), ri = a.IndexOf(rival);
            Assert.IsTrue(a.HasHome[oi] != a.HasHome[ri], "exactly one of them holds the territory");
            int loser = a.HasHome[oi] ? ri : oi;
            Assert.Greater(a.Retreat[loser], 0f, "the loser backs off");
            Assert.AreEqual(Goal.Retreat, a.Goal[loser]);
            Assert.Greater(a.Stress[loser], 0f, "losing is a little stressful");
        }

        [Test]
        public void ElderMale_GivesUpHisTerritory()
        {
            var w = FlatWorld(26f, 62f);
            int i = w.AddFrog(-4f, 0f, Whites, Sex.Male);
            int id = w.Agents.Id[i];
            var loop = SimulationFactory.CreateDefault(w);
            Feed(w);
            Run(loop, 3f);
            i = w.Agents.IndexOf(id);
            Assert.IsTrue(w.Agents.HasHome[i]);
            w.Agents.Age[i] = SpeciesCatalog.Frog.ElderAge + 1f;
            Run(loop, 2f);
            i = w.Agents.IndexOf(id);
            Assert.AreEqual(LifeStage.Elder, w.Agents.Stage[i]);
            Assert.IsFalse(w.Agents.HasHome[i]);
        }

        [Test]
        public void Froglet_KeepsClearOfBigFrogsOfOtherSpecies_ButNotItsOwn()
        {
            var w = FlatWorld(25f, 72f);
            var mum = Genome.Default;
            int f = w.AddEgg(-4f, 0f, RedEyed, Sex.Female, mum, mum);
            var a = w.Agents;
            var sp = SpeciesCatalog.FrogOf(RedEyed);
            a.Growth[f] = sp.FrogletAt + 1f;
            a.Age[f] = sp.FrogletAt + 1f;
            a.Stage[f] = LifeStage.Froglet;
            int froglet = a.Id[f];
            int sameKind = a.Id[w.AddFrog(-3.5f, 0f, RedEyed, Sex.Male)];
            var loop = SimulationFactory.CreateDefault(w);
            Feed(w);
            Run(loop, 2f);
            Assert.AreNotEqual(Goal.Flee, a.Goal[a.IndexOf(froglet)], "a grown frog of its own kind is no threat");

            int fi = a.IndexOf(froglet);
            int big = a.Id[w.AddFrog(a.PosX[fi] + 0.5f, a.PosZ[fi], Whites, Sex.Male)];
            Run(loop, 0.5f);
            Assert.AreEqual(Goal.Flee, a.Goal[a.IndexOf(froglet)], "a big White's tree frog is");
            Run(loop, 3f);
            Assert.Greater(Distance(w, froglet, big), 1f, "so it moves away");
            Assert.GreaterOrEqual(a.IndexOf(sameKind), 0);
        }

        [Test]
        public void BreedingSwitchedOff_NoEggsAreLaid()
        {
            var w = FlatWorld(26f, 60f);
            w.BreedingEnabled = false;
            int mum = w.AddFrog(2f, 0f, Whites, Sex.Female);
            int dad = w.AddFrog(2.5f, 1f, Whites, Sex.Male);
            w.Agents.BreedCooldown[mum] = 0f;
            w.Agents.BreedCooldown[dad] = 0f;
            for (int k = 0; k < 60; k++) w.AddInsect(-6f + 0.2f * k, -4f, w.Random.Get("test"));
            var loop = SimulationFactory.CreateDefault(w);
            var life = SystemOf<LifecycleSystem>(loop);
            for (int k = 0; k < 300; k++) { Feed(w); Run(loop, 1f); }
            Assert.AreEqual(0, life.EggsLaid);
            Assert.AreEqual(0, w.Agents.MateId[w.Agents.IndexOf(w.Agents.Id[mum])]);

            w.BreedingEnabled = true;
            for (int k = 0; k < 600 && life.EggsLaid == 0; k++) { Feed(w); Run(loop, 1f); }
            Assert.Greater(life.EggsLaid, 0, "switching it back on lets them breed again");
        }

        [Test]
        public void FrogLimit_CanOnlyLowerTheTanksCapacity()
        {
            var w = new World(1);
            int cap = w.FrogCapacity;
            Assert.AreEqual(cap, w.FrogLimit);
            w.BreedingLimit = 5;
            Assert.AreEqual(5, w.FrogLimit);
            w.BreedingLimit = cap + 10;
            Assert.AreEqual(cap, w.FrogLimit);
            w.BreedingLimit = 0;
            Assert.AreEqual(cap, w.FrogLimit);
        }

        [Test]
        public void CollectEggs_TakesOnlyEggs()
        {
            var w = FlatWorld(26f, 60f);
            var g = Genome.Default;
            for (int k = 0; k < 4; k++) w.AddEgg(6f, 0.1f * k, Whites, Sex.Female, g, g);
            int tadpole = w.AddEgg(6f, 1f, Whites, Sex.Male, g, g);
            w.Agents.Stage[tadpole] = LifeStage.Tadpole;
            w.AddFrog(0f, 0f);
            w.AddInsect(1f, 1f, w.Random.Get("test"));
            Assert.AreEqual(4, w.CollectEggs());
            Assert.AreEqual(3, w.Agents.Count);
            Assert.AreEqual(4, w.EggsCollected);
            Assert.AreEqual(0, w.CollectEggs());
        }

        [Test]
        public void RehomeExtras_TakesEldersThenOldestAdults_ButKeepsABreedingPairOfEachSpecies()
        {
            var w = FlatWorld(26f, 60f);
            var a = w.Agents;
            int Add(byte species, Sex sex, float age)
            {
                int i = w.AddFrog(0f, 0f, species, sex);
                a.Age[i] = age;
                a.Stage[i] = SpeciesCatalog.FrogOf(species).StageFor(a.Growth[i], age);
                return a.Id[i];
            }
            int oldFemale = Add(Whites, Sex.Female, 3000f);
            int midFemale = Add(Whites, Sex.Female, 2000f);
            int youngFemale = Add(Whites, Sex.Female, 1000f);
            int onlyMale = Add(Whites, Sex.Male, 3500f);
            int redF = Add(RedEyed, Sex.Female, 3000f);
            int redM = Add(RedEyed, Sex.Male, 3000f);
            int elder = Add(RedEyed, Sex.Male, 3700f);
            Assert.AreEqual(LifeStage.Elder, a.Stage[a.IndexOf(elder)]);
            w.BreedingLimit = 2;

            Assert.AreEqual(3, w.RehomeExtras());
            Assert.AreEqual(3, w.Rehomed);
            Assert.Less(a.IndexOf(elder), 0, "the elder goes first");
            Assert.Less(a.IndexOf(oldFemale), 0, "then the oldest adults");
            Assert.Less(a.IndexOf(midFemale), 0);
            foreach (int keep in new[] { youngFemale, onlyMale, redF, redM })
                Assert.GreaterOrEqual(a.IndexOf(keep), 0, "a breeding pair of each species stays");
        }

        [Test]
        public void TankFull_ShowsAnInfoAlert()
        {
            var w = new World(1, 4.5f, 2.5f); // holds 3 frogs
            var alerts = new AlertSystem();
            for (int k = 0; k < 3; k++) w.AddFrog(-2f + k, 0f);
            alerts.Refresh(w);
            Assert.IsNotNull(w.Alerts.Find(AlertSystem.TankFullCause));
            w.BreedingEnabled = false;
            alerts.Refresh(w);
            Assert.IsNull(w.Alerts.Find(AlertSystem.TankFullCause), "no point saying so with breeding off");
        }

        [Test]
        public void FarFromTheView_AnimalsThinkLessOften_ButStillGetAbout()
        {
            var w = FlatWorld(26f, 60f);
            var rng = w.Random.Get("test");
            for (int k = 0; k < 60; k++) w.AddInsect(rng.Range(-9f, 9f), rng.Range(-5f, 5f), rng);
            w.HasFocus = true;
            w.FocusX = -9f; w.FocusZ = -5f; w.FocusRadius = 2f;
            var loop = SimulationFactory.CreateDefault(w);
            var brain = SystemOf<BrainSystem>(loop);
            var startX = new float[w.Agents.Count];
            for (int i = 0; i < startX.Length; i++) startX[i] = w.Agents.PosX[i];
            Run(loop, 10f);
            Assert.Greater(brain.FarAgents, 30, "most crickets are out of view");
            int moved = 0;
            for (int i = 0; i < w.Agents.Count; i++) if (System.Math.Abs(w.Agents.PosX[i] - startX[i]) > 0.05f) moved++;
            Assert.Greater(moved, 30, "they still wander about");

            w.HasFocus = false;
            Run(loop, 1f);
            Assert.AreEqual(0, brain.FarAgents);
        }

        [Test]
        public void RoundTrip_PreservesSocialLifeTerritoriesAndPopulationControls()
        {
            var w = World.CreateStarterTank(9, 6, 20);
            var a = w.Agents;
            a.Social[0] = 0.75f;
            a.Retreat[1] = 3.5f;
            a.Goal[1] = Goal.Retreat;
            a.HasHome[2] = true; a.HomeX[2] = 1.25f; a.HomeZ[2] = -2.5f;
            a.Goal[2] = Goal.Defend;
            w.BreedingEnabled = false;
            w.BreedingLimit = 7;
            w.Rehomed = 4;
            w.EggsCollected = 11;

            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            var b = loaded.Agents;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.Social[i], b.Social[i]);
                Assert.AreEqual(a.Retreat[i], b.Retreat[i]);
                Assert.AreEqual(a.HasHome[i], b.HasHome[i]);
                Assert.AreEqual(a.HomeX[i], b.HomeX[i]);
                Assert.AreEqual(a.HomeZ[i], b.HomeZ[i]);
                Assert.AreEqual(a.Goal[i], b.Goal[i]);
            }
            Assert.IsFalse(loaded.BreedingEnabled);
            Assert.AreEqual(7, loaded.BreedingLimit);
            Assert.AreEqual(4, loaded.Rehomed);
            Assert.AreEqual(11, loaded.EggsCollected);
        }
    }
}

using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 5, part 3: the first-30-minutes tutorial.</summary>
    public class TutorialTests
    {
        private static World Tank(bool money = false)
        {
            var w = World.CreateStarterTank(7, 4, 0);
            if (money) w.Economy.StartMoneyMode();
            return w;
        }

        /// <summary>Runs the check until the step's praise has shown and the next step starts.</summary>
        private static void Settle(TutorialProgress p, World w)
        {
            for (int k = 0; k < 10; k++) p.Update(w, 0.5f);
        }

        private static void Expect(TutorialProgress p, string id)
        {
            Assert.IsNotNull(p.Current, "expected step " + id + " but the tutorial finished");
            Assert.AreEqual(id, p.Current.Id);
        }

        private static void DigPool(World w, float cx, float cz)
        {
            var t = w.Terrain;
            for (int pz = 0; pz < t.PointsZ; pz++)
                for (int px = 0; px < t.PointsX; px++)
                {
                    float dx = t.PointX(px) - cx, dz = t.PointZ(pz) - cz;
                    if (dx * dx + dz * dz < 0.5f * 0.5f) t.Height[t.Index(px, pz)] = t.WaterLevel - 0.3f;
                }
        }

        private static void PlacePart(World w, PartKind kind, float x, float z)
        {
            var p = w.Parts.Place(new PlacedPart { Kind = kind, X = x, Z = z, R = 150, G = 120, B = 90 }, w.Terrain, out string why);
            Assert.IsNotNull(p, why);
        }

        [Test]
        public void Steps_HaveIdsTitlesAndTextForBothModes()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var s in Tutorial.Steps)
            {
                Assert.IsTrue(seen.Add(s.Id), "ids are unique: " + s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.Title));
                foreach (var w in new[] { Tank(), Tank(true) })
                {
                    string text = s.Text(w);
                    Assert.Greater(text.Length, 20, s.Id);
                    Assert.AreEqual(text.Split('{').Length, text.Split('}').Length, s.Id + ": key names are closed");
                }
            }
            Assert.GreaterOrEqual(Tutorial.Steps.Length, 8);
        }

        [Test]
        public void Walkthrough_EachStepMovesOnWhenItIsDone()
        {
            var w = Tank();
            var p = new TutorialProgress();
            Expect(p, "look");
            Settle(p, w);
            Expect(p, "look"); // nothing done yet

            p.Note(TutorialEvent.CameraMoved);
            p.Update(w, 0.1f);
            Assert.IsTrue(p.ShowingPraise, "a done step says so before moving on");
            Expect(p, "look");
            Settle(p, w);
            Expect(p, "frog");

            p.Note(TutorialEvent.FrogSelected); Settle(p, w); Expect(p, "health");
            p.Note(TutorialEvent.HealthOpened); Settle(p, w); Expect(p, "pool");

            DigPool(w, 4f, 3f); Settle(p, w); Expect(p, "cover");
            PlacePart(w, PartKind.Hide, -4f, -3f); Settle(p, w); Expect(p, "food");

            var rng = w.Random.Get("test");
            for (int k = 0; k < Tutorial.FoodInsects; k++) w.AddInsect(0f, 0f, rng);
            Settle(p, w); Expect(p, "speed");

            p.Note(TutorialEvent.SpedUp); Settle(p, w); Expect(p, "play");
            PlacePart(w, PartKind.Slide, -6f, 3f); Settle(p, w); Expect(p, "save");
            p.Note(TutorialEvent.Saved); Settle(p, w); Expect(p, "eggs");

            Settle(p, w);
            Expect(p, "eggs");
            int mum = 0;
            while (w.Agents.Kind[mum] != AgentKind.Frog) mum++;
            w.AddEgg(4f, 3f, w.Agents.Species[mum], Sex.Female, w.Agents.Genes[mum], w.Agents.Genes[mum]);
            Settle(p, w);
            Assert.IsTrue(p.Finished);
            Assert.IsNull(p.Current);
        }

        [Test]
        public void Pool_MustBeNew_NotTheOneTheTankHad()
        {
            var w = Tank();
            DigPool(w, 4f, 3f);
            var p = new TutorialProgress { Step = Tutorial.IndexOf("pool") };
            Settle(p, w);
            Expect(p, "pool");
            DigPool(w, -4f, -2f);
            Settle(p, w);
            Expect(p, "cover");
        }

        [Test]
        public void ANewTank_IsCountedAfresh()
        {
            var p = new TutorialProgress { Step = Tutorial.IndexOf("cover") };
            var first = Tank();
            Settle(p, first);
            var second = Tank();
            PlacePart(second, PartKind.Log, 2f, 2f);
            Settle(p, second); // the log was there before this tank was counted
            Expect(p, "cover");
            PlacePart(second, PartKind.Plant, -2f, 2f);
            Settle(p, second);
            Expect(p, "food");
        }

        [Test]
        public void Feeder_CountsAsAddingFood_AndPlaygroundPiecesAreNotCover()
        {
            var w = Tank();
            var p = new TutorialProgress { Step = Tutorial.IndexOf("cover") };
            Settle(p, w);
            PlacePart(w, PartKind.Slide, 0f, 0f);
            Settle(p, w);
            Expect(p, "cover");

            p = new TutorialProgress { Step = Tutorial.IndexOf("food") };
            Settle(p, w);
            w.Devices.Add(DeviceKind.Feeder, 1f, 1f);
            Settle(p, w);
            Expect(p, "speed");
        }

        [Test]
        public void MoneyMode_ExplainsThePlaygroundNeedsResearch()
        {
            var step = Tutorial.Steps[Tutorial.IndexOf("play")];
            var w = Tank(true);
            StringAssert.Contains("research", step.Text(w));
            StringAssert.DoesNotContain("research", step.Text(Tank()));
            w.Economy.Researched |= 1u << (int)Research.Playground;
            StringAssert.DoesNotContain("research", step.Text(w));
        }

        [Test]
        public void SkipAndRestart()
        {
            var w = Tank();
            var p = new TutorialProgress();
            p.SkipStep();
            Expect(p, "frog");
            p.SkipAll();
            Assert.IsTrue(p.Finished && p.Hidden);
            Assert.IsFalse(p.Update(w, 1f));
            p.Restart();
            Expect(p, "look");
            Assert.IsFalse(p.Hidden);
            for (int k = 0; k < Tutorial.Steps.Length; k++) p.SkipStep();
            Assert.IsTrue(p.Finished);
        }

        [Test]
        public void Progress_IsWrittenAsTextAndReadBack()
        {
            var p = new TutorialProgress { Step = Tutorial.IndexOf("food") };
            p.Note(TutorialEvent.CameraMoved | TutorialEvent.Saved);
            p.Hidden = true;
            var back = TutorialProgress.FromText(p.ToText());
            Expect(back, "food");
            Assert.AreEqual(p.Events, back.Events);
            Assert.IsTrue(back.Hidden);
            Assert.IsFalse(back.Finished);

            var done = new TutorialProgress();
            done.SkipAll();
            Assert.IsTrue(TutorialProgress.FromText(done.ToText()).Finished);

            foreach (var junk in new[] { null, "", "nonsense", "nostep;1;0;0", "food;x;0;0", "a;b" })
            {
                var fresh = TutorialProgress.FromText(junk);
                Expect(fresh, "look");
                Assert.IsFalse(fresh.Hidden);
            }
        }
    }
}

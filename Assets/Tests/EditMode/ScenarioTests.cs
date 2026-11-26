using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 4 part 2: scenarios with goals, time limits, ways to lose and stars.</summary>
    public class ScenarioTests
    {
        private static SimulationLoop Run(World w, float seconds)
        {
            var loop = SimulationFactory.CreateDefault(w);
            for (float t = 0f; t < seconds; t += 0.25f) loop.Advance(0.25);
            return loop;
        }

        [Test]
        public void EveryScenario_StartsAndRuns()
        {
            var ids = new System.Collections.Generic.HashSet<byte>();
            foreach (var info in Scenarios.All)
            {
                Assert.IsTrue(ids.Add(info.Id), "ids are unique");
                Assert.Greater(info.Goals.Length, 0, info.Name);
                Assert.LessOrEqual(info.Goals.Length, Scenarios.MaxGoals, info.Name);
                foreach (var g in info.Goals) Assert.IsNotEmpty(g.Describe(), info.Name);
                var w = Scenarios.Start(info);
                Assert.IsTrue(w.InScenario, info.Name);
                Assert.AreEqual(info.MoneyMode, w.Economy.Enabled, info.Name);
                if (info.MoneyMode) Assert.AreEqual(info.StartMoney, (float)w.Economy.Money, info.Name);
                Assert.AreEqual(info.Map.ToCode(), w.Map.ToCode(), info.Name);
                Run(w, 10f);
                Assert.IsTrue(w.InScenario, info.Name + " is not won or lost in its first seconds");
            }
        }

        [Test]
        public void ReachingEveryGoal_WinsWithStars()
        {
            var info = Scenarios.Find(1); // First steps: 12 frogs, $450, 5 parts
            var w = Scenarios.Start(info);
            Run(w, 2f);
            Assert.IsFalse(w.Scenario.IsDone(1));
            w.Economy.Money = 1000;
            Run(w, 2f);
            Assert.IsTrue(w.Scenario.IsDone(1), "the money goal is reached");
            w.Economy.Money = 0;
            Run(w, 2f);
            Assert.IsTrue(w.Scenario.IsDone(1), "and stays reached");

            for (int k = 0; k < 5; k++)
                Assert.IsNotNull(w.Parts.Place(new PlacedPart { Kind = PartKind.Rock, X = -6f + 2.5f * k, Z = -3f, Scale = 1f }, w.Terrain, out string why), why);
            for (int k = 0; k < 8; k++) w.AddFrog(1f + 0.4f * k, 2f);
            Run(w, 2f);
            Assert.AreEqual(ScenarioStatus.Won, w.Scenario.Status);
            Assert.AreEqual(3, Scenarios.Stars(w), "won in well under half the time");
            StringAssert.Contains("Scenario complete", w.News);
        }

        [Test]
        public void TimeRunningOut_LosesTheScenario()
        {
            var w = Scenarios.Start(Scenarios.Find(1));
            Run(w, 1f);
            w.Scenario.StartTime -= Scenarios.Find(1).TimeLimitMinutes * 60.0;
            Scenarios.Update(w, 1f);
            Assert.AreEqual(ScenarioStatus.Lost, w.Scenario.Status);
            StringAssert.Contains("Time ran out", w.Scenario.Reason);
            Assert.AreEqual(0, Scenarios.Stars(w));
            Assert.AreEqual(0.0, Scenarios.MinutesLeft(w));
        }

        [Test]
        public void TooManyEscapes_LoseTheDesertRescue()
        {
            var info = Scenarios.Find(2);
            var w = Scenarios.Start(info);
            for (int k = 0; k <= info.MaxEscapes; k++) w.Escape(0, "test");
            Assert.AreEqual(info.MaxEscapes + 1, w.Scenario.Escapes);
            Run(w, 2f);
            Assert.AreEqual(ScenarioStatus.Lost, w.Scenario.Status);
            StringAssert.Contains("escaped", w.Scenario.Reason);
        }

        [Test]
        public void HealthGoal_MustHoldWithoutABreak()
        {
            var info = Scenarios.Find(2); // tank health 70+ for 5 minutes
            var w = Scenarios.Start(info);
            w.Scenario.Held[0] = 200f;
            Assert.AreEqual(200f / 60f, Scenarios.Progress(w, 0), 1e-4f);
            // A cold, dry room with every device off drops the score, so the clock restarts.
            foreach (var d in w.Devices.All) d.Enabled = false;
            w.Environment.AmbientTemperature = 5f;
            w.Environment.AmbientHumidity = 5f;
            w.Environment.ResetToAmbient();
            for (int i = 0; i < w.Agents.Count; i++) if (w.Agents.IsLandFrog(i)) { w.Agents.Comfort[i] = 0f; w.Agents.Stress[i] = 0.9f; w.Agents.Hunger[i] = 1f; }
            Scenarios.Update(w, 1f);
            Assert.AreEqual(0f, w.Scenario.Held[0]);
        }

        [Test]
        public void LosingEveryFrog_LosesTheScenario()
        {
            var w = Scenarios.Start(Scenarios.Find(6));
            for (int i = w.Agents.Count - 1; i >= 0; i--) if (w.Agents.Kind[i] == AgentKind.Frog) w.Agents.RemoveAt(i);
            Scenarios.Update(w, 1f);
            Assert.AreEqual(ScenarioStatus.Lost, w.Scenario.Status);
        }

        [Test]
        public void NoTimeLimit_MeansNoClock()
        {
            var w = Scenarios.Start(Scenarios.Find(6));
            Assert.AreEqual(-1.0, Scenarios.MinutesLeft(w));
            w.Scenario.StartTime -= 100000;
            Scenarios.Update(w, 1f);
            Assert.IsTrue(w.InScenario);
        }

        [Test]
        public void Scenario_IsSavedAndContinues()
        {
            var w = Scenarios.Start(Scenarios.Find(2));
            Run(w, 5f);
            w.Scenario.Held[0] = 123f;
            w.Scenario.Escapes = 1;
            var back = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.IsNotNull(back.Scenario);
            Assert.AreEqual(2, back.Scenario.Id);
            Assert.AreEqual(ScenarioStatus.Playing, back.Scenario.Status);
            Assert.AreEqual(1, back.Scenario.Escapes);
            Assert.AreEqual(123f, back.Scenario.Held[0]);
            Assert.AreEqual(w.Scenario.StartTime, back.Scenario.StartTime);
            Run(back, 2f);

            var free = SaveSystem.FromBytes(SaveSystem.ToBytes(World.CreateStarterTank(3, 2, 2)));
            Assert.IsNull(free.Scenario, "a free game has no scenario");
        }

        [Test]
        public void GoalProgress_ReadsTheRightNumbers()
        {
            var w = Scenarios.Start(Scenarios.Find(4)); // visitors, reputation, species
            w.Economy.Visitors = 77;
            w.Economy.Reputation = 21f;
            Assert.AreEqual(77f, Scenarios.Progress(w, 0));
            Assert.AreEqual(21f, Scenarios.Progress(w, 1));
            Assert.AreEqual(2f, Scenarios.Progress(w, 2), "islands start with two species");
        }
    }
}

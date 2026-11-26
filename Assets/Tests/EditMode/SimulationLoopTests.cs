using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    public class SimulationLoopTests
    {
        private sealed class CountingSystem : ISimSystem
        {
            public CountingSystem(int rate) { TicksPerSecond = rate; }
            public string Name => "Counter" + TicksPerSecond;
            public int TicksPerSecond { get; }
            public int Ticks;
            public float LastDt;
            public void Tick(World world, float dt) { Ticks++; LastDt = dt; }
        }

        [Test]
        public void NeedsAndMovement_TickAtTheirOwnRates()
        {
            var loop = new SimulationLoop(new World(1));
            var needs = new CountingSystem(10);
            var move = new CountingSystem(30);
            loop.AddSystem(needs);
            loop.AddSystem(move);

            // One simulated second in 60 frames of 1/60 s.
            for (int i = 0; i < 60; i++) loop.Advance(1.0 / 60.0);

            Assert.That(needs.Ticks, Is.InRange(9, 10));
            Assert.That(move.Ticks, Is.InRange(29, 30));
            Assert.AreEqual(1f / 10f, needs.LastDt, 1e-6f);
            Assert.AreEqual(1f / 30f, move.LastDt, 1e-6f);
        }

        [Test]
        public void TickCount_DoesNotDependOnFrameRate()
        {
            int TicksAt(int fps)
            {
                var loop = new SimulationLoop(new World(1));
                var s = new CountingSystem(30);
                loop.AddSystem(s);
                for (int i = 0; i < fps * 10; i++) loop.Advance(1.0 / fps);
                return s.Ticks;
            }
            int at30 = TicksAt(30), at144 = TicksAt(144), at20 = TicksAt(20);
            Assert.That(at30, Is.InRange(299, 300));
            Assert.That(at144, Is.InRange(299, 300));
            Assert.That(at20, Is.InRange(299, 300));
        }

        [Test]
        public void SlowFrame_IsCappedInsteadOfSpiralling()
        {
            var loop = new SimulationLoop(new World(1));
            var s = new CountingSystem(30);
            loop.AddSystem(s);
            loop.Advance(5.0); // a 5-second hitch
            Assert.LessOrEqual(s.Ticks, (int)(SimulationLoop.MaxCatchUpSeconds * 30) + 1);
            Assert.Greater(loop.DroppedSeconds, 4.0);
        }

        [Test]
        public void Pause_StopsTicks()
        {
            var loop = new SimulationLoop(new World(1)) { TimeScale = 0f };
            var s = new CountingSystem(30);
            loop.AddSystem(s);
            for (int i = 0; i < 60; i++) loop.Advance(1.0 / 60.0);
            Assert.AreEqual(0, s.Ticks);
        }

        [Test]
        public void SameSeed_GivesIdenticalWorldAfterRunning()
        {
            World Run()
            {
                var w = new World(777);
                w.SpawnStartingPopulation(20, 100);
                var loop = SimulationFactory.CreateDefault(w);
                for (int i = 0; i < 300; i++) loop.Advance(1.0 / 60.0);
                return w;
            }
            var a = Run();
            var b = Run();
            Assert.AreEqual(a.Agents.Count, b.Agents.Count);
            for (int i = 0; i < a.Agents.Count; i++)
            {
                Assert.AreEqual(a.Agents.PosX[i], b.Agents.PosX[i]);
                Assert.AreEqual(a.Agents.PosZ[i], b.Agents.PosZ[i]);
                Assert.AreEqual(a.Agents.Hunger[i], b.Agents.Hunger[i]);
            }
        }

        [Test]
        public void Agents_StayInsideTheTank()
        {
            var w = new World(3, 2f, 2f);
            w.SpawnStartingPopulation(10, 50);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 1200; i++) loop.Advance(1.0 / 60.0);
            for (int i = 0; i < w.Agents.Count; i++)
            {
                Assert.That(w.Agents.PosX[i], Is.InRange(-2f, 2f));
                Assert.That(w.Agents.PosZ[i], Is.InRange(-2f, 2f));
            }
        }

        [Test]
        public void TwoHundredAgents_TickWellUnderBudget()
        {
            // Phase 1 target is 200 agents at 60 FPS; the sim must use a small slice of a 16.7 ms frame.
            var w = new World(5);
            w.SpawnStartingPopulation(40, 160);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 120; i++) loop.Advance(1.0 / 60.0); // warm up
            double worst = 0;
            for (int i = 0; i < 600; i++) { loop.Advance(1.0 / 60.0); if (loop.LastAdvanceMs > worst) worst = loop.LastAdvanceMs; }
            Assert.Less(worst, 8.0, "Sim step took " + worst + " ms");
        }
    }

    public class AgentStoreTests
    {
        [Test]
        public void Add_GrowsPastInitialCapacity()
        {
            var s = new AgentStore(2);
            for (int i = 0; i < 100; i++) s.Add(AgentKind.Insect, i, 0, 0);
            Assert.AreEqual(100, s.Count);
            Assert.AreEqual(99f, s.PosX[99]);
        }

        [Test]
        public void RemoveAt_MovesLastAgentIntoTheGap()
        {
            var s = new AgentStore();
            int a = s.Add(AgentKind.Frog, 1, 0, 0);
            s.Add(AgentKind.Frog, 2, 0, 0);
            s.Add(AgentKind.Insect, 3, 0, 0);
            int lastId = s.Id[2];
            s.RemoveAt(a);
            Assert.AreEqual(2, s.Count);
            Assert.AreEqual(lastId, s.Id[0]);
            Assert.AreEqual(3f, s.PosX[0]);
            Assert.AreEqual(AgentKind.Insect, s.Kind[0]);
            Assert.AreEqual(-1, s.IndexOf(1));
        }

        [Test]
        public void Ids_AreUniqueAndNeverReused()
        {
            var s = new AgentStore();
            int first = s.Id[s.Add(AgentKind.Frog, 0, 0, 0)];
            s.RemoveAt(0);
            int second = s.Id[s.Add(AgentKind.Frog, 0, 0, 0)];
            Assert.AreNotEqual(first, second);
        }
    }
}

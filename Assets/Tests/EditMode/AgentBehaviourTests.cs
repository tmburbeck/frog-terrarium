using System.Diagnostics;
using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    public class AgentBehaviourTests
    {
        /// <summary>A flat tank at conditions White's tree frogs like, with nothing in it.</summary>
        private static World ComfyWorld(ulong seed = 1)
        {
            var w = new World(seed);
            w.Environment.AmbientTemperature = 26f;
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            return w;
        }

        private static void Run(SimulationLoop loop, float seconds)
        {
            int frames = (int)(seconds * 30f);
            for (int i = 0; i < frames; i++) loop.Advance(1.0 / 30.0);
        }

        private static int IndexOfId(World w, int id) => w.Agents.IndexOf(id);

        [Test]
        public void HungryFrog_CatchesANearbyInsect()
        {
            // QA case AI-01.
            var w = ComfyWorld();
            var rng = w.Random.Get("test");
            int frog = w.AddFrog(0f, 0f);
            w.Agents.Hunger[frog] = 0.8f;
            int frogId = w.Agents.Id[frog];
            for (int k = 0; k < 3; k++) w.AddInsect(1.5f + k * 0.4f, 0.5f * k, rng);
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 60f);
            int f = IndexOfId(w, frogId);
            Assert.Less(w.Agents.CountOf(AgentKind.Insect), 3, "the frog should have eaten");
            Assert.Less(w.Agents.Hunger[f], 0.6f);
        }

        [Test]
        public void TwoUrgentNeeds_FrogDoesNotFlipFlop()
        {
            // QA case AI-03.
            var w = ComfyWorld();
            TerrainBrush.Apply(w.Terrain, BrushMode.Lower, 6f, 0f, 2.5f, 1f, 1f);
            var rng = w.Random.Get("test");
            int frog = w.AddFrog(-4f, 0f);
            int id = w.Agents.Id[frog];
            w.Agents.Hunger[frog] = 0.9f;
            w.Agents.Thirst[frog] = 0.85f;
            for (int k = 0; k < 6; k++) w.AddInsect(-1f + k, 3f, rng);
            var loop = SimulationFactory.CreateDefault(w);

            int switches = 0;
            Goal last = Goal.Idle;
            for (int step = 0; step < 30 * 20; step++)
            {
                loop.Advance(1.0 / 30.0);
                var g = w.Agents.Goal[IndexOfId(w, id)];
                if (g != last) { switches++; last = g; }
            }
            // First pick, then at most a handful of real changes as needs are met: never every tick.
            Assert.LessOrEqual(switches, 5);
        }

        [Test]
        public void ThirstyFrog_GoesToThePoolAndDrinks()
        {
            var w = ComfyWorld();
            TerrainBrush.Apply(w.Terrain, BrushMode.Lower, 6f, 0f, 2.5f, 1f, 1f);
            int frog = w.AddFrog(-6f, 0f);
            int id = w.Agents.Id[frog];
            w.Agents.Thirst[frog] = 0.8f;
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 60f);
            Assert.Less(w.Agents.Thirst[IndexOfId(w, id)], 0.3f);
        }

        [Test]
        public void TiredFrog_SleepsAndRecovers()
        {
            var w = ComfyWorld();
            int frog = w.AddFrog(0f, 0f);
            int id = w.Agents.Id[frog];
            w.Agents.Fatigue[frog] = 0.9f;
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 2f);
            int f = IndexOfId(w, id);
            Assert.AreEqual(Goal.Sleep, w.Agents.Goal[f]);
            float speed = System.Math.Abs(w.Agents.VelX[f]) + System.Math.Abs(w.Agents.VelZ[f]);
            Assert.Less(speed, 0.01f, "a sleeping frog stays still");
            Run(loop, 60f);
            Assert.Less(w.Agents.Fatigue[IndexOfId(w, id)], 0.2f);
        }

        [Test]
        public void ColdFrog_MovesTowardTheHeater()
        {
            var w = new World(2);
            w.Environment.AmbientTemperature = 17f;
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            w.Devices.Add(DeviceKind.Heater, 5f, 0f).Setting = 30f;
            EnvironmentSystem.WarmUp(w, 600f);
            int frog = w.AddFrog(-8f, 0f);
            int id = w.Agents.Id[frog];
            w.Agents.Comfort[frog] = BrainSystem.ComfortAt(w, SpeciesCatalog.Frog, -8f, 0f);
            float before = w.Agents.Comfort[frog];
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 60f);
            int f = IndexOfId(w, id);
            Assert.Greater(w.Agents.PosX[f], -4f, "frog should have moved toward the warmth");
            Assert.Greater(w.Agents.Comfort[f], before + 0.2f);
        }

        [Test]
        public void Insect_FleesAFastFrog_ButNotACreepingOne()
        {
            // QA case AI-07, and the frogs' stealth rule.
            foreach (bool fast in new[] { true, false })
            {
                var w = ComfyWorld();
                var rng = w.Random.Get("test");
                int bug = w.AddInsect(0f, 0f, rng);
                int frog = w.AddFrog(0.8f, 0f);
                w.Agents.VelX[frog] = fast ? -0.9f : -0.2f;
                var brain = new BrainSystem();
                brain.Tick(w, 0.1f);
                bug = w.Agents.IndexOf(w.Agents.Id[bug]);
                if (fast)
                {
                    Assert.AreEqual(Goal.Flee, w.Agents.Goal[bug]);
                    Assert.Less(w.Agents.VelX[bug], 0f, "runs away from the frog");
                }
                else
                {
                    Assert.AreNotEqual(Goal.Flee, w.Agents.Goal[bug]);
                }
            }
        }

        [Test]
        public void Crickets_BreedWhenThereIsRoom_AndStopAtCapacity()
        {
            var w = ComfyWorld();
            var rng = w.Random.Get("test");
            int cap = LifecycleSystem.InsectCapacity(w);
            Assert.Greater(cap, 40);
            for (int k = 0; k < 20; k++)
            {
                int i = w.AddInsect(rng.Range(-9f, 9f), rng.Range(-5f, 5f), rng);
                w.Agents.Age[i] = 100f;
                w.Agents.Lifespan[i] = 100000f;
            }
            var loop = SimulationFactory.CreateDefault(w);
            int peak = 0;
            for (int m = 0; m < 30; m++)
            {
                Run(loop, 60f);
                int n = w.Agents.CountOf(AgentKind.Insect);
                if (n > peak) peak = n;
                Assert.LessOrEqual(n, cap, "population passed the tank's capacity");
            }
            Assert.Greater(peak, 30, "crickets should have bred");
        }

        [Test]
        public void Crickets_DoNotBreedOnSand()
        {
            var w = ComfyWorld();
            TerrainBrush.Apply(w.Terrain, BrushMode.Paint, 0f, 0f, 30f, 1f, 1f, GroundLayer.Sand);
            var rng = w.Random.Get("test");
            for (int k = 0; k < 20; k++)
            {
                int i = w.AddInsect(rng.Range(-9f, 9f), rng.Range(-5f, 5f), rng);
                w.Agents.Age[i] = 100f;
                w.Agents.Lifespan[i] = 100000f;
            }
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 300f);
            Assert.AreEqual(20, w.Agents.CountOf(AgentKind.Insect));
        }

        [Test]
        public void Crickets_DieOfOldAge()
        {
            var w = ComfyWorld();
            int i = w.AddInsect(0f, 0f, w.Random.Get("test"));
            w.Agents.Lifespan[i] = 3f;
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 5f);
            Assert.AreEqual(0, w.Agents.Count);
        }

        [Test]
        public void Feeder_ReleasesOnSchedule_WarnsWhenLow_AndWhenEmpty()
        {
            // QA case AU-01.
            var w = ComfyWorld();
            var feeder = w.Devices.Add(DeviceKind.Feeder, 0f, 0f);
            feeder.Setting = 10f;
            feeder.Setting2 = 3f;
            feeder.Supply = 50f;
            feeder.Timer = 10f;
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 9f);
            Assert.AreEqual(0, w.Agents.Count, "nothing before the first release");
            Run(loop, 2f);
            Assert.AreEqual(3, w.Agents.Count);
            Assert.AreEqual(47f, feeder.Supply);
            Assert.IsNull(w.Alerts.Find("feeder-" + feeder.Id + "-low"));

            feeder.Supply = 5f;
            Run(loop, 1.5f);
            Assert.IsNotNull(w.Alerts.Find("feeder-" + feeder.Id + "-low"), "warns before it runs out");
            Run(loop, 20f);
            Assert.AreEqual(8, w.Agents.CountOf(AgentKind.Insect), "releases only what it has");
            Assert.IsNotNull(w.Alerts.Find("feeder-" + feeder.Id + "-empty"));
        }

        [Test]
        public void DryAir_RaisesAnAlertNamingHumidity_WithinTenSeconds()
        {
            // QA case CE-01.
            var w = ComfyWorld();
            for (int k = 0; k < 3; k++) w.AddFrog(-3f + 3f * k, 0f);
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 5f);
            Assert.IsNull(w.Alerts.Find("dry"), "a comfy tank is not too dry");

            w.Environment.AmbientHumidity = 15f;
            for (int i = 0; i < w.Environment.Humidity.Length; i++) w.Environment.Humidity[i] = 15f;
            Run(loop, 10f);
            var alert = w.Alerts.Find("dry");
            Assert.IsNotNull(alert);
            StringAssert.Contains("humidity", alert.Text);
            StringAssert.Contains("3 frogs", alert.Text);
            Assert.IsNotNull(alert.Fix);
        }

        [Test]
        public void SpatialHash_FindsTheSameNearestAsCheckingEveryone()
        {
            var w = new World(9);
            var rng = new SeededRandom(3);
            for (int k = 0; k < 300; k++)
                w.Agents.Add(rng.Chance(0.3f) ? AgentKind.Frog : AgentKind.Insect, rng.Range(-10f, 10f), 0f, rng.Range(-6f, 6f));
            var hash = new SpatialHash();
            hash.Build(w.Agents, w.HalfSizeX, w.HalfSizeZ);
            var a = w.Agents;
            for (int q = 0; q < 200; q++)
            {
                float x = rng.Range(-10f, 10f), z = rng.Range(-6f, 6f), r = rng.Range(0.2f, 4f);
                int expected = -1, count = 0;
                float best = r * r;
                for (int i = 0; i < a.Count; i++)
                {
                    if (a.Kind[i] != AgentKind.Insect) continue;
                    float d2 = (a.PosX[i] - x) * (a.PosX[i] - x) + (a.PosZ[i] - z) * (a.PosZ[i] - z);
                    if (d2 <= r * r) count++;
                    if (d2 < best) { best = d2; expected = i; }
                }
                Assert.AreEqual(expected, hash.FindNearest(a, x, z, r, AgentKind.Insect));
                Assert.AreEqual(count, hash.CountWithin(a, x, z, r, AgentKind.Insect));
            }
        }

        [Test]
        public void SameSeed_SameStory()
        {
            World Run10Minutes()
            {
                var w = World.CreateStarterTank(4321, 8, 60);
                var loop = SimulationFactory.CreateDefault(w);
                Run(loop, 600f);
                return w;
            }
            var a = Run10Minutes();
            var b = Run10Minutes();
            Assert.AreEqual(a.Agents.Count, b.Agents.Count);
            for (int i = 0; i < a.Agents.Count; i++)
            {
                Assert.AreEqual(a.Agents.Id[i], b.Agents.Id[i]);
                Assert.AreEqual(a.Agents.PosX[i], b.Agents.PosX[i]);
                Assert.AreEqual(a.Agents.Hunger[i], b.Agents.Hunger[i]);
            }
        }

        [Test]
        public void TwoHundredAgents_StayWithinTheTickBudget_AndKeepTheRules()
        {
            // QA performance target for Phase 1: 200 agents, simulation tick under 4 ms. The real
            // 60 FPS check is the in-game F8 benchmark on your own PC; this guards the simulation half.
            var w = World.CreateStarterTank(2024, 40, 160);
            var loop = SimulationFactory.CreateDefault(w);
            Run(loop, 5f); // warm up (JIT, first allocations)
            var watch = Stopwatch.StartNew();
            const float seconds = 120f;
            for (int step = 0; step < seconds * 30f; step++)
            {
                loop.Advance(1.0 / 30.0);
                if (step % 30 != 0) continue;
                var a = w.Agents;
                Assert.LessOrEqual(a.Count, World.MaxAgents);
                for (int i = 0; i < a.Count; i++)
                {
                    // QA invariants: needs stay in range, nobody leaves the tank or sinks into the ground.
                    Assert.That(a.Hunger[i], Is.InRange(0f, 1f));
                    Assert.That(a.Thirst[i], Is.InRange(0f, 1f));
                    Assert.That(a.Fatigue[i], Is.InRange(0f, 1f));
                    Assert.That(a.Comfort[i], Is.InRange(0f, 1f));
                    Assert.IsTrue(w.Terrain.Contains(a.PosX[i], a.PosZ[i]));
                    Assert.AreEqual(MovementSystem.SurfaceHeight(w.Terrain, a.PosX[i], a.PosZ[i]), a.PosY[i], 1e-3f);
                }
            }
            watch.Stop();
            // Brain and needs tick 10 times a second, so this is the cost of one full 10 Hz step
            // (with movement, environment and the rest included).
            double msPerTick = watch.Elapsed.TotalMilliseconds / (seconds * BrainSystem.Rate);
            TestContext.WriteLine("Average simulation cost per 10 Hz tick with ~200 agents: " + msPerTick.ToString("0.000") + " ms");
            Assert.Less(msPerTick, 4.0);
        }
    }
}

using System.Collections;
using System.IO;
using FrogTerrarium.Game;
using FrogTerrarium.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrogTerrarium.Tests
{
    public class SandboxPlayModeTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            // Remove everything the bootstrap created so tests do not affect each other.
            foreach (var name in new[] { "Test Bootstrap", "Simulation", "Tank Terrain", "Sun", "Main Camera" })
            {
                GameObject go;
                while ((go = GameObject.Find(name)) != null) Object.DestroyImmediate(go);
            }
            _root = null;
        }

        private SandboxBootstrap Boot(int frogs = 10, int insects = 40)
        {
            _root = new GameObject("Test Bootstrap");
            _root.SetActive(false);
            var boot = _root.AddComponent<SandboxBootstrap>();
            boot.startingFrogs = frogs;
            boot.startingInsects = insects;
            _root.SetActive(true);
            return boot;
        }

        private static float SumOfX(World w)
        {
            float sum = 0f;
            for (int i = 0; i < w.Agents.Count; i++) sum += w.Agents.PosX[i];
            return sum;
        }

        [Test]
        public void Materials_LoadWithARealShader()
        {
            // Guards against the pink-everything bug: materials must be real assets with a working shader.
            foreach (var name in GameMaterials.Names)
            {
                var mat = GameMaterials.Load(name);
                Assert.IsNotNull(mat, name + " material missing");
                // A shader that failed to compile shows up as Hidden/InternalErrorShader here.
                Assert.AreEqual(GameMaterials.ExpectedShader(name), mat.shader.name, name + " uses the wrong shader");
                if (GameMaterials.NeedsInstancing(name))
                    Assert.IsTrue(mat.enableInstancing, name + " needs GPU instancing enabled");
            }
        }

        [UnityTest]
        public IEnumerator Sandbox_StartsWithVisuals()
        {
            var boot = Boot();
            yield return null;
            Assert.IsTrue(boot.Runner.VisualsReady, "frog and insect meshes or materials missing");
            var world = boot.Runner.Sim.World;
            Assert.AreEqual(world.Terrain.ChunksX * world.Terrain.ChunksZ, boot.Terrain.ChunkCount);
            var chunk = GameObject.Find("Tank Terrain/Chunk 0");
            Assert.IsNotNull(chunk);
            Assert.AreEqual("Terrain", chunk.GetComponent<Renderer>().sharedMaterial.name);
            Assert.IsNotNull(GameObject.Find("Tank Terrain/Water"));
            Assert.AreEqual(3, world.Devices.Count, "starter tank should have a heater, a mister and a feeder");
        }

        [UnityTest]
        public IEnumerator Terrain_RedrawsAfterSculpting()
        {
            var boot = Boot();
            yield return null;
            yield return null;
            var world = boot.Runner.Sim.World;
            var mesh = GameObject.Find("Tank Terrain/Chunk 0").GetComponent<MeshFilter>().sharedMesh;
            float before = mesh.bounds.max.y;
            TerrainBrush.Apply(world.Terrain, BrushMode.Raise, world.Terrain.MinX + 2f, world.Terrain.MinZ + 2f, 1f, 2f, 1f);
            yield return null;
            Assert.Greater(mesh.bounds.max.y, before + 0.5f, "chunk mesh was not rebuilt after the edit");
        }

        [UnityTest]
        public IEnumerator Benchmark_TopsUpToFiveHundredAnimals()
        {
            var boot = Boot();
            yield return null;
            // The on-screen Speed test button needs these, for laptops where F8 does something else.
            Assert.AreSame(boot.Benchmark, boot.Hud.benchmark);
            Assert.AreSame(boot.Overlay, boot.Hud.overlay);
            boot.Benchmark.Begin();
            Assert.IsTrue(boot.Benchmark.Running);
            Assert.GreaterOrEqual(boot.Runner.Sim.World.Agents.Count, 500);
            Assert.AreEqual(PerformanceBenchmark.TargetFrogs, boot.Runner.Sim.World.Agents.CountOf(FrogTerrarium.Simulation.AgentKind.Frog));
            for (int i = 0; i < 5; i++) yield return null;
            StringAssert.Contains("Benchmark running", boot.Benchmark.Status);
            StringAssert.Contains("Benchmark running", boot.Overlay.BuildText());
        }

        [UnityTest]
        public IEnumerator Frogs_HaveBrains_InTheRealGameLoop()
        {
            var boot = Boot(6, 40);
            float until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until) yield return null;
            var a = boot.Runner.Sim.World.Agents;
            int thinking = 0;
            for (int i = 0; i < a.Count; i++) if (a.Goal[i] != Goal.Idle) thinking++;
            Assert.AreEqual(a.Count, thinking, "every agent should have picked a goal");
            Assert.IsNotNull(boot.Runner.Brain);
            StringAssert.Contains("Brain", boot.Overlay.BuildText());
        }

        [UnityTest]
        public IEnumerator EveryLifeStageAndSpecies_DrawsAndThinks()
        {
            // Phase 2: eggs, tadpoles and the new species go through the real game loop without errors.
            var boot = Boot(4, 20);
            yield return null;
            var world = boot.Runner.Sim.World;
            TerrainBrush.Apply(world.Terrain, BrushMode.Lower, 0f, 0f, 2f, 1f, 1f);
            var g = Genome.Default;
            int eggId = world.Agents.Id[world.AddEgg(0f, 0f, 1, Sex.Female, g, g)];
            int tadpole = world.AddEgg(0.3f, 0f, 2, Sex.Male, g, g);
            world.Agents.Growth[tadpole] = SpeciesCatalog.Frogs[2].HatchAt + 1f;
            world.Agents.Stage[tadpole] = LifeStage.Tadpole;
            for (byte sp = 0; sp < SpeciesCatalog.Frogs.Length; sp++) world.AddFrog(-3f + sp, 3f, sp, Sex.Male);
            boot.Tools.SelectedAgentId = eggId;
            float until = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < until) yield return null;
            boot.Tools.SelectedAgentId = world.Agents.Id[world.Agents.Count - 1];
            yield return null;
            Assert.AreEqual(LifeStage.Egg, world.Agents.Stage[world.Agents.IndexOf(eggId)], "eggs stay eggs for a minute");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HeatAndHumidityViews_Work()
        {
            var boot = Boot();
            yield return null;
            boot.Terrain.mode = TerrainViewMode.Temperature;
            yield return null;
            boot.Terrain.mode = TerrainViewMode.Humidity;
            yield return null;
            boot.Terrain.mode = TerrainViewMode.Normal;
            yield return null;
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator Sandbox_StartsAndTicks()
        {
            var boot = Boot();
            Assert.IsNotNull(boot.Runner);
            Assert.AreEqual(50, boot.Runner.Sim.World.Agents.Count);

            float until = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < until) yield return null;

            foreach (var e in boot.Runner.Sim.Systems)
                Assert.Greater(e.TotalTicks, 0, e.System.Name + " never ticked");
            Assert.Greater(boot.Runner.Sim.World.SimTime, 0.5);
        }

        [UnityTest]
        public IEnumerator DebugOverlay_ShowsTickTimeAgentCountAndFrameTime()
        {
            var boot = Boot();
            for (int i = 0; i < 10; i++) yield return null;
            string text = boot.Overlay.BuildText();
            StringAssert.Contains("Frame", text);
            StringAssert.Contains("Agents 50", text);
            StringAssert.Contains("ms/tick", text);
            StringAssert.Contains("Needs", text);
            StringAssert.Contains("Movement", text);
            StringAssert.Contains("Environment", text);
            Assert.Greater(boot.Overlay.SmoothedFrameMs, 0f);
        }

        [UnityTest]
        public IEnumerator QuickSaveAndLoad_RestoresTheTank()
        {
            var boot = Boot();
            for (int i = 0; i < 10; i++) yield return null;
            var runner = boot.Runner;
            string path = runner.QuickSavePath;
            string backup = path + ".testbackup";
            bool hadSave = File.Exists(path);
            if (hadSave) File.Copy(path, backup, true);
            try
            {
                runner.QuickSave();
                int count = runner.Sim.World.Agents.Count;
                float x0 = runner.Sim.World.Agents.PosX[0];
                float sum0 = SumOfX(runner.Sim.World);

                float until = Time.realtimeSinceStartup + 0.5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Assert.AreNotEqual(sum0, SumOfX(runner.Sim.World), "agents should have moved");

                runner.Sim.TimeScale = 0f;
                runner.QuickLoad();
                Assert.AreEqual(count, runner.Sim.World.Agents.Count);
                Assert.AreEqual(x0, runner.Sim.World.Agents.PosX[0]);
            }
            finally
            {
                if (hadSave) { File.Copy(backup, path, true); File.Delete(backup); }
                else if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}

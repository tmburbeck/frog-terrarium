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
            foreach (var name in new[] { "Test Bootstrap", "Simulation", "Tank Floor", "Sun", "Main Camera" })
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

                float until = Time.realtimeSinceStartup + 0.5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                Assert.AreNotEqual(x0, runner.Sim.World.Agents.PosX[0], "agents should have moved");

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

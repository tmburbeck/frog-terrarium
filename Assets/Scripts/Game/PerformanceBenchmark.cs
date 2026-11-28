using System.IO;
using FrogTerrarium.Simulation;
using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// The performance check: press B (or F8, or the Speed test button) to top the tank up to 500 animals (100 frogs of all
    /// species and 400 crickets), then measure frame rate and simulation time for 20 seconds. The target is
    /// 60 frames per second with the simulation under 4 ms per tick. V-sync is switched off while
    /// measuring, so the result shows what the PC can really do rather than the monitor's refresh rate.
    /// The result shows in the debug overlay and is saved to benchmark.txt next to the saves.
    ///
    /// The tank is put back exactly as it was when the test ends: the extra animals, the money they
    /// cost and the dirt they made all go. (The first playtest showed why: pressing it in money mode
    /// left 100 free frogs that dirtied the tank and emptied the bank for the rest of the game.)
    /// </summary>
    public sealed class PerformanceBenchmark : MonoBehaviour
    {
        public SimulationRunner runner;

        public const int TargetFrogs = 100, TargetInsects = 400;
        public const float Duration = 20f;
        public const float TargetFps = 60f;
        public const double TickBudgetMs = 4.0;

        public bool Running { get; private set; }
        /// <summary>True while any speed test is running (playtest stats skip those minutes).</summary>
        public static bool AnyRunning { get; private set; }
        public string Status { get; private set; } = "";
        public string ResultPath => Path.Combine(Application.persistentDataPath, "benchmark.txt");

        private float _elapsed, _worstMs;
        private int _frames, _slowFrames;
        private double _simMs;
        private long _brainTicksAtStart;
        private int _agentSum;
        private int _savedVSync, _savedTargetFps;
        private byte[] _tankBefore;
        private World _testWorld;
        private float _scaleBefore;

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (GameSettings.Pressed(GameAction.SpeedTest) && !Running) Begin();
#endif
            if (!Running) return;
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            float ms = dt * 1000f;
            if (ms > _worstMs) _worstMs = ms;
            if (ms > 1000f / 30f) _slowFrames++;
            _simMs += runner.Sim.LastAdvanceMs;
            _agentSum += runner.Sim.World.Agents.Count;
            Status = string.Format("Benchmark running... {0:0} s left", Duration - _elapsed);
            if (_elapsed >= Duration) Finish();
        }

        public void Begin()
        {
            if (runner == null || runner.Sim == null) return;
            var world = runner.Sim.World;
            try { _tankBefore = SaveSystem.ToBytes(world); }
            catch (System.Exception e) { _tankBefore = null; Debug.LogWarning("Speed test could not remember the tank: " + e.Message); }
            _testWorld = world;
            _scaleBefore = runner.Sim.TimeScale;
            var rng = world.Random.Get("benchmark");
            int frogs = world.Agents.CountOf(AgentKind.Frog), insects = world.Agents.CountOf(AgentKind.Insect);
            for (; frogs < TargetFrogs; frogs++)
            {
                world.FindDryGround(rng, -world.HalfSizeX, world.HalfSizeX, -world.HalfSizeZ, world.HalfSizeZ, out float x, out float z);
                world.AddFrog(x, z, (byte)(frogs % SpeciesCatalog.Frogs.Length), frogs % 2 == 0 ? Sex.Female : Sex.Male);
            }
            for (; insects < TargetInsects; insects++)
            {
                world.FindDryGround(rng, -world.HalfSizeX, world.HalfSizeX, -world.HalfSizeZ, world.HalfSizeZ, out float x, out float z);
                world.AddInsect(x, z, rng);
            }
            runner.Sim.TimeScale = 1f;
            _savedVSync = QualitySettings.vSyncCount;
            _savedTargetFps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _elapsed = 0f; _worstMs = 0f; _frames = 0; _slowFrames = 0; _simMs = 0; _agentSum = 0;
            _brainTicksAtStart = BrainTicks();
            Running = true;
            AnyRunning = true;
        }

        /// <summary>
        /// Puts the tank back as it was before the test. If the player started a new game or loaded
        /// one meanwhile, that game is left alone.
        /// </summary>
        private void PutTankBack()
        {
            if (_tankBefore == null || runner.Sim.World != _testWorld) { _tankBefore = null; _testWorld = null; return; }
            try
            {
                runner.Restore(SaveSystem.FromBytes(_tankBefore), _scaleBefore);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Speed test could not put the tank back: " + e.Message);
            }
            _tankBefore = null;
            _testWorld = null;
        }

        private void OnDisable() => AnyRunning = false;

        private long BrainTicks()
        {
            foreach (var e in runner.Sim.Systems) if (e.System is BrainSystem) return e.TotalTicks;
            return 0;
        }

        /// <summary>Ends the test now, with the result so far (also used by tests).</summary>
        public void EndNow()
        {
            if (Running) Finish();
        }

        private void Finish()
        {
            if (_elapsed <= 0f) _elapsed = 0.001f;
            Running = false;
            AnyRunning = false;
            PutTankBack();
            QualitySettings.vSyncCount = _savedVSync;
            Application.targetFrameRate = _savedTargetFps;

            float fps = _frames / _elapsed;
            long ticks = BrainTicks() - _brainTicksAtStart;
            double msPerTick = ticks > 0 ? _simMs / ticks : 0;
            int agents = _agentSum / Mathf.Max(1, _frames);
            bool pass = fps >= TargetFps && msPerTick < TickBudgetMs;
            Status = string.Format("Benchmark {0}: {1:0} FPS average, worst frame {2:0.0} ms, {3} slow frames, sim {4:0.00} ms per tick, {5} animals. Your tank is back as it was.",
                pass ? "PASSED" : "FAILED", fps, _worstMs, _slowFrames, msPerTick, agents);
            string details = Status + "\n"
                + "Target: " + TargetFps + " FPS and under " + TickBudgetMs + " ms per tick with " + (TargetFrogs + TargetInsects) + " animals.\n"
                + "GPU: " + SystemInfo.graphicsDeviceName + "\nCPU: " + SystemInfo.processorType + " (" + SystemInfo.processorCount + " threads)\n"
                + "RAM: " + SystemInfo.systemMemorySize + " MB\nScreen: " + Screen.width + "x" + Screen.height + "\n"
                + "Date: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n";
            Debug.Log(details);
            try { File.WriteAllText(ResultPath, details); }
            catch (System.Exception e) { Debug.LogWarning("Could not save benchmark result: " + e.Message); }
        }
    }
}

using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Builds the sandbox at runtime: the terrain, light, orbit camera, the simulation
    /// runner, the building tools and menus, and the debug overlay. The Sandbox scene
    /// contains only this one object, which keeps the scene file tiny and merge-friendly.
    /// </summary>
    public sealed class SandboxBootstrap : MonoBehaviour
    {
        public ulong seed = 12345;
        public int startingFrogs = 8;
        public int startingInsects = 80;

        public SimulationRunner Runner { get; private set; }
        public DebugOverlay Overlay { get; private set; }
        public TerrainView Terrain { get; private set; }
        public ToolController Tools { get; private set; }
        public GameHud Hud { get; private set; }
        public PerformanceBenchmark Benchmark { get; private set; }

        private void Awake()
        {
            var runnerGo = new GameObject("Simulation");
            runnerGo.SetActive(false);
            Runner = runnerGo.AddComponent<SimulationRunner>();
            Runner.seed = seed;
            Runner.startingFrogs = startingFrogs;
            Runner.startingInsects = startingInsects;
            runnerGo.SetActive(true); // Awake runs now, with the settings above

            var terrainGo = new GameObject("Tank Terrain");
            Terrain = terrainGo.AddComponent<TerrainView>();
            Terrain.runner = Runner;

            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Sun");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.backgroundColor = new Color(0.12f, 0.16f, 0.14f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            var orbit = cam.GetComponent<OrbitCamera>();
            if (orbit == null) orbit = cam.gameObject.AddComponent<OrbitCamera>();
            orbit.target = new Vector3(0f, 1f, 0f);

            Overlay = gameObject.AddComponent<DebugOverlay>();
            Overlay.runner = Runner;
            Benchmark = gameObject.AddComponent<PerformanceBenchmark>();
            Benchmark.runner = Runner;
            Overlay.benchmark = Benchmark;

            var devices = gameObject.AddComponent<DeviceView>();
            Tools = gameObject.AddComponent<ToolController>();
            Hud = gameObject.AddComponent<GameHud>();
            devices.runner = Runner;
            devices.tools = Tools;
            Tools.runner = Runner;
            Tools.hud = Hud;
            Tools.deviceView = devices;
            Hud.runner = Runner;
            Hud.tools = Tools;
            Hud.terrainView = Terrain;
            Hud.benchmark = Benchmark;
            Hud.overlay = Overlay;
        }
    }
}

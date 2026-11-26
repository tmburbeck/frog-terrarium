using UnityEngine;

namespace FrogTerrarium.Game
{
    /// <summary>
    /// Builds the Phase 0 sandbox at runtime: a tank floor, light, orbit camera,
    /// the simulation runner and the debug overlay. The Sandbox scene contains only
    /// this one object, which keeps the scene file tiny and merge-friendly.
    /// </summary>
    public sealed class SandboxBootstrap : MonoBehaviour
    {
        public ulong seed = 12345;
        public int startingFrogs = 40;
        public int startingInsects = 200;

        public SimulationRunner Runner { get; private set; }
        public DebugOverlay Overlay { get; private set; }

        private void Awake()
        {
            var runnerGo = new GameObject("Simulation");
            runnerGo.SetActive(false);
            Runner = runnerGo.AddComponent<SimulationRunner>();
            Runner.seed = seed;
            Runner.startingFrogs = startingFrogs;
            Runner.startingInsects = startingInsects;
            runnerGo.SetActive(true); // Awake runs now, with the settings above

            var world = Runner.Sim.World;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Tank Floor";
            floor.transform.localScale = new Vector3(world.HalfSizeX * 0.2f, 1f, world.HalfSizeZ * 0.2f);
            var floorMaterial = GameMaterials.Load("Floor");
            if (floorMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

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
            if (cam.GetComponent<OrbitCamera>() == null) cam.gameObject.AddComponent<OrbitCamera>();

            Overlay = gameObject.AddComponent<DebugOverlay>();
            Overlay.runner = Runner;
        }
    }
}

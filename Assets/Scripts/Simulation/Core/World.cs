namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// All simulation state in one place: agents, terrain, environment, devices, tank bounds,
    /// random streams and the simulated clock. Everything in here is what a save file must capture.
    /// </summary>
    public sealed class World
    {
        public World(ulong seed, float halfSizeX = 10f, float halfSizeZ = 6f)
        {
            Seed = seed;
            Random = new RandomService(seed);
            Agents = new AgentStore();
            HalfSizeX = halfSizeX;
            HalfSizeZ = halfSizeZ;
            Terrain = new TerrainGrid(halfSizeX, halfSizeZ);
            Environment = new EnvironmentField(halfSizeX, halfSizeZ);
            Devices = new DeviceStore();
        }

        /// <summary>
        /// A ready-to-play starter tank: generated terrain with a pool, a heater and a mister,
        /// settled temperature and humidity, and the starting animals.
        /// </summary>
        public static World CreateStarterTank(ulong seed, int frogs, int insects)
        {
            var world = new World(seed);
            MapGenerator.Generate(world);
            var t = world.Terrain;
            world.Devices.Add(DeviceKind.Heater, -t.HalfSizeX * 0.45f, t.HalfSizeZ * 0.35f);
            world.Devices.Add(DeviceKind.Mister, t.HalfSizeX * 0.45f, t.HalfSizeZ * 0.3f);
            EnvironmentSystem.WarmUp(world, 300f);
            world.SpawnStartingPopulation(frogs, insects);
            return world;
        }

        public ulong Seed { get; }
        public RandomService Random { get; }
        public AgentStore Agents { get; }
        public TerrainGrid Terrain { get; }
        public EnvironmentField Environment { get; }
        public DeviceStore Devices { get; }

        /// <summary>Half the tank's width and depth in metres; the tank is centred on the origin.</summary>
        public float HalfSizeX { get; set; }
        public float HalfSizeZ { get; set; }

        /// <summary>Simulated seconds since the world was created.</summary>
        public double SimTime { get; internal set; }

        /// <summary>Fills the tank with a starting population on dry ground, using the "spawn" random stream.</summary>
        public void SpawnStartingPopulation(int frogs, int insects)
        {
            var rng = Random.Get("spawn");
            for (int i = 0; i < frogs; i++) SpawnOnDryGround(AgentKind.Frog, rng);
            for (int i = 0; i < insects; i++) SpawnOnDryGround(AgentKind.Insect, rng);
        }

        private int SpawnOnDryGround(AgentKind kind, SeededRandom rng)
        {
            float x = 0f, z = 0f;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                x = rng.Range(-HalfSizeX, HalfSizeX);
                z = rng.Range(-HalfSizeZ, HalfSizeZ);
                if (!Terrain.IsWater(x, z)) break;
            }
            return Agents.Add(kind, x, Terrain.HeightAt(x, z), z);
        }
    }
}

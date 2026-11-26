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
            world.Devices.Add(DeviceKind.Feeder, 0f, -t.HalfSizeZ * 0.55f);
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
        /// <summary>Current warnings for the player, grouped by cause. Worked out by AlertSystem; not saved.</summary>
        public AlertList Alerts { get; } = new AlertList();

        /// <summary>Hard cap on agents so a population boom can never stall the game.</summary>
        public const int MaxAgents = 1500;

        /// <summary>Half the tank's width and depth in metres; the tank is centred on the origin.</summary>
        public float HalfSizeX { get; set; }
        public float HalfSizeZ { get; set; }

        /// <summary>Simulated seconds since the world was created.</summary>
        public double SimTime { get; internal set; }

        /// <summary>Fills the tank with a starting population on dry ground, using the "spawn" random stream.</summary>
        public void SpawnStartingPopulation(int frogs, int insects)
        {
            var rng = Random.Get("spawn");
            for (int i = 0; i < frogs; i++)
            {
                FindDryGround(rng, -HalfSizeX, HalfSizeX, -HalfSizeZ, HalfSizeZ, out float x, out float z);
                int idx = AddFrog(x, z);
                Agents.Age[idx] = rng.Range(300f, 1800f);
                Agents.Hunger[idx] = rng.Range(0f, 0.3f);
                Agents.Thirst[idx] = rng.Range(0f, 0.3f);
                Agents.Fatigue[idx] = rng.Range(0f, 0.3f);
            }
            for (int i = 0; i < insects; i++)
            {
                FindDryGround(rng, -HalfSizeX, HalfSizeX, -HalfSizeZ, HalfSizeZ, out float x, out float z);
                int idx = AddInsect(x, z, rng);
                Agents.Age[idx] = rng.Range(0f, Agents.Lifespan[idx] * 0.7f);
            }
        }

        /// <summary>Picks a random spot in the rectangle that is not under water (gives up after 20 tries).</summary>
        public void FindDryGround(SeededRandom rng, float minX, float maxX, float minZ, float maxZ, out float x, out float z)
        {
            minX = TerrainGrid.Clamp(minX, -HalfSizeX, HalfSizeX); maxX = TerrainGrid.Clamp(maxX, -HalfSizeX, HalfSizeX);
            minZ = TerrainGrid.Clamp(minZ, -HalfSizeZ, HalfSizeZ); maxZ = TerrainGrid.Clamp(maxZ, -HalfSizeZ, HalfSizeZ);
            x = 0.5f * (minX + maxX); z = 0.5f * (minZ + maxZ);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                x = rng.Range(minX, maxX);
                z = rng.Range(minZ, maxZ);
                if (!Terrain.IsWater(x, z)) return;
            }
        }

        /// <summary>Adds an adult frog standing on the surface at (x, z).</summary>
        public int AddFrog(float x, float z)
        {
            int i = Agents.Add(AgentKind.Frog, x, MovementSystem.SurfaceHeight(Terrain, x, z), z);
            Agents.Age[i] = 600f;
            return i;
        }

        /// <summary>Adds a newborn cricket at (x, z) with a random lifespan.</summary>
        public int AddInsect(float x, float z, SeededRandom rng)
        {
            var sp = SpeciesCatalog.Insect;
            int i = Agents.Add(AgentKind.Insect, x, MovementSystem.SurfaceHeight(Terrain, x, z), z);
            Agents.Lifespan[i] = rng.Range(sp.MinLifespan, sp.MaxLifespan);
            return i;
        }
    }
}

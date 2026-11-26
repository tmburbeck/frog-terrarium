namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// All simulation state in one place: agents, tank bounds, random streams and
    /// the simulated clock. Everything in here is what a save file must capture.
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
        }

        public ulong Seed { get; }
        public RandomService Random { get; }
        public AgentStore Agents { get; }

        /// <summary>Half the tank's width and depth in metres; the tank is centred on the origin.</summary>
        public float HalfSizeX { get; set; }
        public float HalfSizeZ { get; set; }

        /// <summary>Simulated seconds since the world was created.</summary>
        public double SimTime { get; internal set; }

        /// <summary>Fills the tank with a starting population using the "spawn" random stream.</summary>
        public void SpawnStartingPopulation(int frogs, int insects)
        {
            var rng = Random.Get("spawn");
            for (int i = 0; i < frogs; i++)
                Agents.Add(AgentKind.Frog, rng.Range(-HalfSizeX, HalfSizeX), 0f, rng.Range(-HalfSizeZ, HalfSizeZ));
            for (int i = 0; i < insects; i++)
                Agents.Add(AgentKind.Insect, rng.Range(-HalfSizeX, HalfSizeX), 0f, rng.Range(-HalfSizeZ, HalfSizeZ));
        }
    }
}

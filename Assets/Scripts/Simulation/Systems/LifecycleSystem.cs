namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Ageing, death of old age and breeding, once per second.
    ///
    /// Crickets live 8 to 12 minutes. Adults lay young on soil or moss when fewer than five other
    /// crickets are within a metre and the tank is below its carrying capacity (0.35 crickets per
    /// square metre of dry ground). So a few crickets slowly become a colony, and a colony stops
    /// growing on its own instead of exploding.
    /// </summary>
    public sealed class LifecycleSystem : ISimSystem
    {
        public const int Rate = 1;

        public string Name => "Lifecycle";
        public int TicksPerSecond => Rate;

        public int Births { get; private set; }
        public int Deaths { get; private set; }

        private readonly SpatialHash _hash = new SpatialHash();

        /// <summary>How many crickets the tank can support right now.</summary>
        public static int InsectCapacity(World world)
        {
            var env = world.Environment;
            env.RefreshFromTerrain(world.Terrain);
            float dry = 0f;
            for (int i = 0; i < env.Water.Length; i++) dry += 1f - env.Water[i];
            float area = dry * env.CellSize * env.CellSize;
            return (int)(area * SpeciesCatalog.Insect.CapacityPerSquareMetre);
        }

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.Insect;
            var rng = world.Random.Get(Name);

            for (int i = a.Count - 1; i >= 0; i--)
            {
                a.Age[i] += dt;
                if (a.Lifespan[i] > 0f && a.Age[i] >= a.Lifespan[i])
                {
                    a.RemoveAt(i);
                    Deaths++;
                }
            }

            int insects = a.CountOf(AgentKind.Insect);
            int capacity = InsectCapacity(world);
            if (insects >= capacity || a.Count >= World.MaxAgents) return;

            _hash.Build(a, world.HalfSizeX, world.HalfSizeZ);
            int count = a.Count;
            for (int i = 0; i < count && insects < capacity && a.Count < World.MaxAgents; i++)
            {
                if (a.Kind[i] != AgentKind.Insect || a.Age[i] < sp.AdultAge) continue;
                if (!rng.Chance(sp.BreedChancePerSecond * dt)) continue;
                var ground = world.Terrain.LayerAt(a.PosX[i], a.PosZ[i]);
                if (ground != GroundLayer.Soil && ground != GroundLayer.Moss) continue;
                if (_hash.CountWithin(a, a.PosX[i], a.PosZ[i], 1f, AgentKind.Insect, i) >= sp.CrowdedNeighbours) continue;

                float px = a.PosX[i], pz = a.PosZ[i];
                world.FindDryGround(rng, px - 0.4f, px + 0.4f, pz - 0.4f, pz + 0.4f, out float x, out float z);
                world.AddInsect(x, z, rng);
                insects++;
                Births++;
            }
        }
    }

    /// <summary>
    /// Runs the feeders twice per second: each releases a few live crickets on its timer while
    /// it has supply left. The player refills it from its inspector.
    /// </summary>
    public sealed class FeederSystem : ISimSystem
    {
        public const int Rate = 2;

        public string Name => "Feeders";
        public int TicksPerSecond => Rate;

        public int Released { get; private set; }

        public void Tick(World world, float dt)
        {
            SeededRandom rng = null;
            foreach (var d in world.Devices.All)
            {
                if (d.Kind != DeviceKind.Feeder) continue;
                d.Active = d.Enabled && d.Supply >= 1f;
                if (!d.Active) continue;
                d.Timer -= dt;
                if (d.Timer > 0f) continue;
                d.Timer += d.Setting;
                if (d.Timer <= 0f) d.Timer = d.Setting;

                if (rng == null) rng = world.Random.Get(Name);
                int n = (int)System.Math.Min(d.Setting2, d.Supply);
                for (int k = 0; k < n && world.Agents.Count < World.MaxAgents; k++)
                {
                    world.FindDryGround(rng, d.X - 0.6f, d.X + 0.6f, d.Z - 0.6f, d.Z + 0.6f, out float x, out float z);
                    int i = world.AddInsect(x, z, rng);
                    world.Agents.Age[i] = SpeciesCatalog.Insect.AdultAge; // feeder crickets come grown up
                    d.Supply -= 1f;
                    Released++;
                }
                d.Active = d.Supply >= 1f;
            }
        }
    }
}

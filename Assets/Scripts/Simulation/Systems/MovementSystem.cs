using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Moves every agent along the velocity its brain chose, 30 times per second, so motion stays
    /// smooth even though decisions are made only 10 times per second. Agents walk on the terrain,
    /// bounce off the tank walls, and frogs swim at half speed. Insects turn back at the water's edge,
    /// and tadpoles turn back before leaving the water.
    /// </summary>
    public sealed class MovementSystem : ISimSystem
    {
        public const int Rate = 30;

        public string Name => "Movement";
        public int TicksPerSecond => Rate;

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            var terrain = world.Terrain;
            for (int i = 0; i < a.Count; i++)
            {
                float slow = a.Kind[i] == AgentKind.Frog && terrain.IsWater(a.PosX[i], a.PosZ[i]) ? 0.5f : 1f;
                float x = a.PosX[i] + a.VelX[i] * slow * dt;
                float z = a.PosZ[i] + a.VelZ[i] * slow * dt;
                if (x < -world.HalfSizeX || x > world.HalfSizeX) { a.VelX[i] = -a.VelX[i]; x = Clamp(x, -world.HalfSizeX, world.HalfSizeX); }
                if (z < -world.HalfSizeZ || z > world.HalfSizeZ) { a.VelZ[i] = -a.VelZ[i]; z = Clamp(z, -world.HalfSizeZ, world.HalfSizeZ); }
                bool wasWet = terrain.IsWater(a.PosX[i], a.PosZ[i]), nowWet = terrain.IsWater(x, z);
                bool turnBack = a.Kind[i] == AgentKind.Insect ? nowWet && !wasWet  // crickets stay out of the water
                              : a.IsAquaticYoung(i) && wasWet && !nowWet;          // tadpoles stay in it
                if (turnBack)
                {
                    // Turn back at the shore.
                    a.VelX[i] = -a.VelX[i];
                    a.VelZ[i] = -a.VelZ[i];
                    x = a.PosX[i];
                    z = a.PosZ[i];
                }
                a.PosX[i] = x;
                a.PosZ[i] = z;
                a.PosY[i] = SurfaceHeight(terrain, x, z);
            }
        }

        /// <summary>Where an agent stands: on the ground, or floating on the water surface.</summary>
        public static float SurfaceHeight(TerrainGrid terrain, float x, float z)
        {
            float ground = terrain.HeightAt(x, z);
            return ground < terrain.WaterLevel ? terrain.WaterLevel : ground;
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}

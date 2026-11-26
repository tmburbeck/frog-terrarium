using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Placeholder wandering, ticking 30 times per second. Agents drift and turn at
    /// random, bounce off the tank walls, and walk on the terrain. Insects turn back at
    /// the water's edge; frogs can swim, at half speed. Uses its own seeded random
    /// stream, so the same seed always produces the same paths.
    /// </summary>
    public sealed class MovementSystem : ISimSystem
    {
        public const int Rate = 30;

        public string Name => "Movement";
        public int TicksPerSecond => Rate;

        public float FrogSpeed = 0.6f;
        public float InsectSpeed = 1.2f;
        /// <summary>Chance per second that an agent picks a new direction.</summary>
        public float TurnChancePerSecond = 0.5f;

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            var rng = world.Random.Get(Name);
            var terrain = world.Terrain;
            float turnChance = TurnChancePerSecond * dt;
            for (int i = 0; i < a.Count; i++)
            {
                bool stopped = a.VelX[i] == 0f && a.VelZ[i] == 0f;
                if (stopped || rng.Chance(turnChance))
                {
                    float speed = a.Kind[i] == AgentKind.Frog ? FrogSpeed : InsectSpeed;
                    float angle = rng.Range(0f, 2f * (float)Math.PI);
                    a.VelX[i] = (float)Math.Cos(angle) * speed;
                    a.VelZ[i] = (float)Math.Sin(angle) * speed;
                }

                float slow = a.Kind[i] == AgentKind.Frog && terrain.IsWater(a.PosX[i], a.PosZ[i]) ? 0.5f : 1f;
                float x = a.PosX[i] + a.VelX[i] * slow * dt;
                float z = a.PosZ[i] + a.VelZ[i] * slow * dt;
                if (x < -world.HalfSizeX || x > world.HalfSizeX) { a.VelX[i] = -a.VelX[i]; x = Clamp(x, -world.HalfSizeX, world.HalfSizeX); }
                if (z < -world.HalfSizeZ || z > world.HalfSizeZ) { a.VelZ[i] = -a.VelZ[i]; z = Clamp(z, -world.HalfSizeZ, world.HalfSizeZ); }
                if (a.Kind[i] == AgentKind.Insect && terrain.IsWater(x, z) && !terrain.IsWater(a.PosX[i], a.PosZ[i]))
                {
                    // Turn back at the shore instead of walking in.
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

using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Placeholder wandering, ticking 30 times per second. Agents drift and turn at
    /// random and bounce off the tank walls. Uses its own seeded random stream, so
    /// the same seed always produces the same paths.
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

                float x = a.PosX[i] + a.VelX[i] * dt;
                float z = a.PosZ[i] + a.VelZ[i] * dt;
                if (x < -world.HalfSizeX || x > world.HalfSizeX) { a.VelX[i] = -a.VelX[i]; x = Clamp(x, -world.HalfSizeX, world.HalfSizeX); }
                if (z < -world.HalfSizeZ || z > world.HalfSizeZ) { a.VelZ[i] = -a.VelZ[i]; z = Clamp(z, -world.HalfSizeZ, world.HalfSizeZ); }
                a.PosX[i] = x;
                a.PosZ[i] = z;
            }
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}

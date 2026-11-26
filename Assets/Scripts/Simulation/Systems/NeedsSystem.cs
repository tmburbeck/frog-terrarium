namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Frogs' needs, 10 times per second. Hunger, thirst and tiredness rise over time. Sleeping
    /// recovers tiredness (and slows hunger and thirst); sitting in water quenches thirst, because
    /// frogs drink through their skin. Comfort follows the temperature and humidity where the frog
    /// sits, with a short lag so a brief cold draught does not instantly make it miserable.
    /// Insects in Phase 1 have no needs; their lives are handled by LifecycleSystem.
    /// </summary>
    public sealed class NeedsSystem : ISimSystem
    {
        public const int Rate = 10;
        /// <summary>How quickly comfort follows the surroundings (fraction per second).</summary>
        public const float ComfortResponse = 0.35f;

        public string Name => "Needs";
        public int TicksPerSecond => Rate;

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.Frog;
            var env = world.Environment;
            var terrain = world.Terrain;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) continue;
                bool asleep = a.Goal[i] == Goal.Sleep;
                float slow = asleep ? 0.5f : 1f;
                a.Hunger[i] = Clamp01(a.Hunger[i] + sp.HungerPerSecond * slow * dt);
                a.Thirst[i] = Clamp01(a.Thirst[i] + sp.ThirstPerSecond * slow * dt);
                if (terrain.IsWater(a.PosX[i], a.PosZ[i])) a.Thirst[i] = Clamp01(a.Thirst[i] - sp.DrinkPerSecond * dt);
                a.Fatigue[i] = Clamp01(asleep ? a.Fatigue[i] - sp.SleepRecoveryPerSecond * dt : a.Fatigue[i] + sp.FatiguePerSecond * dt);

                float target = sp.ComfortFor(env.TemperatureAt(a.PosX[i], a.PosZ[i]), env.HumidityAt(a.PosX[i], a.PosZ[i]));
                float k = ComfortResponse * dt;
                a.Comfort[i] = Clamp01(a.Comfort[i] + (target - a.Comfort[i]) * (k > 1f ? 1f : k));
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

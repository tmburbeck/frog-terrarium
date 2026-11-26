namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Frogs' needs, 10 times per second. Hunger, thirst and tiredness rise over time (more slowly
    /// for small young frogs). Sleeping recovers tiredness (and slows hunger and thirst); sitting in
    /// water quenches thirst, because frogs drink through their skin. Comfort follows the
    /// temperature and humidity where the frog sits, judged by its own species and genes, and drops
    /// when it is crowded. Comfort changes with a short lag, so a brief cold draught does not
    /// instantly make a frog miserable.
    ///
    /// Stress builds while a frog is uncomfortable, starving or parched, and fades again once it is
    /// comfortable. Bold frogs stress more slowly than shy ones. A frog whose stress reaches 1 tries
    /// to escape (see BrainSystem), so there is always a warning period first.
    ///
    /// Eggs and tadpoles have no needs: they live in the water and graze.
    /// </summary>
    public sealed class NeedsSystem : ISimSystem
    {
        public const int Rate = 10;
        /// <summary>How quickly comfort follows the surroundings (fraction per second).</summary>
        public const float ComfortResponse = 0.35f;
        /// <summary>Comfort drops by this much for each other frog within <see cref="CrowdRadius"/> beyond the first two.</summary>
        public const float CrowdPenalty = 0.12f;
        public const float CrowdRadius = 0.7f;
        /// <summary>Below this comfort, stress builds.</summary>
        public const float StressBelowComfort = 0.45f;
        /// <summary>Above this comfort (and not starving or parched), stress fades.</summary>
        public const float CalmAboveComfort = 0.6f;
        /// <summary>Stress gained per second at the worst distress, for an average frog. Three minutes from calm to breaking point.</summary>
        public const float StressPerSecond = 1f / 180f;
        public const float CalmPerSecond = 1f / 90f;
        /// <summary>Stress at which the player is warned that a frog may escape.</summary>
        public const float WarnStress = 0.5f;

        public string Name => "Needs";
        public int TicksPerSecond => Rate;

        private readonly SpatialHash _hash = new SpatialHash();

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            var env = world.Environment;
            var terrain = world.Terrain;
            _hash.Build(a, world.HalfSizeX, world.HalfSizeZ);
            for (int i = 0; i < a.Count; i++)
            {
                if (!a.IsLandFrog(i)) continue;
                var sp = SpeciesCatalog.FrogOf(a.Species[i]);
                bool asleep = a.Goal[i] == Goal.Sleep;
                float slow = (asleep ? 0.5f : 1f) * AppetiteFor(a.Stage[i]);
                a.Hunger[i] = Clamp01(a.Hunger[i] + sp.HungerPerSecond * slow * dt);
                a.Thirst[i] = Clamp01(a.Thirst[i] + sp.ThirstPerSecond * slow * dt);
                if (terrain.IsWater(a.PosX[i], a.PosZ[i])) a.Thirst[i] = Clamp01(a.Thirst[i] - sp.DrinkPerSecond * dt);
                a.Fatigue[i] = Clamp01(asleep ? a.Fatigue[i] - sp.SleepRecoveryPerSecond * dt : a.Fatigue[i] + sp.FatiguePerSecond * dt);

                float target = sp.ComfortFor(env.TemperatureAt(a.PosX[i], a.PosZ[i]), env.HumidityAt(a.PosX[i], a.PosZ[i]), a.Genes[i]);
                int crowd = _hash.CountWithin(a, a.PosX[i], a.PosZ[i], CrowdRadius, AgentKind.Frog, i, SpatialHash.LandFrogs);
                if (crowd > 2) target -= CrowdPenalty * (crowd - 2);
                if (target < 0f) target = 0f;
                float k = ComfortResponse * dt;
                a.Comfort[i] = Clamp01(a.Comfort[i] + (target - a.Comfort[i]) * (k > 1f ? 1f : k));

                a.Stress[i] = Clamp01(a.Stress[i] + StressChange(a, i) * dt);
            }
        }

        /// <summary>How fast this frog's stress is changing, per second (positive = rising).</summary>
        public static float StressChange(AgentStore a, int i)
        {
            float distress = 0f;
            if (a.Comfort[i] < StressBelowComfort) distress += (StressBelowComfort - a.Comfort[i]) * 2f;
            if (a.Hunger[i] > 0.85f || a.Thirst[i] > 0.85f) distress += 0.5f;
            if (distress > 0f)
            {
                if (distress > 1f) distress = 1f;
                // Shy frogs (boldness 0) stress 1.5 times as fast as average; bold ones (1) half as fast.
                float temperament = 1.5f - a.Genes[i].Boldness;
                return distress * StressPerSecond * temperament;
            }
            return a.Comfort[i] > CalmAboveComfort ? -CalmPerSecond : 0f;
        }

        /// <summary>Small frogs eat and drink less.</summary>
        public static float AppetiteFor(LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.Froglet: return 0.6f;
                case LifeStage.Juvenile: return 0.8f;
                default: return 1f;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

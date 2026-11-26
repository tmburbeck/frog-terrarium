namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Placeholder needs model, ticking 10 times per second. Needs rise slowly
    /// toward 1. Phase 1 replaces this with the real needs model and goal picker;
    /// it is here now so the tick loop has realistic work to time.
    /// </summary>
    public sealed class NeedsSystem : ISimSystem
    {
        public const int Rate = 10;

        public string Name => "Needs";
        public int TicksPerSecond => Rate;

        /// <summary>Per-second rise for frogs; insects use half these values.</summary>
        public float HungerPerSecond = 1f / 300f;
        public float ThirstPerSecond = 1f / 240f;
        public float FatiguePerSecond = 1f / 600f;

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            for (int i = 0; i < a.Count; i++)
            {
                float k = a.Kind[i] == AgentKind.Frog ? 1f : 0.5f;
                a.Hunger[i] = Clamp01(a.Hunger[i] + HungerPerSecond * k * dt);
                a.Thirst[i] = Clamp01(a.Thirst[i] + ThirstPerSecond * k * dt);
                a.Fatigue[i] = Clamp01(a.Fatigue[i] + FatiguePerSecond * k * dt);
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}

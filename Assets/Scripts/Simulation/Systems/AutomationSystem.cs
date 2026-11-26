namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// The automation tiers and the smart controller, once a second.
    ///
    /// * The tank gets dirtier while frogs live in it, and is cleaned by hand, by the cleaning
    ///   service (when it gets quite dirty) or by the smart controller (sooner, so it stays nicer).
    /// * Feeders in Sensor or Smart mode refill themselves when they run low, paying for the crickets.
    /// * With a smart controller switched on, every device in Smart mode gets its target from the
    ///   frogs near it: a heater's thermostat and a mister's humidity are set to the middle of what
    ///   those frogs like (their species and genes), and a feeder keeps more crickets when they are
    ///   hungry. So two corners with different frogs can each get their own climate.
    ///   Without a controller, Smart devices keep their last targets and work like Sensor ones.
    /// </summary>
    public sealed class AutomationSystem : ISimSystem
    {
        public string Name => "Automation";
        public int TicksPerSecond => 1;

        /// <summary>Dirt each land frog adds per second (10 frogs make a clean tank dirty in about half an hour).</summary>
        public const float DirtPerFrog = 0.003f;

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            int frogs = 0;
            for (int i = 0; i < a.Count; i++) if (a.IsLandFrog(i)) frogs++;
            world.Dirt = world.Has(SandboxRules.NoDirt) ? 0f : System.Math.Min(100f, world.Dirt + DirtPerFrog * frogs * dt);

            bool controller = world.Devices.HasController;
            float cleanAt = world.Cleaning == DeviceMode.Smart && controller ? World.SmartCleansAt
                          : world.Cleaning != DeviceMode.Basic ? World.ServiceCleansAt : float.MaxValue;
            if (world.Dirt >= cleanAt) world.CleanTank(out _);

            foreach (var d in world.Devices.All)
            {
                if (d.Kind == DeviceKind.Controller) d.Active = d.Enabled;
                if (d.Kind == DeviceKind.Feeder && d.Mode != DeviceMode.Basic && d.Enabled)
                {
                    if (d.Supply < DeviceStore.FeederCapacity * DeviceStore.FeederLowShare)
                    {
                        d.RefillFailed = !world.Economy.Spend(Economy.FeederRefillCost, "a feeder refill", out _);
                        if (!d.RefillFailed) d.Supply = DeviceStore.FeederCapacity;
                    }
                    else d.RefillFailed = false;
                }
                if (d.Mode == DeviceMode.Smart && controller && d.Enabled) SuitToFrogs(world, d);
            }
        }

        /// <summary>Sets a Smart device's target from the frogs near it (or from all of them if none are near).</summary>
        public static void SuitToFrogs(World world, Device d)
        {
            if (!Ideal(world, d.X, d.Z, DeviceStore.SmartRadius, out float t, out float h, out float hunger)
                && !Ideal(world, d.X, d.Z, float.MaxValue, out t, out h, out hunger))
                return; // no frogs: keep the last targets
            switch (d.Kind)
            {
                case DeviceKind.Heater: d.Setting = t; break;
                case DeviceKind.Mister: d.Target = h; break;
                case DeviceKind.Feeder: d.Target = 1.5f + 2.5f * hunger; break;
            }
            DeviceStore.ClampSettings(d);
        }

        /// <summary>
        /// The middle of what the land frogs within radius of (x, z) like: temperature, humidity and
        /// their average hunger. False if there are no such frogs.
        /// </summary>
        public static bool Ideal(World world, float x, float z, float radius, out float temperature, out float humidity, out float hunger)
        {
            var a = world.Agents;
            double t = 0, h = 0, food = 0;
            int n = 0;
            float r2 = radius == float.MaxValue ? float.MaxValue : radius * radius;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a.IsLandFrog(i)) continue;
                float dx = a.PosX[i] - x, dz = a.PosZ[i] - z;
                if (dx * dx + dz * dz > r2) continue;
                var sp = SpeciesCatalog.FrogOf(a, i);
                var g = a.Genes[i];
                t += 0.5f * (sp.MinTemperature + sp.MaxTemperature) + g.HeatShift;
                h += 0.5f * (sp.MinHumidity + sp.MaxHumidity) + g.DampShift;
                food += a.Hunger[i];
                n++;
            }
            temperature = n > 0 ? (float)(t / n) : 0f;
            humidity = n > 0 ? (float)(h / n) : 0f;
            hunger = n > 0 ? (float)(food / n) : 0f;
            return n > 0;
        }
    }
}

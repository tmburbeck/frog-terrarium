using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    public enum AlertLevel : byte
    {
        Info,
        Warning,
        Critical,
    }

    /// <summary>One warning for the player, about one cause, however many frogs it affects.</summary>
    public sealed class Alert
    {
        /// <summary>Stable key for the cause, such as "cold" or "feeder-3-low".</summary>
        public string Cause;
        public AlertLevel Level;
        /// <summary>What is wrong, e.g. "3 frogs are too cold (below 22 °C)".</summary>
        public string Text;
        /// <summary>What the player can do about it.</summary>
        public string Fix;
        public int Count;
        /// <summary>Where it is happening (the first affected frog or the device), so the camera can go there.</summary>
        public float X, Z;
        /// <summary>Simulated time when this alert first appeared.</summary>
        public double Since;
    }

    /// <summary>The current alerts, most serious first.</summary>
    public sealed class AlertList
    {
        private readonly List<Alert> _alerts = new List<Alert>();
        public IReadOnlyList<Alert> All => _alerts;
        public int Count => _alerts.Count;

        public Alert Find(string cause) => _alerts.Find(a => a.Cause == cause);

        internal void Replace(List<Alert> fresh)
        {
            // Keep the original start time for alerts that are still active.
            foreach (var f in fresh)
            {
                var old = Find(f.Cause);
                if (old != null) f.Since = old.Since;
            }
            _alerts.Clear();
            _alerts.AddRange(fresh);
            _alerts.Sort((x, y) => y.Level != x.Level ? y.Level.CompareTo(x.Level) : y.Count.CompareTo(x.Count));
        }
    }

    /// <summary>
    /// Works out the player's alerts once per second, grouped by cause rather than one per frog
    /// ("3 frogs are too dry", not three separate warnings), each with a suggested fix.
    /// </summary>
    public sealed class AlertSystem : ISimSystem
    {
        public const int Rate = 1;
        /// <summary>Frogs below this comfort count as uncomfortable.</summary>
        public const float UncomfortableBelow = 0.5f;
        public const float StarvingAbove = 0.75f;
        public const float ParchedAbove = 0.75f;

        public string Name => "Alerts";
        public int TicksPerSecond => Rate;

        private readonly List<Alert> _fresh = new List<Alert>();

        public void Tick(World world, float dt) => Refresh(world);

        public void Refresh(World world)
        {
            _fresh.Clear();
            var a = world.Agents;
            var env = world.Environment;
            var sp = SpeciesCatalog.Frog;
            int frogs = 0, insects = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Insect) { insects++; continue; }
                frogs++;
                float x = a.PosX[i], z = a.PosZ[i];
                if (a.Comfort[i] < UncomfortableBelow)
                {
                    float t = env.TemperatureAt(x, z), h = env.HumidityAt(x, z);
                    float tooCold = sp.MinTemperature - t, tooHot = t - sp.MaxTemperature;
                    float tooDry = (sp.MinHumidity - h) * 0.25f, tooWet = (h - sp.MaxHumidity) * 0.25f; // 4 % humidity ~ 1 °C
                    float worst = Max(Max(tooCold, tooHot), Max(tooDry, tooWet));
                    if (worst == tooCold)
                        Add(world, "cold", AlertLevel.Warning, "{0} too cold (below " + sp.MinTemperature.ToString("0") + " °C)",
                            "Move a heater closer or raise its thermostat.", x, z);
                    else if (worst == tooHot)
                        Add(world, "hot", AlertLevel.Warning, "{0} too hot (above " + sp.MaxTemperature.ToString("0") + " °C)",
                            "Lower the heater's thermostat or move it away.", x, z);
                    else if (worst == tooDry)
                        Add(world, "dry", AlertLevel.Warning, "{0} too dry (humidity below " + sp.MinHumidity.ToString("0") + "%)",
                            "Add a mister, mist more often, paint moss or dig a pool nearby.", x, z);
                    else
                        Add(world, "wet", AlertLevel.Warning, "{0} too damp (humidity above " + sp.MaxHumidity.ToString("0") + "%)",
                            "Mist less often or paint some sand.", x, z);
                }
                if (a.Hunger[i] > StarvingAbove)
                    Add(world, "hungry", AlertLevel.Critical, "{0} starving", null, x, z);
                if (a.Thirst[i] > ParchedAbove)
                    Add(world, "thirsty", AlertLevel.Critical, "{0} parched", null, x, z);
            }

            var hungry = FindFresh("hungry");
            if (hungry != null)
                hungry.Fix = insects == 0 ? "There are no insects left. Add a feeder or some crickets." : "Insects are scarce. Add a feeder or release crickets more often.";
            var thirsty = FindFresh("thirsty");
            if (thirsty != null)
                thirsty.Fix = HasWater(world) ? "The water is too far away. Dig a pool closer to them." : "There is no water. Dig below the water line to make a pool.";
            if (frogs > 0 && insects == 0 && hungry == null)
                Add(world, "no-insects", AlertLevel.Warning, "No insects left in the tank", "Add a feeder or some crickets.", 0f, 0f, countsFrogs: false);

            foreach (var d in world.Devices.All)
            {
                if (d.Kind != DeviceKind.Feeder) continue;
                if (d.Supply < 1f)
                    Add(world, "feeder-" + d.Id + "-empty", AlertLevel.Warning, d.DisplayName + " is empty",
                        "Refill it from its inspector.", d.X, d.Z, countsFrogs: false);
                else if (d.Supply < DeviceStore.FeederCapacity * DeviceStore.FeederLowShare)
                    Add(world, "feeder-" + d.Id + "-low", AlertLevel.Info,
                        d.DisplayName + " is running low (" + ((int)d.Supply) + " crickets left)",
                        "Refill it from its inspector before it runs out.", d.X, d.Z, countsFrogs: false);
            }

            world.Alerts.Replace(new List<Alert>(_fresh));
        }

        private Alert FindFresh(string cause) => _fresh.Find(x => x.Cause == cause);

        private void Add(World world, string cause, AlertLevel level, string format, string fix, float x, float z, bool countsFrogs = true)
        {
            var existing = FindFresh(cause);
            if (existing != null)
            {
                existing.Count++;
                existing.Text = string.Format(format, existing.Count == 1 ? "1 frog is" : existing.Count + " frogs are");
                return;
            }
            _fresh.Add(new Alert
            {
                Cause = cause,
                Level = level,
                Count = 1,
                Text = countsFrogs ? string.Format(format, "1 frog is") : format,
                Fix = fix,
                X = x,
                Z = z,
                Since = world.SimTime,
            });
        }

        private static bool HasWater(World world)
        {
            var env = world.Environment;
            env.RefreshFromTerrain(world.Terrain);
            foreach (float w in env.Water) if (w > 0f) return true;
            return false;
        }

        private static float Max(float x, float y) => x > y ? x : y;
    }
}

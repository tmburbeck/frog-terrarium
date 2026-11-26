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
            int frogs = 0, insects = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Insect) { insects++; continue; }
                if (!a.IsLandFrog(i)) continue; // eggs and tadpoles live in the water and have no needs
                frogs++;
                var sp = SpeciesCatalog.FrogOf(a.Species[i]);
                var g = a.Genes[i];
                float x = a.PosX[i], z = a.PosZ[i];
                if (a.Comfort[i] < UncomfortableBelow)
                {
                    string plural = PluralName(sp);
                    switch (ComfortProblem(world, i))
                    {
                        case "cold":
                            Add(world, "cold-" + sp.Id, AlertLevel.Warning, "{0} too cold",
                                "Move a heater closer or raise its thermostat. " + plural + " like " + (sp.MinTemperature + g.HeatShift).ToString("0") + "–" + (sp.MaxTemperature + g.HeatShift).ToString("0") + " °C.", x, z);
                            break;
                        case "hot":
                            Add(world, "hot-" + sp.Id, AlertLevel.Warning, "{0} too hot",
                                "Lower the heater's thermostat or move it away. " + plural + " like " + (sp.MinTemperature + g.HeatShift).ToString("0") + "–" + (sp.MaxTemperature + g.HeatShift).ToString("0") + " °C.", x, z);
                            break;
                        case "dry":
                            Add(world, "dry-" + sp.Id, AlertLevel.Warning, "{0} too dry (humidity too low)",
                                "Add a mister, mist more often, paint moss or dig a pool nearby. " + plural + " like " + (sp.MinHumidity + g.DampShift).ToString("0") + "–" + (sp.MaxHumidity + g.DampShift).ToString("0") + "% humidity.", x, z);
                            break;
                        case "wet":
                            Add(world, "wet-" + sp.Id, AlertLevel.Warning, "{0} too damp (humidity too high)",
                                "Mist less often or paint some sand. " + plural + " like " + (sp.MinHumidity + g.DampShift).ToString("0") + "–" + (sp.MaxHumidity + g.DampShift).ToString("0") + "% humidity.", x, z);
                            break;
                        case "crowded":
                            Add(world, "crowded", AlertLevel.Warning, "{0} too crowded",
                                "Give them more room: a bigger tank, or rehome a few frogs.", x, z);
                            break;
                        case "lonely":
                            Add(world, "lonely-" + sp.Id, AlertLevel.Warning, "{0} lonely",
                                plural + " like company. Add another one, or let them breed.", x, z);
                            break;
                    }
                }
                if (a.Hunger[i] > StarvingAbove)
                    Add(world, "hungry", AlertLevel.Critical, "{0} starving", null, x, z);
                if (a.Thirst[i] > ParchedAbove)
                    Add(world, "thirsty", AlertLevel.Critical, "{0} parched", null, x, z);
                if (a.Goal[i] == Goal.Escape)
                    Add(world, "escaping", AlertLevel.Critical, "{0} trying to climb out of the tank!",
                        "Fix what is upsetting them right away (see the other warnings), or they will escape.", x, z);
                else if (a.Stress[i] >= NeedsSystem.WarnStress)
                    Add(world, "stressed", AlertLevel.Warning, "{0} stressed and may try to escape",
                        "Find out what is wrong (click a frog to see) and fix it soon.", x, z);
                if (a.MateId[i] != 0 && a.Gravid[i] > 60f)
                    Add(world, "no-pool", AlertLevel.Info, "{0} ready to lay eggs but cannot reach water",
                        "Dig a pool below the water line. Eggs and tadpoles need water.", x, z);
            }

            var hungry = FindFresh("hungry");
            if (hungry != null)
                hungry.Fix = insects == 0 ? "There are no insects left. Add a feeder or some crickets." : "Insects are scarce. Add a feeder or release crickets more often.";
            var thirsty = FindFresh("thirsty");
            if (thirsty != null)
                thirsty.Fix = HasWater(world) ? "The water is too far away. Dig a pool closer to them." : "There is no water. Dig below the water line to make a pool.";
            var noPool = FindFresh("no-pool");
            if (noPool != null && HasWater(world))
                noPool.Fix = "The pool is too far away or blocked. Dig one closer.";
            if (frogs > 0 && insects == 0 && hungry == null)
                Add(world, "no-insects", AlertLevel.Warning, "No insects left in the tank", "Add a feeder or some crickets.", 0f, 0f, countsFrogs: false);

            if (world.Escaped.Count > 0)
            {
                var last = world.Escaped[world.Escaped.Count - 1];
                string text = world.Escaped.Count == 1
                    ? SpeciesCatalog.FrogName(last.Id) + " escaped (" + last.Reason + ")"
                    : world.Escaped.Count + " frogs escaped (the last was " + last.Reason + ")";
                Add(world, EscapedCause, AlertLevel.Critical, text,
                    "Fix what drove them out, then press Bring back.", 0f, 0f, countsFrogs: false);
                FindFresh(EscapedCause).Count = world.Escaped.Count;
            }

            int allFrogs = world.FrogCount();
            if (world.BreedingEnabled && allFrogs >= world.FrogLimit && allFrogs > 0)
            {
                bool own = world.BreedingLimit > 0 && world.BreedingLimit < world.FrogCapacity;
                Add(world, TankFullCause, AlertLevel.Info,
                    own ? "Frog limit reached (" + world.FrogLimit + "): no more breeding" : "The tank is full (" + world.FrogLimit + " frogs): no more breeding",
                    "Open Frogs to rehome some, collect eggs, or change the limit.", 0f, 0f, countsFrogs: false);
            }

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

        /// <summary>The cause key of the alert that offers a Bring back button.</summary>
        public const string EscapedCause = "escaped";
        /// <summary>The cause key of the alert that says breeding has stopped because the tank is full.</summary>
        public const string TankFullCause = "tank-full";

        /// <summary>
        /// The main thing making this frog uncomfortable: "cold", "hot", "dry", "wet" or "crowded"
        /// (or "" if nothing is wrong with where it sits).
        /// </summary>
        public static string ComfortProblem(World world, int i)
        {
            var a = world.Agents;
            var env = world.Environment;
            var sp = SpeciesCatalog.FrogOf(a.Species[i]);
            var g = a.Genes[i];
            float x = a.PosX[i], z = a.PosZ[i];
            float t = env.TemperatureAt(x, z), h = env.HumidityAt(x, z);
            float tooCold = sp.MinTemperature + g.HeatShift - t, tooHot = t - sp.MaxTemperature - g.HeatShift;
            float tooDry = (sp.MinHumidity + g.DampShift - h) * 0.25f, tooWet = (h - sp.MaxHumidity - g.DampShift) * 0.25f; // 4 % humidity ~ 1 °C
            float worst = Max(Max(tooCold, tooHot), Max(tooDry, tooWet));
            if (worst <= 0f)
            {
                if (NeedsSystem.LonelinessCost(a.Social[i]) >= 0.1f) return "lonely";
                return sp.ComfortFor(t, h, g) > a.Comfort[i] + 0.2f || a.Comfort[i] < UncomfortableBelow ? "crowded" : "";
            }
            if (worst == tooCold) return "cold";
            if (worst == tooHot) return "hot";
            if (worst == tooDry) return "dry";
            return "wet";
        }

        /// <summary>The main reason a frog is unhappy, in plain words, such as "too cold" or "starving".</summary>
        public static string ProblemWords(World world, int i)
        {
            var a = world.Agents;
            if (a.Hunger[i] > 0.85f) return "starving";
            if (a.Thirst[i] > 0.85f) return "parched";
            switch (ComfortProblem(world, i))
            {
                case "cold": return "too cold";
                case "hot": return "too hot";
                case "dry": return "air too dry";
                case "wet": return "air too damp";
                case "crowded": return "too crowded";
                case "lonely": return "lonely";
                default: return "unhappy";
            }
        }

        private static string PluralName(FrogSpecies sp)
        {
            string n = sp.Name;
            n = n.EndsWith("frog") || n.EndsWith("toad") ? n + "s" : n;
            return char.ToUpperInvariant(n[0]) + n.Substring(1);
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

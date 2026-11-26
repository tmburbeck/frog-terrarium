using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>How one species is doing, for the tank health screen.</summary>
    public sealed class SpeciesHealth
    {
        public byte Species;
        public string Name;
        /// <summary>Land frogs (froglets and older) of this species.</summary>
        public int Count;
        /// <summary>What this species likes, on average over its frogs' genes.</summary>
        public float LikesMinTemperature, LikesMaxTemperature, LikesMinHumidity, LikesMaxHumidity;
        /// <summary>The coldest and warmest, driest and dampest spots these frogs are sitting in.</summary>
        public float MinTemperature, MaxTemperature, MinHumidity, MaxHumidity;
        /// <summary>Average comfort, 0 to 1.</summary>
        public float Comfort;
        /// <summary>How many are uncomfortable right now.</summary>
        public int Unhappy;
    }

    /// <summary>A summary of how the tank is doing, worked out on request (not saved).</summary>
    public sealed class TankHealthReport
    {
        /// <summary>0 to 100.</summary>
        public int Score;
        /// <summary>"Thriving", "Good", "Okay", "Poor", "Critical" or "No frogs yet".</summary>
        public string Word;
        public int Frogs, Young, Capacity, Insects, Hungry, Thirsty, Stressed, Escaped;
        /// <summary>Averages over land frogs, 0 to 1.</summary>
        public float Comfort, Stress;
        /// <summary>100 = spotless.</summary>
        public float Cleanliness;
        /// <summary>The coldest and warmest, driest and dampest spots in the whole tank.</summary>
        public float MinTemperature, MaxTemperature, MinHumidity, MaxHumidity;
        public bool HasWater;
        public int Devices, DevicesOn, DevicesAutomated;
        public bool Controller;
        public readonly List<SpeciesHealth> Species = new List<SpeciesHealth>();
    }

    /// <summary>
    /// Works out the tank health summary: one score from comfort, stress, food, water and
    /// cleanliness, and how each species is doing against what it likes.
    /// </summary>
    public static class TankHealth
    {
        /// <summary>A frog is counted as hungry or thirsty above this.</summary>
        public const float NeedyAbove = 0.6f;

        public static TankHealthReport Compute(World world)
        {
            var r = new TankHealthReport();
            var a = world.Agents;
            var env = world.Environment;
            var bySpecies = new Dictionary<byte, SpeciesHealth>();
            double comfort = 0, stress = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Insect) { r.Insects++; continue; }
                if (!a.IsLandFrog(i)) { r.Young++; continue; }
                r.Frogs++;
                comfort += a.Comfort[i];
                stress += a.Stress[i];
                if (a.Hunger[i] > NeedyAbove) r.Hungry++;
                if (a.Thirst[i] > NeedyAbove) r.Thirsty++;
                if (a.Stress[i] >= NeedsSystem.WarnStress) r.Stressed++;

                var sp = SpeciesCatalog.FrogOf(a, i);
                var g = a.Genes[i];
                float t = env.TemperatureAt(a.PosX[i], a.PosZ[i]), h = env.HumidityAt(a.PosX[i], a.PosZ[i]);
                if (!bySpecies.TryGetValue(sp.Id, out var s))
                {
                    s = new SpeciesHealth
                    {
                        Species = sp.Id, Name = sp.Name,
                        MinTemperature = t, MaxTemperature = t, MinHumidity = h, MaxHumidity = h,
                    };
                    bySpecies[sp.Id] = s;
                    r.Species.Add(s);
                }
                s.Count++;
                s.LikesMinTemperature += sp.MinTemperature + g.HeatShift;
                s.LikesMaxTemperature += sp.MaxTemperature + g.HeatShift;
                s.LikesMinHumidity += sp.MinHumidity + g.DampShift;
                s.LikesMaxHumidity += sp.MaxHumidity + g.DampShift;
                if (t < s.MinTemperature) s.MinTemperature = t;
                if (t > s.MaxTemperature) s.MaxTemperature = t;
                if (h < s.MinHumidity) s.MinHumidity = h;
                if (h > s.MaxHumidity) s.MaxHumidity = h;
                s.Comfort += a.Comfort[i];
                if (a.Comfort[i] < AlertSystem.UncomfortableBelow) s.Unhappy++;
            }
            foreach (var s in r.Species)
            {
                s.LikesMinTemperature /= s.Count;
                s.LikesMaxTemperature /= s.Count;
                s.LikesMinHumidity /= s.Count;
                s.LikesMaxHumidity /= s.Count;
                s.Comfort /= s.Count;
            }
            r.Species.Sort((x, y) => y.Count.CompareTo(x.Count));

            r.Capacity = world.FrogCapacity;
            r.Escaped = world.Escaped.Count;
            r.Comfort = r.Frogs > 0 ? (float)(comfort / r.Frogs) : 0f;
            r.Stress = r.Frogs > 0 ? (float)(stress / r.Frogs) : 0f;
            r.Cleanliness = 100f - world.Dirt;

            env.RefreshFromTerrain(world.Terrain);
            r.MinTemperature = float.MaxValue; r.MaxTemperature = float.MinValue;
            r.MinHumidity = float.MaxValue; r.MaxHumidity = float.MinValue;
            for (int i = 0; i < env.Temperature.Length; i++)
            {
                if (env.Water[i] > 0f) r.HasWater = true;
                float t = env.Temperature[i], h = env.Humidity[i];
                if (t < r.MinTemperature) r.MinTemperature = t;
                if (t > r.MaxTemperature) r.MaxTemperature = t;
                if (h < r.MinHumidity) r.MinHumidity = h;
                if (h > r.MaxHumidity) r.MaxHumidity = h;
            }

            foreach (var d in world.Devices.All)
            {
                r.Devices++;
                if (d.Enabled) r.DevicesOn++;
                if (d.Mode != DeviceMode.Basic) r.DevicesAutomated++;
            }
            r.Controller = world.Devices.HasController;

            if (r.Frogs == 0)
            {
                r.Score = 0;
                r.Word = "No frogs yet";
                return r;
            }
            float fed = 1f - (float)r.Hungry / r.Frogs, watered = 1f - (float)r.Thirsty / r.Frogs;
            float score = 100f * (0.4f * r.Comfort + 0.25f * (1f - r.Stress) + 0.1f * fed + 0.1f * watered + 0.15f * r.Cleanliness / 100f);
            foreach (var al in world.Alerts.All) if (al.Level == AlertLevel.Critical) score -= 10f;
            r.Score = (int)System.Math.Round(System.Math.Max(0f, System.Math.Min(100f, score)));
            r.Word = WordFor(r.Score);
            return r;
        }

        public static string WordFor(int score) =>
            score >= 85 ? "Thriving" : score >= 70 ? "Good" : score >= 50 ? "Okay" : score >= 30 ? "Poor" : "Critical";
    }
}

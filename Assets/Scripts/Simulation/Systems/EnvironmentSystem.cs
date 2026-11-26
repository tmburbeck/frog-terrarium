namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Updates temperature and humidity five times per second.
    ///
    /// Each cell drifts toward the room's temperature and humidity. Open water pulls humidity up
    /// and cools slightly as it evaporates; moss holds moisture, sand and rock lose it quickly.
    /// The heater warms (up to its rated maximum) and dries the air; the mister sprays on a timer.
    /// Building parts work like weak devices: shady parts keep the ground under them up to 1.5 °C
    /// cooler than the room, and plants and moss pads add moisture.
    /// Finally heat and moisture spread to neighbouring cells, so devices affect an area that
    /// fades with distance.
    /// </summary>
    public sealed class EnvironmentSystem : ISimSystem
    {
        public const int Rate = 5;

        public string Name => "Environment";
        public int TicksPerSecond => Rate;

        /// <summary>How fast each cell drifts toward the room (fraction per second).</summary>
        public float TemperatureLoss = 0.008f;
        public float HumidityLoss = 0.02f;
        /// <summary>How fast heat and moisture spread, in square metres per second.</summary>
        public float Diffusion = 0.2f;

        private float[] _scratch;

        public void Tick(World world, float dt)
        {
            var env = world.Environment;
            env.RefreshFromTerrain(world.Terrain);
            UpdateDevices(world, dt);

            var T = env.Temperature;
            var H = env.Humidity;
            float ambT = env.AmbientTemperature, ambH = env.AmbientHumidity;
            for (int i = 0; i < T.Length; i++)
            {
                float water = env.Water[i];
                float hLoss = HumidityLoss * (1f - 0.5f * env.Moss[i] + 0.6f * env.Dry[i]);
                float dT = (ambT - T[i]) * TemperatureLoss + (ambT - 1.5f - T[i]) * 0.01f * water;
                float dH = (ambH - H[i]) * hLoss + (95f - H[i]) * 0.06f * water;
                T[i] += dT * dt;
                H[i] += dH * dt;
            }

            ApplyDeviceEffects(world, dt);
            ApplyPartEffects(world, dt);
            Diffuse(env, T, dt);
            Diffuse(env, H, dt);

            for (int i = 0; i < T.Length; i++)
            {
                T[i] = TerrainGrid.Clamp(T[i], EnvironmentField.MinTemperature, EnvironmentField.MaxTemperature);
                H[i] = TerrainGrid.Clamp(H[i], 0f, 100f);
            }
        }

        private static void UpdateDevices(World world, float dt)
        {
            var env = world.Environment;
            foreach (var d in world.Devices.All)
            {
                if (!d.Enabled) { d.Active = false; continue; }
                switch (d.Kind)
                {
                    case DeviceKind.Heater:
                    {
                        // Thermostat with a small dead band so it does not flicker on and off.
                        float here = env.Temperature[env.CellIndex(d.X, d.Z)];
                        if (here < d.Setting - 0.5f) d.Active = true;
                        else if (here > d.Setting + 0.5f) d.Active = false;
                        break;
                    }
                    case DeviceKind.Mister when d.Mode != DeviceMode.Basic:
                    {
                        // Humidity sensor: spray while the air around it is drier than the target.
                        float here = AverageHumidity(env, d.X, d.Z, DeviceStore.SensorRadius);
                        if (here < d.Target - DeviceStore.MisterBand) d.Active = true;
                        else if (here > d.Target + DeviceStore.MisterBand) d.Active = false;
                        d.Timer = 0f;
                        break;
                    }
                    case DeviceKind.Mister:
                    {
                        d.Timer -= dt;
                        if (d.Timer <= 0f)
                        {
                            d.Active = !d.Active;
                            d.Timer += d.Active ? d.Setting2 : d.Setting - d.Setting2;
                            if (d.Timer <= 0f) d.Timer = 0.5f;
                        }
                        break;
                    }
                }
            }
        }

        private static void ApplyDeviceEffects(World world, float dt)
        {
            var env = world.Environment;
            var T = env.Temperature;
            var H = env.Humidity;
            foreach (var d in world.Devices.All)
            {
                if (!d.Active || (d.Kind != DeviceKind.Heater && d.Kind != DeviceKind.Mister)) continue;
                float radius = d.Kind == DeviceKind.Heater ? DeviceStore.HeaterRadius : DeviceStore.MisterRadius;
                int c0x = (int)((d.X - radius + env.HalfSizeX) / env.CellSize), c1x = (int)((d.X + radius + env.HalfSizeX) / env.CellSize);
                int c0z = (int)((d.Z - radius + env.HalfSizeZ) / env.CellSize), c1z = (int)((d.Z + radius + env.HalfSizeZ) / env.CellSize);
                if (c0x < 0) c0x = 0;
                if (c0z < 0) c0z = 0;
                if (c1x >= env.CellsX) c1x = env.CellsX - 1;
                if (c1z >= env.CellsZ) c1z = env.CellsZ - 1;
                for (int cz = c0z; cz <= c1z; cz++)
                {
                    for (int cx = c0x; cx <= c1x; cx++)
                    {
                        float f = TerrainBrush.Falloff(env.CellCentreX(cx) - d.X, env.CellCentreZ(cz) - d.Z, radius);
                        if (f <= 0f) continue;
                        int i = cz * env.CellsX + cx;
                        if (d.Kind == DeviceKind.Heater)
                        {
                            if (T[i] < DeviceStore.HeaterMaxTemperature)
                            {
                                T[i] += DeviceStore.HeaterPower * f * dt;
                                if (T[i] > DeviceStore.HeaterMaxTemperature) T[i] = DeviceStore.HeaterMaxTemperature;
                            }
                            H[i] -= 0.25f * f * dt;
                        }
                        else
                        {
                            H[i] += DeviceStore.MisterPower * f * dt;
                            T[i] -= 0.04f * f * dt;
                        }
                    }
                }
            }
        }

        /// <summary>The average humidity of the cells within radius of (x, z).</summary>
        public static float AverageHumidity(EnvironmentField env, float x, float z, float radius)
        {
            int c0x = (int)((x - radius + env.HalfSizeX) / env.CellSize), c1x = (int)((x + radius + env.HalfSizeX) / env.CellSize);
            int c0z = (int)((z - radius + env.HalfSizeZ) / env.CellSize), c1z = (int)((z + radius + env.HalfSizeZ) / env.CellSize);
            if (c0x < 0) c0x = 0;
            if (c0z < 0) c0z = 0;
            if (c1x >= env.CellsX) c1x = env.CellsX - 1;
            if (c1z >= env.CellsZ) c1z = env.CellsZ - 1;
            float sum = 0f;
            int n = 0;
            for (int cz = c0z; cz <= c1z; cz++)
                for (int cx = c0x; cx <= c1x; cx++)
                {
                    float dx = env.CellCentreX(cx) - x, dz = env.CellCentreZ(cz) - z;
                    if (dx * dx + dz * dz > radius * radius) continue;
                    sum += env.Humidity[cz * env.CellsX + cx];
                    n++;
                }
            return n > 0 ? sum / n : env.HumidityAt(x, z);
        }

        /// <summary>How fast full shade pulls the air under a part toward 1.5 °C below the room (fraction per second).</summary>
        public const float ShadePull = 0.5f;

        private static void ApplyPartEffects(World world, float dt)
        {
            var parts = world.Parts;
            if (parts.Count == 0) return;
            var env = world.Environment;
            var T = env.Temperature;
            var H = env.Humidity;
            float cool = env.AmbientTemperature - 1.5f;
            foreach (var p in parts.All)
            {
                var info = p.Info;
                if (info.Shade <= 0f && info.Damp <= 0f) continue;
                const float margin = 0.25f;
                float reach = 0.5f * (float)System.Math.Sqrt(p.SizeX * p.SizeX + p.SizeZ * p.SizeZ) + margin;
                int c0x = (int)((p.X - reach + env.HalfSizeX) / env.CellSize), c1x = (int)((p.X + reach + env.HalfSizeX) / env.CellSize);
                int c0z = (int)((p.Z - reach + env.HalfSizeZ) / env.CellSize), c1z = (int)((p.Z + reach + env.HalfSizeZ) / env.CellSize);
                if (c0x < 0) c0x = 0;
                if (c0z < 0) c0z = 0;
                if (c1x >= env.CellsX) c1x = env.CellsX - 1;
                if (c1z >= env.CellsZ) c1z = env.CellsZ - 1;
                float shade = info.Shade * ShadePull * dt;
                float damp = info.Damp * dt;
                for (int cz = c0z; cz <= c1z; cz++)
                    for (int cx = c0x; cx <= c1x; cx++)
                    {
                        if (!Parts.Contains(p, env.CellCentreX(cx), env.CellCentreZ(cz), margin)) continue;
                        int i = cz * env.CellsX + cx;
                        if (shade > 0f && T[i] > cool) T[i] += (cool - T[i]) * shade;
                        if (H[i] < 95f) H[i] += damp;
                    }
            }
        }

        private void Diffuse(EnvironmentField env, float[] field, float dt)
        {
            int w = env.CellsX, h = env.CellsZ;
            if (_scratch == null || _scratch.Length != field.Length) _scratch = new float[field.Length];
            float k = Diffusion * dt / (env.CellSize * env.CellSize);
            if (k > 0.24f) k = 0.24f; // keeps the maths stable
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = z * w + x;
                    float c = field[i];
                    // Walls do not let heat or moisture through: a missing neighbour counts as this cell.
                    float l = x > 0 ? field[i - 1] : c;
                    float r = x < w - 1 ? field[i + 1] : c;
                    float d = z > 0 ? field[i - w] : c;
                    float u = z < h - 1 ? field[i + w] : c;
                    _scratch[i] = c + k * (l + r + d + u - 4f * c);
                }
            }
            System.Array.Copy(_scratch, field, field.Length);
        }

        /// <summary>Runs the environment on its own for a while, so a new tank starts settled.</summary>
        public static void WarmUp(World world, float seconds)
        {
            var sys = new EnvironmentSystem();
            float dt = 1f / Rate;
            for (float t = 0f; t < seconds; t += dt) sys.Tick(world, dt);
        }
    }
}

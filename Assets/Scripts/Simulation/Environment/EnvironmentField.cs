using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Air temperature (°C), relative humidity (%) and standing water over the tank, on a coarse grid
    /// of 0.5 m cells. EnvironmentSystem updates it from the terrain, the room around the tank and
    /// the devices. Frogs read it to decide how comfortable they are.
    /// </summary>
    public sealed class EnvironmentField
    {
        public const float DefaultCellSize = 0.5f;
        public const float MinTemperature = 5f, MaxTemperature = 45f;

        public EnvironmentField(float halfSizeX, float halfSizeZ, float cellSize = DefaultCellSize)
        {
            HalfSizeX = halfSizeX;
            HalfSizeZ = halfSizeZ;
            CellSize = cellSize;
            CellsX = Math.Max(1, (int)Math.Ceiling(2f * halfSizeX / cellSize));
            CellsZ = Math.Max(1, (int)Math.Ceiling(2f * halfSizeZ / cellSize));
            int n = CellsX * CellsZ;
            Temperature = new float[n];
            Humidity = new float[n];
            Water = new float[n];
            Moss = new float[n];
            Dry = new float[n];
            ResetToAmbient();
        }

        public float HalfSizeX { get; }
        public float HalfSizeZ { get; }
        public float CellSize { get; }
        public int CellsX { get; }
        public int CellsZ { get; }

        /// <summary>The room the tank stands in. The tank drifts toward these without devices.</summary>
        public float AmbientTemperature { get; set; } = 21f;
        public float AmbientHumidity { get; set; } = 40f;

        public readonly float[] Temperature;
        public readonly float[] Humidity;

        // The rest is worked out from the terrain (see RefreshFromTerrain) and is not saved.
        /// <summary>Share of each cell that is open water, 0..1.</summary>
        public readonly float[] Water;
        /// <summary>Share of each cell covered in moss (holds moisture), 0..1.</summary>
        public readonly float[] Moss;
        /// <summary>Share of each cell that is sand or rock (dries out fast), 0..1.</summary>
        public readonly float[] Dry;
        /// <summary>The terrain Version these were worked out from; -1 means never.</summary>
        public int TerrainVersionSeen { get; private set; } = -1;

        public void ResetToAmbient()
        {
            for (int i = 0; i < Temperature.Length; i++)
            {
                Temperature[i] = AmbientTemperature;
                Humidity[i] = AmbientHumidity;
            }
        }

        public int CellIndex(float x, float z)
        {
            int cx = (int)((x + HalfSizeX) / CellSize);
            int cz = (int)((z + HalfSizeZ) / CellSize);
            cx = cx < 0 ? 0 : (cx >= CellsX ? CellsX - 1 : cx);
            cz = cz < 0 ? 0 : (cz >= CellsZ ? CellsZ - 1 : cz);
            return cz * CellsX + cx;
        }

        public float CellCentreX(int cx) => -HalfSizeX + (cx + 0.5f) * CellSize;
        public float CellCentreZ(int cz) => -HalfSizeZ + (cz + 0.5f) * CellSize;

        public float TemperatureAt(float x, float z) => Sample(Temperature, x, z);
        public float HumidityAt(float x, float z) => Sample(Humidity, x, z);

        /// <summary>Smoothly blended value between cell centres.</summary>
        public float Sample(float[] field, float x, float z)
        {
            float gx = (x + HalfSizeX) / CellSize - 0.5f;
            float gz = (z + HalfSizeZ) / CellSize - 0.5f;
            gx = gx < 0f ? 0f : (gx > CellsX - 1 ? CellsX - 1 : gx);
            gz = gz < 0f ? 0f : (gz > CellsZ - 1 ? CellsZ - 1 : gz);
            int x0 = Math.Min((int)gx, Math.Max(0, CellsX - 2));
            int z0 = Math.Min((int)gz, Math.Max(0, CellsZ - 2));
            int x1 = Math.Min(x0 + 1, CellsX - 1), z1 = Math.Min(z0 + 1, CellsZ - 1);
            float tx = gx - x0, tz = gz - z0;
            float a = field[z0 * CellsX + x0] + (field[z0 * CellsX + x1] - field[z0 * CellsX + x0]) * tx;
            float b = field[z1 * CellsX + x0] + (field[z1 * CellsX + x1] - field[z1 * CellsX + x0]) * tx;
            return a + (b - a) * tz;
        }

        /// <summary>Works out water, moss and dry-ground shares per cell from the terrain, if it changed.</summary>
        public void RefreshFromTerrain(TerrainGrid terrain)
        {
            if (terrain.Version == TerrainVersionSeen) return;
            TerrainVersionSeen = terrain.Version;
            const int samples = 4; // 4 x 4 samples per cell
            for (int cz = 0; cz < CellsZ; cz++)
            {
                for (int cx = 0; cx < CellsX; cx++)
                {
                    int water = 0, moss = 0, dry = 0;
                    for (int sz = 0; sz < samples; sz++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = -HalfSizeX + (cx + (sx + 0.5f) / samples) * CellSize;
                            float z = -HalfSizeZ + (cz + (sz + 0.5f) / samples) * CellSize;
                            if (terrain.IsWater(x, z)) { water++; continue; }
                            var layer = terrain.LayerAt(x, z);
                            if (layer == GroundLayer.Moss) moss++;
                            else if (layer == GroundLayer.Sand || layer == GroundLayer.Rock) dry++;
                        }
                    }
                    int i = cz * CellsX + cx;
                    const float total = samples * samples;
                    Water[i] = water / total;
                    Moss[i] = moss / total;
                    Dry[i] = dry / total;
                }
            }
        }

        /// <summary>Forces RefreshFromTerrain to recompute next time (used after loading).</summary>
        public void InvalidateTerrainCache() => TerrainVersionSeen = -1;
    }
}

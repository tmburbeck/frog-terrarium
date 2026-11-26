using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Makes a starter tank from the world seed: gentle hills, one pool dug below the water line,
    /// and patches of moss, sand and rock. The same seed always gives the same tank.
    /// Uses the "mapgen" random stream only.
    /// </summary>
    public static class MapGenerator
    {
        public static void Generate(World world)
        {
            var t = world.Terrain;
            var rng = world.Random.Get("mapgen");

            // Three layers of smooth random bumps (value noise), large to small.
            var hills = new ValueNoise(rng, 8);
            var bumps = new ValueNoise(rng, 8);
            var patches = new ValueNoise(rng, 8);
            float phase = rng.Range(0f, 100f);

            // The pool sits somewhere away from the walls.
            float poolX = rng.Range(-t.HalfSizeX * 0.5f, t.HalfSizeX * 0.5f);
            float poolZ = rng.Range(-t.HalfSizeZ * 0.4f, t.HalfSizeZ * 0.4f);
            float poolR = Math.Min(t.HalfSizeX, t.HalfSizeZ) * rng.Range(0.3f, 0.42f);

            for (int pz = 0; pz < t.PointsZ; pz++)
            {
                for (int px = 0; px < t.PointsX; px++)
                {
                    float x = t.PointX(px), z = t.PointZ(pz);
                    float h = TerrainGrid.DefaultBaseHeight
                              + 0.45f * hills.Sample(x * 0.15f + phase, z * 0.15f)
                              + 0.12f * bumps.Sample(x * 0.6f, z * 0.6f + phase);

                    // Ground rises toward the back wall, like a sloped vivarium.
                    h += 0.35f * (z + t.HalfSizeZ) / (2f * t.HalfSizeZ);

                    float dx = (x - poolX) / poolR, dz = (z - poolZ) / poolR;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d < 1.3f)
                    {
                        float bowl = d < 1f ? 1f - d * d : 0f;
                        float rim = 1f - Smooth(1f, 1.3f, d);
                        float poolFloor = t.WaterLevel - 0.35f * bowl - 0.05f;
                        h = h + (poolFloor - h) * rim;
                    }
                    int i = t.Index(px, pz);
                    t.Height[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);

                    float p = patches.Sample(x * 0.35f + 31f, z * 0.35f - 17f);
                    GroundLayer layer;
                    if (d < 1.45f && h < t.WaterLevel + 0.15f) layer = GroundLayer.Sand; // shoreline
                    else if (p > 0.35f) layer = GroundLayer.Moss;
                    else if (p < -0.55f) layer = GroundLayer.Rock;
                    else layer = GroundLayer.Soil;
                    t.Layer[i] = layer;
                }
            }
            t.MarkAllChanged();
        }

        private static float Smooth(float edge0, float edge1, float v)
        {
            float k = TerrainGrid.Clamp((v - edge0) / (edge1 - edge0), 0f, 1f);
            return k * k * (3f - 2f * k);
        }

        /// <summary>Smooth noise in -1..1 from a small grid of random values, repeating every Size units.</summary>
        private sealed class ValueNoise
        {
            private readonly int _size;
            private readonly float[] _values;

            public ValueNoise(SeededRandom rng, int size)
            {
                _size = size;
                _values = new float[size * size];
                for (int i = 0; i < _values.Length; i++) _values[i] = rng.Range(-1f, 1f);
            }

            public float Sample(float x, float z)
            {
                int x0 = (int)Math.Floor(x), z0 = (int)Math.Floor(z);
                float tx = x - x0, tz = z - z0;
                tx = tx * tx * (3f - 2f * tx);
                tz = tz * tz * (3f - 2f * tz);
                float a = V(x0, z0) + (V(x0 + 1, z0) - V(x0, z0)) * tx;
                float b = V(x0, z0 + 1) + (V(x0 + 1, z0 + 1) - V(x0, z0 + 1)) * tx;
                return a + (b - a) * tz;
            }

            private float V(int x, int z)
            {
                int ix = ((x % _size) + _size) % _size, iz = ((z % _size) + _size) % _size;
                return _values[iz * _size + ix];
            }
        }
    }
}

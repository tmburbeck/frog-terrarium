using System;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Makes the ground of a new tank from its map settings (see <see cref="MapSettings"/>): the
    /// biome decides the shape (hills, dunes, a stream, islands, a volcano) and the sliders make it
    /// hillier, wetter or greener. The same settings always give the same tank.
    /// Uses the "mapgen" random stream only.
    /// </summary>
    public static class MapGenerator
    {
        /// <summary>The classic starter tank, as every game had before biomes.</summary>
        public static void Generate(World world) => Generate(world, MapSettings.Classic(world.Seed));

        public static void Generate(World world, MapSettings settings)
        {
            settings = settings.Clamped();
            var t = world.Terrain;
            var rng = world.Random.Get("mapgen");
            switch (settings.Biome)
            {
                case Biome.Rainforest: Rainforest(t, rng, settings); break;
                case Biome.Desert: Desert(t, rng, settings); break;
                case Biome.Riverbank: Riverbank(t, rng, settings); break;
                case Biome.Islands: Islands(t, rng, settings); break;
                case Biome.Volcanic: Volcanic(t, rng, settings); break;
                default: Classic(t, rng, settings); break;
            }
            t.MarkAllChanged();
        }

        /// <summary>
        /// Gentle hills rising to the back wall, one pool, and patches of moss, sand and rock. With all
        /// sliders at 1 this is exactly the starter tank of earlier versions.
        /// </summary>
        private static void Classic(TerrainGrid t, SeededRandom rng, MapSettings s)
        {
            // Three layers of smooth random bumps (value noise), large to small.
            var hills = new ValueNoise(rng, 8);
            var bumps = new ValueNoise(rng, 8);
            var patches = new ValueNoise(rng, 8);
            float phase = rng.Range(0f, 100f);

            // The pool sits somewhere away from the walls.
            float poolX = rng.Range(-t.HalfSizeX * 0.5f, t.HalfSizeX * 0.5f);
            float poolZ = rng.Range(-t.HalfSizeZ * 0.4f, t.HalfSizeZ * 0.4f);
            float poolR = Math.Min(t.HalfSizeX, t.HalfSizeZ) * rng.Range(0.3f, 0.42f) * PoolScale(s.Water);
            float mossAbove = 0.35f - (s.Plants - 1f) * 0.5f;
            float rockBelow = -0.55f + (s.Plants - 1f) * 0.25f;

            for (int pz = 0; pz < t.PointsZ; pz++)
            {
                for (int px = 0; px < t.PointsX; px++)
                {
                    float x = t.PointX(px), z = t.PointZ(pz);
                    float h = TerrainGrid.DefaultBaseHeight
                              + s.Hills * 0.45f * hills.Sample(x * 0.15f + phase, z * 0.15f)
                              + s.Hills * 0.12f * bumps.Sample(x * 0.6f, z * 0.6f + phase);

                    // Ground rises toward the back wall, like a sloped vivarium.
                    h += 0.35f * (z + t.HalfSizeZ) / (2f * t.HalfSizeZ);

                    float d = Distance(x, z, poolX, poolZ, poolR);
                    h = DigPool(t, h, d, 0.35f);
                    int i = t.Index(px, pz);
                    t.Height[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);

                    float p = patches.Sample(x * 0.35f + 31f, z * 0.35f - 17f);
                    GroundLayer layer;
                    if (d < 1.45f && h < t.WaterLevel + 0.15f) layer = GroundLayer.Sand; // shoreline
                    else if (p > mossAbove) layer = GroundLayer.Moss;
                    else if (p < rockBelow) layer = GroundLayer.Rock;
                    else layer = GroundLayer.Soil;
                    t.Layer[i] = layer;
                }
            }
        }

        /// <summary>Steep hills, two to four pools (more with more water) and moss nearly everywhere.</summary>
        private static void Rainforest(TerrainGrid t, SeededRandom rng, MapSettings s)
        {
            var hills = new ValueNoise(rng, 8);
            var bumps = new ValueNoise(rng, 8);
            var patches = new ValueNoise(rng, 8);
            float phase = rng.Range(0f, 100f);
            int pools = 2 + (int)Math.Round(s.Water);
            var pool = Pools(t, rng, pools, 0.18f, 0.26f, PoolScale(s.Water));
            float mossAbove = -0.35f - (s.Plants - 1f) * 0.6f;

            for (int pz = 0; pz < t.PointsZ; pz++)
                for (int px = 0; px < t.PointsX; px++)
                {
                    float x = t.PointX(px), z = t.PointZ(pz);
                    float h = TerrainGrid.DefaultBaseHeight + 0.15f
                              + s.Hills * (0.75f * hills.Sample(x * 0.18f + phase, z * 0.18f)
                              + 0.2f * bumps.Sample(x * 0.7f, z * 0.7f + phase));
                    h += 0.5f * (z + t.HalfSizeZ) / (2f * t.HalfSizeZ);
                    float d = NearestPool(x, z, pool, out _);
                    h = DigPool(t, h, d, 0.3f);
                    int i = t.Index(px, pz);
                    t.Height[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
                    float p = patches.Sample(x * 0.4f + 11f, z * 0.4f - 5f);
                    GroundLayer layer;
                    if (d < 1.35f && h < t.WaterLevel + 0.1f) layer = GroundLayer.Sand;
                    else if (p > mossAbove) layer = GroundLayer.Moss;
                    else if (h > 1.9f) layer = GroundLayer.Rock;
                    else layer = GroundLayer.Soil;
                    t.Layer[i] = layer;
                }
        }

        /// <summary>Long dunes, red rock outcrops and one small oasis (none at the lowest water setting).</summary>
        private static void Desert(TerrainGrid t, SeededRandom rng, MapSettings s)
        {
            var dunes = new ValueNoise(rng, 8);
            var ripples = new ValueNoise(rng, 8);
            var patches = new ValueNoise(rng, 8);
            float phase = rng.Range(0f, 100f);
            float angle = rng.Range(0f, (float)Math.PI);
            float ca = (float)Math.Cos(angle), sa = (float)Math.Sin(angle);
            var pool = Pools(t, rng, s.Water < 0.25f ? 0 : 1, 0.16f, 0.22f, PoolScale(s.Water));
            float mossBand = 0.35f * s.Plants;

            for (int pz = 0; pz < t.PointsZ; pz++)
                for (int px = 0; px < t.PointsX; px++)
                {
                    float x = t.PointX(px), z = t.PointZ(pz);
                    // Dunes are long ridges: noise stretched along one direction and folded into crests.
                    float u = x * ca + z * sa, v = -x * sa + z * ca;
                    float ridge = 1f - Math.Abs(dunes.Sample(u * 0.09f + phase, v * 0.35f));
                    float h = TerrainGrid.DefaultBaseHeight
                              + s.Hills * (0.6f * ridge * ridge - 0.15f + 0.04f * ripples.Sample(u * 1.6f, v * 1.6f + phase));
                    float d = NearestPool(x, z, pool, out _);
                    h = DigPool(t, h, d, 0.3f);
                    int i = t.Index(px, pz);
                    t.Height[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
                    float p = patches.Sample(x * 0.3f - 7f, z * 0.3f + 3f);
                    GroundLayer layer;
                    if (d < 1.2f && h < t.WaterLevel + 0.12f) layer = GroundLayer.Soil; // wet mud at the water's edge
                    else if (d < 1.2f + mossBand * 2f) layer = GroundLayer.Moss;     // the green ring of the oasis
                    else if (p < -0.45f || ridge < 0.12f) layer = GroundLayer.Rock;
                    else layer = GroundLayer.Sand;
                    t.Layer[i] = layer;
                }
        }

        /// <summary>A winding stream from the left wall to the right, sandy banks, soil and moss meadows.</summary>
        private static void Riverbank(TerrainGrid t, SeededRandom rng, MapSettings s)
        {
            var hills = new ValueNoise(rng, 8);
            var patches = new ValueNoise(rng, 8);
            float phase = rng.Range(0f, 100f);
            float wave = rng.Range(0.18f, 0.32f);
            float bend = t.HalfSizeZ * rng.Range(0.25f, 0.45f);
            float offset = rng.Range(-t.HalfSizeZ * 0.2f, t.HalfSizeZ * 0.2f);
            float width = 0.6f + 0.5f * s.Water;
            float mossAbove = 0.1f - (s.Plants - 1f) * 0.5f;

            for (int pz = 0; pz < t.PointsZ; pz++)
                for (int px = 0; px < t.PointsX; px++)
                {
                    float x = t.PointX(px), z = t.PointZ(pz);
                    float centre = offset + bend * (float)Math.Sin(x * wave + phase);
                    float d = Math.Abs(z - centre) / width;
                    float h = TerrainGrid.DefaultBaseHeight + 0.1f + s.Hills * 0.35f * hills.Sample(x * 0.16f + phase, z * 0.16f);
                    // Banks rise gently away from the stream.
                    h += 0.08f * Math.Min(d, 4f);
                    h = DigPool(t, h, d, 0.3f);
                    int i = t.Index(px, pz);
                    t.Height[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
                    float p = patches.Sample(x * 0.35f + 5f, z * 0.35f + 9f);
                    GroundLayer layer;
                    if (d < 1.6f && h < t.WaterLevel + 0.2f) layer = GroundLayer.Sand;
                    else if (d < 2f && p < -0.3f) layer = GroundLayer.Rock; // pebbles along the banks
                    else if (p > mossAbove) layer = GroundLayer.Moss;
                    else layer = GroundLayer.Soil;
                    t.Layer[i] = layer;
                }
        }

        /// <summary>Mostly water with islands. More water means smaller islands, but there is always some land.</summary>
        private static void Islands(TerrainGrid t, SeededRandom rng, MapSettings s)
        {
            var land = new ValueNoise(rng, 8);
            var bumps = new ValueNoise(rng, 8);
            var patches = new ValueNoise(rng, 8);
            float phase = rng.Range(0f, 100f);
            // Where the sea level cuts the noise: more water lifts it, leaving less land.
            float cut = -0.3f + 0.2f * s.Water;
            // One island is always in the middle, so frogs and devices have somewhere to stand.
            float mainX = rng.Range(-t.HalfSizeX * 0.3f, t.HalfSizeX * 0.3f);
            float mainZ = rng.Range(-t.HalfSizeZ * 0.3f, t.HalfSizeZ * 0.3f);
            float mainR = Math.Min(t.HalfSizeX, t.HalfSizeZ) * 0.45f;
            float mossAbove = 0.0f - (s.Plants - 1f) * 0.5f;

            for (int pz = 0; pz < t.PointsZ; pz++)
                for (int px = 0; px < t.PointsX; px++)
                {
                    float x = t.PointX(px), z = t.PointZ(pz);
                    float n = land.Sample(x * 0.2f + phase, z * 0.2f);
                    float dm = Distance(x, z, mainX, mainZ, mainR);
                    if (dm < 1f) n = Math.Max(n, cut + 0.35f * (1f - dm * dm));
                    float h = t.WaterLevel + (n - cut) * (0.7f + 0.5f * s.Hills)
                              + 0.06f * s.Hills * bumps.Sample(x * 0.8f, z * 0.8f + phase);
                    int i = t.Index(px, pz);
                    t.Height[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
                    float p = patches.Sample(x * 0.4f - 13f, z * 0.4f + 2f);
                    GroundLayer layer;
                    if (h < t.WaterLevel + 0.15f) layer = GroundLayer.Sand; // beaches and the sea floor
                    else if (p > mossAbove) layer = GroundLayer.Moss;
                    else if (h > t.WaterLevel + 0.6f) layer = GroundLayer.Rock;
                    else layer = GroundLayer.Soil;
                    t.Layer[i] = layer;
                }
        }

        /// <summary>A rocky cone with a dip at the top, ash slopes, and one warm spring pool at its foot.</summary>
        private static void Volcanic(TerrainGrid t, SeededRandom rng, MapSettings s)
        {
            var bumps = new ValueNoise(rng, 8);
            var patches = new ValueNoise(rng, 8);
            float phase = rng.Range(0f, 100f);
            float coneX = rng.Range(-t.HalfSizeX * 0.25f, t.HalfSizeX * 0.25f);
            float coneZ = rng.Range(0f, t.HalfSizeZ * 0.3f);
            float coneR = Math.Min(t.HalfSizeX, t.HalfSizeZ) * rng.Range(0.7f, 0.85f);
            // The spring sits at the foot of the cone, toward the front.
            float side = rng.Range(-1f, 1f);
            var pool = new[]
            {
                coneX + side * coneR * 0.9f, coneZ - coneR * 0.95f,
                Math.Min(t.HalfSizeX, t.HalfSizeZ) * 0.3f * PoolScale(s.Water),
            };
            float mossAbove = 0.45f - (s.Plants - 1f) * 0.4f;

            for (int pz = 0; pz < t.PointsZ; pz++)
                for (int px = 0; px < t.PointsX; px++)
                {
                    float x = t.PointX(px), z = t.PointZ(pz);
                    float dc = Distance(x, z, coneX, coneZ, coneR);
                    float cone = dc < 1f ? (1f - dc) : 0f;
                    // A crater: the very top dips down again.
                    float crater = dc < 0.22f ? (0.22f - dc) * 3f : 0f;
                    float h = TerrainGrid.DefaultBaseHeight
                              + s.Hills * (1.8f * cone * cone * (3f - 2f * cone) - 0.6f * crater
                              + 0.1f * bumps.Sample(x * 0.7f + phase, z * 0.7f));
                    float d = NearestPool(x, z, pool, out _);
                    h = DigPool(t, h, d, 0.3f);
                    int i = t.Index(px, pz);
                    t.Height[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
                    float p = patches.Sample(x * 0.45f + 3f, z * 0.45f - 21f);
                    GroundLayer layer;
                    if (d < 1.4f && h < t.WaterLevel + 0.15f) layer = GroundLayer.Sand; // grey ash beach
                    else if (dc < 0.75f) layer = p > mossAbove + 0.3f ? GroundLayer.Moss : GroundLayer.Rock;
                    else if (p > mossAbove) layer = GroundLayer.Moss;
                    else layer = GroundLayer.Soil;
                    t.Layer[i] = layer;
                }
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>Pool size from the water slider: a bit over half as big at 0, half as big again at 2.</summary>
        private static float PoolScale(float water) => water >= 1f ? 1f + 0.5f * (water - 1f) : 0.55f + 0.45f * water;

        /// <summary>Distance from a pool centre, in pool radii.</summary>
        private static float Distance(float x, float z, float cx, float cz, float r)
        {
            float dx = (x - cx) / r, dz = (z - cz) / r;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Lowers the ground into a bowl below the water line near a pool (d is the distance in pool radii).</summary>
        private static float DigPool(TerrainGrid t, float h, float d, float depth)
        {
            if (d >= 1.3f) return h;
            float bowl = d < 1f ? 1f - d * d : 0f;
            float rim = 1f - Smooth(1f, 1.3f, d);
            float poolFloor = t.WaterLevel - depth * bowl - 0.05f;
            return h + (poolFloor - h) * rim;
        }

        /// <summary>Picks pool centres and radii (x, z, r for each) away from the walls.</summary>
        private static float[] Pools(TerrainGrid t, SeededRandom rng, int count, float minR, float maxR, float scale)
        {
            var p = new float[count * 3];
            float size = Math.Min(t.HalfSizeX, t.HalfSizeZ);
            for (int k = 0; k < count; k++)
            {
                p[k * 3] = rng.Range(-t.HalfSizeX * 0.65f, t.HalfSizeX * 0.65f);
                p[k * 3 + 1] = rng.Range(-t.HalfSizeZ * 0.55f, t.HalfSizeZ * 0.55f);
                p[k * 3 + 2] = size * rng.Range(minR, maxR) * scale;
            }
            return p;
        }

        /// <summary>Distance to the nearest pool in its own radii (a large number when there are none).</summary>
        private static float NearestPool(float x, float z, float[] pools, out int which)
        {
            float best = 99f;
            which = -1;
            for (int k = 0; k * 3 < pools.Length; k++)
            {
                float d = Distance(x, z, pools[k * 3], pools[k * 3 + 1], pools[k * 3 + 2]);
                if (d < best) { best = d; which = k; }
            }
            return best;
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

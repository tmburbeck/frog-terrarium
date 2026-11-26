using System;
using System.Globalization;

namespace FrogTerrarium.Simulation
{
    /// <summary>The kinds of landscape the map generator can make. Never reorder: saves and map codes store the number.</summary>
    public enum Biome : byte
    {
        Classic = 0,
        Rainforest = 1,
        Desert = 2,
        Riverbank = 3,
        Islands = 4,
        Volcanic = 5,
    }

    /// <summary>What a biome looks like and the room it sits in.</summary>
    public sealed class BiomeInfo
    {
        public Biome Id;
        public string Name;
        /// <summary>The word used in map codes ("rainforest").</summary>
        public string Code;
        public string Description;
        /// <summary>The style set it starts with (see <see cref="TankLooks.Styles"/>).</summary>
        public byte Style;
        /// <summary>The room's temperature (°C) and humidity (%) around the tank.</summary>
        public float RoomTemperature, RoomHumidity;
        /// <summary>The species the starting frogs are, taken in turn, a pair of each.</summary>
        public byte[] StartingSpecies;
        /// <summary>Extra misters to start with (a dry room needs more).</summary>
        public int ExtraMisters;
    }

    /// <summary>
    /// Everything that decides a generated map: the biome, the seed, the tank size and three
    /// sliders. The same settings always give exactly the same ground, so a map can be shared as
    /// a short code (see <see cref="ToCode"/>) and rebuilt on another computer.
    /// </summary>
    public struct MapSettings
    {
        public Biome Biome;
        public ulong Seed;
        /// <summary>Index into <see cref="TankLooks.Sizes"/>.</summary>
        public byte Size;
        /// <summary>How hilly the ground is: 0 flat, 1 normal, 2 twice as hilly.</summary>
        public float Hills;
        /// <summary>How much water: 0 a little, 1 normal, 2 lots.</summary>
        public float Water;
        /// <summary>How much moss and plant cover: 0 bare, 1 normal, 2 lush.</summary>
        public float Plants;

        public const float MinSlider = 0f, MaxSlider = 2f;
        public const string CodePrefix = "FTMAP1";

        /// <summary>The default settings for a biome and seed: standard size and every slider at normal.</summary>
        public static MapSettings For(Biome biome, ulong seed) =>
            new MapSettings { Biome = biome, Seed = seed, Size = 0, Hills = 1f, Water = 1f, Plants = 1f };

        /// <summary>The starter tank every game had before Phase 4.</summary>
        public static MapSettings Classic(ulong seed) => For(Biome.Classic, seed);

        public BiomeInfo Info => Biomes.Info(Biome);
        public TankSize TankSize => TankLooks.Sizes[Size < TankLooks.Sizes.Length ? Size : 0];

        /// <summary>Keeps every value in range (sliders 0 to 2 in steps of 0.05, a size that exists).</summary>
        public MapSettings Clamped()
        {
            var s = this;
            if ((byte)s.Biome >= Biomes.All.Length) s.Biome = Biome.Classic;
            if (s.Size >= TankLooks.Sizes.Length) s.Size = 0;
            s.Hills = Step(s.Hills);
            s.Water = Step(s.Water);
            s.Plants = Step(s.Plants);
            return s;
        }

        private static float Step(float v)
        {
            if (float.IsNaN(v)) return 1f;
            v = TerrainGrid.Clamp(v, MinSlider, MaxSlider);
            return (float)Math.Round(v * 20f) / 20f;
        }

        /// <summary>
        /// A short code that rebuilds this map anywhere, for example
        /// <c>FTMAP1:rainforest:48213:large:120:80:100</c> (biome, seed, size, then hills, water and
        /// plants in percent).
        /// </summary>
        public string ToCode()
        {
            var s = Clamped();
            return string.Join(":", CodePrefix, s.Info.Code, s.Seed.ToString(CultureInfo.InvariantCulture),
                s.TankSize.Name.ToLowerInvariant(), Percent(s.Hills), Percent(s.Water), Percent(s.Plants));
        }

        private static string Percent(float v) => ((int)Math.Round(v * 100f)).ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Reads a map code made by <see cref="ToCode"/>. Spaces around it are ignored and words may
        /// be in any case. The last four parts may be left out (standard size, normal sliders).
        /// Returns false and why if the code can't be read.
        /// </summary>
        public static bool TryParse(string code, out MapSettings settings, out string why)
        {
            settings = Classic(0);
            why = "";
            if (string.IsNullOrWhiteSpace(code)) { why = "The map code is empty."; return false; }
            var parts = code.Trim().Split(':');
            if (parts.Length < 3 || !string.Equals(parts[0].Trim(), CodePrefix, StringComparison.OrdinalIgnoreCase))
            {
                why = "That isn't a map code. Map codes start with " + CodePrefix + ":";
                return false;
            }
            if (parts.Length > 7) { why = "The map code has too many parts."; return false; }
            var biome = Biomes.FromCode(parts[1].Trim());
            if (biome == null) { why = "Unknown biome \"" + parts[1].Trim() + "\"."; return false; }
            if (!ulong.TryParse(parts[2].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
            {
                why = "The seed in the map code must be a whole number.";
                return false;
            }
            var s = For(biome.Id, seed);
            if (parts.Length > 3)
            {
                int size = -1;
                for (int k = 0; k < TankLooks.Sizes.Length; k++)
                    if (string.Equals(TankLooks.Sizes[k].Name, parts[3].Trim(), StringComparison.OrdinalIgnoreCase)) size = k;
                if (size < 0) { why = "Unknown tank size \"" + parts[3].Trim() + "\"."; return false; }
                s.Size = (byte)size;
            }
            float[] sliders = { 1f, 1f, 1f };
            for (int k = 4; k < parts.Length; k++)
            {
                if (!int.TryParse(parts[k].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int pct) || pct > 200)
                {
                    why = "Hills, water and plants must be whole percentages from 0 to 200.";
                    return false;
                }
                sliders[k - 4] = pct / 100f;
            }
            s.Hills = sliders[0]; s.Water = sliders[1]; s.Plants = sliders[2];
            settings = s.Clamped();
            return true;
        }

        /// <summary>
        /// Turns what the player typed as a seed into a number: a whole number is used as it is, and
        /// any other text (a word, a name) is turned into a number the same way on every computer.
        /// </summary>
        public static ulong SeedFromText(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return 0;
            if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong n)) return n;
            // FNV-1a over the lower-case text, so "Froggy" and "froggy" give the same map.
            ulong hash = 14695981039346656037UL;
            foreach (char c in text.ToLowerInvariant())
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        public override string ToString() => ToCode();
    }

    public static class Biomes
    {
        /// <summary>Every biome, by its number.</summary>
        public static readonly BiomeInfo[] All =
        {
            new BiomeInfo { Id = Biome.Classic, Name = "Classic", Code = "classic", Style = 0,
                Description = "Gentle hills sloping up to the back, one pool, patches of moss, sand and rock.",
                RoomTemperature = 21f, RoomHumidity = 40f, StartingSpecies = new byte[] { 0 } },
            new BiomeInfo { Id = Biome.Rainforest, Name = "Rainforest", Code = "rainforest", Style = 1,
                Description = "Steep green hills, several pools and moss nearly everywhere, in a warm, damp room.",
                RoomTemperature = 24f, RoomHumidity = 65f, StartingSpecies = new byte[] { 1, 3 } },
            new BiomeInfo { Id = Biome.Desert, Name = "Desert", Code = "desert", Style = 2,
                Description = "Rolling sand dunes, red rock and one small oasis, in a hot, dry room. A real challenge for frogs.",
                RoomTemperature = 28f, RoomHumidity = 25f, StartingSpecies = new byte[] { 0 }, ExtraMisters = 1 },
            new BiomeInfo { Id = Biome.Riverbank, Name = "Riverbank", Code = "riverbank", Style = 3,
                Description = "A winding stream crosses the tank between sandy banks and mossy meadows.",
                RoomTemperature = 20f, RoomHumidity = 55f, StartingSpecies = new byte[] { 2, 5 } },
            new BiomeInfo { Id = Biome.Islands, Name = "Islands", Code = "islands", Style = 3,
                Description = "Mostly water, with islands of sand and moss. Frogs swim between them.",
                RoomTemperature = 23f, RoomHumidity = 60f, StartingSpecies = new byte[] { 2, 0 } },
            new BiomeInfo { Id = Biome.Volcanic, Name = "Volcanic", Code = "volcanic", Style = 4,
                Description = "A rocky cone in the middle, dark ash around it and a warm spring at its foot.",
                RoomTemperature = 26f, RoomHumidity = 35f, StartingSpecies = new byte[] { 3, 0 } },
        };

        public static BiomeInfo Info(Biome biome) => All[(byte)biome < All.Length ? (byte)biome : 0];

        /// <summary>The biome with this map-code word or name (any case), or null.</summary>
        public static BiomeInfo FromCode(string code)
        {
            foreach (var b in All)
                if (string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase) || string.Equals(b.Name, code, StringComparison.OrdinalIgnoreCase))
                    return b;
            return null;
        }
    }
}

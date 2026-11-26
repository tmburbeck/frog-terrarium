namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// The numbers that make a frog species behave the way it does. Each frog also carries a
    /// <see cref="Genome"/> that nudges some of these numbers, so no two frogs are quite alike.
    ///
    /// Times are in game seconds at normal speed. Real frogs take months to grow up; the game
    /// squeezes that into minutes so you can watch a family grow in one sitting.
    /// </summary>
    public sealed class FrogSpecies
    {
        /// <summary>Index in <see cref="SpeciesCatalog.Frogs"/>. Saved as one byte per frog.</summary>
        public byte Id;
        public string Name = "White's tree frog";
        public string LatinName = "Litoria caerulea";
        /// <summary>A short label for buttons.</summary>
        public string ShortName = "White's";
        /// <summary>
        /// The genus. Only species in the same genus will be able to interbreed (hybrids arrive
        /// in Phase 3; this is here so the data is ready for them).
        /// </summary>
        public string Genus = "Litoria";

        // Needs rise per second, from 0 (fine) to 1 (desperate).
        public float HungerPerSecond = 1f / 360f;
        public float ThirstPerSecond = 1f / 200f;
        public float FatiguePerSecond = 1f / 420f;
        /// <summary>How fast sleep recovers fatigue, per second.</summary>
        public float SleepRecoveryPerSecond = 1f / 45f;
        /// <summary>How fast sitting in water removes thirst, per second (frogs drink through their skin).</summary>
        public float DrinkPerSecond = 1f / 12f;
        /// <summary>How much one insect fills the frog up.</summary>
        public float MealSize = 0.3f;

        // Comfortable conditions. Outside these, comfort drops.
        public float MinTemperature = 22f, MaxTemperature = 30f;
        public float MinHumidity = 45f, MaxHumidity = 75f;

        // Movement and senses, in metres and metres per second.
        public float WanderSpeed = 0.35f;
        public float HuntSpeed = 0.9f;
        public float TravelSpeed = 0.7f;
        public float SenseRadius = 3.5f;
        public float TongueReach = 0.35f;
        /// <summary>Seconds between tongue strikes.</summary>
        public float StrikeCooldown = 1.2f;

        // Looks (placeholder art until real models arrive).
        /// <summary>Drawn size of a grown adult, in metres.</summary>
        public float AdultSize = 0.25f;
        public float ColourR = 0.35f, ColourG = 0.66f, ColourB = 0.38f;

        // Life cycle. Growth time only ticks at full speed when the young are fed and comfortable.
        public float EggSeconds = 60f;
        public float TadpoleSeconds = 240f;
        public float FrogletSeconds = 180f;
        public float JuvenileSeconds = 360f;
        /// <summary>Real age, in seconds, at which an adult becomes an elder and stops breeding.</summary>
        public float ElderAge = 3600f;
        /// <summary>Natural lifespan range, in seconds (before the longevity gene).</summary>
        public float MinLifespan = 4500f, MaxLifespan = 5400f;

        // Social life.
        /// <summary>
        /// 0 solitary to 1 very sociable. Sociable frogs get lonely away from their own kind and
        /// go to find them, so they gather in groups.
        /// </summary>
        public float Sociability = 0.4f;
        /// <summary>Radius in metres of the patch an adult male defends against rival males of his species (0 = not territorial).</summary>
        public float TerritoryRadius = 1.2f;

        // Breeding.
        public int ClutchMin = 3, ClutchMax = 6;
        /// <summary>Seconds a female rests after laying before she can breed again.</summary>
        public float BreedCooldown = 600f;

        /// <summary>Growth seconds needed to hatch from the egg.</summary>
        public float HatchAt => EggSeconds;
        /// <summary>Growth seconds needed to leave the water as a froglet.</summary>
        public float FrogletAt => EggSeconds + TadpoleSeconds;
        /// <summary>Growth seconds needed to become a juvenile.</summary>
        public float JuvenileAt => FrogletAt + FrogletSeconds;
        /// <summary>Growth seconds needed to be a grown adult.</summary>
        public float AdultAt => JuvenileAt + JuvenileSeconds;

        /// <summary>
        /// 0..1 comfort for given conditions: 1 inside the comfortable range, falling by
        /// 0.12 per °C and 0.03 per humidity point outside it.
        /// </summary>
        public float ComfortFor(float temperature, float humidity) => ComfortFor(temperature, humidity, Genome.Default);

        /// <summary>Comfort for one frog, whose genes can shift its comfortable range a little.</summary>
        public float ComfortFor(float temperature, float humidity, in Genome genes)
        {
            float minT = MinTemperature + genes.HeatShift, maxT = MaxTemperature + genes.HeatShift;
            float minH = MinHumidity + genes.DampShift, maxH = MaxHumidity + genes.DampShift;
            float t = 1f;
            if (temperature < minT) t -= (minT - temperature) * 0.12f;
            else if (temperature > maxT) t -= (temperature - maxT) * 0.12f;
            float h = 1f;
            if (humidity < minH) h -= (minH - humidity) * 0.03f;
            else if (humidity > maxH) h -= (humidity - maxH) * 0.03f;
            float c = t < h ? t : h;
            return c < 0f ? 0f : c;
        }

        /// <summary>The life stage that goes with an amount of growth and a real age.</summary>
        public LifeStage StageFor(float growth, float age)
        {
            if (growth < HatchAt) return LifeStage.Egg;
            if (growth < FrogletAt) return LifeStage.Tadpole;
            if (growth < JuvenileAt) return LifeStage.Froglet;
            if (growth < AdultAt) return LifeStage.Juvenile;
            return age >= ElderAge ? LifeStage.Elder : LifeStage.Adult;
        }

        /// <summary>How big a frog is compared to a grown adult (0..1), from its growth.</summary>
        public float SizeFraction(float growth)
        {
            if (growth < HatchAt) return 0.18f;
            if (growth < FrogletAt) return 0.25f + 0.15f * (growth - HatchAt) / TadpoleSeconds;
            if (growth >= AdultAt) return 1f;
            return 0.4f + 0.6f * (growth - FrogletAt) / (AdultAt - FrogletAt);
        }
    }

    /// <summary>
    /// The feeder insect: the house cricket (Acheta domesticus). Crickets wander, run from frogs,
    /// and breed on soil or moss when the tank is not too crowded.
    /// </summary>
    public sealed class InsectSpecies
    {
        public string Name = "House cricket";
        public string LatinName = "Acheta domesticus";

        public float WanderSpeed = 0.6f;
        public float FleeSpeed = 1.6f;
        /// <summary>A cricket notices a frog this close and runs.</summary>
        public float FleeRadius = 1.1f;

        /// <summary>Crickets can breed from this age, in seconds.</summary>
        public float AdultAge = 90f;
        public float MinLifespan = 480f, MaxLifespan = 720f;
        /// <summary>Chance per second that an adult lays young when conditions are good.</summary>
        public float BreedChancePerSecond = 1f / 120f;
        /// <summary>No breeding when this many other crickets are within one metre.</summary>
        public int CrowdedNeighbours = 5;
        /// <summary>The tank supports this many crickets per square metre of dry ground.</summary>
        public float CapacityPerSquareMetre = 0.35f;
    }

    /// <summary>All species in the game.</summary>
    public static class SpeciesCatalog
    {
        /// <summary>
        /// The frog species, indexed by the species byte each frog carries. Never reorder this
        /// list: saves store the index. Add new species to the end.
        /// </summary>
        public static readonly FrogSpecies[] Frogs =
        {
            // White's tree frog: a hardy, popular pet frog from Australia. Copes with a fairly wide
            // range of conditions, so it suits the starter tank.
            new FrogSpecies { Id = 0 },

            // Red-eyed tree frog: a small, bright green rainforest frog from Central America.
            // Needs warm, very humid air; keep it near the mister and the pool.
            new FrogSpecies
            {
                Id = 1,
                Name = "Red-eyed tree frog",
                LatinName = "Agalychnis callidryas",
                ShortName = "Red-eyed",
                Genus = "Agalychnis",
                MinTemperature = 24f, MaxTemperature = 28f,
                MinHumidity = 70f, MaxHumidity = 90f,
                HungerPerSecond = 1f / 330f,
                ThirstPerSecond = 1f / 160f,
                WanderSpeed = 0.4f, HuntSpeed = 1.0f, TravelSpeed = 0.75f,
                AdultSize = 0.19f,
                ColourR = 0.30f, ColourG = 0.82f, ColourB = 0.22f,
                ClutchMin = 4, ClutchMax = 7,
                MinLifespan = 4000f, MaxLifespan = 4800f,
                Sociability = 0.2f,
                TerritoryRadius = 1.0f,
            },

            // Oriental fire-bellied toad: a small, tough frog from Korea and north-east China with a
            // bright red belly. Likes it cooler than the others and spends a lot of time in water.
            new FrogSpecies
            {
                Id = 2,
                Name = "Oriental fire-bellied toad",
                LatinName = "Bombina orientalis",
                ShortName = "Fire-bellied",
                Genus = "Bombina",
                MinTemperature = 18f, MaxTemperature = 24f,
                MinHumidity = 55f, MaxHumidity = 85f,
                HungerPerSecond = 1f / 390f,
                ThirstPerSecond = 1f / 150f,
                DrinkPerSecond = 1f / 8f,
                WanderSpeed = 0.3f, HuntSpeed = 0.8f, TravelSpeed = 0.6f,
                AdultSize = 0.17f,
                ColourR = 0.30f, ColourG = 0.48f, ColourB = 0.16f,
                ClutchMin = 3, ClutchMax = 5,
                MinLifespan = 4800f, MaxLifespan = 5800f,
                // Fire-bellied toads are famously sociable: in the wild and in tanks they sit in groups.
                Sociability = 0.9f,
                TerritoryRadius = 0f,
            },
        };

        /// <summary>White's tree frog, the starter species.</summary>
        public static FrogSpecies Frog => Frogs[0];

        public static readonly InsectSpecies Insect = new InsectSpecies();

        /// <summary>The species for a stored species byte (unknown values fall back to the first species).</summary>
        public static FrogSpecies FrogOf(byte id) => id < Frogs.Length ? Frogs[id] : Frogs[0];

        private static readonly string[] FrogNames =
        {
            "Basil", "Pip", "Mossy", "Jade", "Puddle", "Kiwi", "Olive", "Sprout", "Fern", "Pickle",
            "Lily", "Bean", "Clover", "Gumdrop", "Dewey", "Juniper", "Taro", "Minnow", "Pebble", "Sage",
            "Bramble", "Twig", "Lime", "Nori", "Wasabi", "Marsh", "Reed", "Peapod", "Bud", "Algae",
        };

        /// <summary>A friendly name that never changes for a given frog id.</summary>
        public static string FrogName(int id)
        {
            string name = FrogNames[(id * 7 + 3) % FrogNames.Length];
            int round = id / FrogNames.Length;
            return round == 0 ? name : name + " " + (round + 1);
        }

        /// <summary>A plain word for a life stage, for the inspector.</summary>
        public static string StageName(LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.Egg: return "egg";
                case LifeStage.Tadpole: return "tadpole";
                case LifeStage.Froglet: return "froglet";
                case LifeStage.Juvenile: return "juvenile";
                case LifeStage.Elder: return "elder";
                default: return "adult";
            }
        }
    }
}

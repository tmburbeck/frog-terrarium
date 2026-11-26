namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// The numbers that make a frog species behave the way it does. Phase 1 has one:
    /// White's tree frog (Litoria caerulea), a hardy, popular pet frog from Australia.
    /// Later phases add more species and genetics that shift these numbers per frog.
    /// </summary>
    public sealed class FrogSpecies
    {
        public string Name = "White's tree frog";
        public string LatinName = "Litoria caerulea";

        // Needs rise per second, from 0 (fine) to 1 (desperate).
        public float HungerPerSecond = 1f / 240f;
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

        /// <summary>
        /// 0..1 comfort for given conditions: 1 inside the comfortable range, falling by
        /// 0.12 per °C and 0.03 per humidity point outside it.
        /// </summary>
        public float ComfortFor(float temperature, float humidity)
        {
            float t = 1f;
            if (temperature < MinTemperature) t -= (MinTemperature - temperature) * 0.12f;
            else if (temperature > MaxTemperature) t -= (temperature - MaxTemperature) * 0.12f;
            float h = 1f;
            if (humidity < MinHumidity) h -= (MinHumidity - humidity) * 0.03f;
            else if (humidity > MaxHumidity) h -= (humidity - MaxHumidity) * 0.03f;
            float c = t < h ? t : h;
            return c < 0f ? 0f : c;
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
        public float BreedChancePerSecond = 1f / 400f;
        /// <summary>No breeding when this many other crickets are within one metre.</summary>
        public int CrowdedNeighbours = 5;
        /// <summary>The tank supports this many crickets per square metre of dry ground.</summary>
        public float CapacityPerSquareMetre = 0.35f;
    }

    /// <summary>All species in the game. Phase 1 has exactly one of each kind.</summary>
    public static class SpeciesCatalog
    {
        public static readonly FrogSpecies Frog = new FrogSpecies();
        public static readonly InsectSpecies Insect = new InsectSpecies();

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
    }
}

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
        /// <summary>The genus. Only species in the same genus can interbreed and make hybrids.</summary>
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
        /// <summary>How far away a female hears a calling male, in metres.</summary>
        public float CallRange = 8f;
        /// <summary>Extra strength in territorial disputes (teeth and claws add to it).</summary>
        public float FightBonus = 0f;
        /// <summary>How good at climbing the glass when trying to escape (1 = normal).</summary>
        public float ClimbSkill = 1f;

        // Looks (placeholder art until real models arrive).
        /// <summary>Drawn size of a grown adult, in metres.</summary>
        public float AdultSize = 0.25f;
        public float ColourR = 0.35f, ColourG = 0.66f, ColourB = 0.38f;
        /// <summary>Belly colour (used for the underside once frogs have real models).</summary>
        public float BellyR = 0.92f, BellyG = 0.92f, BellyB = 0.85f;
        /// <summary>Eye colour.</summary>
        public float EyeR = 0.75f, EyeG = 0.55f, EyeB = 0.15f;

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
        /// <summary>Tree frogs like to sit up on perches (branches, logs, plants); ground frogs do not care.</summary>
        public bool LikesPerches = true;
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
        /// <summary>A copy of this species that can be changed without touching the original.</summary>
        public FrogSpecies Clone() => (FrogSpecies)MemberwiseClone();

        /// <summary>
        /// A species part way between two others: 0 is all <paramref name="a"/>, 1 all <paramref name="b"/>.
        /// Hybrids get the in-between numbers of their parents' species (size, colour, climate,
        /// growth, temperament). Identity (id, genus) stays with <paramref name="a"/>.
        /// </summary>
        public static FrogSpecies Blend(FrogSpecies a, FrogSpecies b, float t)
        {
            var s = a.Clone();
            float L(float x, float y) => x + (y - x) * t;
            s.HungerPerSecond = L(a.HungerPerSecond, b.HungerPerSecond);
            s.ThirstPerSecond = L(a.ThirstPerSecond, b.ThirstPerSecond);
            s.FatiguePerSecond = L(a.FatiguePerSecond, b.FatiguePerSecond);
            s.SleepRecoveryPerSecond = L(a.SleepRecoveryPerSecond, b.SleepRecoveryPerSecond);
            s.DrinkPerSecond = L(a.DrinkPerSecond, b.DrinkPerSecond);
            s.MealSize = L(a.MealSize, b.MealSize);
            s.MinTemperature = L(a.MinTemperature, b.MinTemperature);
            s.MaxTemperature = L(a.MaxTemperature, b.MaxTemperature);
            s.MinHumidity = L(a.MinHumidity, b.MinHumidity);
            s.MaxHumidity = L(a.MaxHumidity, b.MaxHumidity);
            s.WanderSpeed = L(a.WanderSpeed, b.WanderSpeed);
            s.HuntSpeed = L(a.HuntSpeed, b.HuntSpeed);
            s.TravelSpeed = L(a.TravelSpeed, b.TravelSpeed);
            s.SenseRadius = L(a.SenseRadius, b.SenseRadius);
            s.TongueReach = L(a.TongueReach, b.TongueReach);
            s.StrikeCooldown = L(a.StrikeCooldown, b.StrikeCooldown);
            s.CallRange = L(a.CallRange, b.CallRange);
            s.AdultSize = L(a.AdultSize, b.AdultSize);
            s.ColourR = L(a.ColourR, b.ColourR);
            s.ColourG = L(a.ColourG, b.ColourG);
            s.ColourB = L(a.ColourB, b.ColourB);
            s.BellyR = L(a.BellyR, b.BellyR);
            s.BellyG = L(a.BellyG, b.BellyG);
            s.BellyB = L(a.BellyB, b.BellyB);
            s.EyeR = L(a.EyeR, b.EyeR);
            s.EyeG = L(a.EyeG, b.EyeG);
            s.EyeB = L(a.EyeB, b.EyeB);
            s.EggSeconds = L(a.EggSeconds, b.EggSeconds);
            s.TadpoleSeconds = L(a.TadpoleSeconds, b.TadpoleSeconds);
            s.FrogletSeconds = L(a.FrogletSeconds, b.FrogletSeconds);
            s.JuvenileSeconds = L(a.JuvenileSeconds, b.JuvenileSeconds);
            s.ElderAge = L(a.ElderAge, b.ElderAge);
            s.MinLifespan = L(a.MinLifespan, b.MinLifespan);
            s.MaxLifespan = L(a.MaxLifespan, b.MaxLifespan);
            s.Sociability = L(a.Sociability, b.Sociability);
            s.TerritoryRadius = L(a.TerritoryRadius, b.TerritoryRadius);
            s.ClutchMin = (int)(L(a.ClutchMin, b.ClutchMin) + 0.5f);
            s.ClutchMax = (int)(L(a.ClutchMax, b.ClutchMax) + 0.5f);
            s.BreedCooldown = L(a.BreedCooldown, b.BreedCooldown);
            return s;
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
                EyeR = 0.9f, EyeG = 0.1f, EyeB = 0.08f,
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
                LikesPerches = false,
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
                BellyR = 0.85f, BellyG = 0.2f, BellyB = 0.1f,
            },

            // Phase 3: one close relative for each starting species, so every species has a partner
            // it can make hybrids with.

            // White-lipped tree frog: the world's biggest tree frog, from northern Australia and New
            // Guinea. A close cousin of White's tree frog; likes it a little warmer and damper.
            new FrogSpecies
            {
                Id = 3,
                Name = "White-lipped tree frog",
                LatinName = "Litoria infrafrenata",
                ShortName = "White-lipped",
                Genus = "Litoria",
                MinTemperature = 24f, MaxTemperature = 31f,
                MinHumidity = 60f, MaxHumidity = 85f,
                HungerPerSecond = 1f / 320f,
                WanderSpeed = 0.38f, HuntSpeed = 1.0f, TravelSpeed = 0.75f,
                AdultSize = 0.32f,
                ColourR = 0.24f, ColourG = 0.72f, ColourB = 0.30f,
                EyeR = 0.6f, EyeG = 0.4f, EyeB = 0.2f,
                ClutchMin = 4, ClutchMax = 7,
                Sociability = 0.3f,
                TerritoryRadius = 1.4f,
            },

            // Morelet's tree frog: a cousin of the red-eyed tree frog from the cloud forests of Mexico
            // and Central America, with dark eyes. Likes it cooler than its lowland cousin.
            new FrogSpecies
            {
                Id = 4,
                Name = "Morelet's tree frog",
                LatinName = "Agalychnis moreletii",
                ShortName = "Morelet's",
                Genus = "Agalychnis",
                MinTemperature = 20f, MaxTemperature = 26f,
                MinHumidity = 70f, MaxHumidity = 92f,
                HungerPerSecond = 1f / 350f,
                ThirstPerSecond = 1f / 170f,
                WanderSpeed = 0.38f, HuntSpeed = 0.95f, TravelSpeed = 0.7f,
                AdultSize = 0.2f,
                ColourR = 0.24f, ColourG = 0.58f, ColourB = 0.22f,
                EyeR = 0.08f, EyeG = 0.07f, EyeB = 0.07f,
                ClutchMin = 4, ClutchMax = 6,
                MinLifespan = 4200f, MaxLifespan = 5000f,
                Sociability = 0.3f,
                TerritoryRadius = 0.9f,
            },

            // Yellow-bellied toad: a small European toad with a bright yellow and black belly. In
            // the wild it really does make hybrids with its fire-bellied cousins where they meet.
            new FrogSpecies
            {
                Id = 5,
                Name = "Yellow-bellied toad",
                LatinName = "Bombina variegata",
                ShortName = "Yellow-bellied",
                Genus = "Bombina",
                LikesPerches = false,
                MinTemperature = 16f, MaxTemperature = 23f,
                MinHumidity = 60f, MaxHumidity = 90f,
                HungerPerSecond = 1f / 400f,
                ThirstPerSecond = 1f / 150f,
                DrinkPerSecond = 1f / 8f,
                WanderSpeed = 0.3f, HuntSpeed = 0.8f, TravelSpeed = 0.6f,
                AdultSize = 0.16f,
                ColourR = 0.42f, ColourG = 0.40f, ColourB = 0.30f,
                BellyR = 0.95f, BellyG = 0.80f, BellyB = 0.15f,
                ClutchMin = 3, ClutchMax = 5,
                MinLifespan = 5000f, MaxLifespan = 6000f,
                Sociability = 0.8f,
                TerritoryRadius = 0f,
            },
        };

        /// <summary>True if two species can interbreed (they are in the same genus).</summary>
        public static bool CanInterbreed(byte a, byte b) => a == b || FrogOf(a).Genus == FrogOf(b).Genus;

        private static readonly System.Collections.Generic.Dictionary<int, FrogSpecies> Variants =
            new System.Collections.Generic.Dictionary<int, FrogSpecies>();

        /// <summary>The numbers for one particular frog: its species, blended with its hybrid ancestry and changed by its mutations.</summary>
        public static FrogSpecies FrogOf(AgentStore a, int i) => FrogFor(a.Species[i], a.Genes[i]);

        /// <summary>
        /// The numbers for a frog of this main species and these genes. Pure-bred frogs without
        /// mutations get the plain species. Others get a blended and mutated copy, worked out once
        /// and then reused (the copies depend only on fixed data, so sharing them is safe).
        /// </summary>
        public static FrogSpecies FrogFor(byte species, in Genome g)
        {
            ushort visible = g.Visible;
            bool hybrid = g.IsHybrid && g.HybridSpecies != species && g.HybridSpecies < Frogs.Length;
            if (!hybrid && visible == 0) return FrogOf(species);
            int step = hybrid ? (int)(g.HybridShare * 8f + 0.5f) : 0; // in eighths, 0..4
            if (step == 0) hybrid = false;
            int key = (species & 0xF) | ((hybrid ? g.HybridSpecies & 0xF : 0) << 4) | (step << 8) | (visible << 12);
            lock (Variants)
            {
                if (Variants.TryGetValue(key, out var cached)) return cached;
                var main = FrogOf(species);
                var sp = hybrid ? FrogSpecies.Blend(main, FrogOf(g.HybridSpecies), step / 8f) : main.Clone();
                if (hybrid)
                {
                    var other = FrogOf(g.HybridSpecies);
                    sp.Name = main.ShortName + " × " + other.ShortName + " hybrid";
                    sp.ShortName = main.ShortName + " hybrid";
                }
                Mutations.Apply(sp, visible);
                Variants[key] = sp;
                return sp;
            }
        }

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

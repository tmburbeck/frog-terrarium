namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// All simulation state in one place: agents, terrain, environment, devices, tank bounds,
    /// random streams and the simulated clock. Everything in here is what a save file must capture.
    /// </summary>
    public sealed class World
    {
        public World(ulong seed, float halfSizeX = 10f, float halfSizeZ = 6f)
        {
            Seed = seed;
            Random = new RandomService(seed);
            Agents = new AgentStore();
            HalfSizeX = halfSizeX;
            HalfSizeZ = halfSizeZ;
            Terrain = new TerrainGrid(halfSizeX, halfSizeZ);
            Environment = new EnvironmentField(halfSizeX, halfSizeZ);
            Devices = new DeviceStore();
            Parts = new PartStore();
        }

        /// <summary>
        /// A ready-to-play starter tank: generated terrain with a pool, a heater and a mister,
        /// settled temperature and humidity, and the starting animals.
        /// </summary>
        public static World CreateStarterTank(ulong seed, int frogs, int insects)
        {
            var world = new World(seed);
            MapGenerator.Generate(world);
            var t = world.Terrain;
            world.Devices.Add(DeviceKind.Heater, -t.HalfSizeX * 0.45f, t.HalfSizeZ * 0.35f);
            world.Devices.Add(DeviceKind.Mister, t.HalfSizeX * 0.45f, t.HalfSizeZ * 0.3f);
            world.Devices.Add(DeviceKind.Feeder, 0f, -t.HalfSizeZ * 0.55f);
            EnvironmentSystem.WarmUp(world, 300f);
            world.SpawnStartingPopulation(frogs, insects);
            return world;
        }

        public ulong Seed { get; }
        public RandomService Random { get; }
        public AgentStore Agents { get; }
        public TerrainGrid Terrain { get; }
        public EnvironmentField Environment { get; }
        public DeviceStore Devices { get; }
        /// <summary>Building and decoration parts (rocks, logs, plants, walls, platforms...).</summary>
        public PartStore Parts { get; }
        /// <summary>Frogs that climbed out of the tank. They can be brought back with <see cref="BringBackEscapedFrogs"/>.</summary>
        public System.Collections.Generic.List<EscapedFrog> Escaped { get; } = new System.Collections.Generic.List<EscapedFrog>();

        /// <summary>Current warnings for the player, grouped by cause. Worked out by AlertSystem; not saved.</summary>
        public AlertList Alerts { get; } = new AlertList();

        /// <summary>Hard cap on agents so a population boom can never stall the game.</summary>
        public const int MaxAgents = 1500;

        /// <summary>Half the tank's width and depth in metres; the tank is centred on the origin.</summary>
        public float HalfSizeX { get; set; }
        public float HalfSizeZ { get; set; }

        /// <summary>Simulated seconds since the world was created.</summary>
        public double SimTime { get; internal set; }

        /// <summary>
        /// Fills the tank with a starting population on dry ground, using the "spawn" random stream.
        /// Starting frogs are grown White's tree frogs, half female and half male.
        /// </summary>
        public void SpawnStartingPopulation(int frogs, int insects)
        {
            var rng = Random.Get("spawn");
            for (int i = 0; i < frogs; i++)
            {
                FindDryGround(rng, -HalfSizeX, HalfSizeX, -HalfSizeZ, HalfSizeZ, out float x, out float z);
                int idx = AddFrog(x, z, 0, i % 2 == 0 ? Sex.Female : Sex.Male);
                Agents.Age[idx] = SpeciesCatalog.Frog.AdultAt + rng.Range(300f, 1800f);
                Agents.Hunger[idx] = rng.Range(0f, 0.3f);
                Agents.Thirst[idx] = rng.Range(0f, 0.3f);
                Agents.Fatigue[idx] = rng.Range(0f, 0.3f);
            }
            for (int i = 0; i < insects; i++)
            {
                FindDryGround(rng, -HalfSizeX, HalfSizeX, -HalfSizeZ, HalfSizeZ, out float x, out float z);
                int idx = AddInsect(x, z, rng);
                Agents.Age[idx] = rng.Range(0f, Agents.Lifespan[idx] * 0.7f);
            }
        }

        /// <summary>Picks a random spot in the rectangle that is not under water (gives up after 20 tries).</summary>
        public void FindDryGround(SeededRandom rng, float minX, float maxX, float minZ, float maxZ, out float x, out float z)
        {
            minX = TerrainGrid.Clamp(minX, -HalfSizeX, HalfSizeX); maxX = TerrainGrid.Clamp(maxX, -HalfSizeX, HalfSizeX);
            minZ = TerrainGrid.Clamp(minZ, -HalfSizeZ, HalfSizeZ); maxZ = TerrainGrid.Clamp(maxZ, -HalfSizeZ, HalfSizeZ);
            x = 0.5f * (minX + maxX); z = 0.5f * (minZ + maxZ);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                x = rng.Range(minX, maxX);
                z = rng.Range(minZ, maxZ);
                if (!Terrain.IsWater(x, z)) return;
            }
        }

        /// <summary>Adds a grown White's tree frog standing on the surface at (x, z). Sex alternates by id.</summary>
        public int AddFrog(float x, float z) => AddFrog(x, z, 0, Agents.NextId % 2 == 1 ? Sex.Female : Sex.Male);

        /// <summary>
        /// Adds a grown frog of a species from outside the tank: no known parents, wild genes, and a
        /// lifespan worked out from its species and genes (all from the "genetics" random stream).
        /// </summary>
        public int AddFrog(float x, float z, byte species, Sex sex)
        {
            var sp = SpeciesCatalog.FrogOf(species);
            var rng = Random.Get(GeneticsStream);
            int i = Agents.Add(AgentKind.Frog, x, MovementSystem.SurfaceHeight(Terrain, x, z), z);
            var a = Agents;
            a.Species[i] = sp.Id;
            a.Sex[i] = sex;
            a.Genes[i] = Genome.Wild(rng);
            a.Growth[i] = sp.AdultAt;
            a.Age[i] = sp.AdultAt;
            a.Stage[i] = LifeStage.Adult;
            a.Lifespan[i] = rng.Range(sp.MinLifespan, sp.MaxLifespan) * a.Genes[i].Longevity;
            a.BreedCooldown[i] = rng.Range(60f, 400f); // settling in before thinking about breeding
            return i;
        }

        /// <summary>
        /// Adds a frog egg in the water at (x, z) whose parents are both of one species, with genes
        /// mixed from its parents and no chance of a new mutation. The caller fills in the parents' ids.
        /// </summary>
        public int AddEgg(float x, float z, byte species, Sex sex, in Genome mother, in Genome father) =>
            AddEgg(x, z, species, mother, species, father, sex, 0f);

        /// <summary>
        /// Adds a frog egg in the water at (x, z). Its genes are mixed from its parents, it inherits
        /// their mutations and hybrid mix, and it may get a brand-new mutation (chance from 0 to 1).
        /// The caller fills in the parents' ids.
        /// </summary>
        public int AddEgg(float x, float z, byte motherSpecies, in Genome mother, byte fatherSpecies, in Genome father,
                          Sex sex, float newMutationChance)
        {
            var rng = Random.Get(GeneticsStream);
            var genes = Genome.Inherit(mother, father, rng);
            genes.Alleles = Mutations.Inherit(mother.Alleles, father.Alleles, rng);
            if (newMutationChance > 0f && rng.Chance(newMutationChance))
                genes.Alleles = Mutations.AddRandom(genes.Alleles, rng, out _);
            Lineage.ChildAncestry(motherSpecies, mother, fatherSpecies, father, out byte main, out byte other, out float share);
            genes.HybridSpecies = share > 0f ? other : (byte)0;
            genes.HybridShare = share;
            genes.Generation = Lineage.NextGeneration(Lineage.TypeOf(main, genes), motherSpecies, mother, fatherSpecies, father);

            var sp = SpeciesCatalog.FrogFor(main, genes);
            int i = Agents.Add(AgentKind.Frog, x, MovementSystem.SurfaceHeight(Terrain, x, z), z);
            var a = Agents;
            a.Species[i] = main;
            a.Sex[i] = sex;
            a.Genes[i] = genes;
            a.Stage[i] = LifeStage.Egg;
            a.Lifespan[i] = rng.Range(sp.MinLifespan, sp.MaxLifespan) * genes.Longevity;
            a.Hunger[i] = 0f;
            return i;
        }

        // ---------------------------------------------------------------- mutations, hybrids and new species

        /// <summary>Sandbox switch: makes new mutations 25 times more common, for trying them out. Saved.</summary>
        public bool MutationBoost { get; set; }

        /// <summary>One bit per mutation the player has seen on a froglet in this tank. Saved.</summary>
        public ushort DiscoveredMutations { get; set; }

        /// <summary>One bit per pair of species the player has seen a hybrid of (bit = smaller id × 8 + larger id). Saved.</summary>
        public ulong DiscoveredHybrids { get; set; }

        /// <summary>New species the player has registered and named. Saved.</summary>
        public System.Collections.Generic.List<RegisteredSpecies> Registered { get; } = new System.Collections.Generic.List<RegisteredSpecies>();

        /// <summary>The latest piece of good news ("A froglet with teeth!"), shown for a minute. Not saved.</summary>
        public string News { get; private set; } = "";
        public double NewsTime { get; private set; } = double.NegativeInfinity;
        public float NewsX { get; private set; }
        public float NewsZ { get; private set; }

        /// <summary>
        /// Called when a frog first leaves the water as a froglet: if it shows a mutation or is a hybrid
        /// never seen in this tank before, it is announced as news and remembered.
        /// </summary>
        public void NoteDiscoveries(int i)
        {
            var a = Agents;
            var g = a.Genes[i];
            ushort fresh = (ushort)(g.Visible & ~DiscoveredMutations);
            string name = SpeciesCatalog.FrogName(a.Id[i]);
            if (fresh != 0)
            {
                DiscoveredMutations |= fresh;
                SetNews("New mutation! " + name + " was born with " + Mutations.Names(fresh).ToLowerInvariant() + ".", a.PosX[i], a.PosZ[i]);
            }
            if (g.IsHybrid && g.HybridSpecies != a.Species[i])
            {
                int lo = a.Species[i] < g.HybridSpecies ? a.Species[i] : g.HybridSpecies;
                int hi = a.Species[i] < g.HybridSpecies ? g.HybridSpecies : a.Species[i];
                ulong bit = 1UL << (lo * 8 + hi);
                if ((DiscoveredHybrids & bit) == 0UL)
                {
                    DiscoveredHybrids |= bit;
                    SetNews("First hybrid! " + name + " is a " + SpeciesCatalog.FrogOf(a, i).Name + ".", a.PosX[i], a.PosZ[i]);
                }
            }
        }

        private void SetNews(string text, float x, float z)
        {
            News = text;
            NewsTime = SimTime;
            NewsX = x;
            NewsZ = z;
        }

        /// <summary>The registered species a frog belongs to, or null.</summary>
        public RegisteredSpecies RegisteredFor(int i)
        {
            int type = Lineage.TypeOf(Agents, i);
            foreach (var r in Registered) if (r.Type == type) return r;
            return null;
        }

        /// <summary>
        /// The name to show for a frog's kind: its registered species name if it has one, otherwise its
        /// species (or hybrid) name.
        /// </summary>
        public string KindName(int i)
        {
            var r = RegisteredFor(i);
            return r != null ? r.Name : SpeciesCatalog.FrogOf(Agents, i).Name;
        }

        /// <summary>
        /// New types of frog in the tank that have bred true for long enough to be registered as a
        /// species and are not registered yet. Each type is listed once.
        /// </summary>
        public System.Collections.Generic.List<int> TypesReadyToRegister()
        {
            var list = new System.Collections.Generic.List<int>();
            var a = Agents;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog || a.Genes[i].Generation < Lineage.GenerationsToRegister) continue;
                int type = Lineage.TypeOf(a, i);
                if (!Lineage.IsNewType(type) || list.Contains(type) || IsRegistered(type)) continue;
                list.Add(type);
            }
            return list;
        }

        public bool IsRegistered(int type)
        {
            foreach (var r in Registered) if (r.Type == type) return true;
            return false;
        }

        /// <summary>
        /// Registers a type of frog as a new species with the player's name for it. Refused (returns
        /// false) if the type is not ready, is already registered, or the name is empty or taken.
        /// </summary>
        public bool Register(int type, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > MaxSpeciesNameLength) return false;
            if (IsRegistered(type) || !TypesReadyToRegister().Contains(type)) return false;
            foreach (var r in Registered) if (string.Equals(r.Name, name, System.StringComparison.OrdinalIgnoreCase)) return false;
            foreach (var sp in SpeciesCatalog.Frogs) if (string.Equals(sp.Name, name, System.StringComparison.OrdinalIgnoreCase)) return false;
            Registered.Add(new RegisteredSpecies { Type = type, Name = name, Time = SimTime });
            SetNews("New species registered: " + name + "!", 0f, 0f);
            return true;
        }

        public const int MaxSpeciesNameLength = 40;

        /// <summary>Frogs of a type in the tank right now, counting eggs and tadpoles.</summary>
        public int CountOfType(int type)
        {
            var a = Agents;
            int n = 0;
            for (int i = 0; i < a.Count; i++) if (a.Kind[i] == AgentKind.Frog && Lineage.TypeOf(a, i) == type) n++;
            return n;
        }

        /// <summary>Name of the random stream used for genes, sexes and lifespans.</summary>
        public const string GeneticsStream = "Genetics";

        /// <summary>
        /// How many frogs the tank can support, from its size: one per 15 square metres of floor.
        /// Eggs and tadpoles count too, because they will soon be frogs. Frogs stop breeding when
        /// the tank is full, so populations level off instead of exploding.
        /// </summary>
        public int FrogCapacity => (int)(4f * HalfSizeX * HalfSizeZ / 15f);

        // ---------------------------------------------------------------- population controls

        /// <summary>When false, no frog pairs up or lays eggs. Saved.</summary>
        public bool BreedingEnabled { get; set; } = true;

        /// <summary>
        /// The most frogs (counting eggs and tadpoles) the player wants in the tank. 0 means "as many
        /// as the tank can support". It can only lower the tank's own capacity. Saved.
        /// </summary>
        public int BreedingLimit { get; set; }

        /// <summary>The number of frogs breeding stops at: the player's limit or the tank's capacity, whichever is lower.</summary>
        public int FrogLimit => BreedingLimit > 0 && BreedingLimit < FrogCapacity ? BreedingLimit : FrogCapacity;

        /// <summary>Frogs given away to new homes so far. Saved.</summary>
        public int Rehomed { get; set; }

        /// <summary>Eggs taken out of the water so far. Saved.</summary>
        public int EggsCollected { get; set; }

        /// <summary>
        /// Gives a frog away to a new home (it leaves the tank for good). Anything it was carrying
        /// goes with it. Returns false if the index is not a frog.
        /// </summary>
        public bool Rehome(int index)
        {
            if (index < 0 || index >= Agents.Count || Agents.Kind[index] != AgentKind.Frog) return false;
            Agents.RemoveAt(index);
            Rehomed++;
            return true;
        }

        /// <summary>Takes every frog egg out of the tank. Returns how many were collected.</summary>
        public int CollectEggs()
        {
            var a = Agents;
            int n = 0;
            for (int i = a.Count - 1; i >= 0; i--)
            {
                if (a.Kind[i] != AgentKind.Frog || a.Stage[i] != LifeStage.Egg) continue;
                a.RemoveAt(i);
                n++;
            }
            EggsCollected += n;
            return n;
        }

        /// <summary>Frogs in the tank, counting eggs and tadpoles.</summary>
        public int FrogCount()
        {
            var a = Agents;
            int n = 0;
            for (int i = 0; i < a.Count; i++) if (a.Kind[i] == AgentKind.Frog) n++;
            return n;
        }

        /// <summary>
        /// Rehomes frogs until the tank is down to its frog limit: elders first, then the oldest
        /// adults. It never takes the last adult female or the last adult male of a species, so
        /// every species can still breed. Young frogs are never rehomed. Returns how many went.
        /// </summary>
        public int RehomeExtras()
        {
            var a = Agents;
            int over = FrogCount() - FrogLimit;
            int n = 0;
            while (over > 0)
            {
                int pick = -1;
                bool pickElder = false;
                for (int i = 0; i < a.Count; i++)
                {
                    if (a.Kind[i] != AgentKind.Frog) continue;
                    bool elder = a.Stage[i] == LifeStage.Elder;
                    if (!elder && a.Stage[i] != LifeStage.Adult) continue;
                    if (!elder && AdultsLike(i) <= 1) continue;
                    bool better = pick < 0 || (elder && !pickElder) || (elder == pickElder && a.Age[i] > a.Age[pick]);
                    if (better) { pick = i; pickElder = elder; }
                }
                if (pick < 0) break;
                Rehome(pick);
                n++;
                over--;
            }
            return n;
        }

        /// <summary>How many breeding adults share this frog's species and sex (itself included).</summary>
        private int AdultsLike(int index)
        {
            var a = Agents;
            int n = 0;
            for (int i = 0; i < a.Count; i++)
                if (a.Kind[i] == AgentKind.Frog && a.Stage[i] == LifeStage.Adult && a.Species[i] == a.Species[index] && a.Sex[i] == a.Sex[index]) n++;
            return n;
        }

        // ---------------------------------------------------------------- level of detail (not saved)

        /// <summary>
        /// Where the player is looking. Agents further than <see cref="FocusRadius"/> from here think
        /// less often, which saves time in a big tank. Set by the game each frame; not saved.
        /// </summary>
        public bool HasFocus { get; set; }
        public float FocusX { get; set; }
        public float FocusZ { get; set; }
        public float FocusRadius { get; set; } = 6f;

        /// <summary>Takes a frog out of the tank because it climbed out, remembering it so it can be brought back.</summary>
        public void Escape(int index, string reason)
        {
            var a = Agents;
            Escaped.Add(new EscapedFrog
            {
                Id = a.Id[index],
                Species = a.Species[index],
                Sex = a.Sex[index],
                Age = a.Age[index],
                Growth = a.Growth[index],
                Lifespan = a.Lifespan[index],
                MotherId = a.MotherId[index],
                FatherId = a.FatherId[index],
                Genes = a.Genes[index],
                Reason = reason ?? "",
                Time = SimTime,
            });
        }

        /// <summary>
        /// Puts every escaped frog back in the tank on dry ground near the middle. They come back a
        /// little hungry, thirsty and still somewhat stressed, so fix what upset them first.
        /// Returns how many came back (none if the tank is at its agent limit).
        /// </summary>
        public int BringBackEscapedFrogs()
        {
            var rng = Random.Get("spawn");
            int n = 0;
            while (Escaped.Count > 0 && Agents.Count < MaxAgents)
            {
                var e = Escaped[0];
                Escaped.RemoveAt(0);
                FindDryGround(rng, -HalfSizeX * 0.3f, HalfSizeX * 0.3f, -HalfSizeZ * 0.3f, HalfSizeZ * 0.3f, out float x, out float z);
                var a = Agents;
                int i = a.Add(AgentKind.Frog, x, MovementSystem.SurfaceHeight(Terrain, x, z), z);
                a.SetId(i, e.Id);
                a.Species[i] = e.Species;
                a.Sex[i] = e.Sex;
                a.Age[i] = e.Age;
                a.Growth[i] = e.Growth;
                a.Lifespan[i] = e.Lifespan;
                a.Stage[i] = SpeciesCatalog.FrogFor(e.Species, e.Genes).StageFor(e.Growth, e.Age);
                a.MotherId[i] = e.MotherId;
                a.FatherId[i] = e.FatherId;
                a.Genes[i] = e.Genes;
                a.Hunger[i] = 0.5f; a.Thirst[i] = 0.5f;
                a.Comfort[i] = 0.5f; a.Stress[i] = 0.4f;
                a.BreedCooldown[i] = 120f;
                n++;
            }
            return n;
        }

        /// <summary>Adds a newborn cricket at (x, z) with a random lifespan.</summary>
        public int AddInsect(float x, float z, SeededRandom rng)
        {
            var sp = SpeciesCatalog.Insect;
            int i = Agents.Add(AgentKind.Insect, x, MovementSystem.SurfaceHeight(Terrain, x, z), z);
            Agents.Lifespan[i] = rng.Range(sp.MinLifespan, sp.MaxLifespan);
            return i;
        }
    }

    /// <summary>A frog that climbed out of the tank, remembered so the player can bring it back. Saved.</summary>
    public sealed class EscapedFrog
    {
        public int Id;
        public byte Species;
        public Sex Sex;
        public float Age, Growth, Lifespan;
        public int MotherId, FatherId;
        public Genome Genes;
        /// <summary>What drove it out, in plain words ("too cold", "starving").</summary>
        public string Reason;
        /// <summary>Simulated time when it escaped.</summary>
        public double Time;
    }
}

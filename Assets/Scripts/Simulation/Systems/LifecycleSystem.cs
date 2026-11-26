namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Ageing, growing up, death and breeding, once per second.
    ///
    /// Crickets live 8 to 12 minutes. Adults lay young on soil or moss when fewer than five other
    /// crickets are within a metre and the tank is below its carrying capacity (0.35 crickets per
    /// square metre of dry ground). So a few crickets slowly become a colony, and a colony stops
    /// growing on its own instead of exploding.
    ///
    /// Frogs go egg, tadpole, froglet, juvenile, adult, elder. Young frogs grow at full speed only
    /// when they are fed and comfortable, so a cold or hungry tank holds them back. Eggs and
    /// tadpoles need water: if the pool is drained under them they die. A healthy, calm adult female
    /// with a willing male of her species within calling range may pair up; she then heads for water and lays a
    /// clutch of eggs there. Each egg remembers both parents and mixes their genes. Breeding stops
    /// while the tank is at its frog capacity (eggs and tadpoles count) or food is short (fewer than
    /// four insects per frog), and the player can switch breeding off or set a lower frog limit.
    /// Frogs die of old age at the end of their lifespan.
    ///
    /// Adult males of territorial species claim a patch of their own (see BrainSystem for how they
    /// defend it).
    ///
    /// Phase 3: a female with no male of her own species in range may pair with a male of a related
    /// species (same genus) instead, and their eggs are hybrids. Hybrids are less fertile. Each egg
    /// inherits its parents' mutations and has a small chance of a brand-new one, higher when the
    /// mother is stressed, the parents are hybrids, or the sandbox mutation boost is on (see
    /// <see cref="Lineage"/> and <see cref="Mutations"/>).
    /// </summary>
    public sealed class LifecycleSystem : ISimSystem
    {
        public const int Rate = 1;
        /// <summary>
        /// A ready female pairs with a ready male within this distance (metres). Male frogs call to
        /// attract females, so she can hear one from across a normal-sized tank. Each species has its
        /// own call range (<see cref="FrogSpecies.CallRange"/>); this is the usual one.
        /// </summary>
        public const float MatingRange = 8f;
        /// <summary>Chance per second that a ready female pairs with a nearby male (times her fertility).</summary>
        public const float PairChancePerSecond = 1f / 40f;
        /// <summary>A female who cannot find water within this many seconds reabsorbs her eggs.</summary>
        public const float GravidGiveUpSeconds = 600f;
        /// <summary>Frogs only pair up while there are at least this many insects per frog in the tank.</summary>
        public const int InsectsPerFrogToBreed = 4;
        /// <summary>Room (in frogs) a female sets aside for her clutch when she pairs up.</summary>
        private const int Reserve = 3;

        public string Name => "Lifecycle";
        public int TicksPerSecond => Rate;

        /// <summary>Crickets born since the simulation started.</summary>
        public int Births { get; private set; }
        /// <summary>Agents (crickets and frogs) that died of old age or lost their water.</summary>
        public int Deaths { get; private set; }
        public int EggsLaid { get; private set; }
        public int Hatched { get; private set; }
        /// <summary>Territories claimed by males since the simulation started.</summary>
        public int TerritoriesClaimed { get; private set; }
        /// <summary>Pairs of different (related) species formed since the simulation started.</summary>
        public int CrossPairings { get; private set; }
        /// <summary>Eggs that got a brand-new mutation since the simulation started.</summary>
        public int NewMutations { get; private set; }

        private readonly SpatialHash _hash = new SpatialHash();
        private readonly System.Func<AgentStore, int, bool> _readyMale;
        private byte _wantSpecies;
        private bool _acceptRelated;

        public LifecycleSystem()
        {
            _readyMale = IsReadyMale;
        }

        /// <summary>How many crickets the tank can support right now.</summary>
        public static int InsectCapacity(World world)
        {
            var env = world.Environment;
            env.RefreshFromTerrain(world.Terrain);
            float dry = 0f;
            for (int i = 0; i < env.Water.Length; i++) dry += 1f - env.Water[i];
            float area = dry * env.CellSize * env.CellSize;
            return (int)(area * SpeciesCatalog.Insect.CapacityPerSquareMetre);
        }

        public void Tick(World world, float dt)
        {
            var a = world.Agents;
            var rng = world.Random.Get(Name);

            for (int i = a.Count - 1; i >= 0; i--)
            {
                a.Age[i] += dt;
                bool dies = a.Lifespan[i] > 0f && a.Age[i] >= a.Lifespan[i]
                            && !(a.Kind[i] == AgentKind.Frog && world.Has(SandboxRules.NoOldAge));
                if (!dies && a.Kind[i] == AgentKind.Frog) dies = GrowFrog(world, i, dt, rng);
                if (dies)
                {
                    a.RemoveAt(i);
                    Deaths++;
                }
            }

            _hash.Build(a, world.HalfSizeX, world.HalfSizeZ);
            ClaimTerritories(world);
            BreedFrogs(world, dt, rng);
            BreedInsects(world, dt, rng);
        }

        /// <summary>Growth, stage changes and breeding timers for one frog. Returns true if it dies.</summary>
        private bool GrowFrog(World world, int i, float dt, SeededRandom rng)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.FrogOf(a, i);
            var stage = a.Stage[i];
            if (a.BreedCooldown[i] > 0f) a.BreedCooldown[i] -= dt;

            bool inWater = world.Terrain.IsWater(a.PosX[i], a.PosZ[i]);
            float rate;
            if (stage <= LifeStage.Tadpole)
            {
                // Eggs and tadpoles dry out and die out of water (for example if the pool is drained).
                if (!inWater) return rng.Chance((stage == LifeStage.Egg ? 0.15f : 0.2f) * dt);
                if (stage == LifeStage.Tadpole && rng.Chance(dt / 2400f)) return true; // not every tadpole makes it
                float warmth = sp.ComfortFor(world.Environment.TemperatureAt(a.PosX[i], a.PosZ[i]),
                                             0.5f * (sp.MinHumidity + sp.MaxHumidity), a.Genes[i]);
                rate = 0.4f + 0.6f * warmth;
            }
            else if (a.Growth[i] < sp.AdultAt)
            {
                rate = (1f - 0.75f * a.Hunger[i]) * (0.3f + 0.7f * a.Comfort[i]);
            }
            else rate = 0f;

            if (rate > 0f)
            {
                a.Growth[i] += rate * dt;
                if (a.Growth[i] > sp.AdultAt) a.Growth[i] = sp.AdultAt;
            }
            var next = sp.StageFor(a.Growth[i], a.Age[i]);
            if (next != stage)
            {
                a.Stage[i] = next;
                if (stage == LifeStage.Egg) Hatched++;
                if (next == LifeStage.Froglet)
                {
                    // Out of the water for the first time: a little peckish, not thirsty.
                    a.Hunger[i] = 0.3f; a.Thirst[i] = 0f; a.Fatigue[i] = 0.1f; a.Comfort[i] = 1f;
                    a.Goal[i] = Goal.Idle; a.GoalTime[i] = 0f; a.Timer[i] = 0f;
                }
                if (next == LifeStage.Froglet) world.NoteDiscoveries(i);
                if (next == LifeStage.Elder && a.MateId[i] != 0) { a.MateId[i] = 0; a.Gravid[i] = 0f; }
            }
            return false;
        }

        /// <summary>
        /// Adult males of territorial species claim the spot they are sitting on as their patch, if
        /// it is dry, comfortable and not inside another male's patch. Elders give theirs up.
        /// </summary>
        private void ClaimTerritories(World world)
        {
            var a = world.Agents;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog || !a.HasHome[i] && (a.Sex[i] != Sex.Male || a.Stage[i] != LifeStage.Adult)) continue;
                var sp = SpeciesCatalog.FrogOf(a, i);
                if (a.HasHome[i])
                {
                    if (a.Stage[i] != LifeStage.Adult || sp.TerritoryRadius <= 0f) a.HasHome[i] = false;
                    continue;
                }
                if (sp.TerritoryRadius <= 0f || a.Comfort[i] < 0.6f || a.Retreat[i] > 0f) continue;
                float x = a.PosX[i], z = a.PosZ[i];
                if (world.Terrain.IsWater(x, z) || HomeNear(a, i, x, z, 2f * sp.TerritoryRadius)) continue;
                a.HasHome[i] = true;
                a.HomeX[i] = x;
                a.HomeZ[i] = z;
                TerritoriesClaimed++;
            }
        }

        /// <summary>True if another frog of the same species has a home within this distance.</summary>
        private static bool HomeNear(AgentStore a, int self, float x, float z, float distance)
        {
            for (int j = 0; j < a.Count; j++)
            {
                if (j == self || !a.HasHome[j] || a.Species[j] != a.Species[self]) continue;
                float dx = a.HomeX[j] - x, dz = a.HomeZ[j] - z;
                if (dx * dx + dz * dz < distance * distance) return true;
            }
            return false;
        }

        private void BreedFrogs(World world, float dt, SeededRandom rng)
        {
            var a = world.Agents;
            int frogs = 0, insects = 0, carrying = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) { insects++; continue; }
                frogs++;
                if (a.MateId[i] != 0) carrying++;
            }
            int room = world.FrogLimit - frogs - carrying * Reserve;
            // Frogs only pair up when there is plenty of food about: at least this many insects per frog.
            bool foodForMore = insects >= InsectsPerFrogToBreed * frogs;
            int count = a.Count;
            for (int i = 0; i < count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog || a.Sex[i] != Sex.Female || a.Stage[i] != LifeStage.Adult) continue;
                var sp = SpeciesCatalog.FrogOf(a, i);

                if (a.MateId[i] != 0 && !world.BreedingEnabled)
                {
                    // The player switched breeding off: she reabsorbs her eggs.
                    a.MateId[i] = 0;
                    a.Gravid[i] = 0f;
                    continue;
                }
                if (a.MateId[i] != 0)
                {
                    a.Gravid[i] += dt;
                    if (world.Terrain.IsWater(a.PosX[i], a.PosZ[i]))
                    {
                        // She already set aside room for a clutch when she paired up.
                        int laid = LayEggs(world, i, sp, room + Reserve, rng);
                        room -= laid - Reserve;
                        a.MateId[i] = 0;
                        a.Gravid[i] = 0f;
                        a.BreedCooldown[i] = sp.BreedCooldown / a.Genes[i].Fertility;
                    }
                    else if (a.Gravid[i] > GravidGiveUpSeconds)
                    {
                        a.MateId[i] = 0;
                        a.Gravid[i] = 0f;
                        a.BreedCooldown[i] = sp.BreedCooldown * 0.5f;
                    }
                    continue;
                }

                if (!world.BreedingEnabled || room < Reserve || !foodForMore || a.Count >= World.MaxAgents) continue;
                if (!ReadyToBreed(a, i, 0.5f, 0.6f, 0.65f, 0.3f)) continue;
                if (!rng.Chance(PairChancePerSecond * a.Genes[i].Fertility * Lineage.HybridFertility(a.Genes[i]) * dt)) continue;
                _wantSpecies = a.Species[i];
                _acceptRelated = false;
                // Males call; a male with the Loud call mutation is heard further away.
                float range = sp.CallRange * 1.6f;
                int male = FindMale(a, i, sp.CallRange, range);
                if (male < 0 && rng.Chance(Lineage.CrossPairChance))
                {
                    // No male of her own kind about: she may settle for a related species.
                    _acceptRelated = true;
                    male = FindMale(a, i, sp.CallRange, range);
                }
                if (male < 0) continue;
                if (a.Species[male] != a.Species[i]) CrossPairings++;
                a.MateId[i] = a.Id[male];
                a.MateGenes[i] = a.Genes[male];
                a.MateSpecies[i] = a.Species[male];
                a.Gravid[i] = 0f;
                a.BreedCooldown[male] = sp.BreedCooldown * 0.5f;
                room -= Reserve; // keep room for the clutch she is now carrying
            }
        }

        /// <summary>
        /// The nearest ready male she can hear: within her species' normal call range, or within the
        /// longer range for a male with the Loud call mutation. -1 if none.
        /// </summary>
        private int FindMale(AgentStore a, int female, float normalRange, float loudRange)
        {
            int male = _hash.FindNearest(a, a.PosX[female], a.PosZ[female], loudRange, AgentKind.Frog, female, null, _readyMale);
            if (male < 0) return -1;
            float dx = a.PosX[male] - a.PosX[female], dz = a.PosZ[male] - a.PosZ[female];
            float heard = SpeciesCatalog.FrogOf(a, male).CallRange;
            if (heard < normalRange) heard = normalRange;
            return dx * dx + dz * dz <= heard * heard ? male : -1;
        }

        private bool IsReadyMale(AgentStore a, int i) =>
            a.Sex[i] == Sex.Male && a.Stage[i] == LifeStage.Adult &&
            (a.Species[i] == _wantSpecies || _acceptRelated && SpeciesCatalog.CanInterbreed(a.Species[i], _wantSpecies)) &&
            ReadyToBreed(a, i, 0.6f, 0.7f, 0.6f, 0.4f);

        private static bool ReadyToBreed(AgentStore a, int i, float maxHunger, float maxThirst, float minComfort, float maxStress) =>
            a.BreedCooldown[i] <= 0f && a.Hunger[i] < maxHunger && a.Thirst[i] < maxThirst && a.Comfort[i] >= minComfort && a.Stress[i] < maxStress;

        /// <summary>
        /// Lays a clutch around the female, in the water. Hybrid parents lay fewer eggs. Returns how
        /// many eggs were laid.
        /// </summary>
        private int LayEggs(World world, int mother, FrogSpecies sp, int room, SeededRandom rng)
        {
            var a = world.Agents;
            var motherGenes = a.Genes[mother];
            var fatherGenes = a.MateGenes[mother];
            byte species = a.Species[mother];
            byte fatherSpecies = a.MateSpecies[mother];
            if (!SpeciesCatalog.CanInterbreed(species, fatherSpecies)) fatherSpecies = species; // damaged data: treat as her own kind
            float fertility = motherGenes.Fertility * Lineage.HybridFertility(motherGenes) * Lineage.HybridFertility(fatherGenes);
            int n = (int)(rng.Range(sp.ClutchMin, sp.ClutchMax + 1) * fertility + 0.5f);
            if (n > room) n = room;
            if (n < 0) n = 0;
            if (n > World.MaxAgents - a.Count) n = World.MaxAgents - a.Count;
            float mx = a.PosX[mother], mz = a.PosZ[mother];
            int motherId = a.Id[mother], fatherId = a.MateId[mother];
            bool hybridParents = motherGenes.IsHybrid || fatherGenes.IsHybrid || fatherSpecies != species;
            float mutationChance = Mutations.ChancePerEgg(a.Stress[mother], hybridParents, world.MutationBoost);
            // Alternate the sexes within a clutch, so a small family never ends up all one sex.
            bool firstFemale = rng.Chance(0.5f);
            for (int k = 0; k < n; k++)
            {
                float x = mx, z = mz;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    float tx = mx + rng.Range(-0.35f, 0.35f), tz = mz + rng.Range(-0.35f, 0.35f);
                    if (world.Terrain.Contains(tx, tz) && world.Terrain.IsWater(tx, tz)) { x = tx; z = tz; break; }
                }
                var sex = (k % 2 == 0) == firstFemale ? Sex.Female : Sex.Male;
                int e = world.AddEgg(x, z, species, motherGenes, fatherSpecies, fatherGenes, sex, mutationChance);
                if (NewMutationIn(a.Genes[e].Alleles, motherGenes.Alleles, fatherGenes.Alleles)) NewMutations++;
                a.MotherId[e] = motherId;
                a.FatherId[e] = fatherId;
            }
            EggsLaid += n;
            return n;
        }

        /// <summary>True if the child carries a mutation neither parent had.</summary>
        private static bool NewMutationIn(uint child, uint mother, uint father)
        {
            for (int m = 0; m < Mutations.Count; m++)
                if (Mutations.Copies(child, (Mutation)m) > 0 && Mutations.Copies(mother, (Mutation)m) == 0 && Mutations.Copies(father, (Mutation)m) == 0)
                    return true;
            return false;
        }

        private void BreedInsects(World world, float dt, SeededRandom rng)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.Insect;
            int insects = a.CountOf(AgentKind.Insect);
            int capacity = InsectCapacity(world);
            if (insects >= capacity || a.Count >= World.MaxAgents) return;

            int count = a.Count;
            for (int i = 0; i < count && insects < capacity && a.Count < World.MaxAgents; i++)
            {
                if (a.Kind[i] != AgentKind.Insect || a.Age[i] < sp.AdultAge) continue;
                if (!rng.Chance(sp.BreedChancePerSecond * dt)) continue;
                var ground = world.Terrain.LayerAt(a.PosX[i], a.PosZ[i]);
                if (ground != GroundLayer.Soil && ground != GroundLayer.Moss) continue;
                if (_hash.CountWithin(a, a.PosX[i], a.PosZ[i], 1f, AgentKind.Insect, i) >= sp.CrowdedNeighbours) continue;

                float px = a.PosX[i], pz = a.PosZ[i];
                world.FindDryGround(rng, px - 0.4f, px + 0.4f, pz - 0.4f, pz + 0.4f, out float x, out float z);
                world.AddInsect(x, z, rng);
                insects++;
                Births++;
            }
        }
    }

    /// <summary>
    /// Runs the feeders twice per second: each releases a few live crickets on its timer while
    /// it has supply left. The player refills it from its inspector.
    /// </summary>
    public sealed class FeederSystem : ISimSystem
    {
        public const int Rate = 2;

        public string Name => "Feeders";
        public int TicksPerSecond => Rate;

        public int Released { get; private set; }

        public void Tick(World world, float dt)
        {
            SeededRandom rng = null;
            foreach (var d in world.Devices.All)
            {
                if (d.Kind != DeviceKind.Feeder) continue;
                if (world.Has(SandboxRules.EndlessFeeders)) d.Supply = DeviceStore.FeederCapacity;
                d.Active = d.Enabled && d.Supply >= 1f;
                if (!d.Active) continue;
                d.Timer -= dt;
                if (d.Timer > 0f) continue;
                d.Timer += d.Setting;
                if (d.Timer <= 0f) d.Timer = d.Setting;
                // Sensor and Smart feeders check on their timer and release only when crickets are short.
                if (d.Mode != DeviceMode.Basic && !FoodShort(world, d.Target)) continue;

                if (rng == null) rng = world.Random.Get(Name);
                int n = (int)System.Math.Min(d.Setting2, d.Supply);
                for (int k = 0; k < n && world.Agents.Count < World.MaxAgents; k++)
                {
                    world.FindDryGround(rng, d.X - 0.6f, d.X + 0.6f, d.Z - 0.6f, d.Z + 0.6f, out float x, out float z);
                    int i = world.AddInsect(x, z, rng);
                    world.Agents.Age[i] = SpeciesCatalog.Insect.AdultAge; // feeder crickets come grown up
                    d.Supply -= 1f;
                    Released++;
                }
                d.Active = d.Supply >= 1f;
            }
        }

        /// <summary>True if the tank has fewer than perFrog crickets for each land frog.</summary>
        public static bool FoodShort(World world, float perFrog)
        {
            var a = world.Agents;
            int frogs = 0, insects = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Insect) insects++;
                else if (a.IsLandFrog(i)) frogs++;
            }
            return frogs > 0 && insects < perFrog * frogs;
        }
    }
}

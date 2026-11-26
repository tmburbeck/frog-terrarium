using System;
using System.IO;
using System.Text;

namespace FrogTerrarium.Simulation
{
    /// <summary>Thrown when a save file cannot be read.</summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Versioned save and load for the whole World.
    ///
    /// File layout: 4-byte magic "FTSV", then a 32-bit format version, then the body.
    /// Rules for changing the format later (see docs/SAVE_FORMAT.md):
    ///   1. Never change how an old version is read. Add a new version instead.
    ///   2. Bump CurrentVersion, write the new layout in Write, and add a
    ///      ReadVn method plus a case in Read for the new number.
    ///   3. Keep a sample save of every old version under Assets/Tests/SaveSamples
    ///      so the tests prove old saves still load.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 9;
        private static readonly byte[] Magic = { (byte)'F', (byte)'T', (byte)'S', (byte)'V' };

        public static void Write(Stream stream, World world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            using (var w = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                w.Write(Magic);
                w.Write(CurrentVersion);

                w.Write(world.Seed);
                w.Write(world.HalfSizeX);
                w.Write(world.HalfSizeZ);
                w.Write(world.SimTime);

                int streamCount = 0;
                foreach (var _ in world.Random.Streams) streamCount++;
                w.Write(streamCount);
                foreach (var kv in world.Random.Streams)
                {
                    w.Write(kv.Key);
                    w.Write(kv.Value.State);
                    w.Write(kv.Value.Increment);
                }

                var a = world.Agents;
                w.Write(a.NextId);
                w.Write(a.Count);
                for (int i = 0; i < a.Count; i++)
                {
                    w.Write(a.Id[i]);
                    w.Write((byte)a.Kind[i]);
                    w.Write(a.PosX[i]); w.Write(a.PosY[i]); w.Write(a.PosZ[i]);
                    w.Write(a.VelX[i]); w.Write(a.VelZ[i]);
                    w.Write(a.Hunger[i]); w.Write(a.Thirst[i]); w.Write(a.Fatigue[i]);
                    // Version 3 additions to each agent: comfort and brain state, age.
                    w.Write(a.Comfort[i]);
                    w.Write((byte)a.Goal[i]);
                    w.Write(a.GoalTime[i]);
                    w.Write(a.TargetId[i]);
                    w.Write(a.TargetX[i]); w.Write(a.TargetZ[i]);
                    w.Write(a.Age[i]); w.Write(a.Lifespan[i]);
                    w.Write(a.Timer[i]); w.Write(a.Cooldown[i]);
                    // Version 4 additions to each agent: species, life stage, family, stress and genes.
                    w.Write(a.Species[i]);
                    w.Write((byte)a.Sex[i]);
                    w.Write((byte)a.Stage[i]);
                    w.Write(a.Growth[i]);
                    w.Write(a.Stress[i]);
                    w.Write(a.MotherId[i]); w.Write(a.FatherId[i]); w.Write(a.MateId[i]);
                    w.Write(a.Gravid[i]); w.Write(a.BreedCooldown[i]);
                    a.Genes[i].Write(w);
                    a.MateGenes[i].Write(w);
                    // Version 5 additions to each agent: loneliness and territory.
                    w.Write(a.Social[i]);
                    w.Write(a.Retreat[i]);
                    w.Write(a.HasHome[i]);
                    w.Write(a.HomeX[i]); w.Write(a.HomeZ[i]);
                    // Version 6 addition to each agent: the species of the father of the eggs she carries.
                    // (Version 6 genes also carry mutations, hybrid mix and generation; see Genome.Write.)
                    w.Write(a.MateSpecies[i]);
                }

                // Version 2 additions: terrain, environment, devices.
                var t = world.Terrain;
                w.Write(t.PointsX);
                w.Write(t.PointsZ);
                w.Write(t.CellSize);
                w.Write(t.WaterLevel);
                for (int i = 0; i < t.Height.Length; i++) w.Write(t.Height[i]);
                for (int i = 0; i < t.Layer.Length; i++) w.Write((byte)t.Layer[i]);

                var env = world.Environment;
                w.Write(env.AmbientTemperature);
                w.Write(env.AmbientHumidity);
                w.Write(env.CellsX);
                w.Write(env.CellsZ);
                for (int i = 0; i < env.Temperature.Length; i++) w.Write(env.Temperature[i]);
                for (int i = 0; i < env.Humidity.Length; i++) w.Write(env.Humidity[i]);

                w.Write(world.Devices.NextId);
                w.Write(world.Devices.Count);
                foreach (var d in world.Devices.All)
                {
                    w.Write(d.Id);
                    w.Write((byte)d.Kind);
                    w.Write(d.X); w.Write(d.Z);
                    w.Write(d.Enabled);
                    w.Write(d.Setting); w.Write(d.Setting2);
                    w.Write(d.Supply); w.Write(d.Timer);
                    w.Write(d.Active);
                }

                // Version 4 addition: frogs that escaped and can be brought back.
                w.Write(world.Escaped.Count);
                foreach (var e in world.Escaped)
                {
                    w.Write(e.Id);
                    w.Write(e.Species);
                    w.Write((byte)e.Sex);
                    w.Write(e.Age); w.Write(e.Growth); w.Write(e.Lifespan);
                    w.Write(e.MotherId); w.Write(e.FatherId);
                    e.Genes.Write(w);
                    w.Write(e.Reason ?? "");
                    w.Write(e.Time);
                }

                // Version 5 addition: population controls.
                w.Write(world.BreedingEnabled);
                w.Write(world.BreedingLimit);
                w.Write(world.Rehomed);
                w.Write(world.EggsCollected);

                // Version 6 addition: mutations, hybrids and registered species.
                w.Write(world.MutationBoost);
                w.Write(world.DiscoveredMutations);
                w.Write(world.DiscoveredHybrids);
                w.Write(world.Registered.Count);
                foreach (var reg in world.Registered)
                {
                    w.Write(reg.Type);
                    w.Write(reg.Name ?? "");
                    w.Write(reg.Time);
                }

                // Version 7 addition: building and decoration parts.
                w.Write(world.Parts.NextId);
                w.Write(world.Parts.Count);
                foreach (var part in world.Parts.All)
                {
                    w.Write(part.Id);
                    w.Write((byte)part.Kind);
                    w.Write(part.X); w.Write(part.Z);
                    w.Write(part.Rotation);
                    w.Write(part.Scale);
                    w.Write(part.R); w.Write(part.G); w.Write(part.B);
                    w.Write(part.OnPartId);
                }

                // Version 8 addition: money mode.
                var eco = world.Economy;
                w.Write(eco.Enabled);
                w.Write(eco.Money);
                w.Write(eco.Reputation);
                w.Write(eco.ResearchPoints);
                w.Write(eco.Researched);
                w.Write(eco.Visitors);
                w.Write(eco.Sold);
                w.Write(eco.EarnedVisitors); w.Write(eco.EarnedSales); w.Write(eco.EarnedContracts);
                w.Write(eco.SpentPower); w.Write(eco.SpentUpkeep); w.Write(eco.SpentShop);
                w.Write(eco.NextContractId);
                w.Write(eco.ContractTimer);
                w.Write(eco.VisitorCarry);
                w.Write(eco.Contracts.Count);
                foreach (var c in eco.Contracts)
                {
                    w.Write(c.Id);
                    w.Write((byte)c.Kind);
                    w.Write(c.A); w.Write(c.B);
                    w.Write(c.Type);
                    w.Write(c.Needed); w.Write(c.Delivered);
                    w.Write(c.Reward);
                    w.Write(c.Deadline);
                    w.Write(c.Buyer ?? "");
                }

                // Version 9 addition: the style set and the style sets bought.
                w.Write(world.Style);
                w.Write(eco.StylesOwned);
            }
        }

        public static World Read(Stream stream)
        {
            using (var r = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                byte[] magic = r.ReadBytes(Magic.Length);
                if (magic.Length != Magic.Length || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                    throw new SaveFormatException("This is not a Frog Terrarium save file.");

                int version = r.ReadInt32();
                try
                {
                    switch (version)
                    {
                        case 1: return ReadV1(r);
                        case 2: return ReadV2(r);
                        case 3: return ReadV3(r);
                        case 4: return ReadV4(r);
                        case 5: return ReadV5(r);
                        case 6: return ReadV6(r);
                        case 7: return ReadV7(r);
                        case 8: return ReadV8(r);
                        case 9: return ReadV9(r);
                        default:
                            if (version > CurrentVersion)
                                throw new SaveFormatException($"Save is version {version}, made by a newer game. This game reads up to version {CurrentVersion}.");
                            throw new SaveFormatException($"Unknown save version {version}.");
                    }
                }
                catch (EndOfStreamException)
                {
                    throw new SaveFormatException("Save file is cut short or damaged.");
                }
            }
        }

        public static byte[] ToBytes(World world)
        {
            using (var ms = new MemoryStream())
            {
                Write(ms, world);
                return ms.ToArray();
            }
        }

        public static World FromBytes(byte[] data)
        {
            using (var ms = new MemoryStream(data, writable: false)) return Read(ms);
        }

        /// <summary>Writes to a temporary file first, then swaps it in, so a crash mid-save never destroys the old save.</summary>
        public static void WriteFile(string path, World world)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, ToBytes(world));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public static World ReadFile(string path) => FromBytes(File.ReadAllBytes(path));

        /// <summary>
        /// Version 1: seed, tank size, clock, random streams and agents. There was no terrain yet
        /// (the floor was flat at height 0), so the tank gets flat default ground and agents are
        /// placed on top of it.
        /// </summary>
        private static World ReadV1(BinaryReader r)
        {
            var world = ReadV1Body(r, extendedAgents: false);
            var a = world.Agents;
            for (int i = 0; i < a.Count; i++) a.PosY[i] = MovementSystem.SurfaceHeight(world.Terrain, a.PosX[i], a.PosZ[i]);
            GiveOldAgentsDefaults(world);
            GiveOldFrogsLifeDetails(world);
            return world;
        }

        /// <summary>Version 2: everything in version 1, then terrain, environment and devices.</summary>
        private static World ReadV2(BinaryReader r)
        {
            var world = ReadV1Body(r, extendedAgents: false);
            ReadTerrainEnvironmentDevices(r, world, DeviceKind.Mister);
            GiveOldAgentsDefaults(world);
            GiveOldFrogsLifeDetails(world);
            return world;
        }

        /// <summary>Version 3: like version 2, but each agent also stores comfort, brain state and age, and feeders exist.</summary>
        private static World ReadV3(BinaryReader r)
        {
            var world = ReadV1Body(r, extendedAgents: true);
            ReadTerrainEnvironmentDevices(r, world, DeviceKind.Feeder);
            GiveOldFrogsLifeDetails(world);
            return world;
        }

        /// <summary>Version 4: like version 3, but frogs also store species, life stage, family, stress and genes, and escaped frogs are kept.</summary>
        private static World ReadV4(BinaryReader r)
        {
            var world = ReadV1Body(r, extendedAgents: true, lifeDetails: true);
            ReadTerrainEnvironmentDevices(r, world, DeviceKind.Feeder);
            ReadEscaped(r, world);
            return world;
        }

        /// <summary>
        /// Version 5: like version 4, but each frog also stores loneliness and its territory, and the
        /// population controls (breeding switch, frog limit, rehomed and collected counts) are kept.
        /// </summary>
        private static World ReadV5(BinaryReader r)
        {
            var world = ReadV1Body(r, extendedAgents: true, lifeDetails: true, social: true);
            ReadTerrainEnvironmentDevices(r, world, DeviceKind.Feeder);
            ReadEscaped(r, world);
            world.BreedingEnabled = r.ReadBoolean();
            world.BreedingLimit = ReadCount(r, World.MaxAgents);
            world.Rehomed = ReadCount(r, int.MaxValue);
            world.EggsCollected = ReadCount(r, int.MaxValue);
            return world;
        }

        /// <summary>
        /// Version 6: like version 5, but genes also carry mutations, hybrid mix and generations, each
        /// frog stores the species of the father of the eggs she carries, and the mutation boost switch,
        /// discoveries and registered species are kept.
        /// </summary>
        private static World ReadV6(BinaryReader r) => ReadV6Body(r);

        /// <summary>Version 9: everything in version 8, then the style set in use and the style sets bought.</summary>
        private static World ReadV9(BinaryReader r)
        {
            var world = ReadV8(r);
            byte style = r.ReadByte();
            if (style >= TankLooks.Styles.Length) throw new SaveFormatException("Damaged save: unknown tank style.");
            world.Style = style;
            world.Economy.StylesOwned = r.ReadUInt32();
            return world;
        }

        /// <summary>Version 8: everything in version 7, then money mode (money, research, contracts).</summary>
        private static World ReadV8(BinaryReader r)
        {
            var world = ReadV7(r);
            var eco = world.Economy;
            eco.Enabled = r.ReadBoolean();
            eco.Money = r.ReadDouble();
            eco.Reputation = r.ReadSingle();
            eco.ResearchPoints = ReadCount(r, int.MaxValue);
            eco.Researched = r.ReadUInt32();
            eco.Visitors = r.ReadInt64();
            eco.Sold = ReadCount(r, int.MaxValue);
            eco.EarnedVisitors = r.ReadDouble(); eco.EarnedSales = r.ReadDouble(); eco.EarnedContracts = r.ReadDouble();
            eco.SpentPower = r.ReadDouble(); eco.SpentUpkeep = r.ReadDouble(); eco.SpentShop = r.ReadDouble();
            eco.NextContractId = r.ReadInt32();
            eco.ContractTimer = r.ReadSingle();
            eco.VisitorCarry = r.ReadSingle();
            if (double.IsNaN(eco.Money) || float.IsNaN(eco.Reputation) || eco.Visitors < 0)
                throw new SaveFormatException("Damaged save: bad money values.");
            int count = ReadCount(r, 64);
            for (int k = 0; k < count; k++)
            {
                var c = new Contract { Id = r.ReadInt32() };
                byte kind = r.ReadByte();
                if (kind > (byte)ContractKind.Registered) throw new SaveFormatException("Damaged save: unknown contract.");
                c.Kind = (ContractKind)kind;
                c.A = r.ReadByte(); c.B = r.ReadByte();
                c.Type = r.ReadInt32();
                c.Needed = r.ReadInt32(); c.Delivered = r.ReadInt32();
                c.Reward = r.ReadSingle();
                c.Deadline = r.ReadDouble();
                c.Buyer = r.ReadString();
                bool badSpecies = (c.Kind == ContractKind.Species || c.Kind == ContractKind.Hybrid)
                    && (c.A >= SpeciesCatalog.Frogs.Length || c.B >= SpeciesCatalog.Frogs.Length);
                if (badSpecies || (c.Kind == ContractKind.Mutation && c.A >= Mutations.Count))
                    throw new SaveFormatException("Damaged save: bad contract.");
                eco.Contracts.Add(c);
            }
            return world;
        }

        /// <summary>Version 7: everything in version 6, then the building and decoration parts.</summary>
        private static World ReadV7(BinaryReader r)
        {
            var world = ReadV6Body(r);
            int nextId = r.ReadInt32();
            int count = ReadCount(r, PartStore.MaxParts);
            for (int k = 0; k < count; k++)
            {
                var p = new PlacedPart { Id = r.ReadInt32() };
                byte kind = r.ReadByte();
                if (kind >= Parts.All.Length) throw new SaveFormatException("Damaged save: unknown building part.");
                p.Kind = (PartKind)kind;
                p.X = r.ReadSingle(); p.Z = r.ReadSingle();
                p.Rotation = r.ReadSingle();
                p.Scale = r.ReadSingle();
                p.R = r.ReadByte(); p.G = r.ReadByte(); p.B = r.ReadByte();
                p.OnPartId = r.ReadInt32();
                if (float.IsNaN(p.X) || float.IsNaN(p.Z) || float.IsNaN(p.Rotation) || float.IsNaN(p.Scale))
                    throw new SaveFormatException("Damaged save: bad building part.");
                p.Scale = Parts.ClampScale(p.Scale);
                p.Rotation = Parts.NormaliseAngle(p.Rotation);
                world.Parts.Restore(p);
            }
            world.Parts.SetNextId(nextId);
            return world;
        }

        private static World ReadV6Body(BinaryReader r)
        {
            var world = ReadV1Body(r, extendedAgents: true, lifeDetails: true, social: true, phase3: true);
            ReadTerrainEnvironmentDevices(r, world, DeviceKind.Feeder);
            ReadEscaped(r, world, phase3: true);
            world.BreedingEnabled = r.ReadBoolean();
            world.BreedingLimit = ReadCount(r, World.MaxAgents);
            world.Rehomed = ReadCount(r, int.MaxValue);
            world.EggsCollected = ReadCount(r, int.MaxValue);
            world.MutationBoost = r.ReadBoolean();
            world.DiscoveredMutations = r.ReadUInt16();
            world.DiscoveredHybrids = r.ReadUInt64();
            int registered = ReadCount(r, 10_000);
            for (int k = 0; k < registered; k++)
            {
                var reg = new RegisteredSpecies { Type = r.ReadInt32(), Name = r.ReadString(), Time = r.ReadDouble() };
                if (Lineage.SpeciesOfType(reg.Type) >= SpeciesCatalog.Frogs.Length || Lineage.OtherOfType(reg.Type) >= SpeciesCatalog.Frogs.Length)
                    throw new SaveFormatException("Damaged save: registered species of unknown kind.");
                world.Registered.Add(reg);
            }
            return world;
        }

        /// <summary>The escaped frogs list (versions 4 and later).</summary>
        private static void ReadEscaped(BinaryReader r, World world, bool phase3 = false)
        {
            int escaped = ReadCount(r, 100_000);
            for (int k = 0; k < escaped; k++)
            {
                var e = new EscapedFrog { Id = r.ReadInt32(), Species = r.ReadByte() };
                if (e.Species >= SpeciesCatalog.Frogs.Length) throw new SaveFormatException("Damaged save: unknown frog species.");
                byte sex = r.ReadByte();
                if (sex > (byte)Sex.Male) throw new SaveFormatException("Damaged save: unknown sex.");
                e.Sex = (Sex)sex;
                e.Age = r.ReadSingle(); e.Growth = r.ReadSingle(); e.Lifespan = r.ReadSingle();
                e.MotherId = r.ReadInt32(); e.FatherId = r.ReadInt32();
                e.Genes = Genome.Read(r, phase3);
                e.Reason = r.ReadString();
                e.Time = r.ReadDouble();
                world.Escaped.Add(e);
            }
        }

        /// <summary>
        /// Frogs from saves before version 4 were all White's tree frogs with no life stage, sex,
        /// family or genes. They load as grown White's tree frogs with average genes, alternating
        /// female and male by id, and a lifespan worked out from their id (so the same file always
        /// loads the same way) that leaves them at least 20 more minutes.
        /// </summary>
        private static void GiveOldFrogsLifeDetails(World world)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.Frog;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] != AgentKind.Frog) continue;
                a.Species[i] = sp.Id;
                a.Sex[i] = a.Id[i] % 2 == 1 ? Sex.Female : Sex.Male;
                a.Growth[i] = sp.AdultAt;
                if (a.Age[i] < sp.AdultAt) a.Age[i] = sp.AdultAt;
                float span = sp.MinLifespan + (a.Id[i] * 53 % (int)(sp.MaxLifespan - sp.MinLifespan));
                a.Lifespan[i] = Math.Max(span, a.Age[i] + 1200f);
                a.Stage[i] = sp.StageFor(a.Growth[i], a.Age[i]);
            }
        }

        /// <summary>
        /// Agents from saves before version 3 had no age or lifespan. Frogs become adults; crickets
        /// get a lifespan worked out from their id, so loading the same file always gives the same result.
        /// </summary>
        private static void GiveOldAgentsDefaults(World world)
        {
            var a = world.Agents;
            var sp = SpeciesCatalog.Insect;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.Kind[i] == AgentKind.Frog) a.Age[i] = 600f;
                else a.Lifespan[i] = sp.MinLifespan + (a.Id[i] * 37 % 240);
            }
        }

        private static void ReadTerrainEnvironmentDevices(BinaryReader r, World world, DeviceKind newestKind)
        {
            var t = world.Terrain;
            int pointsX = r.ReadInt32(), pointsZ = r.ReadInt32();
            float cellSize = r.ReadSingle();
            if (pointsX != t.PointsX || pointsZ != t.PointsZ || Math.Abs(cellSize - t.CellSize) > 1e-6f)
                throw new SaveFormatException("Damaged save: terrain size does not match the tank.");
            t.WaterLevel = r.ReadSingle();
            var heights = new float[t.Height.Length];
            for (int i = 0; i < heights.Length; i++)
            {
                float h = r.ReadSingle();
                if (float.IsNaN(h)) throw new SaveFormatException("Damaged save: bad terrain height.");
                heights[i] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
            }
            var layers = new GroundLayer[t.Layer.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                byte b = r.ReadByte();
                if (b > (byte)GroundLayer.Rock) throw new SaveFormatException("Damaged save: unknown ground layer.");
                layers[i] = (GroundLayer)b;
            }
            t.CopyFrom(heights, layers);

            var env = world.Environment;
            env.AmbientTemperature = r.ReadSingle();
            env.AmbientHumidity = r.ReadSingle();
            int cellsX = r.ReadInt32(), cellsZ = r.ReadInt32();
            if (cellsX != env.CellsX || cellsZ != env.CellsZ)
                throw new SaveFormatException("Damaged save: environment grid does not match the tank.");
            for (int i = 0; i < env.Temperature.Length; i++) env.Temperature[i] = r.ReadSingle();
            for (int i = 0; i < env.Humidity.Length; i++) env.Humidity[i] = r.ReadSingle();
            env.InvalidateTerrainCache();

            int nextDeviceId = r.ReadInt32();
            int deviceCount = ReadCount(r, 10_000);
            for (int i = 0; i < deviceCount; i++)
            {
                var d = new Device { Id = r.ReadInt32() };
                byte kind = r.ReadByte();
                if (kind > (byte)newestKind) throw new SaveFormatException("Damaged save: unknown device type.");
                d.Kind = (DeviceKind)kind;
                d.X = r.ReadSingle(); d.Z = r.ReadSingle();
                d.Enabled = r.ReadBoolean();
                d.Setting = r.ReadSingle(); d.Setting2 = r.ReadSingle();
                d.Supply = r.ReadSingle(); d.Timer = r.ReadSingle();
                d.Active = r.ReadBoolean();
                world.Devices.Restore(d);
            }
            world.Devices.SetNextId(nextDeviceId);
        }

        /// <summary>
        /// The version 1 body. Version 2 starts with exactly the same bytes; version 3 adds extra
        /// fields to the end of each agent's record.
        /// </summary>
        private static World ReadV1Body(BinaryReader r, bool extendedAgents, bool lifeDetails = false, bool social = false, bool phase3 = false)
        {
            ulong seed = r.ReadUInt64();
            float halfX = r.ReadSingle();
            float halfZ = r.ReadSingle();
            var world = new World(seed, halfX, halfZ) { SimTime = r.ReadDouble() };

            int streamCount = ReadCount(r, 10_000);
            for (int s = 0; s < streamCount; s++)
            {
                string name = r.ReadString();
                ulong state = r.ReadUInt64();
                ulong inc = r.ReadUInt64();
                if ((inc & 1UL) == 0UL) throw new SaveFormatException("Damaged random stream in save.");
                world.Random.Restore(name, SeededRandom.FromState(state, inc));
            }

            var a = world.Agents;
            int nextId = r.ReadInt32();
            int count = ReadCount(r, 1_000_000);
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt32();
                var kind = (AgentKind)r.ReadByte();
                int idx = a.Add(kind, r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                a.Id[idx] = id;
                a.VelX[idx] = r.ReadSingle(); a.VelZ[idx] = r.ReadSingle();
                a.Hunger[idx] = r.ReadSingle(); a.Thirst[idx] = r.ReadSingle(); a.Fatigue[idx] = r.ReadSingle();
                if (!extendedAgents) continue;
                a.Comfort[idx] = r.ReadSingle();
                byte goal = r.ReadByte();
                if (goal > (byte)(social ? Goal.Retreat : lifeDetails ? Goal.Disperse : Goal.Flee)) throw new SaveFormatException("Damaged save: unknown goal.");
                a.Goal[idx] = (Goal)goal;
                a.GoalTime[idx] = r.ReadSingle();
                a.TargetId[idx] = r.ReadInt32();
                a.TargetX[idx] = r.ReadSingle(); a.TargetZ[idx] = r.ReadSingle();
                a.Age[idx] = r.ReadSingle(); a.Lifespan[idx] = r.ReadSingle();
                a.Timer[idx] = r.ReadSingle(); a.Cooldown[idx] = r.ReadSingle();
                if (!lifeDetails) continue;
                byte species = r.ReadByte();
                if (species >= SpeciesCatalog.Frogs.Length) throw new SaveFormatException("Damaged save: unknown frog species.");
                a.Species[idx] = species;
                a.MateSpecies[idx] = species; // before version 6 every pair was of one species
                byte sex = r.ReadByte();
                if (sex > (byte)Sex.Male) throw new SaveFormatException("Damaged save: unknown sex.");
                a.Sex[idx] = (Sex)sex;
                byte stage = r.ReadByte();
                if (stage > (byte)LifeStage.Elder) throw new SaveFormatException("Damaged save: unknown life stage.");
                a.Stage[idx] = (LifeStage)stage;
                a.Growth[idx] = r.ReadSingle();
                a.Stress[idx] = r.ReadSingle();
                a.MotherId[idx] = r.ReadInt32(); a.FatherId[idx] = r.ReadInt32(); a.MateId[idx] = r.ReadInt32();
                a.Gravid[idx] = r.ReadSingle(); a.BreedCooldown[idx] = r.ReadSingle();
                a.Genes[idx] = Genome.Read(r, phase3);
                a.MateGenes[idx] = Genome.Read(r, phase3);
                if (!social) continue;
                a.Social[idx] = r.ReadSingle();
                a.Retreat[idx] = r.ReadSingle();
                a.HasHome[idx] = r.ReadBoolean();
                a.HomeX[idx] = r.ReadSingle(); a.HomeZ[idx] = r.ReadSingle();
                if (!phase3) continue;
                byte mateSpecies = r.ReadByte();
                if (mateSpecies >= SpeciesCatalog.Frogs.Length) throw new SaveFormatException("Damaged save: unknown frog species.");
                a.MateSpecies[idx] = mateSpecies;
            }
            a.SetNextId(nextId);
            return world;
        }

        private static int ReadCount(BinaryReader r, int max)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > max) throw new SaveFormatException("Damaged save: impossible item count.");
            return n;
        }
    }
}

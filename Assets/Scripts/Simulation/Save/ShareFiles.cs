using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace FrogTerrarium.Simulation
{
    /// <summary>The kinds of file players can share with each other.</summary>
    public enum ShareKind
    {
        Unknown,
        /// <summary>A blueprint of building parts (FTBP1:, .ftbp).</summary>
        Blueprint,
        /// <summary>A whole map: the sculpted ground, devices and parts (FTMF1:, .ftmap).</summary>
        Map,
        /// <summary>A registered species with a breeding pair (FTSP1:, .ftsp).</summary>
        Species,
        /// <summary>A map code that rebuilds a generated map (FTMAP1:...).</summary>
        MapCode,
    }

    /// <summary>
    /// Shared files are plain text, so they can be emailed, posted or pasted: a short prefix that
    /// says what they are, then the data packed and written as letters and numbers (base64).
    /// Each one ends with a fingerprint (a check number) so a damaged copy is noticed, and two
    /// players can compare fingerprints to be sure they have exactly the same thing.
    /// </summary>
    public static class ShareText
    {
        /// <summary>What kind of shared text this is, from its prefix (spaces and line breaks around it are ignored).</summary>
        public static ShareKind Detect(string text)
        {
            text = (text ?? "").TrimStart();
            if (text.StartsWith(Blueprint.TextPrefix, StringComparison.Ordinal)) return ShareKind.Blueprint;
            if (text.StartsWith(MapFile.TextPrefix, StringComparison.Ordinal)) return ShareKind.Map;
            if (text.StartsWith(SpeciesFile.TextPrefix, StringComparison.Ordinal)) return ShareKind.Species;
            if (text.StartsWith(MapSettings.CodePrefix + ":", StringComparison.OrdinalIgnoreCase)) return ShareKind.MapCode;
            return ShareKind.Unknown;
        }

        /// <summary>The file ending for a kind of shared file ("" for map codes, which are only text).</summary>
        public static string ExtensionOf(ShareKind kind)
        {
            switch (kind)
            {
                case ShareKind.Blueprint: return ".ftbp";
                case ShareKind.Map: return ".ftmap";
                case ShareKind.Species: return ".ftsp";
                default: return "";
            }
        }

        /// <summary>FNV-1a over the bytes, as 8 hex digits: the same on every computer.</summary>
        public static string Fingerprint(byte[] data)
        {
            uint h = 2166136261u;
            foreach (byte b in data) { h ^= b; h *= 16777619u; }
            return h.ToString("x8");
        }

        /// <summary>Packs data into shared text: prefix, then base64 of the deflated data, then "." and its fingerprint.</summary>
        internal static string Pack(string prefix, byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var z = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(data, 0, data.Length);
                return prefix + Convert.ToBase64String(ms.ToArray()) + "." + Fingerprint(data);
            }
        }

        /// <summary>Unpacks shared text made by <see cref="Pack"/>, checking the fingerprint. Throws <see cref="SaveFormatException"/> with a plain message.</summary>
        internal static byte[] Unpack(string prefix, string text, string what, out string fingerprint)
        {
            text = (text ?? "").Trim();
            int start = text.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) throw new SaveFormatException("That is not a Frog Terrarium " + what + " (it should start with " + prefix + ").");
            var sb = new StringBuilder();
            foreach (char ch in text.Substring(start + prefix.Length)) if (!char.IsWhiteSpace(ch)) sb.Append(ch);
            string body = sb.ToString();
            int dot = body.LastIndexOf('.');
            if (dot < 0) throw new SaveFormatException("The " + what + " is cut short (its fingerprint at the end is missing).");
            fingerprint = body.Substring(dot + 1);
            byte[] packed;
            try { packed = Convert.FromBase64String(body.Substring(0, dot)); }
            catch (FormatException) { throw new SaveFormatException("The " + what + " is damaged (part of it may be missing)."); }
            byte[] data;
            try
            {
                using (var input = new MemoryStream(packed))
                using (var z = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int n;
                    while ((n = z.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, n);
                        if (output.Length > 16 * 1024 * 1024) throw new SaveFormatException("The " + what + " is far too big.");
                    }
                    data = output.ToArray();
                }
            }
            catch (InvalidDataException) { throw new SaveFormatException("The " + what + " is damaged."); }
            if (!string.Equals(Fingerprint(data), fingerprint, StringComparison.OrdinalIgnoreCase))
                throw new SaveFormatException("The " + what + " is damaged: its fingerprint does not match.");
            return data;
        }

        /// <summary>A name safe for a file: letters, numbers, spaces, dashes and underscores, at most 40 characters.</summary>
        public static string CleanName(string name, string fallback)
        {
            var sb = new StringBuilder();
            foreach (char ch in name ?? "")
                if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_') sb.Append(ch);
            string s = sb.ToString().Trim();
            if (s.Length > 40) s = s.Substring(0, 40).Trim();
            return s.Length == 0 ? fallback : s;
        }
    }

    /// <summary>
    /// A whole map to share: the ground exactly as sculpted and painted (every height and ground
    /// layer), the water line, the room, the style set, the devices and their settings, and all
    /// building parts. No animals. Starting a game from it gives exactly the same tank.
    /// </summary>
    public sealed class MapFile
    {
        public const string TextPrefix = "FTMF1:";
        public const int FormatVersion = 1;

        public string Name = "Untitled map";
        /// <summary>Who made it (optional, typed by the player).</summary>
        public string Author = "";
        /// <summary>The settings the ground was first generated from (for the biome's species and as a note).</summary>
        public MapSettings Settings = MapSettings.Classic(0);
        public float HalfSizeX, HalfSizeZ, CellSize, WaterLevel;
        public float[] Height = new float[0];
        public GroundLayer[] Layer = new GroundLayer[0];
        public float RoomTemperature, RoomHumidity;
        public byte Style;
        public readonly List<Device> Devices = new List<Device>();
        public readonly List<PlacedPart> Parts = new List<PlacedPart>();
        /// <summary>The fingerprint read with it (or made when it was written).</summary>
        public string Fingerprint = "";

        /// <summary>Takes a copy of a tank's map (not its animals).</summary>
        public static MapFile FromWorld(World world, string name, string author = "")
        {
            var t = world.Terrain;
            var m = new MapFile
            {
                Name = ShareText.CleanName(name, "Untitled map"),
                Author = ShareText.CleanName(author, ""),
                Settings = world.Map,
                HalfSizeX = world.HalfSizeX, HalfSizeZ = world.HalfSizeZ,
                CellSize = t.CellSize, WaterLevel = t.WaterLevel,
                Height = (float[])t.Height.Clone(),
                Layer = (GroundLayer[])t.Layer.Clone(),
                RoomTemperature = world.Environment.AmbientTemperature,
                RoomHumidity = world.Environment.AmbientHumidity,
                Style = world.Style,
            };
            foreach (var d in world.Devices.All)
                m.Devices.Add(new Device
                {
                    Kind = d.Kind, X = d.X, Z = d.Z, Enabled = d.Enabled, Setting = d.Setting, Setting2 = d.Setting2,
                    Mode = d.Mode, Target = d.Target, Supply = d.Supply,
                });
            foreach (var p in world.Parts.All)
                m.Parts.Add(new PlacedPart
                {
                    Id = p.Id, Kind = p.Kind, X = p.X, Z = p.Z, Rotation = p.Rotation, Scale = p.Scale,
                    R = p.R, G = p.G, B = p.B, OnPartId = p.OnPartId,
                });
            return m;
        }

        public string ToText()
        {
            byte[] data = ToBytes();
            Fingerprint = ShareText.Fingerprint(data);
            return ShareText.Pack(TextPrefix, data);
        }

        private byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    w.Write(FormatVersion);
                    w.Write(Name ?? "");
                    w.Write(Author ?? "");
                    var s = Settings.Clamped();
                    w.Write((byte)s.Biome); w.Write(s.Seed); w.Write(s.Size);
                    w.Write(s.Hills); w.Write(s.Water); w.Write(s.Plants);
                    w.Write(HalfSizeX); w.Write(HalfSizeZ); w.Write(CellSize); w.Write(WaterLevel);
                    w.Write(Height.Length);
                    foreach (var h in Height) w.Write(h);
                    foreach (var l in Layer) w.Write((byte)l);
                    w.Write(RoomTemperature); w.Write(RoomHumidity);
                    w.Write(Style);
                    w.Write(Devices.Count);
                    foreach (var d in Devices)
                    {
                        w.Write((byte)d.Kind);
                        w.Write(d.X); w.Write(d.Z);
                        w.Write(d.Enabled);
                        w.Write(d.Setting); w.Write(d.Setting2);
                        w.Write((byte)d.Mode); w.Write(d.Target);
                    }
                    w.Write(Parts.Count);
                    foreach (var p in Parts)
                    {
                        w.Write(p.Id);
                        w.Write((byte)p.Kind);
                        w.Write(p.X); w.Write(p.Z);
                        w.Write(p.Rotation); w.Write(p.Scale);
                        w.Write(p.R); w.Write(p.G); w.Write(p.B);
                        w.Write(p.OnPartId);
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>Reads a map from its text. Throws <see cref="SaveFormatException"/> with a plain message if it is not a map or is damaged.</summary>
        public static MapFile FromText(string text)
        {
            byte[] data = ShareText.Unpack(TextPrefix, text, "map", out string fingerprint);
            try
            {
                using (var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
                {
                    int version = r.ReadInt32();
                    if (version > FormatVersion) throw new SaveFormatException("This map was made by a newer game.");
                    if (version < 1) throw new SaveFormatException("The map is damaged.");
                    var m = new MapFile
                    {
                        Name = ShareText.CleanName(r.ReadString(), "Untitled map"),
                        Author = ShareText.CleanName(r.ReadString(), ""),
                        Fingerprint = fingerprint,
                    };
                    byte biome = r.ReadByte();
                    if (biome >= Biomes.All.Length) throw new SaveFormatException("The map uses a biome this game does not have.");
                    m.Settings = new MapSettings
                    {
                        Biome = (Biome)biome, Seed = r.ReadUInt64(), Size = r.ReadByte(),
                        Hills = r.ReadSingle(), Water = r.ReadSingle(), Plants = r.ReadSingle(),
                    }.Clamped();
                    m.HalfSizeX = r.ReadSingle(); m.HalfSizeZ = r.ReadSingle();
                    m.CellSize = r.ReadSingle(); m.WaterLevel = r.ReadSingle();
                    if (!(m.HalfSizeX >= 2f && m.HalfSizeX <= 40f && m.HalfSizeZ >= 2f && m.HalfSizeZ <= 40f
                          && m.CellSize >= 0.05f && m.CellSize <= 2f && m.WaterLevel >= TerrainGrid.MinHeight && m.WaterLevel <= TerrainGrid.MaxHeight))
                        throw new SaveFormatException("The map has an impossible size.");
                    var grid = new TerrainGrid(m.HalfSizeX, m.HalfSizeZ, m.CellSize);
                    int points = r.ReadInt32();
                    if (points != grid.Height.Length) throw new SaveFormatException("The map is damaged (wrong number of ground points).");
                    m.Height = new float[points];
                    m.Layer = new GroundLayer[points];
                    for (int k = 0; k < points; k++)
                    {
                        float h = r.ReadSingle();
                        if (float.IsNaN(h)) throw new SaveFormatException("The map is damaged.");
                        m.Height[k] = TerrainGrid.Clamp(h, TerrainGrid.MinHeight, TerrainGrid.MaxHeight);
                    }
                    for (int k = 0; k < points; k++)
                    {
                        byte l = r.ReadByte();
                        if (l > (byte)GroundLayer.Rock) throw new SaveFormatException("The map is damaged.");
                        m.Layer[k] = (GroundLayer)l;
                    }
                    m.RoomTemperature = r.ReadSingle(); m.RoomHumidity = r.ReadSingle();
                    if (!(m.RoomTemperature > -20f && m.RoomTemperature < 60f && m.RoomHumidity >= 0f && m.RoomHumidity <= 100f))
                        throw new SaveFormatException("The map's room is impossible.");
                    m.Style = r.ReadByte();
                    if (m.Style >= TankLooks.Styles.Length) m.Style = 0;
                    int devices = r.ReadInt32();
                    if (devices < 0 || devices > 500) throw new SaveFormatException("The map is damaged.");
                    for (int k = 0; k < devices; k++)
                    {
                        byte kind = r.ReadByte();
                        if (kind > (byte)DeviceKind.Controller) throw new SaveFormatException("The map uses a device this game does not have.");
                        var d = new Device
                        {
                            Kind = (DeviceKind)kind, X = r.ReadSingle(), Z = r.ReadSingle(), Enabled = r.ReadBoolean(),
                            Setting = r.ReadSingle(), Setting2 = r.ReadSingle(),
                        };
                        byte mode = r.ReadByte();
                        if (mode > (byte)DeviceMode.Smart) throw new SaveFormatException("The map is damaged.");
                        d.Mode = (DeviceMode)mode;
                        d.Target = r.ReadSingle();
                        if (float.IsNaN(d.X) || float.IsNaN(d.Z) || float.IsNaN(d.Setting) || float.IsNaN(d.Setting2) || float.IsNaN(d.Target))
                            throw new SaveFormatException("The map is damaged.");
                        m.Devices.Add(d);
                    }
                    int parts = r.ReadInt32();
                    if (parts < 0 || parts > PartStore.MaxParts) throw new SaveFormatException("The map is damaged.");
                    for (int k = 0; k < parts; k++)
                    {
                        var p = new PlacedPart { Id = r.ReadInt32() };
                        byte kind = r.ReadByte();
                        if (kind >= Simulation.Parts.All.Length) throw new SaveFormatException("The map uses a building part this game does not have.");
                        p.Kind = (PartKind)kind;
                        p.X = r.ReadSingle(); p.Z = r.ReadSingle();
                        p.Rotation = r.ReadSingle(); p.Scale = r.ReadSingle();
                        p.R = r.ReadByte(); p.G = r.ReadByte(); p.B = r.ReadByte();
                        p.OnPartId = r.ReadInt32();
                        if (float.IsNaN(p.X) || float.IsNaN(p.Z) || float.IsNaN(p.Rotation) || float.IsNaN(p.Scale))
                            throw new SaveFormatException("The map is damaged.");
                        p.Scale = Simulation.Parts.ClampScale(p.Scale);
                        p.Rotation = Simulation.Parts.NormaliseAngle(p.Rotation);
                        m.Parts.Add(p);
                    }
                    return m;
                }
            }
            catch (EndOfStreamException) { throw new SaveFormatException("The map is cut short."); }
        }

        /// <summary>
        /// A new game on this map: exactly this ground, room, style, devices and parts, with the
        /// starting frogs of its biome and crickets, settled temperature and humidity.
        /// </summary>
        public World CreateWorld(int frogs, int insects)
        {
            var world = new World(Settings.Seed, HalfSizeX, HalfSizeZ) { Map = Settings };
            var t = new TerrainGrid(HalfSizeX, HalfSizeZ, CellSize) { WaterLevel = WaterLevel };
            Array.Copy(Height, t.Height, t.Height.Length);
            Array.Copy(Layer, t.Layer, t.Layer.Length);
            t.MarkAllChanged();
            world.ReplaceGround(t, RoomTemperature, RoomHumidity);
            world.Style = Style;
            if (Style != 0) world.Economy.StylesOwned |= 1u << Style;
            foreach (var src in Devices)
            {
                var d = world.Devices.Add(src.Kind, src.X, src.Z);
                d.Enabled = src.Enabled; d.Setting = src.Setting; d.Setting2 = src.Setting2;
                d.Mode = src.Mode; d.Target = src.Target;
                DeviceStore.ClampSettings(d);
            }
            int nextId = 1;
            foreach (var p in Parts)
            {
                world.Parts.Restore(new PlacedPart
                {
                    Id = p.Id, Kind = p.Kind, X = p.X, Z = p.Z, Rotation = p.Rotation, Scale = p.Scale,
                    R = p.R, G = p.G, B = p.B, OnPartId = p.OnPartId,
                });
                nextId = Math.Max(nextId, p.Id + 1);
            }
            world.Parts.SetNextId(nextId);
            EnvironmentSystem.WarmUp(world, 300f);
            world.SpawnStartingPopulation(frogs, insects, Settings.Info.StartingSpecies);
            return world;
        }
    }

    /// <summary>
    /// A registered species to share: its name and type, and a female and a male of it (their
    /// genes and species) so someone else can start breeding it in their own tank.
    /// </summary>
    public sealed class SpeciesFile
    {
        public const string TextPrefix = "FTSP1:";
        public const int FormatVersion = 1;

        public string Name = "";
        public string Author = "";
        public int Type;
        /// <summary>The main species of the female and the male.</summary>
        public byte FemaleSpecies, MaleSpecies;
        public Genome Female, Male;
        public string Fingerprint = "";

        /// <summary>
        /// A species file for a registered species, using a grown female and male of it from the tank.
        /// Returns null and why if the species is not registered or there is no grown pair of it.
        /// </summary>
        public static SpeciesFile FromWorld(World world, RegisteredSpecies reg, string author, out string why)
        {
            why = "";
            if (reg == null || !world.Registered.Contains(reg)) { why = "That species is not registered."; return null; }
            var a = world.Agents;
            int female = -1, male = -1;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a.IsLandFrog(i) || a.Stage[i] < LifeStage.Adult || Lineage.TypeOf(a, i) != reg.Type) continue;
                if (a.Sex[i] == Sex.Female && female < 0) female = i;
                if (a.Sex[i] == Sex.Male && male < 0) male = i;
            }
            if (female < 0 || male < 0)
            {
                why = "To share " + reg.Name + ", the tank needs a grown female and a grown male of it.";
                return null;
            }
            return new SpeciesFile
            {
                Name = reg.Name, Author = ShareText.CleanName(author, ""), Type = reg.Type,
                FemaleSpecies = a.Species[female], MaleSpecies = a.Species[male],
                Female = a.Genes[female], Male = a.Genes[male],
            };
        }

        public string ToText()
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
                {
                    w.Write(FormatVersion);
                    w.Write(Name ?? "");
                    w.Write(Author ?? "");
                    w.Write(Type);
                    w.Write(FemaleSpecies);
                    Female.Write(w);
                    w.Write(MaleSpecies);
                    Male.Write(w);
                }
                byte[] data = ms.ToArray();
                Fingerprint = ShareText.Fingerprint(data);
                return ShareText.Pack(TextPrefix, data);
            }
        }

        /// <summary>Reads a species file from its text. Throws <see cref="SaveFormatException"/> with a plain message.</summary>
        public static SpeciesFile FromText(string text)
        {
            byte[] data = ShareText.Unpack(TextPrefix, text, "species file", out string fingerprint);
            try
            {
                using (var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
                {
                    int version = r.ReadInt32();
                    if (version > FormatVersion) throw new SaveFormatException("This species file was made by a newer game.");
                    if (version < 1) throw new SaveFormatException("The species file is damaged.");
                    var f = new SpeciesFile { Name = r.ReadString().Trim(), Author = ShareText.CleanName(r.ReadString(), ""), Type = r.ReadInt32(), Fingerprint = fingerprint };
                    if (f.Name.Length == 0 || f.Name.Length > World.MaxSpeciesNameLength) throw new SaveFormatException("The species file has a bad name.");
                    f.FemaleSpecies = r.ReadByte();
                    f.Female = Genome.Read(r, true);
                    f.MaleSpecies = r.ReadByte();
                    f.Male = Genome.Read(r, true);
                    if (f.FemaleSpecies >= SpeciesCatalog.Frogs.Length || f.MaleSpecies >= SpeciesCatalog.Frogs.Length
                        || Lineage.SpeciesOfType(f.Type) >= SpeciesCatalog.Frogs.Length || Lineage.OtherOfType(f.Type) >= SpeciesCatalog.Frogs.Length)
                        throw new SaveFormatException("The species file uses a frog this game does not have.");
                    if (Lineage.TypeOf(f.FemaleSpecies, f.Female) != f.Type || Lineage.TypeOf(f.MaleSpecies, f.Male) != f.Type)
                        throw new SaveFormatException("The species file is damaged: its frogs are not of its species.");
                    return f;
                }
            }
            catch (EndOfStreamException) { throw new SaveFormatException("The species file is cut short."); }
        }

        /// <summary>What a breeding pair from a species file costs in money mode (twice the price of each frog's species).</summary>
        public float Cost => 2f * (Economy.FrogCost(FemaleSpecies) + Economy.FrogCost(MaleSpecies));

        /// <summary>
        /// Adds this species to a tank: registers its name there (unless that type is already
        /// registered, when the tank's own name is kept) and adds the grown female and male near
        /// (x, z). In money mode the pair costs <see cref="Cost"/>. Returns false and why if it
        /// can't (no money, the name is taken by another kind of frog, the tank is full).
        /// </summary>
        public bool AddTo(World world, float x, float z, out string why)
        {
            why = "";
            if (world.Agents.Count + 2 > World.MaxAgents) { why = "The tank is too full."; return false; }
            RegisteredSpecies existing = null;
            foreach (var reg in world.Registered) if (reg.Type == Type) existing = reg;
            if (existing == null)
            {
                foreach (var reg in world.Registered)
                    if (string.Equals(reg.Name, Name, StringComparison.OrdinalIgnoreCase))
                    { why = "This tank already has a different species called " + Name + "."; return false; }
                foreach (var sp in SpeciesCatalog.Frogs)
                    if (string.Equals(sp.Name, Name, StringComparison.OrdinalIgnoreCase))
                    { why = Name + " is already the name of a real species."; return false; }
            }
            if (!world.Economy.Spend(Cost, "a pair of " + Name, out why)) return false;
            if (existing == null) world.Registered.Add(new RegisteredSpecies { Type = Type, Name = Name, Time = world.SimTime });
            var rng = world.Random.Get("spawn");
            Add(world, rng, x, z, FemaleSpecies, Sex.Female, Female);
            Add(world, rng, x, z, MaleSpecies, Sex.Male, Male);
            world.Announce(Name + " has arrived" + (Author.Length > 0 ? " from " + Author : "") + "!");
            return true;
        }

        private static void Add(World world, SeededRandom rng, float x, float z, byte species, Sex sex, Genome genes)
        {
            world.FindDryGround(rng, x - 1f, x + 1f, z - 1f, z + 1f, out float fx, out float fz);
            int i = world.AddFrog(fx, fz, species, sex);
            var a = world.Agents;
            a.Genes[i] = genes;
            var sp = SpeciesCatalog.FrogOf(a, i);
            a.Growth[i] = sp.AdultAt;
            a.Age[i] = sp.AdultAt;
            a.Lifespan[i] = 0.5f * (sp.MinLifespan + sp.MaxLifespan) * genes.Longevity;
            a.Stage[i] = LifeStage.Adult;
        }
    }
}

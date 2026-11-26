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
        public const int CurrentVersion = 1;
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
                }
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

        private static World ReadV1(BinaryReader r)
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

using System.IO;
using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    public class SaveSystemTests
    {
        private static World MakeRunningWorld()
        {
            var w = new World(2024, 8f, 5f);
            w.SpawnStartingPopulation(15, 60);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 200; i++) loop.Advance(1.0 / 60.0);
            w.Agents.RemoveAt(3); // make ids non-contiguous
            return w;
        }

        [Test]
        public void EmptyWorld_SavesAndLoads()
        {
            var w = new World(1);
            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.AreEqual(1UL, loaded.Seed);
            Assert.AreEqual(0, loaded.Agents.Count);
        }

        [Test]
        public void RoundTrip_PreservesEverything()
        {
            var w = MakeRunningWorld();
            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));

            Assert.AreEqual(w.Seed, loaded.Seed);
            Assert.AreEqual(w.HalfSizeX, loaded.HalfSizeX);
            Assert.AreEqual(w.HalfSizeZ, loaded.HalfSizeZ);
            Assert.AreEqual(w.SimTime, loaded.SimTime);
            Assert.AreEqual(w.Agents.Count, loaded.Agents.Count);
            Assert.AreEqual(w.Agents.NextId, loaded.Agents.NextId);
            for (int i = 0; i < w.Agents.Count; i++)
            {
                Assert.AreEqual(w.Agents.Id[i], loaded.Agents.Id[i]);
                Assert.AreEqual(w.Agents.Kind[i], loaded.Agents.Kind[i]);
                Assert.AreEqual(w.Agents.PosX[i], loaded.Agents.PosX[i]);
                Assert.AreEqual(w.Agents.PosZ[i], loaded.Agents.PosZ[i]);
                Assert.AreEqual(w.Agents.VelX[i], loaded.Agents.VelX[i]);
                Assert.AreEqual(w.Agents.Hunger[i], loaded.Agents.Hunger[i]);
                Assert.AreEqual(w.Agents.Thirst[i], loaded.Agents.Thirst[i]);
                Assert.AreEqual(w.Agents.Fatigue[i], loaded.Agents.Fatigue[i]);
            }
        }

        [Test]
        public void LoadedWorld_ContinuesExactlyLikeTheOriginal()
        {
            // Saving must capture the random streams too, or a loaded game would diverge.
            var original = MakeRunningWorld();
            var copy = SaveSystem.FromBytes(SaveSystem.ToBytes(original));
            var a = SimulationFactory.CreateDefault(original);
            var b = SimulationFactory.CreateDefault(copy);
            for (int i = 0; i < 300; i++) { a.Advance(1.0 / 60.0); b.Advance(1.0 / 60.0); }
            for (int i = 0; i < original.Agents.Count; i++)
            {
                Assert.AreEqual(original.Agents.PosX[i], copy.Agents.PosX[i]);
                Assert.AreEqual(original.Agents.PosZ[i], copy.Agents.PosZ[i]);
            }
        }

        /// <summary>
        /// A real version 1 save, made by the Phase 0 game (seed 4242, 6 x 4 m half-size tank,
        /// 3 frogs and 5 insects run for one second, then agent 2 removed). Never change these bytes:
        /// the test proves old saves keep loading.
        /// </summary>
        private const string Version1Sample =
            "RlRTVgEAAACSEAAAAAAAAAAAwEAAAIBA////////7z8CAAAACE1vdmVtZW50emNtKjdpGkdxFXywcwpD4gVzcGF3bkD2YGn6XwfnMT8cZBXvUYYJAAAABwAAAAEAAAAA8HpFQAAAAABAD1XAgPnaPTgkFz8PdFo7iYiIOw902joIAAAAAVGMisAAAAAAeBFGPxq/kj/QerW+D3TaOomICDsPdFo6AwAAAADwJZC/AAAAAGH0I8Adqg4+22YVPw90WjuJiIg7D3TaOgQAAAABy4+MwAAAAACGjwlAjpjXvQECmb8PdNo6iYgIOw90WjoFAAAAAeESFsAAAAAAHspGPopDKz/PB3+/D3TaOomICDsPdFo6BgAAAAH9YG9AAAAAACG9gT8OPdm+n62PPw902jqJiAg7D3RaOgcAAAABeExqPwAAAADv81C/Bi4Jv15vib8PdNo6iYgIOw90Wjo=";

        [Test]
        public void Version1Save_StillLoads()
        {
            var w = SaveSystem.FromBytes(System.Convert.FromBase64String(Version1Sample));
            Assert.AreEqual(4242UL, w.Seed);
            Assert.AreEqual(6f, w.HalfSizeX);
            Assert.AreEqual(4f, w.HalfSizeZ);
            Assert.AreEqual(1.0, w.SimTime, 1e-9);
            Assert.AreEqual(7, w.Agents.Count);
            Assert.AreEqual(9, w.Agents.NextId);
            Assert.AreEqual(1, w.Agents.Id[0]);
            Assert.AreEqual(AgentKind.Frog, w.Agents.Kind[0]);
            Assert.AreEqual(3.0856285f, w.Agents.PosX[0]);
            Assert.AreEqual(-3.3290558f, w.Agents.PosZ[0]);
            Assert.AreEqual(AgentKind.Insect, w.Agents.Kind[6]);
            // Version 1 had no terrain: the tank loads with flat ground, agents standing on it.
            Assert.AreEqual(w.Terrain.HeightAt(0f, 0f), w.Agents.PosY[0], 1e-5f);
            Assert.AreEqual(0, w.Devices.Count);
            // And it carries on running.
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 30; i++) loop.Advance(1.0 / 30.0);
        }

        [Test]
        public void RoundTrip_PreservesTerrainEnvironmentAndDevices()
        {
            var w = World.CreateStarterTank(77, 3, 10);
            TerrainBrush.Apply(w.Terrain, BrushMode.Paint, 1f, 1f, 2f, 1f, 1f, GroundLayer.Rock);
            w.Terrain.WaterLevel = 0.7f;
            var heater = w.Devices.All[0];
            heater.Setting = 31f;
            heater.Enabled = false;
            w.Devices.Remove(w.Devices.All[1].Id);
            w.Devices.Add(DeviceKind.Mister, 2f, -1f);

            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            CollectionAssert.AreEqual(w.Terrain.Height, loaded.Terrain.Height);
            CollectionAssert.AreEqual(w.Terrain.Layer, loaded.Terrain.Layer);
            Assert.AreEqual(0.7f, loaded.Terrain.WaterLevel);
            CollectionAssert.AreEqual(w.Environment.Temperature, loaded.Environment.Temperature);
            CollectionAssert.AreEqual(w.Environment.Humidity, loaded.Environment.Humidity);
            Assert.AreEqual(w.Devices.Count, loaded.Devices.Count);
            Assert.AreEqual(w.Devices.NextId, loaded.Devices.NextId);
            for (int i = 0; i < w.Devices.Count; i++)
            {
                var a = w.Devices.All[i];
                var b = loaded.Devices.All[i];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Kind, b.Kind);
                Assert.AreEqual(a.X, b.X);
                Assert.AreEqual(a.Z, b.Z);
                Assert.AreEqual(a.Enabled, b.Enabled);
                Assert.AreEqual(a.Setting, b.Setting);
                Assert.AreEqual(a.Setting2, b.Setting2);
                Assert.AreEqual(a.Timer, b.Timer);
                Assert.AreEqual(a.Active, b.Active);
            }
        }

        [Test]
        public void LoadedStarterTank_EnvironmentContinuesExactly()
        {
            var original = World.CreateStarterTank(31, 4, 20);
            var copy = SaveSystem.FromBytes(SaveSystem.ToBytes(original));
            var a = SimulationFactory.CreateDefault(original);
            var b = SimulationFactory.CreateDefault(copy);
            for (int i = 0; i < 300; i++) { a.Advance(1.0 / 60.0); b.Advance(1.0 / 60.0); }
            CollectionAssert.AreEqual(original.Environment.Temperature, copy.Environment.Temperature);
            CollectionAssert.AreEqual(original.Environment.Humidity, copy.Environment.Humidity);
            for (int i = 0; i < original.Agents.Count; i++)
                Assert.AreEqual(original.Agents.PosY[i], copy.Agents.PosY[i]);
        }

        [Test]
        public void SaveStartsWithMagicAndVersion()
        {
            byte[] data = SaveSystem.ToBytes(new World(1));
            Assert.AreEqual((byte)'F', data[0]);
            Assert.AreEqual((byte)'T', data[1]);
            Assert.AreEqual((byte)'S', data[2]);
            Assert.AreEqual((byte)'V', data[3]);
            Assert.AreEqual(SaveSystem.CurrentVersion, System.BitConverter.ToInt32(data, 4));
        }

        [Test]
        public void NewerVersion_IsRejectedWithAClearMessage()
        {
            byte[] data = SaveSystem.ToBytes(new World(1));
            byte[] future = System.BitConverter.GetBytes(SaveSystem.CurrentVersion + 1);
            System.Array.Copy(future, 0, data, 4, 4);
            var ex = Assert.Throws<SaveFormatException>(() => SaveSystem.FromBytes(data));
            StringAssert.Contains("newer", ex.Message);
        }

        [Test]
        public void GarbageAndTruncatedFiles_AreRejected()
        {
            Assert.Throws<SaveFormatException>(() => SaveSystem.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            byte[] good = SaveSystem.ToBytes(MakeRunningWorld());
            byte[] cut = new byte[good.Length / 2];
            System.Array.Copy(good, cut, cut.Length);
            Assert.Throws<SaveFormatException>(() => SaveSystem.FromBytes(cut));
        }

        [Test]
        public void WriteFile_ThenReadFile_Works()
        {
            string dir = Path.Combine(Path.GetTempPath(), "frog-terrarium-tests-" + System.Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "test.ftsv");
            try
            {
                var w = MakeRunningWorld();
                SaveSystem.WriteFile(path, w);
                SaveSystem.WriteFile(path, w); // overwriting an existing save also works
                var loaded = SaveSystem.ReadFile(path);
                Assert.AreEqual(w.Agents.Count, loaded.Agents.Count);
                Assert.IsFalse(File.Exists(path + ".tmp"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}

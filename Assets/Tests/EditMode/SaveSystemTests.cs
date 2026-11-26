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

        /// <summary>
        /// A real version 2 save from the terrain update: seed 777, 3 x 2 m half-size tank made by
        /// MapGenerator, a heater (thermostat 29 °C) and a mister, 20 s of environment warm-up,
        /// 2 frogs and 3 insects run for one second. Never change these bytes.
        /// </summary>
        private const string Version2Sample =
            "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";

        [Test]
        public void Version2Save_StillLoads()
        {
            var w = SaveSystem.FromBytes(System.Convert.FromBase64String(Version2Sample));
            Assert.AreEqual(777UL, w.Seed);
            Assert.AreEqual(1.0, w.SimTime, 1e-9);
            Assert.AreEqual(5, w.Agents.Count);
            Assert.AreEqual(-1.5421901f, w.Agents.PosX[0]);
            Assert.AreEqual(1.2516893f, w.Agents.PosY[0]);
            Assert.AreEqual(1.1404356f, w.Terrain.Height[0]);
            Assert.AreEqual(28.989565f, w.Environment.Temperature[0]);
            Assert.AreEqual(60.34111f, w.Environment.Humidity[5]);
            Assert.AreEqual(2, w.Devices.Count);
            Assert.AreEqual(DeviceKind.Heater, w.Devices.All[0].Kind);
            Assert.AreEqual(29f, w.Devices.All[0].Setting);
            Assert.AreEqual(26.199963f, w.Devices.All[1].Timer);
            // Version 2 had no ages: frogs load as adults, crickets get a lifespan.
            for (int i = 0; i < w.Agents.Count; i++)
            {
                if (w.Agents.Kind[i] == AgentKind.Insect) Assert.Greater(w.Agents.Lifespan[i], 0f);
                else Assert.Greater(w.Agents.Age[i], 0f);
            }
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 60; i++) loop.Advance(1.0 / 30.0);
        }

        /// <summary>
        /// A real version 3 save from the Phase 1 game: seed 909, 3 x 2 m half-size tank made by
        /// MapGenerator, a heater and a feeder, 20 s of environment warm-up, 2 frogs and 3 crickets
        /// run for one second. Never change these bytes.
        /// </summary>
        private const string Version3Sample =
            "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";

        [Test]
        public void Version3Save_StillLoads()
        {
            var w = SaveSystem.FromBytes(System.Convert.FromBase64String(Version3Sample));
            Assert.AreEqual(909UL, w.Seed);
            Assert.AreEqual(1.0, w.SimTime, 1e-9);
            Assert.AreEqual(5, w.Agents.Count);
            Assert.AreEqual(6, w.Agents.NextId);
            Assert.AreEqual(-0.6086828f, w.Agents.PosX[0]);
            Assert.AreEqual(Goal.Drink, w.Agents.Goal[1]);
            Assert.AreEqual(599.4023f, w.Agents.Lifespan[2]);
            Assert.AreEqual(2, w.Devices.Count);
            Assert.AreEqual(DeviceKind.Feeder, w.Devices.All[1].Kind);
            // Version 3 frogs had no species, sex, family or genes: they load as grown White's
            // tree frogs (frog 1 female, frog 2 male) with average genes and a lifespan.
            var sp = SpeciesCatalog.Frog;
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(AgentKind.Frog, w.Agents.Kind[i]);
                Assert.AreEqual(0, w.Agents.Species[i]);
                Assert.AreEqual(LifeStage.Adult, w.Agents.Stage[i]);
                Assert.AreEqual(sp.AdultAt, w.Agents.Growth[i]);
                Assert.AreEqual(Genome.Default.Size, w.Agents.Genes[i].Size);
                Assert.Greater(w.Agents.Lifespan[i], w.Agents.Age[i] + 1000f);
                Assert.AreEqual(0, w.Agents.MotherId[i]);
            }
            Assert.AreEqual(Sex.Female, w.Agents.Sex[0]);
            Assert.AreEqual(Sex.Male, w.Agents.Sex[1]);
            Assert.AreEqual(0, w.Escaped.Count);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 60; i++) loop.Advance(1.0 / 30.0);
        }

        /// <summary>
        /// A real version 4 save, made by the game just before version 5: seed 4040, a 6 x 4 m tank
        /// from MapGenerator with a mister, a White's tree frog female carrying eggs, a red-eyed
        /// male, a red-eyed egg, two crickets and one frog that escaped, run for one second.
        /// Never change these bytes.
        /// </summary>
        private const string Version4Sample =
            "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";

        [Test]
        public void Version4Save_StillLoads()
        {
            var w = SaveSystem.FromBytes(System.Convert.FromBase64String(Version4Sample));
            var a = w.Agents;
            Assert.AreEqual(4040UL, w.Seed);
            Assert.AreEqual(1.0, w.SimTime, 1e-9);
            Assert.AreEqual(5, a.Count);
            Assert.AreEqual(7, a.NextId);
            Assert.AreEqual(1, a.Id[0]);
            Assert.AreEqual(-1.245116f, a.PosX[0]);
            Assert.AreEqual(LifeStage.Adult, a.Stage[0]);
            Assert.AreEqual(2, a.MateId[0]);
            Assert.AreEqual(12.5f, a.Gravid[0]);
            Assert.AreEqual(1, a.Species[1]);
            Assert.AreEqual(LifeStage.Egg, a.Stage[1]);
            Assert.AreEqual(1, a.MotherId[1]);
            Assert.AreEqual(5, a.FatherId[1]);
            Assert.AreEqual(AgentKind.Insect, a.Kind[2]);
            Assert.AreEqual(Sex.Male, a.Sex[4]);
            Assert.AreEqual(1.25f, a.Genes[4].HeatShift);
            Assert.AreEqual(0.29083133f, a.PosX[4]);
            Assert.AreEqual(1, w.Escaped.Count);
            Assert.AreEqual(2, w.Escaped[0].Id);
            Assert.AreEqual("too cold", w.Escaped[0].Reason);
            // Version 4 had no social life, territories or population settings: they start fresh.
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(0f, a.Social[i]);
                Assert.IsFalse(a.HasHome[i]);
            }
            Assert.IsTrue(w.BreedingEnabled);
            Assert.AreEqual(0, w.BreedingLimit);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 60; i++) loop.Advance(1.0 / 30.0);
        }

        [Test]
        public void RoundTrip_PreservesLifeStagesFamiliesGenesAndEscapedFrogs()
        {
            var w = World.CreateStarterTank(8, 6, 30);
            var a = w.Agents;
            int red = w.AddFrog(1f, 1f, 1, Sex.Male);
            a.Genes[red].HeatShift = 1.5f;
            a.Stress[red] = 0.6f;
            int mum = 0;
            a.MateId[mum] = a.Id[red];
            a.MateGenes[mum] = a.Genes[red];
            a.Gravid[mum] = 42f;
            int egg = w.AddEgg(0.5f, 0.5f, 1, Sex.Female, a.Genes[mum], a.Genes[red]);
            a.MotherId[egg] = a.Id[mum];
            a.FatherId[egg] = a.Id[red];
            a.Goal[red] = Goal.Escape;
            w.Escape(3, "too cold");
            a.RemoveAt(3);

            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            var b = loaded.Agents;
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.Species[i], b.Species[i]);
                Assert.AreEqual(a.Sex[i], b.Sex[i]);
                Assert.AreEqual(a.Stage[i], b.Stage[i]);
                Assert.AreEqual(a.Growth[i], b.Growth[i]);
                Assert.AreEqual(a.Stress[i], b.Stress[i]);
                Assert.AreEqual(a.MotherId[i], b.MotherId[i]);
                Assert.AreEqual(a.FatherId[i], b.FatherId[i]);
                Assert.AreEqual(a.MateId[i], b.MateId[i]);
                Assert.AreEqual(a.Gravid[i], b.Gravid[i]);
                Assert.AreEqual(a.BreedCooldown[i], b.BreedCooldown[i]);
                Assert.AreEqual(a.Genes[i], b.Genes[i]);
                Assert.AreEqual(a.MateGenes[i], b.MateGenes[i]);
                Assert.AreEqual(a.Goal[i], b.Goal[i]);
            }
            Assert.AreEqual(1, loaded.Escaped.Count);
            var e = w.Escaped[0];
            var f = loaded.Escaped[0];
            Assert.AreEqual(e.Id, f.Id);
            Assert.AreEqual(e.Species, f.Species);
            Assert.AreEqual(e.Sex, f.Sex);
            Assert.AreEqual(e.Age, f.Age);
            Assert.AreEqual(e.Genes, f.Genes);
            Assert.AreEqual("too cold", f.Reason);
            Assert.AreEqual(e.Time, f.Time);
        }

        [Test]
        public void RoundTrip_PreservesBrainStateAndAges()
        {
            var w = World.CreateStarterTank(5, 6, 30);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 600; i++) loop.Advance(1.0 / 60.0);
            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            var a = w.Agents;
            var b = loaded.Agents;
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.Comfort[i], b.Comfort[i]);
                Assert.AreEqual(a.Goal[i], b.Goal[i]);
                Assert.AreEqual(a.GoalTime[i], b.GoalTime[i]);
                Assert.AreEqual(a.TargetId[i], b.TargetId[i]);
                Assert.AreEqual(a.TargetX[i], b.TargetX[i]);
                Assert.AreEqual(a.TargetZ[i], b.TargetZ[i]);
                Assert.AreEqual(a.Age[i], b.Age[i]);
                Assert.AreEqual(a.Lifespan[i], b.Lifespan[i]);
                Assert.AreEqual(a.Timer[i], b.Timer[i]);
                Assert.AreEqual(a.Cooldown[i], b.Cooldown[i]);
            }
            Assert.AreEqual(DeviceKind.Feeder, loaded.Devices.All[2].Kind);
            Assert.AreEqual(w.Devices.All[2].Supply, loaded.Devices.All[2].Supply);
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

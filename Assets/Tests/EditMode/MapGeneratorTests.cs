using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 4 part 1: biomes, map settings, map codes and sandbox switches.</summary>
    public class MapGeneratorTests
    {
        private static ulong HashOf(TerrainGrid t)
        {
            ulong h = 1469598103934665603UL;
            foreach (var f in t.Height) { h ^= (ulong)System.BitConverter.SingleToInt32Bits(f); h *= 1099511628211UL; }
            foreach (var l in t.Layer) { h ^= (byte)l; h *= 1099511628211UL; }
            return h;
        }

        private static float DryShare(TerrainGrid t)
        {
            int dry = 0;
            for (int i = 0; i < t.Height.Length; i++) if (t.Height[i] >= t.WaterLevel) dry++;
            return dry / (float)t.Height.Length;
        }

        [Test]
        public void ClassicStarterTank_IsUnchangedFromEarlierVersions()
        {
            // Worked out with the map generator as it was before biomes, so old seeds still give the same tank.
            Assert.AreEqual(776475108608429141UL, HashOf(World.CreateStarterTank(12345, 8, 80).Terrain));
            Assert.AreEqual(5725494632090735645UL, HashOf(World.CreateStarterTank(1, 8, 80).Terrain));
        }

        [Test]
        public void SameSettings_GiveTheSameMap_InEveryBiome()
        {
            foreach (var b in Biomes.All)
            {
                var s = MapSettings.For(b.Id, 2024);
                var a = World.CreateTank(s, 4, 10);
                var c = World.CreateTank(s, 4, 10);
                Assert.AreEqual(HashOf(a.Terrain), HashOf(c.Terrain), b.Name);
                Assert.AreEqual(a.Agents.PosX[0], c.Agents.PosX[0], b.Name);
                var other = World.CreateTank(MapSettings.For(b.Id, 2025), 4, 10);
                Assert.AreNotEqual(HashOf(a.Terrain), HashOf(other.Terrain), b.Name + ": another seed gives another map");
            }
        }

        [Test]
        public void EveryBiome_HasLandAndWater_AtEverySliderSetting()
        {
            foreach (var b in Biomes.All)
                for (int seed = 1; seed <= 4; seed++)
                    foreach (float v in new[] { 0f, 1f, 2f })
                    {
                        var s = MapSettings.For(b.Id, (ulong)seed);
                        s.Hills = v; s.Water = v; s.Plants = v;
                        var w = World.CreateTank(s, 0, 0);
                        float dry = DryShare(w.Terrain);
                        string what = b.Name + " seed " + seed + " sliders " + v;
                        Assert.Greater(dry, b.Id == Biome.Islands ? 0.2f : 0.5f, what + ": enough dry ground for frogs");
                        bool desertWithNoWater = b.Id == Biome.Desert && v == 0f;
                        if (!desertWithNoWater) Assert.Less(dry, 0.995f, what + ": some water");
                        foreach (var d in w.Devices.All)
                            Assert.IsFalse(w.Terrain.IsWater(d.X, d.Z), what + ": " + d.Kind + " on dry ground");
                    }
        }

        [Test]
        public void Biomes_LookDifferent()
        {
            int Count(World w, GroundLayer layer)
            {
                int n = 0;
                foreach (var l in w.Terrain.Layer) if (l == layer) n++;
                return n;
            }
            var forest = World.CreateTank(MapSettings.For(Biome.Rainforest, 7), 0, 0);
            var desert = World.CreateTank(MapSettings.For(Biome.Desert, 7), 0, 0);
            var islands = World.CreateTank(MapSettings.For(Biome.Islands, 7), 0, 0);
            var classic = World.CreateTank(MapSettings.For(Biome.Classic, 7), 0, 0);
            Assert.Greater(Count(forest, GroundLayer.Moss), forest.Terrain.Layer.Length / 2, "rainforest is mostly moss");
            Assert.Greater(Count(desert, GroundLayer.Sand), desert.Terrain.Layer.Length / 2, "desert is mostly sand");
            Assert.Less(DryShare(islands.Terrain), DryShare(classic.Terrain), "islands have more water");
            Assert.AreEqual(2, desert.Style, "the desert uses the Desert style set");
            Assert.Greater(desert.Environment.AmbientTemperature, classic.Environment.AmbientTemperature);
            Assert.Less(desert.Environment.AmbientHumidity, classic.Environment.AmbientHumidity);
        }

        [Test]
        public void Sliders_ChangeTheMap()
        {
            var flat = MapSettings.For(Biome.Classic, 5); flat.Hills = 0f;
            var hilly = MapSettings.For(Biome.Classic, 5); hilly.Hills = 2f;
            float Spread(World w)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var h in w.Terrain.Height) if (h > w.Terrain.WaterLevel) { if (h < lo) lo = h; if (h > hi) hi = h; }
                return hi - lo;
            }
            Assert.Greater(Spread(World.CreateTank(hilly, 0, 0)), Spread(World.CreateTank(flat, 0, 0)) + 0.3f);

            var dry = MapSettings.For(Biome.Rainforest, 5); dry.Water = 0f;
            var wet = MapSettings.For(Biome.Rainforest, 5); wet.Water = 2f;
            Assert.Greater(DryShare(World.CreateTank(dry, 0, 0).Terrain), DryShare(World.CreateTank(wet, 0, 0).Terrain));
        }

        [Test]
        public void BiggerSize_MakesABiggerTank_WithTheBiomesFrogs()
        {
            var s = MapSettings.For(Biome.Rainforest, 3);
            s.Size = 2;
            var w = World.CreateTank(s, 4, 10);
            Assert.AreEqual(TankLooks.Sizes[2].HalfX, w.HalfSizeX);
            Assert.AreEqual(2, TankLooks.SizeIndex(w));
            Assert.AreEqual(1, w.Agents.Species[0], "rainforest starts with red-eyed tree frogs");
            Assert.AreEqual(1, w.Agents.Species[1]);
            Assert.AreEqual(3, w.Agents.Species[2], "then White-lipped tree frogs");
            Assert.AreEqual(Sex.Female, w.Agents.Sex[0]);
            Assert.AreEqual(Sex.Male, w.Agents.Sex[1]);
        }

        [Test]
        public void MapCode_RoundTrips()
        {
            var s = MapSettings.For(Biome.Islands, 987654321);
            s.Size = 1; s.Hills = 1.25f; s.Water = 0.4f; s.Plants = 2f;
            string code = s.ToCode();
            Assert.AreEqual("FTMAP1:islands:987654321:large:125:40:200", code);
            Assert.IsTrue(MapSettings.TryParse("  " + code.ToLowerInvariant() + " ", out var back, out string why), why);
            Assert.AreEqual(s.Biome, back.Biome);
            Assert.AreEqual(s.Seed, back.Seed);
            Assert.AreEqual(s.Size, back.Size);
            Assert.AreEqual(s.Hills, back.Hills);
            Assert.AreEqual(s.Water, back.Water);
            Assert.AreEqual(s.Plants, back.Plants);
            Assert.AreEqual(HashOf(World.CreateTank(s, 0, 0).Terrain), HashOf(World.CreateTank(back, 0, 0).Terrain));

            Assert.IsTrue(MapSettings.TryParse("FTMAP1:Desert:12", out var shortCode, out why), why);
            Assert.AreEqual(Biome.Desert, shortCode.Biome);
            Assert.AreEqual(1f, shortCode.Hills);
        }

        [Test]
        public void BadMapCodes_AreRefusedWithAReason()
        {
            foreach (var bad in new[] { "", "hello", "FTMAP1:swamp:1", "FTMAP1:desert:-4", "FTMAP1:desert:1:tiny",
                                        "FTMAP1:desert:1:large:500", "FTMAP1:desert:1:large:1:1:1:1" })
            {
                Assert.IsFalse(MapSettings.TryParse(bad, out _, out string why), bad);
                Assert.IsNotEmpty(why, bad);
            }
        }

        [Test]
        public void TextSeeds_AreStable()
        {
            Assert.AreEqual(42UL, MapSettings.SeedFromText(" 42 "));
            Assert.AreEqual(MapSettings.SeedFromText("Froggy"), MapSettings.SeedFromText("froggy"));
            Assert.AreNotEqual(MapSettings.SeedFromText("froggy"), MapSettings.SeedFromText("toady"));
        }

        // ---------------------------------------------------------------- sandbox switches

        private static World Sandbox(SandboxRules rules)
        {
            var w = World.CreateStarterTank(77, 6, 30);
            w.Rules = rules;
            return w;
        }

        [Test]
        public void NoNeeds_KeepsFrogsFedAndRested()
        {
            var w = Sandbox(SandboxRules.NoNeeds);
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 300; k++) loop.Advance(0.2);
            for (int i = 0; i < w.Agents.Count; i++)
            {
                if (!w.Agents.IsLandFrog(i)) continue;
                Assert.AreEqual(0f, w.Agents.Hunger[i]);
                Assert.AreEqual(0f, w.Agents.Thirst[i]);
                Assert.AreEqual(0f, w.Agents.Fatigue[i]);
            }
        }

        [Test]
        public void PerfectClimateAndNoStress_KeepFrogsCalm_InAColdDryRoom()
        {
            var w = Sandbox(SandboxRules.PerfectClimate | SandboxRules.NoStress);
            foreach (var d in w.Devices.All) d.Enabled = false;
            w.Environment.AmbientTemperature = 8f;
            w.Environment.AmbientHumidity = 10f;
            w.Environment.ResetToAmbient();
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 300; k++) loop.Advance(0.2);
            int frogs = 0;
            for (int i = 0; i < w.Agents.Count; i++)
            {
                if (!w.Agents.IsLandFrog(i)) continue;
                frogs++;
                Assert.AreEqual(0f, w.Agents.Stress[i]);
                Assert.Greater(w.Agents.Comfort[i], 0.6f);
            }
            Assert.Greater(frogs, 0);
        }

        [Test]
        public void NoOldAge_FrogsOutliveTheirLifespan()
        {
            var w = Sandbox(SandboxRules.NoOldAge);
            int id = w.Agents.Id[0];
            w.Agents.Age[0] = w.Agents.Lifespan[0] + 5f;
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 30; k++) loop.Advance(0.2);
            Assert.GreaterOrEqual(w.Agents.IndexOf(id), 0);

            w.Rules = SandboxRules.None;
            for (int k = 0; k < 30; k++) loop.Advance(0.2);
            Assert.AreEqual(-1, w.Agents.IndexOf(id), "with old age back on, it dies");
        }

        [Test]
        public void NoDirt_AndEndlessFeeders()
        {
            var w = Sandbox(SandboxRules.NoDirt | SandboxRules.EndlessFeeders);
            w.Dirt = 70f;
            var feeder = w.Devices.All[2];
            Assert.AreEqual(DeviceKind.Feeder, feeder.Kind);
            feeder.Supply = 0f;
            var loop = SimulationFactory.CreateDefault(w);
            for (int k = 0; k < 20; k++) loop.Advance(0.2);
            Assert.AreEqual(0f, w.Dirt);
            Assert.Greater(feeder.Supply, 100f);
        }

        [Test]
        public void Switches_OnlyCountInTheSandbox()
        {
            var w = Sandbox(SandboxRules.NoNeeds);
            Assert.IsTrue(w.Has(SandboxRules.NoNeeds));
            w.Economy.StartMoneyMode();
            Assert.IsFalse(w.Has(SandboxRules.NoNeeds), "money mode runs everything");
        }

        [Test]
        public void MapAndSwitches_AreSaved()
        {
            var s = MapSettings.For(Biome.Volcanic, 31337);
            s.Hills = 1.5f; s.Water = 0.5f; s.Plants = 0.25f;
            var w = World.CreateTank(s, 4, 10);
            w.Rules = SandboxRules.NoDirt | SandboxRules.NoLoneliness;
            var back = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.AreEqual(s.ToCode(), back.Map.ToCode());
            Assert.AreEqual(w.Rules, back.Rules);
            Assert.AreEqual(4, back.Style);
        }
    }
}

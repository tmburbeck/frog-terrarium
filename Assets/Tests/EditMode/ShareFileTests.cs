using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 4 part 3: map and species files to share, and telling shared texts apart.</summary>
    public class ShareFileTests
    {
        private static World SculptedTank()
        {
            var s = MapSettings.For(Biome.Rainforest, 515);
            s.Size = 1;
            var w = World.CreateTank(s, 4, 20);
            TerrainBrush.Apply(w.Terrain, BrushMode.Raise, 2f, 1f, 1.5f, 1f, 1f);
            TerrainBrush.Apply(w.Terrain, BrushMode.Paint, -3f, -1f, 1.5f, 1f, 1f, GroundLayer.Rock);
            w.Devices.All[0].Setting = 29.5f;
            w.Devices.Add(DeviceKind.Controller, 1f, 2f);
            Assert.IsNotNull(w.Parts.Place(new PlacedPart { Kind = PartKind.Platform, X = -5f, Z = 2f, Scale = 1f }, w.Terrain, out string why), why);
            Assert.IsNotNull(w.Parts.Place(new PlacedPart { Kind = PartKind.Fern, X = 6f, Z = -2f, Scale = 1.5f, R = 10, G = 200, B = 30 }, w.Terrain, out why), why);
            w.Style = 5;
            return w;
        }

        [Test]
        public void Map_RoundTripsWithNoDifferences()
        {
            var w = SculptedTank();
            var map = MapFile.FromWorld(w, "My jungle!", "Tim");
            string text = map.ToText();
            Assert.AreEqual(ShareKind.Map, ShareText.Detect("  " + text));

            // "Another computer": read it back, start a game from it, and export it again.
            var back = MapFile.FromText(text);
            Assert.AreEqual("My jungle", back.Name);
            Assert.AreEqual("Tim", back.Author);
            Assert.AreEqual(map.Fingerprint, back.Fingerprint);
            var other = back.CreateWorld(4, 20);
            Assert.AreEqual(w.HalfSizeX, other.HalfSizeX);
            Assert.AreEqual(w.Terrain.Height, other.Terrain.Height);
            Assert.AreEqual(w.Terrain.Layer, other.Terrain.Layer);
            Assert.AreEqual(w.Terrain.WaterLevel, other.Terrain.WaterLevel);
            Assert.AreEqual(w.Environment.AmbientTemperature, other.Environment.AmbientTemperature);
            Assert.AreEqual(5, other.Style);
            Assert.AreEqual(w.Devices.Count, other.Devices.Count);
            Assert.AreEqual(29.5f, other.Devices.All[0].Setting);
            Assert.AreEqual(DeviceKind.Controller, other.Devices.All[other.Devices.Count - 1].Kind);
            Assert.AreEqual(w.Parts.Count, other.Parts.Count);
            Assert.AreEqual(w.Map.ToCode(), other.Map.ToCode());
            string again = MapFile.FromWorld(other, "My jungle", "Tim").ToText();
            Assert.AreEqual(text, again, "exporting the imported map gives exactly the same file");

            var loop = SimulationFactory.CreateDefault(other);
            for (int i = 0; i < 40; i++) loop.Advance(0.25);
        }

        [Test]
        public void DamagedMap_IsRefusedWithAReason()
        {
            string text = MapFile.FromWorld(SculptedTank(), "x").ToText();
            int dot = text.LastIndexOf('.');
            string wrongPrint = text.Substring(0, dot + 1) + "00000000";
            string cut = text.Substring(0, text.Length / 2);
            string swapped = text.Substring(0, 40) + (text[40] == 'A' ? 'B' : 'A') + text.Substring(41);
            foreach (var bad in new[] { "", "hello", wrongPrint, cut, swapped })
            {
                var e = Assert.Throws<SaveFormatException>(() => MapFile.FromText(bad));
                Assert.IsNotEmpty(e.Message);
            }
        }

        private static World TankWithRegisteredSpecies(out RegisteredSpecies reg)
        {
            var w = World.CreateStarterTank(808, 0, 10);
            var g = Genome.Default;
            g.Alleles = Mutations.WithCopies(0u, Mutation.Teeth, 2);
            g.Generation = 3;
            int first = -1;
            for (int k = 0; k < 2; k++)
            {
                int i = w.AddFrog(1f + k, 1f, 0, k == 0 ? Sex.Female : Sex.Male);
                w.Agents.Genes[i] = g;
                if (first < 0) first = i;
            }
            int type = Lineage.TypeOf(w.Agents, first);
            Assert.IsTrue(w.Register(type, "Toothy tree frog"));
            reg = w.Registered[0];
            return w;
        }

        [Test]
        public void Species_TravelsToAnotherTank()
        {
            var w = TankWithRegisteredSpecies(out var reg);
            var file = SpeciesFile.FromWorld(w, reg, "Tim", out string why);
            Assert.IsNotNull(file, why);
            string text = file.ToText();
            Assert.AreEqual(ShareKind.Species, ShareText.Detect(text));

            var other = World.CreateStarterTank(909, 4, 20);
            int before = other.FrogCount();
            var back = SpeciesFile.FromText(text);
            Assert.AreEqual("Toothy tree frog", back.Name);
            Assert.IsTrue(back.AddTo(other, 0f, 0f, out why), why);
            Assert.AreEqual(before + 2, other.FrogCount());
            Assert.AreEqual(1, other.Registered.Count);
            Assert.AreEqual(reg.Type, other.Registered[0].Type);
            int newFrog = other.Agents.Count - 1;
            Assert.AreEqual("Toothy tree frog", other.KindName(newFrog));
            Assert.AreEqual(Sex.Male, other.Agents.Sex[newFrog]);
            Assert.AreEqual(Sex.Female, other.Agents.Sex[newFrog - 1]);
            StringAssert.Contains("from Tim", other.News);

            // A second pair keeps the one registration.
            Assert.IsTrue(back.AddTo(other, 0f, 0f, out why), why);
            Assert.AreEqual(1, other.Registered.Count);
        }

        [Test]
        public void Species_CostsMoneyInMoneyMode_AndNeedsAPairToShare()
        {
            var w = TankWithRegisteredSpecies(out var reg);
            var file = SpeciesFile.FromWorld(w, reg, "", out string why);
            var poor = World.CreateStarterTank(5, 2, 10);
            poor.Economy.StartMoneyMode();
            poor.Economy.Money = 1;
            Assert.IsFalse(file.AddTo(poor, 0f, 0f, out why));
            Assert.IsNotEmpty(why);
            poor.Economy.Money = 1000;
            Assert.IsTrue(file.AddTo(poor, 0f, 0f, out why), why);
            Assert.AreEqual(1000 - file.Cost, poor.Economy.Money, 0.01);

            for (int i = 0; i < w.Agents.Count; i++) if (w.Agents.Kind[i] == AgentKind.Frog && w.Agents.Sex[i] == Sex.Female) { w.Rehome(i); break; }
            Assert.IsNull(SpeciesFile.FromWorld(w, reg, "", out why), "only a male is left");
            StringAssert.Contains("female", why);
        }

        [Test]
        public void SpeciesName_ClashIsRefused()
        {
            var w = TankWithRegisteredSpecies(out var reg);
            var file = SpeciesFile.FromWorld(w, reg, "", out _);
            file.Name = "White's tree frog";
            var other = World.CreateStarterTank(5, 2, 10);
            Assert.IsFalse(SpeciesFile.FromText(file.ToText()).AddTo(other, 0f, 0f, out string why));
            StringAssert.Contains("real species", why);
        }

        [Test]
        public void Blueprint_RoundTripsWithNoDifferences()
        {
            var w = SculptedTank();
            string text = Blueprint.FromParts(w.Parts, "Jungle gym").ToText();
            var back = Blueprint.FromText(text);
            Assert.AreEqual(text, back.ToText(), "reading and writing a blueprint again gives exactly the same text");
        }

        [Test]
        public void SharedTexts_AreToldApart()
        {
            var w = SculptedTank();
            Assert.AreEqual(ShareKind.Blueprint, ShareText.Detect(Blueprint.FromParts(w.Parts, "b").ToText()));
            Assert.AreEqual(ShareKind.MapCode, ShareText.Detect(w.Map.ToCode()));
            Assert.AreEqual(ShareKind.Unknown, ShareText.Detect("hello"));
            Assert.AreEqual(".ftmap", ShareText.ExtensionOf(ShareKind.Map));
            Assert.AreEqual(8, ShareText.Fingerprint(new byte[] { 1, 2, 3 }).Length);
        }
    }
}

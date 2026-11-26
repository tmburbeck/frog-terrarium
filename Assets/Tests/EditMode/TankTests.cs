using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 3 part 3b: bigger tanks and style sets.</summary>
    public class TankTests
    {
        [Test]
        public void Sizes_GetBigger_AndCostMore()
        {
            for (int k = 1; k < TankLooks.Sizes.Length; k++)
            {
                Assert.Greater(TankLooks.Sizes[k].HalfX, TankLooks.Sizes[k - 1].HalfX);
                Assert.Greater(TankLooks.Sizes[k].HalfZ, TankLooks.Sizes[k - 1].HalfZ);
                Assert.Greater(TankLooks.Sizes[k].Cost, TankLooks.Sizes[k - 1].Cost);
            }
            var w = new World(1);
            Assert.AreEqual(0, TankLooks.SizeIndex(w), "a new tank is the first size");
        }

        [Test]
        public void Upgrading_KeepsEverythingWhereItWas()
        {
            var w = World.CreateStarterTank(42, 6, 20);
            w.Parts.Place(new PlacedPart { Kind = PartKind.Rock, X = 3f, Z = 2f, Scale = 1f }, w.Terrain, out _);
            float h = w.Terrain.HeightAt(3.3f, -1.7f);
            var layer = w.Terrain.LayerAt(-4.1f, 2.2f);
            float temp = w.Environment.TemperatureAt(1f, 1f);
            float frogX = w.Agents.PosX[0];
            int capacity = w.FrogCapacity;

            Assert.IsTrue(TankLooks.Upgrade(w, out string why), why);
            Assert.AreEqual(13f, w.HalfSizeX);
            Assert.AreEqual(13f, w.Terrain.HalfSizeX);
            Assert.AreEqual(8f, w.Environment.HalfSizeZ);
            Assert.AreEqual(1, TankLooks.SizeIndex(w));
            Assert.AreEqual(h, w.Terrain.HeightAt(3.3f, -1.7f), 1e-4f, "the old ground is copied exactly");
            Assert.AreEqual(layer, w.Terrain.LayerAt(-4.1f, 2.2f));
            Assert.AreEqual(temp, w.Environment.TemperatureAt(1f, 1f), 0.2f);
            Assert.IsFalse(w.Terrain.IsWater(12f, 7f), "the new border is dry ground");
            Assert.AreEqual(frogX, w.Agents.PosX[0]);
            Assert.AreEqual(1, w.Parts.Count);
            Assert.Greater(w.FrogCapacity, capacity, "room for more frogs");

            // The bigger tank runs, saves and loads.
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 90; i++) loop.Advance(1.0 / 30.0);
            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.AreEqual(13f, loaded.HalfSizeX);
            Assert.AreEqual(w.Terrain.HeightAt(11f, 7f), loaded.Terrain.HeightAt(11f, 7f), 1e-4f);

            Assert.IsTrue(TankLooks.Upgrade(w, out why), why);
            Assert.IsFalse(TankLooks.Upgrade(w, out why), "Huge is the biggest");
            Assert.IsFalse(w.ResizeTank(5f, 5f), "tanks never shrink");
        }

        [Test]
        public void MoneyMode_PaysForSizesAndStyles()
        {
            var w = new World(2);
            w.Economy.StartMoneyMode();
            Assert.IsFalse(TankLooks.Upgrade(w, out string why), "$300 is not enough for a large tank");
            StringAssert.Contains("Not enough money", why);
            Assert.AreEqual(10f, w.HalfSizeX);

            Assert.IsTrue(TankLooks.StyleOwned(w, 0), "classic is free");
            Assert.IsFalse(TankLooks.StyleOwned(w, 1));
            Assert.IsTrue(TankLooks.UseStyle(w, 1, out why), why);
            Assert.AreEqual(1, w.Style);
            Assert.AreEqual(Economy.StartingMoney - TankLooks.Styles[1].Cost, w.Economy.Money, 1e-6);
            Assert.IsTrue(TankLooks.UseStyle(w, 0, out _));
            Assert.IsTrue(TankLooks.UseStyle(w, 1, out _), "bought once, free to switch back");
            Assert.AreEqual(Economy.StartingMoney - TankLooks.Styles[1].Cost, w.Economy.Money, 1e-6);

            w.Economy.Money = 5000;
            Assert.IsTrue(TankLooks.Upgrade(w, out why), why);
            Assert.AreEqual(5000 - TankLooks.Sizes[1].Cost, w.Economy.Money, 1e-6);

            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.AreEqual(1, loaded.Style);
            Assert.IsTrue(TankLooks.StyleOwned(loaded, 1));
            Assert.IsFalse(TankLooks.StyleOwned(loaded, 2));
        }

        [Test]
        public void EveryStyle_HasItsColours()
        {
            foreach (var s in TankLooks.Styles)
            {
                Assert.AreEqual(12, s.Ground.Length, s.Name);
                Assert.AreEqual(18, s.Palette.Length, s.Name);
                Assert.IsNotEmpty(s.Description);
            }
        }
    }
}

using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    public class EnvironmentTests
    {
        private static void Run(World w, float seconds)
        {
            var sys = new EnvironmentSystem();
            float dt = 1f / EnvironmentSystem.Rate;
            for (float t = 0f; t < seconds; t += dt) sys.Tick(w, dt);
        }

        [Test]
        public void EmptyTank_StaysAtRoomConditions()
        {
            var w = new World(1);
            Run(w, 60f);
            Assert.AreEqual(w.Environment.AmbientTemperature, w.Environment.TemperatureAt(0f, 0f), 0.01f);
            Assert.AreEqual(w.Environment.AmbientHumidity, w.Environment.HumidityAt(0f, 0f), 0.01f);
        }

        [Test]
        public void Heater_WarmsNearbyMoreThanFarAway_AndNeverPassesItsRatedMaximum()
        {
            // QA case EN-01.
            var w = new World(1);
            var heater = w.Devices.Add(DeviceKind.Heater, -5f, 0f);
            heater.Setting = DeviceStore.HeaterMaxTemperature; // ask for as hot as it goes
            Run(w, 600f);
            float near = w.Environment.TemperatureAt(-5f, 0f);
            float far = w.Environment.TemperatureAt(8f, 0f);
            Assert.Greater(near, w.Environment.AmbientTemperature + 4f);
            Assert.Greater(near, far + 2f);
            foreach (float t in w.Environment.Temperature)
                Assert.LessOrEqual(t, DeviceStore.HeaterMaxTemperature + 1e-4f);
        }

        [Test]
        public void Heater_ThermostatHoldsTheTarget()
        {
            var w = new World(1);
            var heater = w.Devices.Add(DeviceKind.Heater, 0f, 0f);
            heater.Setting = 26f;
            Run(w, 900f);
            Assert.AreEqual(26f, w.Environment.TemperatureAt(0f, 0f), 1.2f);
        }

        [Test]
        public void SwitchedOffHeater_DoesNothing()
        {
            var w = new World(1);
            w.Devices.Add(DeviceKind.Heater, 0f, 0f).Enabled = false;
            Run(w, 120f);
            Assert.AreEqual(w.Environment.AmbientTemperature, w.Environment.TemperatureAt(0f, 0f), 0.01f);
        }

        [Test]
        public void Mister_RaisesHumidityAroundIt()
        {
            var w = new World(1);
            w.Devices.Add(DeviceKind.Mister, 4f, 0f);
            Run(w, 300f);
            Assert.Greater(w.Environment.HumidityAt(4f, 0f), w.Environment.AmbientHumidity + 8f);
            Assert.Greater(w.Environment.HumidityAt(4f, 0f), w.Environment.HumidityAt(-8f, 0f) + 5f);
        }

        [Test]
        public void Pool_RaisesHumidityNearby()
        {
            // QA case EN-02.
            var w = new World(1);
            TerrainBrush.Apply(w.Terrain, BrushMode.Lower, 0f, 0f, 2.5f, 1f, 1f);
            Run(w, 300f);
            Assert.Greater(w.Environment.HumidityAt(0f, 0f), w.Environment.AmbientHumidity + 15f);
            Assert.Greater(w.Environment.HumidityAt(1.5f, 0f), w.Environment.HumidityAt(8f, 0f));
        }

        [Test]
        public void Moss_HoldsMoistureLongerThanSand()
        {
            var w = new World(1);
            var t = w.Terrain;
            TerrainBrush.Apply(t, BrushMode.Paint, -5f, 0f, 2.5f, 1f, 1f, GroundLayer.Moss);
            TerrainBrush.Apply(t, BrushMode.Paint, 5f, 0f, 2.5f, 1f, 1f, GroundLayer.Sand);
            for (int i = 0; i < w.Environment.Humidity.Length; i++) w.Environment.Humidity[i] = 90f;
            Run(w, 30f);
            Assert.Greater(w.Environment.HumidityAt(-5f, 0f), w.Environment.HumidityAt(5f, 0f) + 3f);
        }

        [Test]
        public void StarterTank_HasVariedConditions_AndIsTheSameEveryTime()
        {
            var a = World.CreateStarterTank(12345, 2, 2);
            var b = World.CreateStarterTank(12345, 2, 2);
            CollectionAssert.AreEqual(a.Environment.Temperature, b.Environment.Temperature);
            CollectionAssert.AreEqual(a.Environment.Humidity, b.Environment.Humidity);

            float minT = float.MaxValue, maxT = float.MinValue;
            foreach (float t in a.Environment.Temperature) { if (t < minT) minT = t; if (t > maxT) maxT = t; }
            Assert.Greater(maxT - minT, 2f, "the heater should make warm and cool zones");
            Assert.AreEqual(2, a.Devices.Count);
        }
    }
}

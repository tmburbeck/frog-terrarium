using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 3 part 4: automation tiers, cleaning, the smart controller and the tank health summary.</summary>
    public class AutomationTests
    {
        private const byte Whites = 0, RedEyed = 1, FireBellied = 2;

        private static World Flat(float humidity = 60f)
        {
            var w = new World(5);
            w.Environment.AmbientTemperature = 24f;
            w.Environment.AmbientHumidity = humidity;
            w.Environment.ResetToAmbient();
            return w;
        }

        private static void Run(World w, float seconds, params ISimSystem[] systems)
        {
            var loop = new SimulationLoop(w);
            foreach (var s in systems) loop.AddSystem(s);
            for (float t = 0f; t < seconds; t += 0.25f) loop.Advance(0.25);
        }

        [Test]
        public void SmartController_GivesEachCornerTheClimateItsFrogsLike()
        {
            var w = Flat();
            for (int k = 0; k < 3; k++) w.AddFrog(-7f, -2f + k, RedEyed, k % 2 == 0 ? Sex.Female : Sex.Male);
            for (int k = 0; k < 3; k++) w.AddFrog(7f, -2f + k, FireBellied, k % 2 == 0 ? Sex.Female : Sex.Male);
            var warm = w.Devices.Add(DeviceKind.Heater, -7f, 0f);
            var cool = w.Devices.Add(DeviceKind.Heater, 7f, 0f);
            var mister = w.Devices.Add(DeviceKind.Mister, -7f, 3f);
            Assert.IsTrue(w.SetDeviceMode(warm, DeviceMode.Smart, out string why), why);
            Assert.IsTrue(w.SetDeviceMode(cool, DeviceMode.Smart, out why), why);
            Assert.IsTrue(w.SetDeviceMode(mister, DeviceMode.Smart, out why), why);
            Assert.IsFalse(w.SetDeviceMode(warm, DeviceMode.Sensor, out why), "heaters have no sensor tier: the thermostat is one");

            // Without a controller, Smart devices keep their settings.
            Run(w, 3f, new AutomationSystem());
            Assert.AreEqual(27f, warm.Setting, 1e-4f);
            new AlertSystem().Refresh(w);
            Assert.IsNotNull(w.Alerts.Find(AlertSystem.NoControllerCause), "the player is told Smart needs a controller");

            w.Devices.Add(DeviceKind.Controller, 0f, 0f);
            Run(w, 3f, new AutomationSystem());
            AutomationSystem.Ideal(w, -7f, 0f, DeviceStore.SmartRadius, out float redT, out float redH, out _);
            AutomationSystem.Ideal(w, 7f, 0f, DeviceStore.SmartRadius, out float fireT, out _, out _);
            Assert.AreEqual(redT, warm.Setting, 1e-3f, "the red-eyed corner's heater suits red-eyed frogs");
            Assert.AreEqual(fireT, cool.Setting, 1e-3f);
            Assert.Greater(warm.Setting, cool.Setting + 3f, "red-eyed frogs like it warmer than fire-bellied toads");
            Assert.AreEqual(redH, mister.Target, 1e-3f);
            new AlertSystem().Refresh(w);
            Assert.IsNull(w.Alerts.Find(AlertSystem.NoControllerCause));
        }

        [Test]
        public void SensorMister_SpraysUntilTheAirIsDampEnough_ThenRests()
        {
            var w = Flat(40f);
            var m = w.Devices.Add(DeviceKind.Mister, 0f, 0f);
            Assert.IsTrue(w.SetDeviceMode(m, DeviceMode.Sensor, out string why), why);
            m.Target = 70f;
            var env = new EnvironmentSystem();
            Run(w, 1f, env);
            Assert.IsTrue(m.Active, "dry air: it sprays straight away, without waiting for a timer");
            Run(w, 120f, env);
            float near = EnvironmentSystem.AverageHumidity(w.Environment, 0f, 0f, DeviceStore.SensorRadius);
            Assert.AreEqual(70f, near, DeviceStore.MisterBand + 1.5f, "it holds the air near its target");

            m.Target = 45f;
            Run(w, 2f, env);
            Assert.IsFalse(m.Active, "already damper than the new target: it rests");
        }

        [Test]
        public void SensorFeeder_ReleasesOnlyWhenFoodIsShort_AndRefillsItself()
        {
            var w = Flat();
            w.Economy.StartMoneyMode();
            w.Economy.Researched = ~0u;
            w.AddFrog(1f, 1f, Whites, Sex.Female);
            w.AddFrog(1.5f, 1f, Whites, Sex.Male);
            var f = w.Devices.Add(DeviceKind.Feeder, 0f, 0f);
            Assert.IsTrue(w.SetDeviceMode(f, DeviceMode.Sensor, out string why), why);
            var rng = w.Random.Get("test");
            for (int k = 0; k < 10; k++) w.AddInsect(-3f, -2f, rng);
            float supply = f.Supply;
            Run(w, 15f, new FeederSystem());
            Assert.AreEqual(supply, f.Supply, 1e-4f, "5 crickets per frog is plenty: no release");

            for (int i = w.Agents.Count - 1; i >= 0; i--) if (w.Agents.Kind[i] == AgentKind.Insect) w.Agents.RemoveAt(i);
            f.Timer = 0.1f;
            Run(w, 1f, new FeederSystem());
            Assert.AreEqual(supply - f.Setting2, f.Supply, 1e-4f, "no crickets left: it releases a batch");

            f.Supply = 5f;
            double money = w.Economy.Money;
            Run(w, 1f, new AutomationSystem());
            Assert.AreEqual(DeviceStore.FeederCapacity, f.Supply, 1e-4f, "it refills itself when low");
            Assert.AreEqual(money - Economy.FeederRefillCost, w.Economy.Money, 1e-6, "and pays for the crickets");

            f.Supply = 5f;
            w.Economy.Money = 1;
            Run(w, 1f, new AutomationSystem());
            Assert.IsTrue(f.RefillFailed);
            Assert.AreEqual(5f, f.Supply, 1e-4f);
            new AlertSystem().Refresh(w);
            Assert.IsNotNull(w.Alerts.Find("feeder-" + f.Id + "-refill"), "the player hears it could not pay");
        }

        [Test]
        public void Frogs_MakeTheTankDirty_AndCleaningWorksAtEachTier()
        {
            var w = Flat();
            for (int k = 0; k < 10; k++) w.AddFrog(-4f + k, 0f, Whites, k % 2 == 0 ? Sex.Female : Sex.Male);
            Run(w, 1000f, new AutomationSystem());
            Assert.AreEqual(10 * AutomationSystem.DirtPerFrog * 1000f, w.Dirt, 0.5f, "dirt builds with the frogs");

            Assert.AreEqual(0f, NeedsSystem.DirtCost(World.DirtyAbove), 1e-6f, "a little dirt does not bother them");
            Assert.AreEqual(NeedsSystem.DirtPenalty, NeedsSystem.DirtCost(100f), 1e-6f);
            float clean = EconomySystem.Appeal(w, out _);
            w.Dirt = 100f;
            Assert.Less(EconomySystem.Appeal(w, out _), clean * 0.7f, "visitors don't like a filthy tank");

            w.Dirt = 70f;
            new AlertSystem().Refresh(w);
            var dirty = w.Alerts.Find(AlertSystem.DirtyCause);
            Assert.IsNotNull(dirty);
            Assert.AreEqual(AlertCategory.Tank, dirty.Category);

            w.Economy.StartMoneyMode();
            Assert.IsFalse(w.SetCleaning(DeviceMode.Sensor, out string why), "the cleaning service needs research");
            StringAssert.Contains("Automation", why);
            double money = w.Economy.Money;
            Assert.IsTrue(w.CleanTank(out why), why);
            Assert.AreEqual(0f, w.Dirt);
            Assert.AreEqual(money - Economy.CleaningCost, w.Economy.Money, 1e-6, "cleaning by hand costs a little");
            Assert.IsFalse(w.CleanTank(out _), "already clean");

            w.Economy.Researched |= 1u << (int)Research.Automation;
            Assert.IsTrue(w.SetCleaning(DeviceMode.Sensor, out why), why);
            w.Dirt = World.ServiceCleansAt - 0.01f;
            Run(w, 2f, new AutomationSystem());
            Assert.Less(w.Dirt, 1f, "the cleaning service cleaned it");

            w.Economy.Researched |= 1u << (int)Research.SmartController;
            Assert.IsTrue(w.SetCleaning(DeviceMode.Smart, out why), why);
            w.Dirt = World.SmartCleansAt + 1f;
            Run(w, 2f, new AutomationSystem());
            Assert.Greater(w.Dirt, World.SmartCleansAt, "without a controller, Smart cleaning waits like the service");
            w.Devices.Add(DeviceKind.Controller, 0f, 0f);
            Run(w, 2f, new AutomationSystem());
            Assert.Less(w.Dirt, 1f, "the controller cleans sooner");
        }

        [Test]
        public void MoneyMode_LocksTiersAndTheController_UntilResearched()
        {
            var w = Flat();
            w.Economy.StartMoneyMode();
            var m = w.Devices.Add(DeviceKind.Mister, 0f, 0f);
            Assert.IsFalse(w.Economy.DeviceUnlocked(DeviceKind.Controller));
            Assert.IsFalse(w.SetDeviceMode(m, DeviceMode.Sensor, out string why));
            StringAssert.Contains("Automation", why);
            Assert.IsFalse(w.SetDeviceMode(m, DeviceMode.Smart, out why));
            StringAssert.Contains("Smart controller", why);
            Assert.IsTrue(w.SetDeviceMode(m, DeviceMode.Basic, out why), why);

            w.Economy.ResearchPoints = 20;
            w.Economy.Money = 1000;
            Assert.IsTrue(w.Economy.DoResearch(Research.Automation, out why), why);
            Assert.IsTrue(w.SetDeviceMode(m, DeviceMode.Sensor, out why), why);
            Assert.IsTrue(w.Economy.DoResearch(Research.SmartController, out why), why);
            Assert.IsTrue(w.Economy.DeviceUnlocked(DeviceKind.Controller));
            Assert.IsTrue(w.SetDeviceMode(m, DeviceMode.Smart, out why), why);

            // The controller uses a little power.
            w.Devices.Add(DeviceKind.Controller, 1f, 1f);
            double before = w.Economy.Money;
            var loop = new SimulationLoop(w);
            loop.AddSystem(new EconomySystem());
            for (int i = 0; i < 40; i++) loop.Advance(0.25);
            Assert.Greater(w.Economy.SpentPower, 0.0);
            Assert.Less(w.Economy.Money, before);
        }

        [Test]
        public void TankHealth_SummarisesTheTank()
        {
            var w = World.CreateStarterTank(7, 6, 30);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 300; i++) loop.Advance(1.0 / 30.0);
            var good = TankHealth.Compute(w);
            Assert.AreEqual(6, good.Frogs);
            Assert.Greater(good.Insects, 0);
            Assert.IsTrue(good.HasWater);
            Assert.AreEqual(1, good.Species.Count);
            Assert.AreEqual(6, good.Species[0].Count);
            Assert.LessOrEqual(good.MinTemperature, good.Species[0].MinTemperature);
            Assert.GreaterOrEqual(good.MaxTemperature, good.Species[0].MaxTemperature);
            Assert.AreEqual(TankHealth.WordFor(good.Score), good.Word);

            w.Dirt = 100f;
            for (int i = 0; i < w.Agents.Count; i++) if (w.Agents.Kind[i] == AgentKind.Frog) w.Agents.Hunger[i] = 0.9f;
            var bad = TankHealth.Compute(w);
            Assert.AreEqual(0f, bad.Cleanliness, 1e-4f);
            Assert.AreEqual(6, bad.Hungry);
            Assert.Less(bad.Score, good.Score - 20);

            Assert.AreEqual("No frogs yet", TankHealth.Compute(Flat()).Word);
        }

        [Test]
        public void Automation_SavesAndLoads()
        {
            var w = Flat();
            w.Dirt = 42.5f;
            w.Cleaning = DeviceMode.Smart;
            var m = w.Devices.Add(DeviceKind.Mister, 1f, 1f);
            m.Mode = DeviceMode.Sensor;
            m.Target = 66f;
            var f = w.Devices.Add(DeviceKind.Feeder, 2f, 2f);
            f.Mode = DeviceMode.Smart;
            f.Target = 3.5f;
            var c = w.Devices.Add(DeviceKind.Controller, 3f, 3f);

            var loaded = SaveSystem.FromBytes(SaveSystem.ToBytes(w));
            Assert.AreEqual(42.5f, loaded.Dirt);
            Assert.AreEqual(DeviceMode.Smart, loaded.Cleaning);
            Assert.AreEqual(DeviceMode.Sensor, loaded.Devices.Find(m.Id).Mode);
            Assert.AreEqual(66f, loaded.Devices.Find(m.Id).Target);
            Assert.AreEqual(DeviceMode.Smart, loaded.Devices.Find(f.Id).Mode);
            Assert.AreEqual(3.5f, loaded.Devices.Find(f.Id).Target);
            Assert.AreEqual(DeviceKind.Controller, loaded.Devices.Find(c.Id).Kind);
            Assert.IsTrue(loaded.Devices.HasController);
        }
    }
}

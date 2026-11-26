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
            "RlRTVgIAAAAJAwAAAAAAAAAAQEAAAABA////////7z8DAAAACE1vdmVtZW50yIn9xD+RACZxFXywcwpD4gZtYXBnZW7G0kjNenec2qMnTJd6LvjIBXNwYXduid8jEP8jF10xPxxkFe9RhgYAAAAFAAAAAQAAAAB8ZsW/WzegP6ZxBb8527++QuzvPg90WjuJiIg7D3TaOgIAAAAAynIrQFSbqD96NY+/wkvDPqoh7T4PdFo7iYiIOw902joDAAAAAQ/8O0ArTsU/kOwXP7/vmD8jLuS9D3TaOomICDsPdFo6BAAAAAHBk2i/eKKfP6Ri0r/6Fpk/qSfIPQ902jqJiAg7D3RaOgUAAAABEM2vv7Zsqj9zGS8/oduCv7vcID8PdNo6iYgIOw90WjoZAAAAEQAAAAAAgD6amRk/y/mRP4Ppkz94ppU/glGXP3gLmT8B9Zo/7NycPwDonT8oQp4/aSyeP6rnnT/5tJ0/RNWdP4zsnj/cUKE/fKakP+hPqD+Rr6s/8SeuP4IFrz/mD60/vuuoP2y2oz9ijZ4/Eo6aP/BtlD9JBZY/1VeXP7GYmD/4+pk/mLGbPwORnT+wnZ4/Wv6eP5vxnj/wtZ4/+YmePzysnj8W1p8/eWyiPyYHpj+m+6k/ep+tPypIsD/eM7E/dxWvP8Oeqj9ZAqU/5HKfPw0jmz94dpY/oPKXP7AlmT9HR5o//Y6bPz00nT9tDp8/Gx6gPxGHoD84haA/WVSgP2MwoD8tVaA/S32hP6ARpD/zqqc/cp+rP0VFrz+Y8rE/UeayP4jUsD8Tbqw/B+SmP4xnoT/NKZ0/cmSYP2qpmT9rmpo/I3qbPzeLnD8nEJ4/hfGfP94/oT/GC6I/zHuiP2e2oj8v4qI/oSWjP28jpD+9TaY/almpP2i4rD+e3K8/+zeyPzsosz8Wj7E/GgCuPzl9qT90CKU/0qOhP4Jamj/3RZs/hcqbP/A9nD/99Zw/P0iePwY7oD97+qE/2HWjP6OupD9GpqU/TV6mPynYpj8Sjac/4/6oP/0Sqz+Ga60/nKqvP2NysT8mVrI/xYaxPzNlrz/Loaw/9uypPyX3pz90Zpw/ruicPzXtnD+84Jw/8i+dP1ZHnj/pUKA//pmiP7fppD8OFac/4/CoPzhSqj/zDas/bWurP0j/qz8Y6aw//ASuPwovrz9gQ7A/qhWxP/Y1sT9uyLA/bhuwP2N9rz/APK8/E5aeP/Sxnj+zOZ4/ibCdP7CZnT8yeJ4/IZmgP3Jqoz/Pi6Y/X6apPzJjrD9za64/OGivP8dvrz8pKa8/Ku2uP/PQrj+n6a4/bkyvP08MsD/5FrE/1FSyP92zsz8jIrU/vI22Py33oD80wqA/OeefP2X7nj/gk54/okWfP615oT/mt6Q/joCoP+lZrD8eyq8/eFeyPySIsz9ZS7M/vVayP6IwsT+gG7A/Q1qvPyAvrz+x368/L6SxP3M1tD/GNLc/h0O6PxADvT+Ml6M/0zmjP/0soj9UD6E/Hn+gP3AaoT9/WKM/ZM6mP2Lsqj/8Jq8/mvKyP7qVsj9sPn0/Bhl7P4RLsT/uxLM/KzGyP50AsT/1jLA/WDWxP+tXsz9XlbY/5We6P+9Jvj/PtcE/8gKmP12ppT80o6Q/YoyjP9YAoz9LnKM/ndSlP49BqT/1VK0/tYSxPx42nz/rngM/REPTPryc0j4hpQI/fX2cP2vNtD9It7M/LVazP1YEtD+qH7Y/ME25P6ELvT/I2cA/cjbEP7p0pz/AN6c/SlmmP61qpT87/aQ/HqKlP4/Hpz8lDas/We2uP1Pmsj8vdBA/5IS6PlSKhj7M44U+TJG4Pm4yDj+QQLc/UaC2Pw6Qtj/rUrc/eD+5P4wPvD+KWb8/6LPCPx+1xT/GOqg/XyWoP9d+pz/3yKY/goWmPxU2pz/FPak/qUesP/varz8IgrM/SzcEP/wfoT7QSlo+wP1YPmQsnz74lgI/5Wy5P8x6uT9o4rk/5cS6PwJuvD8ptr4/hFbBP1IIxD/QhMY/kqyoP5a8qD/WTag/0tCnPwK2pz+8bag/qk6qP9oQrT/USbA/jpGzPyfuED8YD7s+iBSHPgBuhj6EG7k+LosOP8o3uz9+Crw/yvq8P9wDvj9MZb8/ohnBP84Awz/a+sQ/0OfGP5IhqT+zR6k/OgCpP86rqD8Xq6g/l16pP6oRqz93iK0/32WwP5xOsz+B1aA/IikEP7RX1D4osdM+Vy8DP0troT+hhrw/NhO+P9WGvz93ucA/a9/BP5gSwz+jVsQ/Uq/FP2Ygxz9D8ak/EhGqP/vPqT9+g6k/GoGpPzAeqj8wnqs/Ps6tPxlbsD/p8rI/wUO1PyJ1tD9+NYE/wpOAP7f/tT99Lrs/zT69P7xYvz8YNME/U4/CP2SWwz+jecQ/QlbFP5BJxj/hcMc/V3KrP0Biqz9G9qo/pICqP5RTqj83wao/0AqsPwsBrj+HVLA/J7eyP7zatD8scbY/TSy3PwC3tz+8/7g/gvS6PxFEvT8Ynb8/Uq7BPzEtwz+BQsQ/zyXFP438xT9K7MY/nBrIP012rT9/A60/yS+sP1VUqz9Lyqo/w+qqP8P6qz+4w60/9vOvP787sj9HS7Q/zdK1P4mCtj8u8LY/jP+3P9usuT9Etrs/5Nm9P+DVvz9Nb8E/9NbCP+U0xD+vm8U/AR7HP5POyD8AAAAAAAAAAAAAAAAAAAAAAAAAAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAAAAAEBAQEBAQAAAAAAAAAAAAAAAwMDAAAAAAABAQEBAQEAAAAAAAAAAAADAwMDAwAAAAAAAQEBAQEBAAAAAAAAAAMDAwMDAwMDAAAAAAEBAQEBAQAAAAADAwMDAwMDAwMDAwAAAAAAAQEBAQEDAwMDAwMDAwMDAwMDAwMDAAAAAAEBAQEBAwMDAwMDAwMDAwMDAwMDAwAAAAABAQEBAQMDAwMDAwMDAwMDAwMDAwMAAAAAAQEBAQEDAwMDAwMDAwMDAwICAgIDAAAAAAEBAQEBAwMDAwMDAwMDAwICAgICAgAAAAABAQEBAQAAAAAAAAMDAwMCAgICAgIAAAAAAAEBAQEAAAAAAAAAAAAAAgICAgICAAAAAAAAAQEBAAAAAAAAAAAAAAACAgICAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAADAwAAqEEAACBCDAAAAAgAAACh6udB/GznQTdg5kG3q+RBgD/iQWMi30GiedtBUIjXQbWo00HQQdBBR7jNQfFczEH3aOhBV+rnQUrb5kHKIuVBuLDiQZSM30FK3dtByebXQYcC1EEll9BB1wnOQVCszEElR+lBmcboQRGz50GT8uVBenTjQbBB4EFQhtxB0YjYQaWe1EHbLNFBuJnOQa84zUGzU+pBf9DpQa+26EGD6uZBa1nkQfMM4UFmQN1BO0LZQVtX1UE54dFBokjPQf/jzUEVW+tBBtXqQVK06UGK2udBji/lQUm84UGD3t1BnefZQfIH1kEzktJB8fXPQWSOzkG2NexBZ63rQXyH6kGdo+hBUeTlQUdY4kEsa95BHHnaQYCd1kEXKNNBgYjQQX4ez0FUzexB8UPsQSUc60EDN+lBknzmQWf34kGgCN9BiAPbQV4X10FeltNBL/HQQZ6Ez0HIGe1BHpDsQU1o60E2helBMdTmQVhi40H4c99BZVzbQX1b10HL0NNBVCfRQQu5z0HF+VpCEMpcQphBYEIVFWVCYeZqQkxdcULRM3hCLgl/Qi+sgkLEaIVCV2CHQrNfiEIYgVpCxFBcQizHX0IXnmRC43tqQoQHcUIq7HdCnsJ+Qi6DgkKEMYVC6CKHQh4fiELnpllCzHpbQrQCX0L1BmRCRzJqQmsPcUKDAXhCBJZ+QthGgkI/0IRC5a+GQn6lh0KpxFhCp7BaQp1tXkI70GNCE5JqQkEtckJyQHlCaQd/QpoegkKjYIRCoxqGQiMAh0KHV1hCMFxaQttQXkJ9HWRCHr5rQsawdEJg5HtClTmAQp8bgkKj7YNClWuFQls5hkLtbFhC0IFaQpqXXkJkpWRCxuJsQiqBdkLKz31Cxq6AQu8GgkK8aINCCaSEQn9WhUJCo1hC7MBaQmPiXkJj42RCYqlsQr2QdUIgcnxCaSeAQhCIgUIH1IJCZeCDQmx0hEINylhC+edaQksCX0JT3mRCIR5sQjX0c0KPknpCmPR+QjEigUKadIJC6WmDQu7og0IDAAAAAgAAAAEAAAAAAACAvwAAAD8BAADoQQAAAAAAAAAAAAAAAAACAAAAAQAAgD8AAAC/AQAANEIAAABBAAAAAIaZ0UEA";

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

        /// <summary>
        /// A real version 5 save, made by the game just before version 6: seed 5050, a 6 x 4 m tank
        /// from MapGenerator with a heater, a fire-bellied female carrying eggs (a little lonely), the
        /// fire-bellied male backing off after a dispute, a White's male holding a territory, a
        /// fire-bellied egg and a cricket, breeding switched off with a limit of 9, 2 rehomed and 4 eggs
        /// collected, run for one second. Never change these bytes.
        /// </summary>
        private const string Version5Sample =
            "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AAAAAAAAAwMDAwMDAwMDAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMDAwMDAwMDAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAwMDAwMDAwMAAAAAAAEBAQEBAAAAAAAAAAADAwAAAAAAAAAAAAAAAAAAAAAAAAAAAwMDAwMDAwMDAAAAAAABAQEBAQEAAAAAAAADAwMDAAAAAAAAAAABAQEAAAAAAAAAAAMDAwMDAwMDAwAAAAABAQEBAQEBAAAAAAAAAwMDAwAAAAAAAAAAAQEBAAAAAAAAAAADAwMDAwMDAwAAAAAAAQECAgICAgAAAAAAAAADAwAAAAAAAAAAAAEBAQAAAAAAAAAAAAMDAwMDAwMAAAAAAAICAgICAgICAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAwMDAwMDAAAAAAACAgICAgICAgIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMDAwMDAAAAAAACAgICAgICAgICAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgICAgICAgICAgIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAICAgICAgICAgICAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAECAgICAgICAgICAgEBAQEBAQEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAgICAgICAgICAgIBAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAQICAgICAgICAgICAQEBAQEBAQEBAAAAAAAAAwMDAAAAAAAAAAAAAAAAAAAAAAAAAQEBAgICAgICAgICAQEBAQEBAQEBAQAAAAAAAwMDAwAAAAAAAAAAAAAAAAAAAAAAAAEBAQECAgICAgICAQEBAQEBAQEBAQEAAAAAAAMDAwMAAAAAAAAAAAAAAAAAAAAAAAABAQEBAQEBAQEBAQEBAQEBAQEBAQEBAAAAAAADAwMDAAAAAAAAAAAAAAAAAAAAAAAAAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQAAAAAAAwMDAwAAAAAAAAAAAAAAAAAAAAAAAQEB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";

        [Test]
        public void Version5Save_StillLoads()
        {
            var w = SaveSystem.FromBytes(System.Convert.FromBase64String(Version5Sample));
            var a = w.Agents;
            Assert.AreEqual(5050UL, w.Seed);
            Assert.AreEqual(5, a.Count);
            Assert.AreEqual(2, a.Species[0]);
            Assert.AreEqual(2, a.MateId[0]);
            Assert.AreEqual(-0.6893676f, a.PosX[0]);
            Assert.AreEqual(Goal.Retreat, a.Goal[1]);
            Assert.IsTrue(a.HasHome[2]);
            Assert.AreEqual(LifeStage.Egg, a.Stage[3]);
            Assert.AreEqual(AgentKind.Insect, a.Kind[4]);
            Assert.IsFalse(w.BreedingEnabled);
            Assert.AreEqual(9, w.BreedingLimit);
            Assert.AreEqual(2, w.Rehomed);
            Assert.AreEqual(4, w.EggsCollected);
            // Version 5 had no mutations or hybrids: everyone is pure-bred, eggs are being carried
            // for a male of her own species, and nothing has been discovered or registered yet.
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(0u, a.Genes[i].Alleles);
                Assert.IsFalse(a.Genes[i].IsHybrid);
                Assert.AreEqual(0, a.Genes[i].Generation);
            }
            Assert.AreEqual(2, a.MateSpecies[0]);
            Assert.IsFalse(w.MutationBoost);
            Assert.AreEqual(0, w.DiscoveredMutations);
            Assert.AreEqual(0, w.Registered.Count);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 60; i++) loop.Advance(1.0 / 30.0);
        }

        /// <summary>
        /// A real version 6 save, made by the game just before version 7: seed 6060, a 6 x 4 m tank
        /// with a mister, a toothy White's female carrying eggs for a White-lipped male, a half-hybrid
        /// toothy egg and a cricket, with the mutation boost on and "Toothy White's" registered.
        /// Never change these bytes.
        /// </summary>
        private const string Version6Sample =
            "RlRTVgYAAACsFwAAAAAAAAAAwEAAAIBA////////7z8EAAAABUJyYWluw8DvtcqNc+dnNOXfCNOb6AhHZW5ldGljc2G0ntwOKXX7C+k0JE8MMn0GbWFwZ2Vu1+pIYIcXsNijJ0yXei74yAVzcGF3brvlFlnQ0QsvMT8cZBXvUYYFAAAABAAAAAEAAAAAAACAv5qZGT8AAIC/AAAAAAAAAABgCzY7AAAAAMEJHDternc/BwEAgD8AAAAAAACAvwAAgL8AAFJExiOoRQAAAAAAAAAAAAAEAABSRAAAAAAAAAAAAAAAAAIAAAAAAKBAg9YtQ1lcfT8J16Q+Ar4jv5ASgb6QQMA+/wWBP9iHjz8BAAAAAAAAAAACXRNwP1PSKr5yteY+5JAvP4quHT/cxoY/CFKOPwAAAAAAAAAAAAAPdNo6AAAAAAAAAAAAAAAAAAMCAAAAAOTpyb6amRk/SIlbvz7MZz63RZw+zcxMOwAAAADBCRw7IQxSPwEBAIA/AAAAAFjJaz/6gmo/AABSRJFHrEVS+lBAAAAAAAMBBAAAUkQAAAAAAAAAAAAAAAAAAAAAAAAAANhJrkNdE3A/U9IqvnK15j7kkC8/iq4dP9zGhj8IUo4/AAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAAAAAAAA/AACAPwAAgD8AAAAAAAAAAAAAC9ejOgAAAAAAAAAAAAAAAAAAAwAAAAAAAAAAVfi4PwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAAAAAAAAAAAAAAADwZp1FAAAAAAAAAAAAAQAAAAAAAAAAAAEAAAACAAAAAAAAAAAAAAAAAAAA6EVxP+wBwbzKuxE+7FUcvqbEtT5+2oQ/buGKPwEAAAADAAAAPwAAAIA/AAAAAAAAAAAAAAAAAAAAPwAAgD8AAIA/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAABAAAAQL7UsD8AAIC/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAIA/AQEAgD8AAAAAAAAAQAAAgL8AAAAAWKfxQ8wpOkAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgD8AAAAAAAAAAAAAAAAAAAA/AACAPwAAgD8AAAAAAAAAAAAAAACAPwAAAAAAAAAAAAAAAAAAAD8AAIA/AACAPwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAxAAAAIQAAAAAAgD6amRk/MXJuP7r1cT/oHXY/k1R6P8cDfj8NiYA/r/mBP9Zygz8zE4U/zvmGP5ZFiT+v/Is/3wyOP/9ljz+nX5A/TFGRP1+Skj93epQ/wFaXP8UFmz+GIJ8/+j6jPyz5pj/i5qk/LKCrP0aJrD/4OK0/XYitP+Y3rT/nO6w/SI6qP3MlqD+m36Q/tuCgP2VenD+Zjpc/PKeSP/bdjT/sVIk/guWEP06IgD/Nc3g/Qu1vPx53Zz8NCl8/q5pWP7pRTj9GdkY/aVA/P8m1bD/CZ24/A+RwP5jpcz/NN3c/LOR5P+2Uez+G4nw/qmR+P+ZZgD8QNII/YPOEPwiOhz+yyok/RNSLP3vVjT8Q+Y8/52mSP71WlT9Dwpg/T2WcP3v1nz92KKM/pLOlP7pMpz/6dKg/BLSpP9K/qj+fOqs/6vSqPw/EqT+qfKc/jw2kP064nz/1yZo/sY+VP7RWkD/ya4s/1CyHP4vPgz9SCIE/9Qh9PzXidz/793E/c6RqP9QqYj8hg1k/2xZRP4tQST+HhWo/JsRqPyTtaz+tA24/JwtxP4Fxcz8TOHQ/jD90P9VndD+ZkXU/Rp14P+o2fj8jLII/RSaFP9oQiD+58oo/t9KNP9O3kD9TuJM/yvOWP3g8mj+gX50/nCqgP3Vqoj+K7KM/SUmlPxf8pj8On6g/hL2pP1sLqj+fQKk/whanP9OJoz8n854/Y7KZP00nlD+usY4/ErGJP06yhT/bNYM/IbmBP+SpgD9f634/fhV7PzitdD+dNGw/mUljPylxWj9NMVI/x6NnP3olZz+qpmc/c01pPy9AbD+ElW4/7/huPzdwbj9QAG4//a5uP76BcT8RSXc/QgV+P2pvgj8a34U/uUWJP1CXjD8RyI8/ZeGSP6gRlj8bOJk/Fi6cPwTNnj8H7qA/kGqiP+Tmoz/h16U/vMmnP/I+qT/K3Kk/8kupP6s3pz+7oqM/pvKeP5OQmT/K5ZM/k1uOPwBbiT9UiYU/bY2DPyLIgj8ghoI/3hOCP36+gD/qpHs/50FzP55Haj8JSWE/G9pYP0FEZD+YgmM/Is5jP9NZZT/ZWGg/j/BqPyXGaz/JzGs/zfZrP1k3bT9NgXA//pZ2P2yjfT+9bII/iw2GP+6kiT+zI40/0HqQP2mwkz9o8pY/2iKaP/8dnT8uwJ8/cuWhPyVqoz+77KQ/QdumP+TEqD8fNKo/c9CqPxVEqj/5Oqg/Nq+kP1MFoD/CqJo/EwWVP9qFjz91loo/TNqGP0/thD8QMoQ/APiDP1uOgz8ERYI/b9Z+P8+vdj+rCW4//WplP7lbXT/eYWE/wKVfP4EPXz8uAGA/GtliP70OZj9MQWg/7xNqP+gobD9ZI28/G6ZzP1c1ej+804A/HLGEP9ybiD/YfIw/7TyQPyfFkz9kEpc/uU6aP7VlnT/EPaA/bL2iP9/KpD+nTKY/jsinP9STqT8iSas/XYOsP870rD+5Uaw/rkyqP2G8pj+k/6E/3ImcP4vOlj81QZE/J1WMPxCiiD/xkIY/BJKFP4EHhT9rU4Q/atiCP//wfz8VPng/iHpwPwb8aD8wGWI/bfFeP8G9Wz/xz1k/5M1ZP8hdXD/taWA/g4tkP0bvaD8GwW0/eC1zPwZheT/6QYA/wTWEP6xkiD80rIw/oumQPz/6lD9/u5g/thycP3RCnz+YJqI/2L6kPwcBpz+g4qg/e1mqP/bGqz+rW60/v8KuP1qsrz+G2q8/nBCvPx0LrT9dbKk/2YqkP+Xlnj/x/Jg/b0+TP5hcjj/bqoo/u1eIP9znhj982YU/oKqEP/PZgj/yyn8/DK54P5Qmcj9SQWw/+QtnP/DYXD+E/lc/cIxUPwZ2Uz/arlU/qKVaP9zrYD9fKWg/KAVwPxoneD9kG4A/t/qDPz4riD94sIw/7FqRP/T6lT/mYJo/R12eP0DQoT9E2KQ/Tn+nP1DMqT9axqs/G3StP6rcrj+OOLA/VY+xP22esj/oLLM/dg6zP18Xsj9hD7A/Bl6sP4RQpz+cc6E/LlSbPxh/lT8HgZA/D82MPwUsij+FOYg/SJOGP9TWhD9LooI/ZyZ/PzCreD9OgHM/wGBvP3UIbD9m/lo/8JtUP/rBTz+gq00/RpRPP3RlVT9mqV0/x4xnP047cj+j4Xw/DlaDPy9+hz+n54s/iL2QP1vElT8B2ZY/39eBPzqMXD/64k8/dkxiP6TMij/b5qk/uUiwP7DGsT81IrM/8m20P1mLtT/WSLY/3IK2P3Udtj+l/LQ/j/KyPzgwrz81+qk/vOmjPyKYnT+7npc/qJaSPwzhjj8r+Is/7YyJPz9ahz/eGoU/HoqCP3fFfj8D4Xg/NPp0PzCAcj8G43A/0UdZP/DJUT+O7Us/ySFJPwbWSj/jTFE/NwtbPw7kZj92qXM/OBeAP9mihT+mBYo/ap6OP+S0kz9QIoY/4Zc0PwHYDD/vLAM/LKb/PsM5Az953ww/flM+P5wymz+5IbU/Hna2P7i3tz89rLg/pi65PwYsuT9clLg/DVe3P79NtT/NgbE/ezGsPwP/pT+1jJ8/5nyZP7dxlD81v5A/iKaNPwDoij+6U4g/mLmFPxrqgj9Han8/Cft5P8sGdz+DxXU/C3B1P7mbVz8yvU8/J41JP7WMRj9JPUg/0wBPPxJaWT/t+2U/SZhzPwJxgD/VRYY/JcyKP8CEjz+LNoU/W14hP5tXAT9m+eA+psTMPvQQxj5Q3sw+uCzhPhd+AT9y2Sg/JwmhPz4muD8naLk/y0+6Pzu9uj9Rpro/mf+5P3m8uD83ubY/CvGyP8SfrT+Paqc/s/agP3zpmj8A6JU/a0GSP4Yijz8YUYw/uKSJP870hj9UGYQ/sOmAP2mhfD/QFXo/oFR5P8CCeT9RbVY/z1hPP97sST9Fhkc/GoJJP1g/UD8RZFo/N7hmP7UCdD+7hYA/C02GPxbhij8+444/UH87P/ToAj8Mm9Y+POW0PnywoD7I/Jk+JMqgPowYtT4E6NY+QxwDP92hUT8FVLg/kY+6P7ksuz+KMrs/JK+6P4qruT8pL7g/bSu2P06wsj+W5a0/N1SoPz6Foj+9AZ0/klKYP0OxlD8yW5E/MS2OPy8Viz/qAIg/sd6EP0KcgT+mMX4/EeZ7P6lvez8p6Xs/FhtWP6rZUD/sKU0/DBRMP4igTj8wKFU/vqReP1AQaj/0Y3Y/4EyBP73Vhj8oaos/sk+BP+LRED/MPuc+8Ae4PiRSlj5gHYI+WNN2Pgg3gj5whZY+6FS4Pmyl5z6/NRM/Gk+kP4c3vD9vIbw/5DK7P+ysuT80yLc/Z7u1P+2psz++1LA/3zStP0MRqT/0sKQ/AlugP0dWnD/BqZg/C7qUP2CakD9pbYw/nFWIPwR2hD8Z8YA/JhZ9P2Q5ez9+Tns/4Fh8P9yZVj9Uv1M/oVVSP2f1Uj+dN1Y/J21cP3j+ZD8pKG8/wiV6P+qZgj9Px4c/7VKMP/ftaD9Vcgk/vizWPuL1pj4UQIU+qBZiPkCvVD74SWI+ZHOFPtxCpz5ck9Y+eLIJP1KqkD/bMr4/si69Pw39uj9kILg/PBC1P8FBsj9wGrA/ExyuP/4BrD8FvKk/FjqnPyBspD/gQaE/pH+dP/G9mD/yV5M/CraNP25AiD/jX4M/OPl+P4Nuej+cGHk/edt5P+Obez9z3lc/WIlXPziBWD+Y6Vo/SeZeP/C/ZD+GU2w/pSN1P9Oxfj9RQIQ/JgmJP/SGjT9KEGQ/1akHP76b0j7gZKM+EK+BPqD0Wj5AjU0+8CdbPmTigT7YsaM+XALTPvjpBz8GJo0/X1TAP09Vvj/Rz7o/VIq2P1w+sj/Voq4/wWKsPxdEqz9cwao/1W6qP+TgqT/qq6g/GWSmP6iHoj+/5Zw/MSWWP7z3jj/xDog/7RyCP3+mez9rWnc/p4x2PwgBeD85fHo/sN1ZP1e3Wz8pvl4/MrBiP81LZz+L0mw/c4ZzP9omez//uIE/lhOGP66Cij/U8Y4/plN3P9uhDD/Mi9w+7lStPiCfiz7A1G4+WG1hPhAIbz5w0os+6KGtPmjy3D4/zQw/X6KcP+5uwj8Nlr8/7+m6P2ZrtT8gDbA/C7+rP3BoqT9kCqk/QuepP/9Dqz+pZaw/TpGsP80Lqz+rFqc/aLCgP3LBmD8iO5A/og6IP6UtgT+sEnk/T/l0PzGedD9hqXY/vsN5P2aMXD/ayF8/sB1kP3kIaT8YB24/rFZzP4d5eT/UKoA/+ueDPzLnhz9LG4w/HH+QP1+4jT+UtB8/5vzzPgrGxD48EKM+fNuOPsgniD4g9Y4+jEOjPgATxT6EY/Q+kNMpP1X4uD9YVcQ/s/HAPzCKuz9gRLU/QjevPwV3qj9MEag/wyyoPxroqT/aVaw/pIiuPxuTrz+2h64/Y4GqP8Kcoz//65o/5IiRPxCNiD+sEoE/p2Z4P9pqdD8lVnQ/8r52P3w8ej8MxF8/RjJjP+S0Zz9WwGw/aslxP7gJdz8wC30/sd2BP2WDhT93bYk/gZKNP7XwkT/10pY/EDZnP4d3DD8yuOk+ZALIPqTNsz7wGa0+TOezPrQ1yD4sBeo+16oMP5SjiT8IO8Y/MtjFP+9jwj+T5rw/b4m2P65nsD+4mas/hTGpP4hYqT93Kas/zrGtPyD/rz8EH7E/4B6wPwcXrD9iKqU/2W2cP3n9kj8j9Yk/Q3GCP2wbez9QGHc/dvN2PyJHeT9qrnw/M+thP9O3ZD/Kn2g/wjRtP7cIcj/FOXc/nCR9P7bcgT+kc4U/Kk+JPzBnjT9uvpE/6sSWPxktmj+8uFY/eiANP5p1+j7aQOY+KI3fPoBa5j7sqPo+DE8NP9l7cj/ma8I/YSXHP9Tmxj/B1MM/Td2+P38OuT9tabM/luyuPxuNrD9sSaw/dletP7fwrj/KTrA/R6uwP5k/rz9hUqs/yQylP5BQnT84BJU/GA6NPxFVhj96v4E//At/P6vQfT/crn4/oESAP3VQYz83AWU/9dlnP7Ofaz/AF3A/zi91P/n0ej/9rYA/OiyEP4zvhz8g8os/oj6QP2SGlT+Uops/SsygP6W8gT9hzzc/T9YUPxm+Dj8thhU/XOI+P+MikT+VMcI/7P3FP6mqxz9brMc/LEfFP0I7wT/bZrw/NZ23PzWvsz9EX7E/LmCwP7oXsD/PDLA/bsavP53Lrj8wo6w/NOWoP9PFoz/OzJ0/fYKXPwxvkT8wG4w/FA+IP5dzhT9j4YM/WQiDP8eYgj/3x2Q/LSFlPxaxZj/Nemk/tYFtPw55cj/LDXg/Mjp+P7t7gj/qH4Y/mQaKPxREjj+b2JM/QpOaP0/boT/LF6k/SuusPxQWpD+obaA/OGqqP21fvD/DJ8I/8XrEP8Jwxj96Csg/PlnIP7rExj9t1cM/jy/AP+xuvD9RLbk/3u+2P/YNtT8zJ7M/CiCxPxHdrj/bQqw/zTWpP1qwpT/986E/tBuePw4+mj9mcZY/psySPy5mjz/DU4w/jpmJP4wzhz8eHoU/ASZnP6cqZj8hc2Y//j9oPxfSaz8Go3A/5AB2P4nrez/kMIE/u7GEPwl4iD+roYw/7XySP/fOmT/c1qE/eNOpP6QDsT9yprY/CHu6PyDAvT+lmsA/DxPDP/cxxT+R/8Y/eITIPwIeyT/5Vsg/xYDGP5YFxD9kSsE/jbK+P6qGvD/Tw7k/xEK2P2xEsj/QCa4/+dOpP8HjpT/YlKI/4TWgP3B2nj+j/Jw/bm6bP1FymT8/rpY/JCWTP2RFjz8qVos/C5+HP9Y+az+IMGk/9G1oPxxpaT9MlGw/KDtxP/5fdj9aB3w/ZhqBP2l2hD/UGYg/RiqMP600kj9c9pk/TY6iP0Ybqz8PvLI/m4+4P7tKvD9GZL8/3wvCP/9PxD88P8Y/y+fHP0lYyT8pK8o/eAfKP0ISyT/yhcc/dJvFP5OKwz9xa8E/2vK9P1knuT/wk7M/wMOtP+dBqD9WmaM/qXOgPxcqnz8qFp8//IOfP3i/nz8bFZ8/zdCcP2vmmD92GZQ/Xu2OP/vliT+y5nE/tkVvP3bvbT8EcG4/vlNxP+DOdT/WvHo/gxKAP/AGgz9hP4Y/XL+JP8OwjT8vwZM/GqqbPxN6pD9vP60/igi1P+rjuj84gb4/mHTBP3Pzwz+TD8Y/4NrHP+RmyT+Mxco/M7HLP8Hfyz/ZXss/oU3KP/DNyD8IAcc/+uXEPyAMwT/Vkbs/lCi1P/WBrj+RT6g/z0KjP8UtoD8vb58/CzSgP9SZoT/UvaI/6b2iP1u3oD9Flpw/WEqXP1J2kT9YvYs/ElF7P5O3eD+HXXc/+sx3P0CQej8z2H4/38CBP8ZMhD+uFYc/6CGKP6J3jT8fQ5E/eDCXP9z3nj9Gp6c/eEywPzz1tz+Gr70/dCnBPwr1wz9gSsY/PD7IP4TlyT+8VMs/0KDMP1aCzT/Aq80/5inNP+IWzD8clMo//sLIP4ajxj/uzcI/vl+9PzIGtz+ibrA/bEaqP7Y6pT96HKI/lFKhP0YLoj+eZKM/dnykP0RxpD/gYKI/2DSePwjbmD/M95I/7C+NP8IJgz/O5oE/OFuBPxWkgT+W/oI/sP2EP70Vhz8nVok/6s2LP2qMjj/voJE/Qz+VP3oFmz8spqI/UC+rP6uusz8DMrs/TMfAP1wWxD95ocY/CKnIP+JLyj/9qMs/997MP8sMzj8c3M4/Ve3OP0pRzj+MIc0/6oPLP9qeyT/+esc/ltvDP4rVvj/H/Lg/T+WyPywjrT80Sqg/Gh6lP8EkpD+BmqQ/LKmlP2J6pj9WOKY/pwykPwvYnz9qd5o/xI6UP4LCjj+C24g/hviHP+Oihz+rC4g/DWSJP6o2iz9nAI0/+92OP7LrkD89RpM/LgqWP0p2mT+lE58/VoymPx/urj+RRrc/QqO+P+4RxD/qMMc/42rJP2QMyz8KQ8w/iDzNP0Mmzj/0Lc8/5ubPP33Wzz/nE88/OrrNPwz2yz899ck/Pc3HPxmBxD/iGMA/3QK7P2qttT/uhrA/lv2rP1fBqD8Whqc/CJ2nP2dBqD88rqg/JR+oPyrPpT/OkaE/xiqcP3o9lj++bZA/MeuOP2hQjj8zOI4/HsaOP9IdkD+OwJE/xDSTPxCmlD+hP5Y/EC2YP9KZmj/y0J0/5kOjPzOTqj9RzLI/kvy6P0Qxwj/hd8c/B2TKP9pFzD9+d80/FzfOP9/Czj/FWM8/BTfQP8jX0D8UotA/SbTPP7Arzj9/PMw/7BzKP7/wxz+mAsU/Q0zBP5YRvT+5lrg/yR+0P7Hwrz86o6w/5R6rP1XMqj9N+qo/XPeqP6kSqj/Hmqc/ZFSjP+7mnT8b9Zc/EiKSPzYGlT/prZQ/lc2UP0p8lT8z0ZY/40SYP7tmmT8ucJo/UJqbP5MenT9MNp8/9TeiPyt/pz+Yo64/hLK2Pw65vj9TxMU/mOHKP5mazT/oJs8/WO3PP2s70D+9XtA/oKTQP7Na0T/U49E/84rRP/100D+uwM4/QKnMP6hsyj//O8g/a6TFPyySwj8NIr8/+XC7P9ebtz9ev7M/yGCwP4GXrj/l4a0/2qCtPxE1rT/W/6s/22GpPxUSpT+ynZ8/zKaZP+7Pkz/4+Zo/fNCaP3YVmz8M15s/eSOdPzBtnj8uSp8/IP6fP1bMoD92+KE/EMajPxGUpj9erqs/XqayP1iJuj9sZMI/ukTJP4g3zj+Kv9A/ogLSP/pw0j9qY9I/3jLSP/w30j+0y9I/JkDTP/rL0j+WmNE/+sPPP1COzT82O8s/fgXJP5qqxj8cDcQ/ZC3BP+wLvj8uqbo/cAW3PwiXsz86mLE/MpewPwwCsD+wRq8/otOtP8YWqz8jvaY/5EChP7hDmz/EZ5U/wpOgP753oD+uwqA/2H+hP566oj+m46M/s5OkP2ESpT/6pqU/GZmmP0AwqD+1zqo/DruvP9iEtj+6Ob4/M+fFP8WazD8TYtE/p73TP1fN1D/4BNU/x8HUPxJh1D/oP9Q/mrvUP8Ig1T8tn9Q/HGHTPyqA0T/NPc8/qt/MPxKkyj/LWcg/I+DFP3Qtwz84OMA/7Pa8P9VfuT8J5LU/GMqzP/ilsj+c6rE/ygqxP+B5rz+hqqw/nEaoPzrBoj8VvJw/OtmWP2GTpT+PmKU/B/ylP6DCpj9I8ac/ZQapP9CpqT+9Hqo/C6iqP+uIqz91BK0/GXivP2sktD+Dnro/GPzBP8FSyT8VuM8/ykHUP91u1j/KRtc/40DXP0DA1j8FKNY/IdvVP7481j9ir9Y/vWnWP8yD1T+x/9M/Eg3SP3/gzz9Kqc0/rkfLP2aoyD+OyMU/YaXCPxk8vz+8ibs/ZfW3P6qatT/nGbQ/2/ayPw+1sT+t2K8/PuWsPxaBqD+iDKM/nyKdPztelz9N/6k/d1mqPz8Gqz+i+qs/siutP2U3rj9S6K4/kHavP/EZsD+MCrE/YICyP9bMtD+mIbk/MSC/P27wxT86usw/b6XSPwXa1j+U0tg/u1zZP0D32D9AEtg/6R3XPzCK1j9Kx9Y/e2rXP/C51z8lpNc/rPzWPyLK1T/CGNQ/hvTRP25mzz+Qesw/sj3JP7m8xT+JBMI/0SG+Pw5kuj+zgbc/FTi1Pygysz+tGrE/B52uP/Vjqz8IDqc/586hP/wwnD8mv5Y/AQEBAQAAAAAAAAAAAAADAwMDAwMDAwMDAwMDAwAAAAAAAQEBAQEBAQEBAQEBAQEBAQEBAQAAAAAAAAAAAAMDAwMDAwMDAwMDAwMDAwMDAAAAAAABAQEBAQEBAQEBAQEBAQEBAQAAAAAAAAAAAAMDAwMDAwMDAwMDAwMDAwMDAwAAAAAAAAEBAQEBAQEBAQEBAQEBAAAAAAAAAAAAAAADAwMDAwMDAwMDAwMDAwMDAwMAAAAAAAAAAQEBAQEBAQEBAQEBAQAAAAAAAAAAAAAAAwMDAwMDAwMDAwMDAwMDAwMDAAAAAAAAAAAAAQEBAQEBAQEBAQEAAAAAAAAAAAAAAAMDAwMDAwMDAwMDAwMDAwMDAwAAAAAAAAAAAAEBAQEBAQEBAQEBAAAAAAAAAAAAAAADAwMDAwMDAwMDAwMDAwMDAwMAAAAAAAAAAAEBAQEBAQEBAQEBAQAAAAAAAAAAAAADAwMDAwMDAwMDAwMDAwMDAwMDAAAAAAAAAQEBAQEBAQEBAQEBAQEAAAAAAAAAAAAAAwMDAwMDAwMDAwMDAwMDAwMDAAAAAAAAAAEBAQEBAQEBAQEBAQEBAAAAAAAAAAAAAAMDAwMDAgICAgICAgMDAwMDAwAAAAAAAAEBAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAADAwMDAgICAgICAgICAwMDAwMAAAAAAAABAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAAAAwMDAgICAgICAgICAgMDAwMAAAAAAAABAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAAAAAMDAwICAgICAgICAgICAwMDAAAAAAAAAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAAMDAwMCAgICAgICAgICAgMDAAAAAAAAAAEBAQEBAQEAAAAAAAAAAAAAAAAAAAAAAAADAwMDAgICAgICAgICAgIDAwAAAAAAAAEBAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAAAAwMDAwICAgICAgICAgICAwAAAAAAAAABAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAAMDAwMCAgICAgICAgICAgAAAAAAAAAAAQEBAQEBAQEAAAAAAAAAAAAAAAAAAAAAAAADAwMDAwICAgICAgICAgMDAAAAAAAAAAEBAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAAAAwMDAwMDAgICAgICAgMDAwAAAAAAAAABAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAAMDAwMDAwMCAgICAgMDAwMDAAAAAAAAAQEBAQEBAQEAAAAAAAAAAAAAAAAAAAAAAAAAAwMDAwMDAwMDAwMDAwMDAwAAAAAAAAEBAQEBAQEBAAAAAAAAAAAAAAEAAAAAAAAAAAMDAwMDAwMDAwMDAwMDAwMDAAAAAAABAQEBAQEBAQAAAAAAAAAAAAABAAAAAAAAAAADAwMDAwMDAwMDAwMDAwMDAwAAAAAAAQEBAQEBAQEBAAAAAAAAAAAAAQEAAAAAAAAAAwMDAwMDAwMDAwMDAwMDAwMAAAAAAAEBAQEBAQEBAQAAAAAAAAAAAQEBAAAAAAAAAAADAwMDAwMDAwMDAwMDAwMDAwAAAAABAQEBAQEBAQEAAAAAAAAAAAEBAQAAAAAAAAAAAwMDAwMDAwMDAwMDAwMDAwMAAAAAAQEBAQEBAQEBAAAAAAAAAAABAQEAAAAAAAAAAAMDAwMDAwMDAwMDAwMDAwMDAAAAAAEBAQEBAQEBAQAAAAAAAAAAAQEBAAAAAAAAAAAAAwMDAwMDAwMDAwMDAwMDAwAAAAABAQEBAQEBAQEAAAAAAAAAAAEBAQAAAAAAAAAAAAMDAwMDAwMDAwMDAwMDAwMAAAAAAQEBAQEBAQEBAAAAAAAAAAABAQEAAAAAAAAAAAADAwMDAwMDAwMDAwMDAwMDAAAAAAEBAQEBAQEBAQAAAAAAAAAAAQEBAQAAAAAAAAAAAwMDAwMDAwMDAwMDAwMDAAAAAAEBAQEBAQEBAQEAAAAAAAAAAAEBAQEBAAAAAAAAAAMDAwMDAwMDAwMDAwMDAwAAAAABAQEBAQEBAQEBAQAAAAAAAAABAQEBAQAAAAAAAAAAAAADAwMDAwMDAwMDAwAAAAAAAQEBAQEBAQEBAQEBAAAAAAAAAACoQQAAIEIYAAAAEAAAAAAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhB//+nQf//p0EAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQf//p0H6/6dB9f+nQfX/p0H6/6dB//+nQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhB/v+nQez/p0G3/6dBhP+nQYX/p0G4/6dB7v+nQf//p0EAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEH+/6dB5P+nQV3/p0EK/qdB4fynQeX8p0EW/qdBaP+nQef/p0H//6dBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQf//p0Hs/6dBXP+nQfb8p0FD+KdBUvWnQWL1p0F/+KdBM/2nQW//p0Hv/6dB//+nQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQfr/p0G3/6dBC/6nQUb4p0FR8KdBVOynQYHsp0HV8KdBz/inQUP+p0HB/6dB+/+nQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhB//+nQfT/p0GF/6dB6PynQWn1p0Fr7KdB+OinQQzpp0G57KdB0vWnQQz9p0GM/6dB9f+nQf//p0EAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhB//+nQfX/p0GL/6dBC/2nQd/1p0EF7adBPumnQVDpp0FI7adBQvanQTv9p0GU/6dB9v+nQf//p0EAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQfz/p0HL/6dBg/6nQZf5p0EY8qdBcO2nQZ3tp0Fj8qdB1/mnQZf+p0HO/6dB/P+nQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQf//p0H1/6dBl/+nQeP9p0HU+adBr/anQfv2p0FQ+qdBH/6nQaP/p0H3/6dBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEH//6dB8f+nQZj/p0GZ/qdBYP2nQYr9p0HT/qdBrf+nQfX/p0EAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhB//+nQfX/p0HO/6dBmv+nQaH/p0HZ/6dB+f+nQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEH8/6dB9v+nQff/p0H9/6dBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhB//+nQf//p0EAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIUACBCOAAgQjgAIEIUACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQmgAIEKMAiBC8QQgQvEEIEKFAiBCYQAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBC0QAgQj0IIELOHiBCezQgQl80IEJIHiBCtgcgQrQAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIELRACBCzwsgQjRFIEKP1CBCb1IhQjRRIULgzyBCd0AgQmcKIEKnACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQmkAIEI+CCBCNUUgQtVJIUIyRyNC3oUkQlZ/JEKvLSNCti8hQiE9IELjBiBCTQAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCFAAgQo4CIELRHiBCWdQgQjdGI0IIoiZCA1AoQnk9KEKIaiZCzwsjQqa8IELHGiBCHQIgQg4AIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCOAAgQvQEIEIrNCBCpE8hQmx8JEJ+RihCJrgpQiawKUISJihC2U8kQidBIUKkMSBCvwQgQjgAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCOAAgQrwEIEKvMSBCKEEhQq5KJELbBShCdZspQv2TKULy6SdCxCAkQugsIUIsLiBCVgQgQjEAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCBwAgQqQBIEJmFiBCh6EgQo63IkIa4iVCztgnQubFJ0KkwiVCYJwiQo6ZIEIeFSBCiQEgQgcAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQiMAIEKRBCBCmSwgQknlIEKrnSJCtPIjQtfRI0IKaSJCt8sgQosnIEL5AyBCHAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEJFACBCPAYgQpwrIEIdmCBCkhwhQs0KIUKOfyBCUiMgQscEIEIqACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCRQAgQm4EIELUFCBCOysgQuwnIEJFECBC/wIgQhwAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQiMAIEKIASBCGQQgQq4DIEIBASBCBwAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIHACBCMQAgQioAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgIAAAABAAAAAQAAAAEAAABAAACAPwEAADRCAAAAQQAAAACYmZk/AAAAAAABAAAAAAAAAAAAAAAAAQEACAAAAAAAAAABAAAAAAAQAA5Ub290aHkgV2hpdGUncwAAAAAAAOA/";

        [Test]
        public void Version6Save_StillLoads()
        {
            var w = SaveSystem.FromBytes(System.Convert.FromBase64String(Version6Sample));
            var a = w.Agents;
            Assert.AreEqual(6060UL, w.Seed);
            Assert.AreEqual(4, a.Count);
            Assert.AreEqual(0, a.Species[0]);
            Assert.AreEqual(1u, a.Genes[0].Alleles);
            Assert.AreEqual(2, a.MateId[0]);
            Assert.AreEqual(3, a.MateSpecies[0]);
            Assert.AreEqual(3, a.Species[1]);
            Assert.AreEqual(LifeStage.Egg, a.Stage[2]);
            Assert.AreEqual(0.5f, a.Genes[2].HybridShare);
            Assert.AreEqual(3, a.Genes[2].HybridSpecies);
            Assert.AreEqual(AgentKind.Insect, a.Kind[3]);
            Assert.IsTrue(w.MutationBoost);
            Assert.AreEqual(1, w.Registered.Count);
            Assert.AreEqual("Toothy White's", w.Registered[0].Name);
            // Version 6 had no building parts.
            Assert.AreEqual(0, w.Parts.Count);
            var loop = SimulationFactory.CreateDefault(w);
            for (int i = 0; i < 60; i++) loop.Advance(1.0 / 30.0);
        }

        /// <summary>
        /// A real version 7 save, made by the game just before version 8: seed 7070, a 6 x 4 m tank with
        /// a heater, a pair of red-eyed tree frogs, a stone slab with a pink hide (turned 45 degrees, 1.2
        /// times normal size) on it, and a small plant. Never change these bytes.
        /// </summary>
        private const string Version7Sample =
            "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AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEBAQEBAQEBAQEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAQEBAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgICAgIAAAAAAAAAAQEBAQEBAQEBAQEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAgICAgICAgAAAAAAAAEBAQEBAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAgICAgICAgICAAAAAAABAQEBAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAgICAgICAgICAgIAAAAAAQEBAQEBAQEBAQEBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgICAgICAgICAgICAgAAAAABAQEBAQEBAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAgICAgICAgICAgICAgIAAAAAAQEBAQEBAQEBAQEBAQEBAQEBAAAAAAAAAAAAAQEBAQICAgICAgICAgICAgICAAAAAAAAAQEBAQEBAQEBAQEBAQEBAQAAAAAAAAABAQEBAQECAgICAgICAgICAgICAgAAAAAAAAAAAAAAAQEBAQEBAQEBAQEAAAAAAAABAQEBAQEBAgICAgICAgICAgICAgIAAAAAAAAAAAAAAAEBAQEBAQEBAQEBAAAAAAAAAQEBAQEBAQICAgICAgICAgICAgICAAAAAAAAAAAAAAABAQEBAQEBAQEBAQAAAAAAAAEBAQEBAQEBAgICAgICAgICAgICAgAAAAAAAAAAAAAAAAEBAQEBAQEBAQEAAAAAAAABAQEBAQEBAQICAgICAgICAgICAgAAAAAAAAAAAAAAAAEBAQEBAQEBAQEBAAAAAAAAAQEBAQEBAQEBAgICAgICAgICAgAAAAAAAAAAAAAAAAABAQEBAQEBAQEBAQAAAAAAAAABAQEBAQEBAQECAgICAgICAgAAAAAAAAAAAAAAAAABAQEBAQEBAQEBAQEAAAAAAAAAAAEBAQEBAQEBAQEBAgIAAAAAAAAAAAAAAAAAAQEBAQEBAQEBAQEBAQEBAAAAAAAAAAAAAQEBAQEBAQEBAAAAAAAAAAAAAAAAAAAAAQEBAQEBAQEBAQEBAQEBAQAAAAAAAAAAAAAAAQEBAQEAAAAAAAAAAAAA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";

        [Test]
        public void Version7Save_StillLoads()
        {
            var w = SaveSystem.FromBytes(System.Convert.FromBase64String(Version7Sample));
            Assert.AreEqual(7070UL, w.Seed);
            Assert.AreEqual(2, w.Agents.Count);
            Assert.AreEqual(1, w.Agents.Species[0]);
            Assert.AreEqual(3, w.Parts.Count);
            var hide = w.Parts.Find(2);
            Assert.AreEqual(PartKind.Hide, hide.Kind);
            Assert.AreEqual(1, hide.OnPartId);
            Assert.AreEqual(45f, hide.Rotation);
            Assert.AreEqual(1.2f, hide.Scale);
            Assert.AreEqual(200, hide.R);
            Assert.AreEqual(4, w.Parts.NextId);
            // Version 7 had no money: old tanks stay in the sandbox.
            Assert.IsFalse(w.Economy.Enabled);
            Assert.AreEqual(0, w.Economy.Contracts.Count);
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

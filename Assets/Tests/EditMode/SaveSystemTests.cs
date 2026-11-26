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

        /// <summary>
        /// A real version 5 save, made by the game just before version 6: seed 5050, a 6 x 4 m tank
        /// from MapGenerator with a heater, a fire-bellied female carrying eggs (a little lonely), the
        /// fire-bellied male backing off after a dispute, a White's male holding a territory, a
        /// fire-bellied egg and a cricket, breeding switched off with a limit of 9, 2 rehomed and 4 eggs
        /// collected, run for one second. Never change these bytes.
        /// </summary>
        private const string Version5Sample =
            "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AAAAAAAAAwMDAwMDAwMDAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMDAwMDAwMDAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAwMDAwMDAwMAAAAAAAEBAQEBAAAAAAAAAAADAwAAAAAAAAAAAAAAAAAAAAAAAAAAAwMDAwMDAwMDAAAAAAABAQEBAQEAAAAAAAADAwMDAAAAAAAAAAABAQEAAAAAAAAAAAMDAwMDAwMDAwAAAAABAQEBAQEBAAAAAAAAAwMDAwAAAAAAAAAAAQEBAAAAAAAAAAADAwMDAwMDAwAAAAAAAQECAgICAgAAAAAAAAADAwAAAAAAAAAAAAEBAQAAAAAAAAAAAAMDAwMDAwMAAAAAAAICAgICAgICAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAwMDAwMDAAAAAAACAgICAgICAgIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMDAwMDAAAAAAACAgICAgICAgICAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgICAgICAgICAgIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAICAgICAgICAgICAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAECAgICAgICAgICAgEBAQEBAQEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAgICAgICAgICAgIBAQEBAQEBAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAQICAgICAgICAgICAQEBAQEBAQEBAAAAAAAAAwMDAAAAAAAAAAAAAAAAAAAAAAAAAQEBAgICAgICAgICAQEBAQEBAQEBAQAAAAAAAwMDAwAAAAAAAAAAAAAAAAAAAAAAAAEBAQECAgICAgICAQEBAQEBAQEBAQEAAAAAAAMDAwMAAAAAAAAAAAAAAAAAAAAAAAABAQEBAQEBAQEBAQEBAQEBAQEBAQEBAAAAAAADAwMDAAAAAAAAAAAAAAAAAAAAAAAAAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQAAAAAAAwMDAwAAAAAAAAAAAAAAAAAAAAAAAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEAAAAAAAMDAwMAAAAAAAAAAAAAAAAAAAAAAAEBAQEBAQEBAQEBAQEBAQEBAQEBAQEAAAAAAAAAAwMDAAAAAAAAAAAAAAAAAAAAAAEBAQEBAQEBAQEBAQEBAQEBAQEBAQEAAAAAAAAAAAAAAAAAqEEAACBCGAAAABAAAAAAAKhBBQCoQTMAqEHtAKhBswKoQXsFqEH0CKhBZQuoQWULqEH0CKhBewWoQbMCqEHtAKhBMwCoQQUAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEEJAKhBaACoQTQCqEFfB6hBsxGoQWIfqEHzLKhBhTWoQYU1qEHzLKhBYh+oQbMRqEFfB6hBNAKoQWgAqEEJAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEGOAKhBewOoQf8NqEH5JahBGkuoQU11qEGlmahBhK6oQYSuqEGlmahBTXWoQRpLqEH5JahB/w2oQXsDqEGFAKhBCQCoQQAAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEHkA6hBXxGoQVw2qEGRdahBk8WoQfAUqUF/U6lBu3WpQbt1qUF/U6lB8BSpQZPFqEGRdahBXDaoQVoRqEF7A6hBaQCoQQUAqEEAAKhBAACoQQAAqEEAAKhBAACoQQAAqEExEKhBjjaoQQuHqEGn+KhBzXWpQVroqUGpPqpBzGyqQcxsqkGpPqpBWuipQcx1qUGn+KhBCYeoQV02qEH/DahBNAKoQTIAqEEBAKhBAACoQQAAqEEAAKhBAACoQQAAqEFDLahBbHaoQbT4qEEDmqlBqT6qQRDPqkFKOatBZXGrQWVxq0FKOatBDs+qQaE+qkH3malBoPioQZB1qEH5JahBXgeoQeAAqEEOAKhBAACoQQAAqEEAAKhBAACoQQAAqEExXKhB5MeoQfx1qUGrPqpBLgOrQX6rq0GrJaxBvWWsQb1lrEGmJaxBXaurQc8Cq0EsPqpBcXWpQXTFqEEWS6hBsBGoQX4CqEE1AKhBAgCoQQAAqEEAAKhBAACoQQAAqEHxkahBChmpQbToqUEUz6pBfqurQb5lrEHg66xB7TGtQegxrUGm66xBqWSsQeioq0HIy6pB4OWpQfQTqUEZdahBWB+oQf0EqEF+AKhBBwCoQQAAqEEAAKhBAACoQQAAqEF4v6hBCFmpQSQ/qkFPOatBqyWsQeDrrEH6ea1BAsStQdXDrUGleK1BVeasQbgbrEHBLatBPzWqQblOqUGCmKhBvCyoQQoIqEHoAKhBDwCoQQEAqEEAAKhBAACoQQAAqEEz2ahBAXypQVltqkFrcatBvWWsQe4xrUEExK1B/g+uQWUPrkG0v61BHSStQWFRrEF0W6tB1VmqQddpqUHsqqhB3zSoQSEKqEE2AahBFgCoQQEAqEEAAKhBAACoQQAAqEEz2ahBAXypQVptqkFrcatBvmWsQe4xrUEExK1B9Q+uQQ4PrkGtva1B4h6tQVtMrEFsV6tB7lSqQeVkqUHeqKhBgjSoQRcKqEE2AahBFgCoQQEAqEEAAKhBAACoQQAAqEF4v6hBCFmpQSQ/qkFPOatBqyWsQeDrrEH5ea1B68OtQf7CrUF8c61BXtisQSIMrEE9H6tBpiaqQUBCqUHck6hB8SuoQfUHqEHnAKhBDwCoQQEAqEEAAKhBAACoQQAAqEHxkahBCxmpQbfoqUEZz6pBh6urQchlrEHp66xB5jGtQTMxrUH05qxBllasQfuVq0E6uKpBdNSpQfgHqUErcahBtB6oQewEqEF+AKhBBwCoQQAAqEEAAKhBAACoQQAAqEE0XKhBAcioQVl2qUFJP6pBAASrQXisq0G+JqxB2WasQZpmrEHpJKxBUqWrQfL3qkGBMapBLWupQeu/qEG2SahBfRGoQXsCqEE1AKhBAgCoQQAAqEEAAKhBAACoQQAAqEGULahB/XeoQeX8qEEHoalBD0iqQUHaqkGuRatBYn6rQVh+q0FQRatBotiqQZBEqkG3nKlBtfmoQeJ1qEEAJqhBXgeoQeAAqEEOAKhBAACoQQAAqEEAAKhBAACoQQAAqEE0E6hBD0KoQcKjqEEhKKlBdLWpQVg0qkHmkqpBKMWqQSjFqkHbkqpBGjSqQd+0qUFjJ6lBPKOoQa1BqEGlEKhBiwKoQTYAqEEBAKhBAACoQQAAqEEAAKhBAACoQQAAqEEAACBC//8fQvr/H0Lj/x9Cqv8fQlP/H0Ll/h9Cl/4fQpj+H0Lm/h9CU/8fQqr/H0Lj/x9C+v8fQv//H0IAACBCAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEL//x9C8/8fQrr/H0IW/x9Cz/0fQh38H0Jv+h9CX/kfQl/5H0Jv+h9CHfwfQtD9H0IW/x9Cuv8fQvP/H0L//x9CAAAgQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIELu/x9Ckf8fQkP+H0JI+x9CrvYfQnTxH0Lx7B9CWuofQlrqH0Lx7B9CdPEfQrD2H0JL+x9CRf4fQpL/H0Lv/x9C//8fQgAAIEIAACBCAAAgQgAAIEIAACBCAAAgQgAAIEKE/x9C1/0fQj35H0Jh8R9Cd+cfQqLdH0Lf1R9Cn9EfQp7RH0Lb1R9CnN0fQnbnH0Jm8R9CQPkfQtj9H0KR/x9C8/8fQv//H0IAACBCAAAgQgAAIEIAACBCAAAgQgAAIEL9/R9CN/kfQjHvH0IR4R9Ck9EfQmPDH0KuuB9C8rIfQumyH0KVuB9CQ8MfQqDRH0JH4R9CVe8fQjz5H0JC/h9Cuv8fQvr/H0IAACBCAAAgQgAAIEIAACBCAAAgQgAAIEJf+h9CQvEfQgrhH0IBzR9CpLgfQsGmH0KRmR9Ck5IfQnKSH0JNmR9CR6cfQtO7H0LP0R9Cb+QfQiHyH0JH+x9CFf8fQuT/H0L//x9CAAAgQgAAIEIAACBCAAAgQgAAIEKH9B9CHOcfQnHRH0KJuB9CP6AfQmOLH0I5fB9CM3QfQvxzH0K3fR9C5JgfQjLGH0KA6x9CfPcfQn70H0Ja+B9Czv0fQrH/H0L6/x9CAAAgQgAAIEIAACBCAAAgQgAAIELV7R9CAN0fQijDH0KRph9CVosfQkV0H0KeYx9C0VofQrpcH0Ieex9Cg+IfQiuMIELD7yBCxcEgQjlFIELoBiBC9/0fQmL/H0Lw/x9C//8fQgAAIEIAACBCAAAgQgAAIEIq6B9CCdUfQmO4H0JUmR9CIHwfQptjH0LzUR9C00kfQnxbH0Lx1x9CPXchQpk6I0ItBSRCpmkjQnvDIULZZSBCGQsgQgQAIELj/x9C/v8fQgAAIEIAACBCAAAgQgAAIEL25B9CrdAfQp6yH0JLkh9CDnQfQsxaH0LpSB9CX0UfQpyBH0Kt5SBCYWMkQs38JkLk4ydCyRUnQuSQJEJpYSFCNjUgQkEEIEIRACBC/f8fQgAAIEIAACBCAAAgQgAAIEL25B9CrdAfQpyyH0I8kh9C4XMfQolaH0LKSB9CvkgfQn+jH0JapCFCRkMmQuHXKEJ5cylC1QUpQhyMJkI4NiJCVlsgQnQIIEJJACBC/f8fQgAAIEIAACBCAAAgQgAAIEIr6B9CCdUfQl64H0I0mR9Cx3sfQhxjH0LZUR9CcFIfQmWwH0KdvyFCOYEmQh3/KEJnkilC6SYpQoTBJkKySSJCyF8gQhMJIEJTACBC/v8fQgAAIEIAACBCAAAgQgAAIELV7R9CAd0fQiLDH0Jvph9C+oofQsdzH0JtYx9CDGIfQjSpH0ISRCFCfBMlQkupJ0L3eShCd6MnQigaJUJApCFCwkIgQi0GIEI+ACBC//8fQgAAIEIAACBCAAAgQgAAIEKH9B9CGecfQmHRH0JZuB9C2Z8fQtyKH0Knex9CrnUfQl6PH0KuNSBCuEMiQsBEJEK0HSVCljUkQiVDIkLGjCBCPRMgQhcBIEL6/x9CAAAgQgAAIEIAACBCAAAgQgAAIEJU+h9CEfEfQoLgH0IQzB9CQrcfQgylH0Kqlx9CrpAfQruUH0JDvh9C2EogQqcZIUKihSFC9ychQk12IEKFFyBCqgEgQuT/H0L//x9CAAAgQgAAIEIAACBCAAAgQgAAIEKe/R9CyPcfQp3rH0Ih2x9Ch8kfQrm5H0LwrR9Co6cfQq+nH0LTsR9CVNIfQlgGIEK0KSBCMSMgQmoKIEJJACBCr/8fQvr/H0IAACBCAAAgQgAAIEIAACBCAAAgQgAAIEICAAAAAQAAAAEAAAAAAAAAwAAAgD8BAADYQQAAAAAAAAAAAAAAAAEAAAAAAAkAAAACAAAABAAAAA==";

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

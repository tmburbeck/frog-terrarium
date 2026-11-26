using FrogTerrarium.Simulation;
using NUnit.Framework;

namespace FrogTerrarium.Tests
{
    /// <summary>Phase 3 part 3: money mode, prices, rare frogs, visitors, contracts and research.</summary>
    public class EconomyTests
    {
        private static World Flat()
        {
            var w = new World(5);
            w.Environment.AmbientTemperature = 26f;
            w.Environment.AmbientHumidity = 60f;
            w.Environment.ResetToAmbient();
            return w;
        }

        /// <summary>A loop with only the economy, so long stretches of time run fast.</summary>
        private static void Run(World w, float seconds)
        {
            var loop = new SimulationLoop(w);
            loop.AddSystem(new EconomySystem());
            for (float t = 0f; t < seconds; t += 0.25f) loop.Advance(0.25);
        }

        [Test]
        public void Sandbox_IsFreeAndEverythingIsUnlocked()
        {
            var eco = Flat().Economy;
            Assert.IsFalse(eco.Enabled, "a new world starts in the sandbox");
            Assert.IsTrue(eco.Spend(1e6, "a castle", out _));
            Assert.AreEqual(0.0, eco.Money);
            foreach (var r in Economy.AllResearch) Assert.IsTrue(eco.Has(r.Id));
            foreach (var p in Parts.All) Assert.IsTrue(eco.PartUnlocked(p.Kind));
            for (byte s = 0; s < SpeciesCatalog.Frogs.Length; s++) Assert.IsTrue(eco.SpeciesUnlocked(s));
        }

        [Test]
        public void MoneyMode_ChargesAndLocks_UntilResearched()
        {
            var eco = Flat().Economy;
            eco.StartMoneyMode();
            Assert.AreEqual(Economy.StartingMoney, eco.Money, 1e-6);
            Assert.IsFalse(eco.PartUnlocked(PartKind.Branch));
            Assert.IsTrue(eco.PartUnlocked(PartKind.Rock));
            Assert.IsFalse(eco.SpeciesUnlocked(1));
            Assert.IsTrue(eco.SpeciesUnlocked(0));
            Assert.IsFalse(eco.DeviceUnlocked(DeviceKind.Feeder));

            Assert.IsFalse(eco.Spend(400, "a big tank", out string why));
            StringAssert.Contains("Not enough money", why);
            Assert.IsTrue(eco.Spend(50, "a heater", out _));
            Assert.AreEqual(250.0, eco.Money, 1e-6);
            eco.RefundHalf(50);
            Assert.AreEqual(275.0, eco.Money, 1e-6);

            Assert.IsFalse(eco.DoResearch(Research.TreeFrogs, out why), "no research points yet");
            StringAssert.Contains("research points", why);
            eco.AddResearchPoints(3);
            Assert.IsTrue(eco.DoResearch(Research.TreeFrogs, out why), why);
            Assert.IsTrue(eco.SpeciesUnlocked(1));
            Assert.AreEqual(0, eco.ResearchPoints);
            Assert.AreEqual(175.0, eco.Money, 1e-6);
            Assert.IsFalse(eco.DoResearch(Research.TreeFrogs, out _), "only once");
        }

        [Test]
        public void RareFrogs_AreWorthMore_AndYoungOrStressedOnesLess()
        {
            var w = Flat();
            var a = w.Agents;
            int plain = w.AddFrog(0f, 0f, 0, Sex.Female);
            int mutant = w.AddFrog(1f, 0f, 0, Sex.Female);
            int hybrid = w.AddFrog(2f, 0f, 0, Sex.Female);
            int stressed = w.AddFrog(3f, 0f, 0, Sex.Female);
            foreach (int i in new[] { plain, mutant, hybrid, stressed }) a.Stress[i] = 0f;
            var g = a.Genes[mutant];
            g.Alleles = Mutations.WithCopies(0u, Mutation.Teeth, 1);
            a.Genes[mutant] = g;
            g = a.Genes[hybrid];
            g.HybridSpecies = 3;
            g.HybridShare = 0.5f;
            a.Genes[hybrid] = g;
            a.Stress[stressed] = 1f;
            int egg = w.AddEgg(0f, 1f, 0, Sex.Male, a.Genes[plain], a.Genes[plain]);

            float v = Economy.SaleValue(w, plain);
            Assert.AreEqual(SpeciesCatalog.FrogOf(0).Value, v, 1f, "a calm adult sells for its species' value");
            Assert.Greater(Economy.SaleValue(w, mutant), v * 1.5f, "a mutation is worth a lot");
            Assert.Greater(Economy.SaleValue(w, hybrid), v * 1.5f, "so is a hybrid");
            Assert.Less(Economy.SaleValue(w, stressed), v * 0.6f);
            Assert.Less(Economy.SaleValue(w, egg), v * 0.1f);

            // Registering the mutant's type doubles its value again.
            int type = Lineage.TypeOf(a, mutant);
            w.Registered.Add(new RegisteredSpecies { Type = type, Name = "Toothy", Time = 0 });
            Assert.Greater(Economy.SaleValue(w, mutant), v * 3f);
        }

        [Test]
        public void Selling_PaysAndRemovesTheFrog()
        {
            var w = Flat();
            w.Economy.StartMoneyMode();
            int f = w.AddFrog(0f, 0f, 4, Sex.Male);
            w.Agents.Stress[f] = 0f;
            float value = Economy.SaleValue(w, f);
            Assert.AreEqual(value, w.Sell(f), 1e-4f);
            Assert.AreEqual(0, w.FrogCount());
            Assert.AreEqual(Economy.StartingMoney + value, w.Economy.Money, 1e-4);
            Assert.AreEqual(1, w.Economy.Sold);
        }

        [Test]
        public void Contract_PaysWhenEveryFrogIsDelivered()
        {
            var w = Flat();
            w.Economy.StartMoneyMode();
            var c = new Contract { Id = 7, Kind = ContractKind.Species, A = 2, Needed = 2, Reward = 100f, Deadline = 1000 };
            w.Economy.Contracts.Add(c);
            int wrong = w.AddFrog(0f, 0f, 0, Sex.Male);
            Assert.IsFalse(w.Deliver(7, wrong, out string why));
            StringAssert.Contains("fire-bellied", why);
            int egg = w.AddEgg(0f, 1f, 2, Sex.Male, Genome.Default, Genome.Default);
            Assert.IsFalse(w.Deliver(7, egg, out _), "buyers want grown frogs");

            w.AddFrog(1f, 0f, 2, Sex.Male);
            w.AddFrog(2f, 0f, 2, Sex.Female);
            Assert.IsTrue(w.Deliver(7, w.Agents.Count - 1, out why), why);
            Assert.AreEqual(1, c.Delivered);
            Assert.AreEqual(Economy.StartingMoney, w.Economy.Money, 1e-6, "paid only when complete");
            Assert.IsTrue(w.Deliver(7, w.Agents.Count - 1, out why), why);
            Assert.AreEqual(Economy.StartingMoney + 100.0, w.Economy.Money, 1e-6);
            Assert.AreEqual(0, w.Economy.Contracts.Count);
            Assert.AreEqual(1, w.Economy.ResearchPoints);
        }

        [Test]
        public void Visitors_PreferALivelyDecoratedTank_AndPay()
        {
            var bare = Flat();
            bare.Economy.StartMoneyMode();
            bare.AddFrog(0f, 0f, 0, Sex.Female);

            var fancy = Flat();
            fancy.Economy.StartMoneyMode();
            for (byte s = 0; s < 6; s++) fancy.AddFrog(-4f + s, 0f, s, s % 2 == 0 ? Sex.Female : Sex.Male);
            for (int k = 0; k < 12; k++)
            {
                var info = Parts.All[k];
                fancy.Parts.Place(new PlacedPart { Kind = info.Kind, X = -8f + 1.5f * k, Z = 4f, Scale = 1f, R = (byte)(20 * k), G = 100, B = (byte)(250 - 20 * k) }, fancy.Terrain, out _);
            }
            foreach (var w in new[] { bare, fancy }) for (int i = 0; i < w.Agents.Count; i++) w.Agents.Stress[i] = 0f;

            Assert.Greater(EconomySystem.Appeal(fancy, out _), 3f * EconomySystem.Appeal(bare, out _));
            Run(bare, 600f);
            Run(fancy, 600f);
            Assert.Greater(fancy.Economy.Visitors, 2 * bare.Economy.Visitors);
            Assert.Greater(fancy.Economy.EarnedVisitors, 0.0);
            Assert.Greater(fancy.Economy.Money, fancy.Economy.StartingMoneyOrZero() + 20.0, "a good tank pays for itself");
        }

        [Test]
        public void PowerAndUpkeep_CostMoney()
        {
            var w = Flat();
            w.Economy.StartMoneyMode();
            var heater = w.Devices.Add(DeviceKind.Heater, 0f, 0f);
            heater.Active = true;
            heater.Setting = 40f; // keeps heating (the economy loop does not run the thermostat)
            Run(w, 100f);
            float expected = 100f * (EconomySystem.HeaterPower + EconomySystem.BaseUpkeep);
            Assert.AreEqual(Economy.StartingMoney - expected, w.Economy.Money, 0.2);
            Assert.Greater(w.Economy.SpentPower, 0.0);
            Assert.Greater(w.Economy.SpentUpkeep, 0.0);
        }

        [Test]
        public void Contracts_AreOffered_AndMissedOnesHurtTheReputation()
        {
            var w = Flat();
            w.Economy.StartMoneyMode();
            w.Economy.Reputation = 50f;
            Run(w, 40f);
            Assert.AreEqual(1, w.Economy.Contracts.Count, "the first offer comes after 30 seconds");
            var c = w.Economy.Contracts[0];
            Assert.Greater(c.Reward, 0f);
            Assert.IsNotEmpty(c.Describe(w));
            Assert.IsNotEmpty(c.Buyer);
            Run(w, Economy.ContractSeconds + 10f);
            Assert.IsFalse(w.Economy.Contracts.Contains(c), "it ran out");
            Assert.LessOrEqual(w.Economy.Contracts.Count, Economy.MaxContracts);
        }

        [Test]
        public void Discoveries_EarnResearchPoints()
        {
            var w = Flat();
            w.Economy.StartMoneyMode();
            int f = w.AddFrog(0f, 0f, 0, Sex.Female);
            var g = w.Agents.Genes[f];
            g.Alleles = Mutations.WithCopies(0u, Mutation.Claws, 1);
            w.Agents.Genes[f] = g;
            w.NoteDiscoveries(f);
            Assert.AreEqual(2, w.Economy.ResearchPoints);
            w.NoteDiscoveries(f);
            Assert.AreEqual(2, w.Economy.ResearchPoints, "only the first time");
        }

        [Test]
        public void Money_IsSavedAndLoaded()
        {
            var w = Flat();
            var eco = w.Economy;
            eco.StartMoneyMode();
            eco.Money = 1234.5;
            eco.ResearchPoints = 4;
            eco.DoResearch(Research.Botany, out _);
            eco.Visitors = 77;
            eco.Contracts.Add(new Contract { Id = 3, Kind = ContractKind.Hybrid, A = 0, B = 3, Needed = 1, Delivered = 0, Reward = 237f, Deadline = 500.5, Buyer = "A zoo" });
            eco.NextContractId = 4;
            var b = SaveSystem.FromBytes(SaveSystem.ToBytes(w)).Economy;
            Assert.IsTrue(b.Enabled);
            Assert.AreEqual(eco.Money, b.Money);
            Assert.AreEqual(3, b.ResearchPoints);
            Assert.IsTrue(b.Has(Research.Botany));
            Assert.IsFalse(b.Has(Research.Carpentry));
            Assert.AreEqual(77, b.Visitors);
            Assert.AreEqual(4, b.NextContractId);
            Assert.AreEqual(1, b.Contracts.Count);
            Assert.AreEqual(ContractKind.Hybrid, b.Contracts[0].Kind);
            Assert.AreEqual(3, b.Contracts[0].B);
            Assert.AreEqual(237f, b.Contracts[0].Reward);
            Assert.AreEqual("A zoo", b.Contracts[0].Buyer);
        }
    }

    internal static class EconomyTestHelpers
    {
        public static double StartingMoneyOrZero(this Economy eco) => eco.Enabled ? Economy.StartingMoney : 0.0;
    }
}

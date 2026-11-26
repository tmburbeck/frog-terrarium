using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>Things the player can research to unlock. Never reorder: saves store them as bits.</summary>
    public enum Research : byte
    {
        TreeFrogs = 0,
        Toads = 1,
        Carpentry = 2,
        Masonry = 3,
        Botany = 4,
        Feeder = 5,
        GeneticsLab = 6,
        Marketing = 7,
    }

    public sealed class ResearchInfo
    {
        public Research Id;
        public string Name;
        public string Description;
        /// <summary>Research points needed. Points come from discoveries, new species, contracts and visitors.</summary>
        public int Points;
        /// <summary>Money it costs as well.</summary>
        public float Money;
    }

    /// <summary>
    /// Money mode: the tank's money, reputation, research and contracts, and the price of everything.
    ///
    /// * Money comes from visitors (more come to an appealing tank with a good reputation), selling
    ///   frogs (rare ones are worth much more) and contracts (a buyer asks for certain frogs).
    /// * Money goes on parts, devices, frogs and crickets, on power for heaters and misters, and on
    ///   upkeep (cleaning and care: a little per minute, plus a little per frog).
    /// * Research points come from discoveries (new mutations and hybrids), registering new species,
    ///   finishing contracts and visitors. Research unlocks more species, parts, the feeder, the
    ///   mutation boost and marketing.
    ///
    /// With money mode off (sandbox), everything is free and unlocked. Saved (version 8).
    /// </summary>
    public sealed class Economy
    {
        /// <summary>Money mode on. Off is the sandbox: everything free and unlocked.</summary>
        public bool Enabled;
        public double Money;
        /// <summary>0 to 100. A good reputation brings more visitors; escapes and missed contracts hurt it.</summary>
        public float Reputation = 10f;
        public int ResearchPoints;
        /// <summary>Researched items, one bit each (see <see cref="Research"/>).</summary>
        public uint Researched;
        /// <summary>Style sets bought, one bit each (see <see cref="TankLooks.Styles"/>). Saved from version 9.</summary>
        public uint StylesOwned;
        /// <summary>Visitors so far.</summary>
        public long Visitors;
        /// <summary>Frogs sold so far.</summary>
        public int Sold;

        // Totals for the money panel.
        public double EarnedVisitors, EarnedSales, EarnedContracts;
        public double SpentPower, SpentUpkeep, SpentShop;

        public readonly List<Contract> Contracts = new List<Contract>();
        public int NextContractId = 1;
        /// <summary>Seconds until the next contract offer.</summary>
        public float ContractTimer = 30f;
        /// <summary>Part of a visitor still to arrive (visitors come one at a time).</summary>
        public float VisitorCarry;

        /// <summary>How appealing the tank is to visitors, worked out every few seconds. Not saved.</summary>
        public float Appeal;
        /// <summary>Visitors per minute right now. Not saved.</summary>
        public float VisitorsPerMinute;
        /// <summary>Money per minute going on power and upkeep right now. Not saved.</summary>
        public float CostPerMinute;

        public const float StartingMoney = 300f;
        public const int MaxContracts = 3;
        public const float ContractSeconds = 900f;
        public const float ContractEvery = 180f;

        /// <summary>Turns money mode on with starting money, as for a new game.</summary>
        public void StartMoneyMode()
        {
            Enabled = true;
            Money = StartingMoney;
            Reputation = 10f;
        }

        public static string Format(double money) => (money < 0 ? "-$" : "$") + Math.Abs(Math.Round(money)).ToString("0");

        public bool CanAfford(double cost) => !Enabled || Money + 1e-6 >= cost;

        /// <summary>Pays for something if money mode is on. Returns false and why if there is not enough money.</summary>
        public bool Spend(double cost, string what, out string why)
        {
            why = "";
            if (!Enabled || cost <= 0) return true;
            if (Money + 1e-6 < cost)
            {
                why = "Not enough money: " + what + " costs " + Format(cost) + " and you have " + Format(Money) + ".";
                return false;
            }
            Money -= cost;
            SpentShop += cost;
            return true;
        }

        /// <summary>Gives back half of what something cost, when it is taken out of the tank.</summary>
        public void RefundHalf(double cost)
        {
            if (!Enabled || cost <= 0) return;
            Money += 0.5 * cost;
            SpentShop -= 0.5 * cost;
        }

        // ------------------------------------------------------------------ prices

        public static float PartCost(PlacedPart p) => (float)Math.Round(p.Info.Cost * p.Scale * p.Scale);
        public static float PartCost(PartKind kind, float scale) => (float)Math.Round(Parts.Info(kind).Cost * scale * scale);

        public static float DeviceCost(DeviceKind kind) => kind == DeviceKind.Heater ? 60f : kind == DeviceKind.Mister ? 50f : 40f;
        public const float CricketsCost = 3f;
        public const float FeederRefillCost = 15f;

        public static float FrogCost(byte species) => SpeciesCatalog.FrogOf(species).Value;

        /// <summary>
        /// What a frog would sell for. Grown, calm frogs are worth most. Rarity counts for a lot: hybrids
        /// are worth up to 1.75 times more, each mutation it shows 1.6 times, a registered species twice,
        /// and a new type that has bred true for longer a little more again.
        /// </summary>
        public static float SaleValue(World w, int i)
        {
            var a = w.Agents;
            if (i < 0 || i >= a.Count || a.Kind[i] != AgentKind.Frog) return 0f;
            var g = a.Genes[i];
            float value = SpeciesCatalog.FrogOf(a.Species[i]).Value;
            if (g.IsHybrid)
            {
                float other = SpeciesCatalog.FrogOf(g.HybridSpecies).Value;
                value = value + (other - value) * g.HybridShare;
                value *= 1f + 1.5f * g.HybridShare;
            }
            ushort muts = g.Visible;
            for (int m = 0; m < Mutations.Count; m++) if ((muts & (1 << m)) != 0) value *= 1.6f;
            int type = Lineage.TypeOf(a, i);
            if (Lineage.IsNewType(type))
            {
                if (w.IsRegistered(type)) value *= 2f;
                value *= 1f + 0.1f * Math.Min((int)g.Generation, 5);
            }
            switch (a.Stage[i])
            {
                case LifeStage.Egg: value *= 0.05f; break;
                case LifeStage.Tadpole: value *= 0.1f; break;
                case LifeStage.Froglet: value *= 0.3f; break;
                case LifeStage.Juvenile: value *= 0.6f; break;
                case LifeStage.Elder: value *= 0.5f; break;
            }
            value *= 1f - 0.5f * Math.Min(1f, Math.Max(0f, a.Stress[i]));
            return (float)Math.Max(1.0, Math.Round(value));
        }

        // ------------------------------------------------------------------ research

        public static readonly ResearchInfo[] AllResearch =
        {
            new ResearchInfo { Id = Research.TreeFrogs, Name = "Tree frog care", Points = 3, Money = 100f,
                Description = "Lets you buy red-eyed, White-lipped and Morelet's tree frogs." },
            new ResearchInfo { Id = Research.Toads, Name = "Toad keeping", Points = 2, Money = 60f,
                Description = "Lets you buy yellow-bellied toads." },
            new ResearchInfo { Id = Research.Carpentry, Name = "Carpentry", Points = 2, Money = 80f,
                Description = "Branches, pillars, platforms and ramps for the Build menu." },
            new ResearchInfo { Id = Research.Masonry, Name = "Masonry", Points = 2, Money = 60f,
                Description = "Walls for the Build menu." },
            new ResearchInfo { Id = Research.Botany, Name = "Botany", Points = 1, Money = 40f,
                Description = "Ferns for the Build menu." },
            new ResearchInfo { Id = Research.Feeder, Name = "Automatic feeder", Points = 3, Money = 120f,
                Description = "A feeder that releases crickets on its own." },
            new ResearchInfo { Id = Research.GeneticsLab, Name = "Genetics lab", Points = 6, Money = 250f,
                Description = "Unlocks the mutation boost switch: 25 times more mutations." },
            new ResearchInfo { Id = Research.Marketing, Name = "Marketing", Points = 4, Money = 150f,
                Description = "Posters and a website: 25% more visitors." },
        };

        public static ResearchInfo InfoOf(Research r) => AllResearch[(int)r];

        public bool Has(Research r) => !Enabled || (Researched & (1u << (int)r)) != 0;

        /// <summary>Researches something if there are enough points and money. Returns false and why if not.</summary>
        public bool DoResearch(Research r, out string why)
        {
            why = "";
            var info = InfoOf(r);
            if (Has(r)) { why = info.Name + " is already done."; return false; }
            if (ResearchPoints < info.Points) { why = info.Name + " needs " + info.Points + " research points; you have " + ResearchPoints + "."; return false; }
            if (!Spend(info.Money, info.Name, out why)) return false;
            ResearchPoints -= info.Points;
            Researched |= 1u << (int)r;
            return true;
        }

        public void AddResearchPoints(int n)
        {
            if (Enabled) ResearchPoints += n;
        }

        /// <summary>Whether the Build menu offers a part (starter parts always; others need research).</summary>
        public bool PartUnlocked(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.Branch:
                case PartKind.Pillar:
                case PartKind.Platform:
                case PartKind.Ramp:
                    return Has(Research.Carpentry);
                case PartKind.Wall: return Has(Research.Masonry);
                case PartKind.Fern: return Has(Research.Botany);
                default: return true;
            }
        }

        /// <summary>Whether a species can be bought (White's and fire-bellied always; others need research).</summary>
        public bool SpeciesUnlocked(byte species)
        {
            switch (species)
            {
                case 1:
                case 3:
                case 4:
                    return Has(Research.TreeFrogs);
                case 5: return Has(Research.Toads);
                default: return true;
            }
        }

        public bool DeviceUnlocked(DeviceKind kind) => kind != DeviceKind.Feeder || Has(Research.Feeder);

        /// <summary>The research that unlocks a part, for messages.</summary>
        public static string UnlockedBy(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.Wall: return InfoOf(Research.Masonry).Name;
                case PartKind.Fern: return InfoOf(Research.Botany).Name;
                default: return InfoOf(Research.Carpentry).Name;
            }
        }
    }
}

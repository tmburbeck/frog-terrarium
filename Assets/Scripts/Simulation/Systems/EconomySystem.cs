using System;
using System.Collections.Generic;

namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Money mode, once a second: pays for power and upkeep, works out how appealing the tank is,
    /// lets visitors in (each buys a ticket), moves the reputation, and offers and expires
    /// contracts. Does nothing in the sandbox (money mode off).
    ///
    /// Appeal is what makes the tank worth a visit: different species, hybrids, mutations and
    /// registered species, how many frogs there are, and how much variety the decorations have
    /// (parts, kinds of part and colours), playground pieces and frogs playing on them, all scaled
    /// down if the frogs are stressed.
    /// </summary>
    public sealed class EconomySystem : ISimSystem
    {
        public string Name => "Economy";
        public int TicksPerSecond => 1;

        /// <summary>Power while a heater is heating, and while a mister is spraying, in money per second.</summary>
        public const float HeaterPower = 0.03f, MisterPower = 0.015f;
        /// <summary>Power for a smart controller while it is switched on, in money per second.</summary>
        public const float ControllerPower = 0.005f;
        /// <summary>Upkeep: cleaning and care for the tank, plus a little per frog, in money per second.</summary>
        public const float BaseUpkeep = 0.006f, FrogUpkeep = 0.0005f;
        /// <summary>A visitor's ticket, plus a tip that grows with appeal.</summary>
        public const float Ticket = 1f;
        /// <summary>
        /// Visitors a minute per point of appeal (at middling reputation). Tuned from the first
        /// playtest: at 1/15 a starter tank lost money while the player did nothing, and a tank with
        /// six species earned under 1 a minute. At 1/6 a starter tank earns a little (about +1 a
        /// minute) and a lively, decorated one earns well.
        /// </summary>
        public const float VisitorsPerAppeal = 1f / 5f;
        /// <summary>Every this many visitors earns a research point.</summary>
        public const int VisitorsPerPoint = 30;

        private float _appealTimer;

        public void Tick(World world, float dt)
        {
            var eco = world.Economy;
            if (!eco.Enabled) return;

            // Power and upkeep.
            float power = 0f;
            foreach (var d in world.Devices.All)
            {
                if (!d.Enabled || !d.Active) continue;
                if (d.Kind == DeviceKind.Heater) power += HeaterPower;
                else if (d.Kind == DeviceKind.Mister) power += MisterPower;
            }
            foreach (var d in world.Devices.All) if (d.Kind == DeviceKind.Controller && d.Enabled) power += ControllerPower;
            power += Playground.RidePower * PoweredRiders(world);
            int frogs = world.FrogCount();
            float upkeep = BaseUpkeep + FrogUpkeep * frogs;
            eco.Money -= (power + upkeep) * dt;
            eco.SpentPower += power * dt;
            eco.SpentUpkeep += upkeep * dt;
            eco.CostPerMinute = 60f * (power + upkeep);

            _appealTimer -= dt;
            if (_appealTimer <= 0f)
            {
                _appealTimer = 10f;
                eco.Appeal = Appeal(world, out float wellbeing);
                float marketing = eco.Has(Research.Marketing) ? 1.25f : 1f;
                eco.VisitorsPerMinute = eco.Appeal * VisitorsPerAppeal * (0.5f + eco.Reputation / 100f) * marketing;
                // Reputation slowly follows how good the tank is.
                float target = Math.Min(100f, eco.Appeal) * wellbeing;
                eco.Reputation += (target - eco.Reputation) * 0.02f;
            }

            // Visitors arrive one at a time.
            eco.VisitorCarry += eco.VisitorsPerMinute / 60f * dt;
            while (eco.VisitorCarry >= 1f)
            {
                eco.VisitorCarry -= 1f;
                float paid = Ticket + eco.Appeal / 100f;
                eco.Money += paid;
                eco.EarnedVisitors += paid;
                eco.Visitors++;
                if (eco.Visitors % VisitorsPerPoint == 0) eco.AddResearchPoints(1);
            }

            UpdateContracts(world, dt);
            eco.Reputation = Math.Max(0f, Math.Min(100f, eco.Reputation));
        }

        /// <summary>How appealing the tank is to visitors (roughly 0 to 150), and how well the frogs are (0.3 to 1).</summary>
        public static float Appeal(World world, out float wellbeing)
        {
            var a = world.Agents;
            int species = 0, hybrids = 0, registered = 0, frogs = 0;
            ushort mutations = 0;
            var seenTypes = new HashSet<int>();
            var seenHybrids = new HashSet<int>();
            float stress = 0f;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a.IsLandFrog(i)) continue;
                frogs++;
                stress += a.Stress[i];
                species |= 1 << a.Species[i];
                var g = a.Genes[i];
                mutations |= g.Visible;
                if (g.IsHybrid)
                {
                    int lo = Math.Min(a.Species[i], g.HybridSpecies), hi = Math.Max(a.Species[i], g.HybridSpecies);
                    if (seenHybrids.Add(lo * 8 + hi)) hybrids++;
                }
                int type = Lineage.TypeOf(a, i);
                if (Lineage.IsNewType(type) && seenTypes.Add(type) && world.IsRegistered(type)) registered++;
            }
            wellbeing = frogs == 0 ? 1f : Math.Max(0.3f, 1f - stress / frogs);

            float raw = 8f * Bits(species) + 6f * hybrids + 5f * Bits(mutations) + 12f * registered + 3f * (float)Math.Sqrt(frogs);

            // Decorations: more parts, more kinds of part and more colours make a better show.
            var parts = world.Parts.All;
            int kinds = 0;
            var colours = new HashSet<int>();
            foreach (var p in parts)
            {
                kinds |= 1 << (int)p.Kind;
                colours.Add((p.R >> 5) | ((p.G >> 5) << 3) | ((p.B >> 5) << 6));
            }
            raw += 0.25f * Math.Min(parts.Count, 120) + 1.5f * Bits(kinds) + 0.75f * Math.Min(colours.Count, 20);
            // Visitors love watching frogs play: each piece is a small draw, and frogs on rides a bigger one.
            raw += 1.5f * Math.Min(world.Parts.PlayPieces.Count, 10) + 3f * Math.Min(Riders(world), 8);
            // Visitors like a clean tank: a filthy one loses 40% of its appeal.
            return raw * (0.4f + 0.6f * wellbeing) * (1f - 0.4f * world.Dirt / 100f);
        }

        /// <summary>Frogs riding a powered playground piece (an air tunnel) right now.</summary>
        public static int PoweredRiders(World world)
        {
            var a = world.Agents;
            if (world.Parts.PlayPieces.Count == 0) return 0;
            int n = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.RidePart[i] == 0) continue;
                var p = world.Parts.Find(a.RidePart[i]);
                if (p != null && Playground.InfoOf(p.Kind)?.Powered == true) n++;
            }
            return n;
        }

        /// <summary>Frogs riding any playground piece right now.</summary>
        public static int Riders(World world)
        {
            var a = world.Agents;
            int n = 0;
            for (int i = 0; i < a.Count; i++) if (a.RidePart[i] != 0) n++;
            return n;
        }

        private static int Bits(int v)
        {
            int n = 0;
            for (; v != 0; v &= v - 1) n++;
            return n;
        }

        private static readonly string[] Buyers =
        {
            "A pet shop", "A school science club", "A zoo", "A collector", "A frog breeder", "A nature museum",
            "A university lab", "A garden centre",
        };

        private static void UpdateContracts(World world, float dt)
        {
            var eco = world.Economy;
            for (int k = eco.Contracts.Count - 1; k >= 0; k--)
            {
                if (world.SimTime <= eco.Contracts[k].Deadline) continue;
                eco.Contracts.RemoveAt(k);
                eco.Reputation -= 3f; // a missed order
            }
            eco.ContractTimer -= dt;
            if (eco.ContractTimer > 0f) return;
            eco.ContractTimer = Economy.ContractEvery;
            if (eco.Contracts.Count >= Economy.MaxContracts) return;
            var c = MakeContract(world, world.Random.Get("Economy"));
            if (c != null) eco.Contracts.Add(c);
        }

        /// <summary>A new contract for something the player could supply, or null.</summary>
        public static Contract MakeContract(World world, SeededRandom rng)
        {
            var eco = world.Economy;
            var choices = new List<ContractKind> { ContractKind.Species, ContractKind.Species };
            if (world.DiscoveredMutations != 0) choices.Add(ContractKind.Mutation);
            if (world.DiscoveredHybrids != 0UL) choices.Add(ContractKind.Hybrid);
            if (world.Registered.Count > 0) choices.Add(ContractKind.Registered);
            var c = new Contract { Id = eco.NextContractId++, Kind = choices[rng.Range(0, choices.Count)], Deadline = world.SimTime + Economy.ContractSeconds };
            c.Buyer = Buyers[rng.Range(0, Buyers.Length)];
            switch (c.Kind)
            {
                case ContractKind.Species:
                {
                    var open = new List<byte>();
                    for (int s = 0; s < SpeciesCatalog.Frogs.Length; s++) if (eco.SpeciesUnlocked((byte)s)) open.Add((byte)s);
                    c.A = open[rng.Range(0, open.Count)];
                    c.Needed = rng.Range(1, 4);
                    c.Reward = (float)Math.Round(SpeciesCatalog.FrogOf(c.A).Value * c.Needed * 1.6f);
                    break;
                }
                case ContractKind.Mutation:
                {
                    var found = new List<byte>();
                    for (int m = 0; m < Mutations.Count; m++) if ((world.DiscoveredMutations & (1 << m)) != 0) found.Add((byte)m);
                    c.A = found[rng.Range(0, found.Count)];
                    c.Needed = 1;
                    c.Reward = 160f;
                    break;
                }
                case ContractKind.Hybrid:
                {
                    var found = new List<int>();
                    for (int bit = 0; bit < 64; bit++) if ((world.DiscoveredHybrids & (1UL << bit)) != 0UL) found.Add(bit);
                    int pick = found[rng.Range(0, found.Count)];
                    c.A = (byte)(pick / 8);
                    c.B = (byte)(pick % 8);
                    c.Needed = 1;
                    c.Reward = (float)Math.Round((SpeciesCatalog.FrogOf(c.A).Value + SpeciesCatalog.FrogOf(c.B).Value) * 2.5f);
                    break;
                }
                case ContractKind.Registered:
                {
                    var r = world.Registered[rng.Range(0, world.Registered.Count)];
                    c.Type = r.Type;
                    c.Needed = 1;
                    c.Reward = 300f;
                    break;
                }
            }
            return c;
        }
    }
}

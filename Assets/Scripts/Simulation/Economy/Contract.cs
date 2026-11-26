namespace FrogTerrarium.Simulation
{
    public enum ContractKind : byte
    {
        /// <summary>Grown frogs of one species (hybrids count as their main species).</summary>
        Species = 0,
        /// <summary>A grown frog showing one mutation.</summary>
        Mutation = 1,
        /// <summary>A grown hybrid of two species.</summary>
        Hybrid = 2,
        /// <summary>A grown frog of a species the player registered.</summary>
        Registered = 3,
    }

    /// <summary>
    /// A buyer's order: some grown frogs of a certain kind by a deadline, for a reward well above
    /// what they would sell for. Frogs are handed over one at a time from the frog inspector.
    /// Saved (version 8).
    /// </summary>
    public sealed class Contract
    {
        public int Id;
        public ContractKind Kind;
        /// <summary>The species (Species, Hybrid) or the mutation number (Mutation).</summary>
        public byte A;
        /// <summary>The other species of a hybrid.</summary>
        public byte B;
        /// <summary>The type of frog wanted (Registered).</summary>
        public int Type;
        public int Needed;
        public int Delivered;
        public float Reward;
        public double Deadline;
        /// <summary>A made-up buyer, for flavour.</summary>
        public string Buyer = "";

        /// <summary>Whether a frog in the tank is what this contract asks for.</summary>
        public bool Matches(World w, int i)
        {
            var a = w.Agents;
            if (i < 0 || i >= a.Count || a.Kind[i] != AgentKind.Frog) return false;
            if (a.Stage[i] != LifeStage.Juvenile && a.Stage[i] != LifeStage.Adult) return false;
            var g = a.Genes[i];
            switch (Kind)
            {
                case ContractKind.Species: return a.Species[i] == A;
                case ContractKind.Mutation: return (g.Visible & (1 << A)) != 0;
                case ContractKind.Hybrid:
                    return g.IsHybrid && ((a.Species[i] == A && g.HybridSpecies == B) || (a.Species[i] == B && g.HybridSpecies == A));
                case ContractKind.Registered: return Lineage.TypeOf(a, i) == Type;
                default: return false;
            }
        }

        /// <summary>"2 grown red-eyed tree frogs", "a grown frog with teeth" and so on.</summary>
        public string Describe(World w)
        {
            string many = Needed == 1 ? "a grown " : Needed + " grown ";
            switch (Kind)
            {
                case ContractKind.Species:
                {
                    string name = SpeciesCatalog.FrogOf(A).Name.ToLowerInvariant();
                    return many + (Needed == 1 ? name : name + "s");
                }
                case ContractKind.Mutation:
                    return many + (Needed == 1 ? "frog" : "frogs") + " with " + Mutations.All[A].Name.ToLowerInvariant();
                case ContractKind.Hybrid:
                    return many + SpeciesCatalog.FrogOf(A).ShortName + " × " + SpeciesCatalog.FrogOf(B).ShortName + (Needed == 1 ? " hybrid" : " hybrids");
                default:
                {
                    string name = "new species";
                    foreach (var r in w.Registered) if (r.Type == Type) name = r.Name;
                    return many + name;
                }
            }
        }
    }
}

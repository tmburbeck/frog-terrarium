namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// Sandbox switches that turn parts of the simulation off, one bit each. A new game has none
    /// set, so everything is on. Never renumber: saves store the bits.
    /// </summary>
    [System.Flags]
    public enum SandboxRules : uint
    {
        None = 0,
        /// <summary>Frogs never get hungry, thirsty or tired.</summary>
        NoNeeds = 1 << 0,
        /// <summary>Every spot feels perfect: temperature and humidity no longer matter to frogs.</summary>
        PerfectClimate = 1 << 1,
        /// <summary>Frogs never get stressed, so they never try to escape.</summary>
        NoStress = 1 << 2,
        /// <summary>Frogs never die of old age.</summary>
        NoOldAge = 1 << 3,
        /// <summary>The tank never gets dirty.</summary>
        NoDirt = 1 << 4,
        /// <summary>Sociable frogs never get lonely.</summary>
        NoLoneliness = 1 << 5,
        /// <summary>Feeders never run out of crickets.</summary>
        EndlessFeeders = 1 << 6,
    }

    public sealed class SandboxRuleInfo
    {
        public SandboxRules Rule;
        /// <summary>What the switch is called when it is on, in the "system on" sense ("Hunger and thirst").</summary>
        public string System;
        /// <summary>What happens when the system is switched off.</summary>
        public string WhenOff;
    }

    public static class Sandbox
    {
        /// <summary>Every switch, in the order the Rules list shows them.</summary>
        public static readonly SandboxRuleInfo[] All =
        {
            new SandboxRuleInfo { Rule = SandboxRules.NoNeeds, System = "Hunger, thirst and sleep", WhenOff = "Frogs never get hungry, thirsty or tired." },
            new SandboxRuleInfo { Rule = SandboxRules.PerfectClimate, System = "Climate", WhenOff = "Temperature and humidity no longer bother frogs." },
            new SandboxRuleInfo { Rule = SandboxRules.NoLoneliness, System = "Loneliness", WhenOff = "Sociable frogs never get lonely." },
            new SandboxRuleInfo { Rule = SandboxRules.NoStress, System = "Stress and escapes", WhenOff = "Frogs stay calm and never try to climb out." },
            new SandboxRuleInfo { Rule = SandboxRules.NoOldAge, System = "Old age", WhenOff = "Frogs live for ever." },
            new SandboxRuleInfo { Rule = SandboxRules.NoDirt, System = "Dirt", WhenOff = "The tank stays spotless." },
            new SandboxRuleInfo { Rule = SandboxRules.EndlessFeeders, System = "Feeder supply", WhenOff = "Feeders never run out of crickets." },
        };

        /// <summary>Every bit that means something; anything else in a save is ignored.</summary>
        public const SandboxRules Known = (SandboxRules)0x7F;
    }
}

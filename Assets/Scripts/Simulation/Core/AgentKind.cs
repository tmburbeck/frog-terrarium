namespace FrogTerrarium.Simulation
{
    /// <summary>What sort of creature an agent is. Stored as one byte per agent.</summary>
    public enum AgentKind : byte
    {
        Frog = 0,
        Insect = 1,
    }

    /// <summary>What an agent is trying to do right now. Stored as one byte per agent.</summary>
    public enum Goal : byte
    {
        Idle = 0,
        Wander = 1,
        Eat = 2,
        Drink = 3,
        Sleep = 4,
        SeekComfort = 5,
        Flee = 6,
        // Added in save version 4.
        /// <summary>A female carrying eggs heads for water to lay them.</summary>
        LayEggs = 7,
        /// <summary>A frog stressed for too long heads for the tank wall and tries to climb out.</summary>
        Escape = 8,
        /// <summary>A young frog moves away from a crowded spot to find room of its own.</summary>
        Disperse = 9,
    }

    /// <summary>
    /// Where a frog is in its life. Stored as one byte per agent (always Adult for insects).
    /// A frog moves through the stages as its growth adds up; it becomes an elder by real age.
    /// </summary>
    public enum LifeStage : byte
    {
        /// <summary>Sits in the water and develops. Dies if the water dries up.</summary>
        Egg = 0,
        /// <summary>Swims in the water and grazes. Cannot leave it.</summary>
        Tadpole = 1,
        /// <summary>Has just left the water: a tiny frog with the usual needs.</summary>
        Froglet = 2,
        /// <summary>Half grown. Moves away from crowds to find its own space.</summary>
        Juvenile = 3,
        /// <summary>Fully grown and can breed.</summary>
        Adult = 4,
        /// <summary>Old: no longer breeds, and will die of old age before long.</summary>
        Elder = 5,
    }

    /// <summary>Stored as one byte per agent.</summary>
    public enum Sex : byte
    {
        Female = 0,
        Male = 1,
    }
}

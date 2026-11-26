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
    }
}

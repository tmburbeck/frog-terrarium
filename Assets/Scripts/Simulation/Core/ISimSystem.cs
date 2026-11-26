namespace FrogTerrarium.Simulation
{
    /// <summary>
    /// One piece of simulation logic that runs at its own fixed rate,
    /// for example needs at 10 ticks per second or movement at 30.
    /// </summary>
    public interface ISimSystem
    {
        /// <summary>Short name shown in the debug overlay, also used for its random stream.</summary>
        string Name { get; }

        /// <summary>How many times per second this system ticks.</summary>
        int TicksPerSecond { get; }

        /// <summary>Advance by exactly one fixed step of 1 / TicksPerSecond seconds.</summary>
        void Tick(World world, float dt);
    }
}

namespace FrogTerrarium.Simulation
{
    /// <summary>Builds a simulation with the standard set of systems in the standard order.</summary>
    public static class SimulationFactory
    {
        public static SimulationLoop CreateDefault(World world)
        {
            var sim = new SimulationLoop(world);
            sim.AddSystem(new EnvironmentSystem());
            sim.AddSystem(new NeedsSystem());
            sim.AddSystem(new MovementSystem());
            return sim;
        }
    }
}

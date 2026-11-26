# Architecture (Phase 0)

The rule everything follows: **the simulation is separate from presentation.** Simulation
code is plain C# with no Unity references, runs on a fixed tick, and keeps its data in plain
arrays. Unity code reads that data to draw it and never writes to it.

```
Assets/
  Scripts/
    Simulation/        FrogTerrarium.Simulation  (no UnityEngine; testable with plain .NET)
      Core/            SimulationLoop, World, AgentStore, SeededRandom, RandomService
      Systems/         NeedsSystem (10 Hz), MovementSystem (30 Hz), SimulationFactory
      Save/            SaveSystem (versioned binary save/load)
    Game/              FrogTerrarium.Game  (Unity MonoBehaviours)
                       SandboxBootstrap, SimulationRunner, DebugOverlay, OrbitCamera
    Editor/            FrogTerrarium.Editor  (scene setup, CI build entry point)
  Tests/
    EditMode/          fast logic tests (also run by tools/SimCoreTests with .NET)
    PlayMode/          tests that start the sandbox and check it really runs
```

## Fixed-tick loop

`SimulationLoop.Advance(frameSeconds)` is called once per rendered frame. Each system has its
own tick rate and its own time accumulator, and runs as many fixed steps as are due. So needs
tick exactly 10 times per simulated second and movement 30 times, whatever the frame rate.
If one frame is very slow, catch-up is capped at 0.25 s so the game never spirals.
`TimeScale` handles pause and fast-forward.

## Agent data

`AgentStore` holds one array per field (`PosX`, `Hunger`, ...). Agent *i* is the *i*-th entry
of every array. Removing an agent moves the last one into the gap; refer to specific agents by
`Id`, not by index. This layout suits Unity Jobs and Burst later without a rewrite.

## Randomness

All randomness goes through `World.Random.Get("<stream name>")`. Each system has its own
stream derived from the world seed and the stream name, so the same seed always produces the
same tank, and adding a new system never changes another system's numbers. Never use
`UnityEngine.Random` or `System.Random` in simulation code.

## Debug overlay

Shows smoothed and worst frame time, agent counts, total sim time per frame, and per-system
milliseconds per tick and tick counts. F3 toggles it.

## Adding a new system

1. Create a class implementing `ISimSystem` in `Simulation/Systems`.
2. Register it in `SimulationFactory.CreateDefault`.
3. If it has state that must survive a save, add it to the save format (see SAVE_FORMAT.md).
4. Add edit-mode tests.

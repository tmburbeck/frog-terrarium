# Architecture

The rule everything follows: **the simulation is separate from presentation.** Simulation
code is plain C# with no Unity references, runs on a fixed tick, and keeps its data in plain
arrays. Unity code reads that data to draw it and never writes to it.

```
Assets/
  Scripts/
    Simulation/        FrogTerrarium.Simulation  (no UnityEngine; testable with plain .NET)
      Core/            SimulationLoop, World, AgentStore, SeededRandom, RandomService
      Terrain/         TerrainGrid (heightmap + paint), TerrainBrush (+ undo history), MapGenerator
      Environment/     EnvironmentField (temperature, humidity, water), DeviceStore (heater, mister, feeder)
      Agents/          Species, SpatialHash, BehaviourTree (+ UtilityGoalPicker)
      Systems/         EnvironmentSystem (5 Hz), NeedsSystem (10 Hz), BrainSystem (10 Hz),
                       MovementSystem (30 Hz), LifecycleSystem (1 Hz), FeederSystem (2 Hz),
                       AlertSystem (1 Hz), SimulationFactory
      Save/            SaveSystem (versioned binary save/load)
    Game/              FrogTerrarium.Game  (Unity MonoBehaviours)
                       SandboxBootstrap, SimulationRunner, DebugOverlay, OrbitCamera,
                       TerrainView, DeviceView, ToolController, GameHud, PerformanceBenchmark
  Shaders/             Terrain (vertex-coloured ground), Water (see-through surface)
  Resources/FrogTerrarium/   material assets (see "Materials" below)
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

## Terrain

`TerrainGrid` is a heightmap: one height and one painted ground layer (soil, moss, sand, rock)
per point, every 0.25 m. Anything below `WaterLevel` is under water, so digging below the water
line makes a pool and raising ground out of a pool drains it. The grid is split into 16 x 16
cell chunks; every edit bumps the version of the chunks it touched, and `TerrainView` rebuilds
only those meshes. `TerrainHistory` keeps the last 50 strokes for undo and redo (not saved).

## Environment

`EnvironmentField` holds temperature (°C) and humidity (%) on 0.5 m cells, plus the share of
each cell that is water, moss or dry ground (worked out from the terrain when it changes).
`EnvironmentSystem` runs 5 times a second: every cell drifts toward the room's conditions,
water raises humidity, moss holds it, sand and rock lose it, the heater warms (with a
thermostat, never above 35 °C) and dries the air, and the mister sprays on a timer. Heat and
moisture then spread to neighbouring cells. Frogs will read these values for comfort.

## Agents: how a frog decides what to do

Every 10 Hz brain tick, for each agent:

1. **Needs** (`NeedsSystem`): hunger, thirst and tiredness rise; sleep and sitting in water bring
   them down; comfort follows the temperature and humidity where the frog sits.
2. **Utility goal picker** (`UtilityGoalPicker`): every goal gets a score from the needs (eat from
   hunger, drink from thirst, sleep from tiredness, seek comfort from low comfort, explore a small
   constant). The highest score wins. The current goal gets a bonus so frogs do not flip between
   two goals with similar scores.
3. **Behaviour tree** (`BtNode`): each goal has a small tree of steps, for example Eat = "if I have
   prey and it is in tongue reach, strike; otherwise stalk it; if I have no prey, search". Trees are
   shared by all agents and re-run from the top every tick; per-agent memory (target, timers) lives
   in the agent arrays, so it is saved with the game.

The brain only sets a velocity; `MovementSystem` moves agents along it 30 times per second.
`SpatialHash` files agents into 1 m squares so "nearest cricket" and "frogs nearby" are fast.
Crickets flee frogs that move faster than 0.4 m/s, so frogs hop toward prey and creep the last
1.4 m. `LifecycleSystem` ages agents, removes crickets that die of old age and lets adults breed on
soil or moss up to the tank's capacity. `FeederSystem` releases crickets from feeders.
`AlertSystem` groups problems by cause ("3 frogs are too dry") with a suggested fix.

## Player commands

Rendering only reads the simulation. The one exception is `ToolController`, which applies the
player's commands (sculpt, paint, place or remove a device, change a setting) on the main
thread between simulation ticks.

## Materials

A player build only includes shaders that an asset uses, so every material lives as an asset in
`Resources/FrogTerrarium` and is listed in `GameMaterials.Names` with its expected shader. The
build check (`Editor/ShaderBuildCheck.cs`) fails the build if any of them would draw pink, and a
play-mode test checks each material got its shader (a shader with a compile error shows up as
`Hidden/InternalErrorShader`).

## Debug overlay

Shows smoothed and worst frame time, agent counts, total sim time per frame, and per-system
milliseconds per tick and tick counts. O (or F3, or the Stats button) toggles it.

## Adding a new system

1. Create a class implementing `ISimSystem` in `Simulation/Systems`.
2. Register it in `SimulationFactory.CreateDefault`.
3. If it has state that must survive a save, add it to the save format (see SAVE_FORMAT.md).
4. Add edit-mode tests.

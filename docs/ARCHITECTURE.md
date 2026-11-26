# Architecture

The rule everything follows: **the simulation is separate from presentation.** Simulation
code is plain C# with no Unity references, runs on a fixed tick, and keeps its data in plain
arrays. Unity code reads that data to draw it and never writes to it.

```
Assets/
  Scripts/
    Simulation/        FrogTerrarium.Simulation  (no UnityEngine; testable with plain .NET)
      Core/            SimulationLoop, World, AgentStore, SeededRandom, RandomService
      Terrain/         TerrainGrid (heightmap + paint), TerrainBrush (+ undo history), MapGenerator (biomes),
                       MapSettings (biome, seed, size, sliders, map codes)
      Environment/     EnvironmentField (temperature, humidity, water), DeviceStore (heater, mister, feeder)
      Agents/          Species (6 frog species + cricket), Genome, Mutations, Lineage (hybrids, new species), SpatialHash, BehaviourTree (+ UtilityGoalPicker)
      Systems/         EnvironmentSystem (5 Hz), NeedsSystem (10 Hz), BrainSystem (10 Hz),
                       MovementSystem (30 Hz), LifecycleSystem (1 Hz), FeederSystem (2 Hz),
                       EconomySystem (1 Hz), AlertSystem (1 Hz), SimulationFactory
      Economy/         Economy (money, prices, sale values, research), Contract (buyers' orders)
      Building/        Parts (part catalogue, footprints), PartStore (placing, snapping, stacking,
                       cover and perch maps), Blueprint (saved builds as text)
      Scenarios/       Scenario (built-in scenarios, goals, winning and losing, ScenarioSystem at 1 Hz)
      Save/            SaveSystem (versioned binary save/load)
    Game/              FrogTerrarium.Game  (Unity MonoBehaviours)
                       SandboxBootstrap, SimulationRunner, DebugOverlay, OrbitCamera,
                       TerrainView, DeviceView, PartView, ToolController (+ .Build), GameHud (+ .Build, .Money),
                       BlueprintFiles, PerformanceBenchmark
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

## Frog lives, families and genes (Phase 2)

Every frog is one agent from egg to old age. `AgentStore` holds its species, sex, life stage,
growth, stress, parents' ids, genes and, for a female carrying eggs, the father's id and genes.

* **Species** (`SpeciesCatalog.Frogs`): White's tree frog, red-eyed tree frog and Oriental
  fire-bellied toad, each with its own comfort zone, size, colour, clutch size and lifespan. Saves
  store the index, so new species only ever go on the end of the list.
* **Growing up** (`LifecycleSystem`, 1 Hz): growth adds up each second, at full speed only when the
  frog is fed and comfortable (eggs and tadpoles: when the water is warm enough). The stage follows
  from growth: egg, tadpole, froglet, juvenile, adult; an adult becomes an elder by real age and dies
  at the end of its lifespan. Eggs and tadpoles must stay in water (`MovementSystem` keeps tadpoles
  in) and die if the pool is drained under them.
* **Breeding**: a calm, fed adult female pairs with a ready male of her species within 8 m, heads for
  water (goal LayEggs) and lays a clutch there. Sexes alternate within a clutch. Breeding stops
  while the tank is full (one frog per 15 square metres, eggs included) or food is short (fewer than
  four insects per frog), so populations level off on their own.
* **Genes** (`Genome`): size, colour, heat and humidity preference, boldness, longevity and
  fertility. A child gets a random point between its parents for each trait, plus a small wobble,
  using the "Genetics" random stream. Every trait has hard limits. Phase 3 adds mutations and hybrids (see below).
* **Stress and escapes** (`NeedsSystem`, `BrainSystem`): comfort also drops when more than two frogs
  are within 0.7 m. Stress rises while a frog is uncomfortable, starving or parched (faster for shy
  frogs) and fades when it is comfortable. At 0.5 the player is warned; at 1 the frog heads for the
  nearest wall and tries to climb out. Escaped frogs go into `World.Escaped` (saved) and can all be
  brought back from the alert.
* **Juveniles** in a crowd move off to a roomier spot (goal Disperse).

## Social life, territories and population controls (Phase 2, part 2)

* **Loneliness** (`NeedsSystem`): each species has a `Sociability` (fire-bellied toads 0.9, White's
  0.4, red-eyed 0.2). With no land frog of its species within 2 m, a frog's `Social` value rises by
  Sociability/240 per second; with company it falls. Above 0.7 it costs comfort, and above 0.5 the
  frog may pick the Socialise goal and hop to the nearest frog of its kind. That is what makes
  fire-bellied toads gather in groups.
* **Territories** (`LifecycleSystem` claims, `BrainSystem` defends): an adult male of a species with
  a `TerritoryRadius` claims the dry, comfortable spot he is on if no other male of his species has
  a home within twice the radius. He explores inside it. A rival adult male of his species inside it
  and within 3 m triggers the Defend goal: he charges, and when they meet they wrestle. Strength is
  size gene times (0.6 + boldness), with a home advantage and some luck. The loser gets the Retreat
  goal for 6 s, a little stress, and a spot just outside the territory; if the owner loses, the
  winner takes the territory. Elders give theirs up.
* **Keeping clear**: froglets and juveniles flee (goal Flee) from grown frogs of another species that
  are clearly bigger than them and within 1 m.
* **Population controls** (`World`): `BreedingEnabled` and `BreedingLimit` (0 = the tank's capacity;
  it can only lower it) feed `FrogLimit`, which breeding respects. `CollectEggs`, `Rehome` and
  `RehomeExtras` (elders first, then oldest adults, never the last adult of a sex in a species) are
  used by the Frogs panel in `GameHud` and the frog inspector. All of this is saved (version 5).
* **Level of detail** (`BrainSystem`): the game sets `World.HasFocus`/`FocusX`/`FocusZ`/`FocusRadius`
  from the camera each frame (not saved). Agents outside that circle only pick a goal and run their
  tree on one brain tick in three, staggered by id; in between they keep moving and stop at their
  target. With 500 animals the whole simulation takes well under 1 ms per frame in plain .NET.

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

## Mutations, hybrids and new species (Phase 3)

* **Per-frog numbers**: `SpeciesCatalog.FrogOf(agents, i)` returns the numbers a particular frog
  lives by. Pure-bred frogs without mutations get the shared species object. Hybrids get
  `FrogSpecies.Blend` of their two species, and mutations then change some numbers
  (`Mutations.Apply`). These variants depend only on fixed data, so they are made once and cached.
  The simulation systems all use this, so a mutation's effect is just a change of numbers.
* **Mutations** (`Mutations`, stored in `Genome.Alleles`, two bits each): teeth (bigger meals, fight
  bonus), claws (a bit quicker, fight bonus, 2.5 times better at climbing out), giant eyes (sees 40%
  further, tires sooner), long legs (25% faster, hungrier), loud call (heard 60% further, bigger
  territory) are dominant; albino (poor eyesight, dislikes heat), blue (looks only), black (likes it
  1.5 °C cooler) and stubby legs (slow, needs less food) are recessive. Each egg gets one copy from
  each parent (a random one when a parent has a single copy). A brand-new mutation happens in 1% of
  eggs, up to 4 times more with a stressed mother, twice with hybrid parents, 25 times with the
  sandbox `World.MutationBoost`.
* **Hybrids** (`Lineage`): each species has one relative in its genus (White's and White-lipped,
  red-eyed and Morelet's, fire-bellied and yellow-bellied). When a female finds no ready male of her
  species in call range, there is a 30% chance per try that she accepts a related one. The young's
  mix is worked out from both parents (`Genome.HybridSpecies`, `HybridShare` up to 0.5; the main
  species, `AgentStore.Species`, is whichever it has most of). Hybrids count as their main species for
  company and territory. Hybrid fertility is 1 − 0.8 × share (a first cross: 60%), applied to pairing
  and clutch size.
* **New species**: a frog's type is its main species, its mix to the nearest quarter and its visible
  mutations (`Lineage.TypeOf`). `Genome.Generation` counts how many generations in a row parents and
  child were of the same type. A new type (hybrid or mutant) with a frog at generation 3 or more can
  be registered with a name (`World.Register`); frogs of that type then show that name.
* **Discoveries**: when a froglet first shows a mutation or a hybrid mix not seen before in this tank,
  `World.NoteDiscoveries` sets a news line that `AlertSystem` shows for 90 seconds. All of this is
  saved (version 6).
* **Looks** (`SimulationRunner`): until real models arrive, frogs are built from primitive shapes:
  body, eyes and back legs, plus teeth, claws, a throat sac, longer, shorter or fatter legs and
  bigger eyes for the mutations. Colours are rounded so look-alike frogs share a draw batch.

## Building parts and blueprints (Phase 3, part 2)

* **Parts** (`Parts.All`, `PartKind`): twelve kinds, each with a size in metres, a natural colour,
  what it does for frogs (shade, damp, cover, perch), whether other parts can stand on it, and snap
  points (`Socket`: a spot on the edge and the direction it faces). A placed part (`PlacedPart`) has
  a spot, a turn in degrees clockwise from above (the same as Unity's `Quaternion.Euler(0, r, 0)`),
  a size from 0.5 to 2 times, a colour and the id of the part it stands on (0 = the ground).
* **Rules** (`PartStore.Check`): a part must be fully inside the tank, a stacked part's middle must
  be on the part below, and parts at the same height may touch but not overlap (a separating-axis
  test on the two turned rectangles). At most 600 parts.
* **Snapping** (`PartStore.Snap`): first grid rounding (25 cm, 45°) if grid placement is on; then
  the part stands on the highest stackable part under its middle; then, if it has snap points, the
  nearest snap point of another part at the same level within 35 cm pulls it over and turns it so
  the two face each other. `PartStore.Move` moves a part and carries everything stacked on it.
* **Effects**: `EnvironmentSystem` treats shady and damp parts as weak devices (up to 1.5 °C cooler
  underneath; plants and moss add humidity). `PartStore` keeps a cover map and a perch map in
  50 cm cells, rebuilt when parts change. `NeedsSystem` raises comfort near cover and (for tree
  frogs) perches, and stress fades 1.5 times faster under cover. `BrainSystem` counts the same
  bonuses when a frog looks for a comfier spot.
* **Blueprints** (`Blueprint`): parts with spots measured from the build's middle, each pointing at
  the part below by position in the list. `PlaceInto` places all of them or none. `ToText` writes a
  small binary form as base64 after `FTBP1:`; `FromText` checks it carefully, since it may come
  from anyone. In the game, `BlueprintFiles` keeps them as `.ftbp` files in the game's
  `blueprints` folder.
* **Drawing** (`PartView`): placeholder shapes from Unity's cube, sphere and cylinder, batched by
  colour. The part being placed is drawn as a green (fits) or red (does not fit) ghost.
* Saved from save version 7.

## Money mode (Phase 3, part 3)

* **State** (`World.Economy`): on or off, money, reputation (0 to 100), research points and
  researched items, open contracts and running totals. Off is the sandbox: `Spend` always succeeds
  and `Has` says everything is researched, so the rest of the code does not need two paths.
* **Prices**: species `Value` (also what a calm adult sells for), part `Cost` times size squared,
  device, cricket and refill prices in `Economy`. Removing a part or device gives half back.
* **Sale value** (`Economy.SaleValue`): the species value (blended for hybrids, then up to 1.75
  times for the mix), 1.6 times for each mutation shown, twice for a registered species, a little more
  for each generation bred true, less for young, old or stressed frogs.
* **EconomySystem** (1 Hz): power for active heaters and misters, upkeep, appeal every 10 seconds
  (species, hybrids, mutations, registered species, number of frogs, and decoration variety, all
  scaled by how calm the frogs are), visitors (each pays a ticket), reputation drifting toward
  appeal, and contracts offered every 3 minutes (up to 3; from the "Economy" random stream) and
  expired after 15 minutes. Escapes and missed orders cost reputation.
* **Research** (`Economy.AllResearch`): points and money unlock species to buy, parts, the feeder,
  the mutation boost and marketing. Points come from `World.NoteDiscoveries`, `World.Register`,
  `World.Deliver` and visitors.
* The player's purchases are charged in `ToolController`. Saved from version 8.

## Tank sizes and style sets (Phase 3, part 3b)

* **Sizes** (`TankLooks.Sizes`): Standard 20 × 12 m, Large 26 × 16 m, Huge 32 × 20 m.
  `World.ResizeTank` swaps in a bigger `TerrainGrid` and `EnvironmentField` with the old ones copied
  into the middle (same grid spacing, so the copy is exact) and dry flat soil around them. Animals,
  devices and parts keep their spots because positions are measured from the middle. The game
  notices the new terrain object and rebuilds its meshes and the undo history. Tanks never shrink.
* **Style sets** (`TankLooks.Styles`, `World.Style`): ground colours for soil, moss, sand and rock,
  a backdrop colour and six part colours. Looks only. In money mode each style is bought once
  (`Economy.StylesOwned`). Saved from version 9.

## Automation, cleaning and tank health (Phase 3, part 4)

* **Automation tiers** (`DeviceMode` on each `Device`): Basic (heater thermostat, mister and feeder
  timers), Sensor (a mister switches on below `Target` humidity and off above it, reading the
  average air within 1.5 m; a feeder checks on its timer and releases only when there are fewer
  than `Target` crickets per frog, and refills itself when low, paying for it) and Smart. Heaters
  have no Sensor tier. In money mode, Sensor needs Automation research and Smart needs Smart
  controller research.
* **Smart controller** (`DeviceKind.Controller`, `AutomationSystem`, once a second): while one is
  switched on, every Smart device gets its target from the land frogs within 6 m of it (or all
  frogs if none are that close): the middle of their liked temperature or humidity (species plus
  genes), or 1.5 to 4 crickets per frog depending on how hungry they are. Without one, Smart
  devices keep their last targets and work like Sensor ones.
* **Cleaning** (`World.Dirt`, `World.Cleaning`): each land frog adds 0.003 dirt a second (0 to
  100). Above 50 every frog loses up to 0.2 comfort (`NeedsSystem.DirtCost`), and appeal to
  visitors drops by up to 40%. Cleaning by hand costs $10; the cleaning service cleans at 60, and
  Smart cleaning with a controller at 30.
* **Tank health** (`TankHealth.Compute`): a score from average comfort, stress, food, water and
  cleanliness (minus 10 for each critical alert), per-species comfort and the temperatures and
  humidities they sit in against what they like. Alerts carry an `AlertCategory` so the Health
  screen can group them.
* Saved from version 10. Also fixed: feeders used to add humidity as if they were misters.

## Generated maps and sandbox rules (Phase 4)

`MapSettings` holds everything that decides a map: the biome, a 64-bit seed (typed words are turned
into a number with FNV-1a, so "froggy" is the same map everywhere), the tank size and three sliders
(hills, water, plants, 0 to 2). `MapGenerator.Generate(world, settings)` builds the ground from the
"mapgen" random stream with value noise shaped per biome: classic hills, rainforest pools, desert
dunes (stretched, folded noise), a riverbank stream (a sine curve dug below the water line), islands
(the water line cuts the noise, with one guaranteed island) and a volcanic cone. The classic biome
with normal sliders is exactly the starter tank of earlier versions, so old seeds are unchanged.
`World.CreateTank` adds the biome's room conditions, style set, devices (moved onto dry land if
needed) and starting species. A map code (`FTMAP1:biome:seed:size:hills:water:plants`) rebuilds the
same map on another computer.

`World.Rules` is a set of sandbox switches (`SandboxRules`) that turn parts of the simulation off:
needs, climate comfort, loneliness, stress, old age, dirt and feeder supply. Each system checks
`World.Has(rule)`, which is false whenever money mode is on, so money games always run everything.

## Scenarios (Phase 4)

`Scenarios.All` lists the built-in scenarios. Each `ScenarioInfo` gives a map (`MapSettings`), the
starting frogs and money (or the sandbox with its rules), a time limit in simulated minutes, how many
escapes are allowed, and a list of goals (`ScenarioGoal`: frogs, species, money, reputation,
visitors, hybrids, mutations, registered species, building parts, tank health held for some
minutes, or minutes survived). `Scenarios.Start` makes the world and puts a `ScenarioState` in
`World.Scenario`. `ScenarioSystem` runs once a second: a goal stays ticked once reached; all goals
ticked wins (3 stars within half the time limit, 2 within four fifths, else 1); time running out,
too many escapes or no frogs left loses. The game keeps the best stars per scenario in PlayerPrefs.


# Design brief

**Working title:** Frog Terrarium
**Genre:** construction and management sim with sandbox and strategy elements
**Platform:** PC (Windows first; macOS and Linux later), Unity 6, free Unity Personal licence
**Team:** one hobby developer, no budget. Single-player. Not planned for sale (selling stays an option).

## The game in one paragraph

You build and run terrariums. Sculpt the terrain, dig water, place plants, hides and devices,
then stock the tank with real frog species and the live insects they eat. Every frog and
insect is an independent agent with needs (hunger, thirst, sleep, social, territory) and a
life stage from egg to elder. Families form, juveniles grow up and disperse, species react to
each other, and unhappy frogs try to escape. Earn money from visitors, breeding sales and
contracts, then reinvest in bigger tanks, new styles and new species. Interbreeding and
mutation create brand-new species with odd and often funny traits such as teeth, claws or
oversized eyes, which you can name, register and show off.

## The seven pillars

1. **A living tank.** Every frog and insect is an independent agent with needs and a life
   stage. What the player sees comes out of the simulation, not from scripts.
2. **Build anything, it matters.** Terrain sculpting, water, plants and modular structures
   directly change humidity, temperature, cover and food supply, so creative choices have
   real effects.
3. **Manage the system, not the chores.** The player sets up automation (feeders, misters,
   heaters, staff) and solves problems at the system level. Hand work is optional and
   rewarded, never required to keep things alive.
4. **Order on the edge of chaos.** Populations grow, families split, species compete, and
   unhappy frogs escape. Failure should be readable, recoverable and often funny.
5. **Share what you make.** Blueprints, terrarium saves and generated map seeds can be
   exported and shared as files.
6. **Every terrarium is one of a kind.** Personalisation runs deep, in both the species and
   the tank itself, so that over time no two players' terrariums could be recreated. Like
   Minecraft, there should be almost no limit on how unique a terrarium can become, which
   makes it part of the player's identity and something they want to show off and share.
7. **Frogs have things to do.** The tank should be fun to watch. Buildable playground pieces
   such as air-suction tunnels, slides, elevators, teleporters and spiral staircases give frogs
   things to ride, climb and race through. Use comes out of their needs and personality, not
   scripts, so it looks natural. Goal-driven use (taking the elevator up to the feeding ledge)
   is a later stretch.

## Core loop

Build or reshape the habitat → stock it with species → watch needs and comfort → earn money
(visitors, breeding sales, contracts) → reinvest in upgrades, bigger tanks and new species.

## Look and feel

Realistic art and a free 3D orbit camera that zooms from a whole-tank view down to a single
frog. Art and audio come from free (CC0 and similar) libraries, free Asset Store packs and
AI-generated assets, each recorded in [ASSET_LOG.md](ASSET_LOG.md).

## Frog playground (Phase 5)

Playground pieces are ordinary building parts, so they reuse the snapping, grid toggle,
blueprints and share files the game already has. Part 1 (built): the play need, the slide,
spiral staircase and air tunnel, and chains. Part 2: elevator, teleporter pair and traits.

- **Pieces:** air-suction tunnel (uses a little power), slide sections, elevator (uses power),
  teleporter pair (a late, rarer unlock), spiral staircase.
- **Emergent use first:** each piece advertises what it offers (fun, a shortcut, a cool spot,
  an escape route) and frogs pick it up through a new low-priority "play" need and their
  personality. Bold or bored frogs and juveniles play most; shy frogs avoid busy pieces.
- **Chains are the fun:** pieces snap into combinations (tunnel into slide into pool), so every
  playground is different (pillar 6) and can be shared as a blueprint.
- **Genetics joins in:** traits change how frogs use pieces, such as sticky feet or big jumps.
- **No new chores:** no hand upkeep beyond power; problems show as one grouped alert.

## Avoiding micromanagement

Every recurring chore has three tiers: do it by hand (cheap, early game), automate it with a
device that needs occasional upkeep (mid game), then hand it to staff or a smart controller
(late game). Challenge moves from chores to system design: supply chains, layout, species mix
and budget. Alerts group by cause ("Tank 2 too dry") rather than per frog.

## Out of scope (for now)

Online multiplayer, paid releases, Steam Workshop. Revisit only if the game is ever sold.

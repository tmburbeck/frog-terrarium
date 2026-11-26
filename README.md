# Frog Terrarium

A sandbox terrarium builder and management sim. You design living habitats and run a
frog-keeping business while hundreds of frogs and insects live their own need-driven
lives inside them. Built solo in Unity 6 as a hobby project.

* **Design brief and pillars:** [docs/DESIGN_BRIEF.md](docs/DESIGN_BRIEF.md)
* **How the code is organised:** [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
* **Branches and pull requests:** [docs/BRANCHING.md](docs/BRANCHING.md)
* **Automatic builds (one-time licence setup):** [docs/CI_SETUP.md](docs/CI_SETUP.md)
* **Save file format rules:** [docs/SAVE_FORMAT.md](docs/SAVE_FORMAT.md)
* **Art and audio licences:** [docs/ASSET_LOG.md](docs/ASSET_LOG.md)

## Opening the project

1. Install [Unity Hub](https://unity.com/download) (free) and sign in with a free Unity account.
2. In Unity Hub, install editor **6000.3.29f1** (Unity 6.3 LTS). Using exactly this version
   matters: the automatic builds use the same one. Add the **Windows Build Support (Mono)** module.
3. Unity Hub > **Add** > **Add project from disk** > choose this folder.
4. The first open takes a few minutes. The **Sandbox** scene is created and opened for you
   (or use the menu **Frog Terrarium > Create or Open Sandbox Scene**).
5. Press **Play**. You should see a tank of hilly ground with a pool, a heater, a mister and a
   feeder, green frog blobs hunting dark cricket specks, the build bar along the bottom, and the
   debug overlay in the top-left corner. Click a frog to see its needs and what it is thinking.

### Controls in the sandbox

| Key / mouse | Action |
| --- | --- |
| Left mouse | Use the current tool from the build bar (sculpt, paint, place, select) |
| Right mouse drag | Orbit the camera |
| Middle mouse drag, or WASD | Pan |
| Scroll wheel | Zoom |
| [ and ] | Smaller / bigger brush |
| Ctrl+Z / Ctrl+Y | Undo / redo a terrain stroke (last 50) |
| Delete | Remove the selected device or building part |
| R / Shift+R | Turn the part or blueprint being placed (or the selected part) |
| G | Switch between grid placement (25 cm steps, 45° turns) and free placement |
| Esc | Back to the Select tool |
| O or F3 | Show or hide the stats overlay (also the Stats button) |
| B or F8 | Run the 20-second speed test with 500 animals (also the Speed test button) |
| F5 / F9 | Quick-save / quick-load (also the Save and Load buttons) |
| P | Pause |
| 1 / 2 / 3 | Speed x1 / x2 / x4 |

## Running the tests

In Unity: **Window > General > Test Runner**, then **Run All** on the *EditMode* and *PlayMode*
tabs. The same tests run automatically on GitHub for every push.

## Status

Phase 0 (foundations) is done. Phase 1 (the vertical slice) has terrain sculpting and painting
with undo, water pools, temperature and humidity driven by the terrain, a heater and a mister,
White's tree frogs with hunger, thirst, sleep and comfort, house crickets that breed, wander and
flee, a feeder, a frog inspector, grouped alerts, and a speed test (B key or the Speed test button). The art is
still placeholder shapes.

Phase 2 adds whole frog lives (egg, tadpole, froglet, juvenile, adult, elder), breeding and family
trees, genes passed from parents, the red-eyed tree frog and the Oriental fire-bellied toad, stress
and escapes, loneliness, territorial males, and a **Frogs** button for population controls
(breeding on or off, a frog limit, collect eggs, rehome extras).

Phase 3 has started with mutations and hybrids: three more real species (White-lipped tree frog,
Morelet's tree frog, yellow-bellied toad), each a close relative of a starting species so the two
can make hybrids, nine mutations that show on the frog and change how it lives (teeth, claws, giant
eyes, long legs, loud call, albino, blue, black, stubby legs), and a **Species** button with the
species book, where a new kind of frog that has bred true for three generations can be named and
registered as a new species. The **Frogs** panel has a sandbox **Mutation boost** switch.

Phase 3 part 2 adds building: a **Build** button opens a menu of twelve parts (rock, slab, log,
branch, hide, plant, fern, moss pad, wall, pillar, platform, ramp) that snap together, stack on
slabs and platforms, turn, resize (50% to 200%) and take any colour. Grid or free placement (G).
Click a placed part to resize, turn, recolour, move (what stands on it comes along), copy or delete
it. Parts matter to frogs: hides, logs, plants and platforms give cover that calms them, tree frogs
like to perch on logs, branches, plants and ramps, plants shade the ground, and plants and moss
keep the air damp. A build can be saved as a **blueprint**, a short line of text starting `FTBP1:`
that is also saved as a `.ftbp` file and copied to the clipboard, so it can be placed again (turned
or elsewhere) or shared with someone else, who pastes it in with **Paste from clipboard**.

Phase 3 part 3 adds **money mode**, on for new games (the **Money** button at the top left can switch
back to the free sandbox; tanks from older saves stay in the sandbox). Frogs, crickets, devices and
parts cost money; heaters and misters use power and the tank has upkeep. Money comes from visitors
(more come to a tank with many species, hybrids, mutations, new species, frogs and varied
decorations, and a good reputation), from selling frogs (rare ones are worth much more: hybrids,
mutations and registered species) and from buyers' orders. Research points from discoveries, new
species, orders and visitors unlock more species, parts, the feeder, the mutation boost and
marketing.

## Troubleshooting

* **Camera or keys do nothing:** open **Edit > Project Settings > Player > Other Settings** and set
  **Active Input Handling** to **Both** (or **Input Manager (Old)**). The new Input System comes later.
* **"Editor version not installed" in Unity Hub:** install exactly 6000.3.29f1 from the Hub's
  **Installs > Install Editor > Archive** link.

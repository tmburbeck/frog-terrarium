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
5. Press **Play**. You should see a tank of hilly ground with a pool, a heater and a mister,
   green frog blobs and dark insect specks wandering around, the build bar along the bottom,
   and the debug overlay in the top-left corner.

### Controls in the sandbox

| Key / mouse | Action |
| --- | --- |
| Left mouse | Use the current tool from the build bar (sculpt, paint, place, select) |
| Right mouse drag | Orbit the camera |
| Middle mouse drag, or WASD | Pan |
| Scroll wheel | Zoom |
| [ and ] | Smaller / bigger brush |
| Ctrl+Z / Ctrl+Y | Undo / redo a terrain stroke (last 50) |
| Delete | Remove the selected device |
| Esc | Back to the Select tool |
| F3 | Show or hide the debug overlay |
| F5 / F9 | Quick-save / quick-load |
| P | Pause |
| 1 / 2 / 3 | Speed x1 / x2 / x4 |

## Running the tests

In Unity: **Window > General > Test Runner**, then **Run All** on the *EditMode* and *PlayMode*
tabs. The same tests run automatically on GitHub for every push.

## Status

Phase 0 (foundations) is done. Phase 1 (the vertical slice) is in progress: terrain sculpting
and painting with undo, water pools, and temperature and humidity driven by the terrain, a
heater and a mister. The frogs and insects are still placeholders that only wander; their real
behaviour is next.

## Troubleshooting

* **Camera or keys do nothing:** open **Edit > Project Settings > Player > Other Settings** and set
  **Active Input Handling** to **Both** (or **Input Manager (Old)**). The new Input System comes later.
* **"Editor version not installed" in Unity Hub:** install exactly 6000.3.29f1 from the Hub's
  **Installs > Install Editor > Archive** link.

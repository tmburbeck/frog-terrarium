# Save format

Saves are small binary files (`.ftsv`), written by `SaveSystem`.

| Bytes | Content |
| --- | --- |
| 4 | Magic `FTSV` |
| 4 | Format version (int32, little-endian). Currently **1**. |
| rest | Body for that version |

**Version 1 body:** world seed, tank half-size X and Z, simulated time, every random
stream (name, state, increment), the next agent id, then each agent's id, kind, position,
velocity, hunger, thirst and fatigue.

Saves are written to a temporary file and then swapped in, so a crash while saving never
destroys the previous save. Quick-saves go to Unity's `Application.persistentDataPath/saves/`.

## Rules for changing the format

1. **Never change how an existing version is read.** Old saves and shared files must keep loading.
2. To add or change data: bump `CurrentVersion`, change `Write`, add a `ReadV2` (etc.) and a
   `case` for it in `Read`. Older readers fill new fields with sensible defaults.
3. Commit a sample save of every released version under `Assets/Tests/SaveSamples/` and add a
   test that loads it. (Starts when there is a first version worth keeping.)
4. Files from a *newer* game are refused with a clear message instead of being misread.

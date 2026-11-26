# Save format

Saves are small binary files (`.ftsv`), written by `SaveSystem`.

| Bytes | Content |
| --- | --- |
| 4 | Magic `FTSV` |
| 4 | Format version (int32, little-endian). Currently **2**. |
| rest | Body for that version |

**Version 1 body:** world seed, tank half-size X and Z, simulated time, every random
stream (name, state, increment), the next agent id, then each agent's id, kind, position,
velocity, hunger, thirst and fatigue.

**Version 2 body:** the whole version 1 body, then:

* terrain: grid points across and deep, cell size, water level, every point's height, every
  point's ground layer (one byte: 0 soil, 1 moss, 2 sand, 3 rock);
* environment: room temperature and humidity, grid cells across and deep, every cell's
  temperature, every cell's humidity;
* devices: the next device id, then each device's id, kind (0 heater, 1 mister), position,
  on/off, two settings, supply, timer and whether it is working right now.

Loading a version 1 save gives flat ground at the default height, no devices, and room
conditions, with every agent placed on the ground.

Saves are written to a temporary file and then swapped in, so a crash while saving never
destroys the previous save. Quick-saves go to Unity's `Application.persistentDataPath/saves/`.

## Rules for changing the format

1. **Never change how an existing version is read.** Old saves and shared files must keep loading.
2. To add or change data: bump `CurrentVersion`, change `Write`, add a `ReadV2` (etc.) and a
   `case` for it in `Read`. Older readers fill new fields with sensible defaults.
3. Keep a sample save of every old version and a test that loads it. The version 1 sample is
   stored as text inside `Assets/Tests/EditMode/SaveSystemTests.cs` (`Version1Sample`), so both
   Unity and the plain .NET tests can read it. Before bumping the version again, add a
   `Version2Sample` the same way.
4. Files from a *newer* game are refused with a clear message instead of being misread.

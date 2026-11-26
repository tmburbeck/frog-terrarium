# Save format

Saves are small binary files (`.ftsv`), written by `SaveSystem`.

| Bytes | Content |
| --- | --- |
| 4 | Magic `FTSV` |
| 4 | Format version (int32, little-endian). Currently **4**. |
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

**Version 3 body:** the same as version 2, except that each agent's record also has, after
fatigue: comfort, goal (one byte: 0 idle, 1 wander, 2 eat, 3 drink, 4 sleep, 5 seek comfort,
6 flee), seconds on that goal, target agent id, target position X and Z, age, lifespan, wander
timer and tongue cooldown. Device kind 2 (feeder) is allowed.

**Version 4 body:** the same as version 3, except that:

* each agent's record also has, after tongue cooldown: species (one byte: 0 White's tree frog,
  1 red-eyed tree frog, 2 Oriental fire-bellied toad), sex (one byte: 0 female, 1 male), life stage
  (one byte: 0 egg, 1 tadpole, 2 froglet, 3 juvenile, 4 adult, 5 elder), growth, stress, mother id,
  father id, mate id, seconds carrying eggs, breeding cooldown, then the frog's genes and its mate's
  genes (seven floats each: size, hue, heat shift, damp shift, boldness, longevity, fertility);
* goals 7 (lay eggs), 8 (escape) and 9 (disperse) are allowed;
* after the devices comes the list of escaped frogs: a count, then each one's id, species, sex,
  age, growth, lifespan, mother id, father id, genes, reason (text) and time of escape.

Loading a save from version 3 or earlier makes every frog a grown White's tree frog with average
genes and no known parents; odd ids become female and even ids male, and each frog gets a lifespan
worked out from its id that leaves it at least 20 more minutes.

Loading a version 2 save gives frogs an adult age and crickets a lifespan worked out from
their id. Loading a version 1 save gives flat ground at the default height, no devices, and room
conditions, with every agent placed on the ground.

Saves are written to a temporary file and then swapped in, so a crash while saving never
destroys the previous save. Quick-saves go to Unity's `Application.persistentDataPath/saves/`.

## Rules for changing the format

1. **Never change how an existing version is read.** Old saves and shared files must keep loading.
2. To add or change data: bump `CurrentVersion`, change `Write`, add a `ReadV2` (etc.) and a
   `case` for it in `Read`. Older readers fill new fields with sensible defaults.
3. Keep a sample save of every old version and a test that loads it. The version 1 sample is
   stored as text inside `Assets/Tests/EditMode/SaveSystemTests.cs` (`Version1Sample`), so both
   Unity and the plain .NET tests can read it; `Version2Sample` and `Version3Sample` sit next to
   it. Before bumping the version again, add a `Version4Sample` the same way.
4. Files from a *newer* game are refused with a clear message instead of being misread.
